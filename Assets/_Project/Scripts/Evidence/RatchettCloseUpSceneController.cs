using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

[Serializable]
public class RatchettCloseUpHotspot
{
    public string buttonObjectName;
    [Tooltip("同一热点绑定多个按钮（例如窗框四边），名称须与场景中 GameObject 一致。")]
    public List<string> additionalButtonObjectNames = new List<string>();
    public string evidenceId;
    [Tooltip("可选。若为空，则按证据 id 自动映射到 Resources/Dialogue 下的文件名（须与 .json 文件名一致，如 ratchett_pistol → ratchett_pistol）。")]
    public string dialogueResourceId;
    public string availableHint = "点击查看";
}

/// <summary>
/// 案发包厢二级近景（桌子 / 窗户 / 尸体等）：在场景里摆 Button 热点，Inspector 配置证据 id 与悬停文案。
/// </summary>
public class RatchettCloseUpSceneController : MonoBehaviour
{
    [SerializeField] private string cabinSceneName = SceneLoader.SCENE_CRIME_SCENE;
    [SerializeField] private string exhaustedHint = "没有什么值得注意的了";
    [SerializeField] private string backButtonObjectName = "Hotspot_Back_Button";
    [SerializeField] private string backButtonHint = "返回包厢";
    [SerializeField] private List<RatchettCloseUpHotspot> collectionHotspots = new List<RatchettCloseUpHotspot>();

    private const string BurnedPaperFindVideoResourcesPath = "MOV/find";
    private const float BurnedPaperVideoPrepareTimeoutSeconds = 8f;
    private const float BurnedPaperVideoPlaybackPadSeconds = 0.75f;
    private const int BurnedPaperVideoOverlaySortingOrder = 9650;

    private readonly Dictionary<string, HoverHintTrigger> hintTriggers = new Dictionary<string, HoverHintTrigger>();

    private void Start()
    {
        foreach (RatchettCloseUpHotspot hotspot in collectionHotspots)
        {
            if (hotspot == null)
            {
                continue;
            }

            List<string> names = CollectButtonNames(hotspot);
            if (names.Count == 0)
            {
                continue;
            }

            RatchettCloseUpHotspot captured = hotspot;
            foreach (string btnName in names)
            {
                HoverHintTrigger trigger = AutoBindButton(
                    btnName,
                    () => OnCollectionHotspotClicked(captured),
                    captured.availableHint);
                if (trigger != null)
                {
                    hintTriggers[btnName] = trigger;
                }
            }
        }

        AutoBindButton(backButtonObjectName, OnBackToCabinClicked, backButtonHint, false);
        RefreshHintStates();
    }

    private void OnCollectionHotspotClicked(RatchettCloseUpHotspot hotspot)
    {
        if (hotspot == null || string.IsNullOrEmpty(hotspot.evidenceId))
        {
            return;
        }

        DialogueManager busyDm = CrimeSceneEvidenceGrantBridge.FindDialogueManager();
        if (busyDm != null && busyDm.IsDialogueUiActive())
        {
            return;
        }

        if (IsDialogueOnlyWindowEvidence(hotspot.evidenceId))
        {
            if (IsWindowCollectionComplete(hotspot.evidenceId))
            {
                return;
            }

            if (!TryPlayWindowCollectionDialogue(hotspot.evidenceId))
            {
                return;
            }

            RatchettCrimeSceneProgress.TryMarkFinished();
            RefreshHintStates();
            return;
        }

        string dialogueId = ResolvePostCollectDialogueId(hotspot);
        if (!string.IsNullOrEmpty(dialogueId) && CrimeSceneEvidenceGrantBridge.DialogueResourceExists(dialogueId))
        {
            CrimeSceneEvidenceGrantBridge.ClearPending();
            AddEvidence(hotspot.evidenceId);
            string capturedDialogueId = dialogueId;
            if (string.Equals(hotspot.evidenceId, EvidenceIds.BURNED_PAPER, StringComparison.Ordinal))
            {
                NotebookUnlockOverlayPresenter.EnqueueContinuation(() =>
                {
                    StartCoroutine(PlayBurnedPaperFindVideoThenDialogue(capturedDialogueId));
                });
            }
            else
            {
                NotebookUnlockOverlayPresenter.EnqueueContinuation(() =>
                {
                    if (!CrimeSceneEvidenceGrantBridge.TryStartCollectDialogue(capturedDialogueId))
                    {
                        Debug.LogWarning($"RatchettCloseUpSceneController: 搜证对话未能启动：{capturedDialogueId}");
                    }
                });
            }

            RatchettCrimeSceneProgress.TryMarkFinished();
            RefreshHintStates();
            return;
        }

        AddEvidence(hotspot.evidenceId);
        RatchettCrimeSceneProgress.TryMarkFinished();
        RefreshHintStates();
    }

    private static bool IsDialogueOnlyWindowEvidence(string evidenceId)
    {
        return string.Equals(evidenceId, EvidenceIds.WINDOW_FRAME, StringComparison.Ordinal)
            || string.Equals(evidenceId, EvidenceIds.SNOW_NO_FOOTPRINTS, StringComparison.Ordinal);
    }

    private bool IsWindowCollectionComplete(string evidenceId)
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        GameManager gm = GameManager.Instance;

        if (string.Equals(evidenceId, EvidenceIds.WINDOW_FRAME, StringComparison.Ordinal))
        {
            return (gm != null && gm.RatchettWindowFrameDialogueDone)
                || HasEvidence(evidenceManager, notebookManager, evidenceId);
        }

        if (string.Equals(evidenceId, EvidenceIds.SNOW_NO_FOOTPRINTS, StringComparison.Ordinal))
        {
            return (gm != null && gm.RatchettWindowSnowDialogueDone)
                || HasEvidence(evidenceManager, notebookManager, evidenceId)
                || (notebookManager != null && notebookManager.HasEvidence(EvidenceIds.SNOW_VIEW));
        }

        return false;
    }

    private bool TryPlayWindowCollectionDialogue(string evidenceId)
    {
        string dialogueId = string.Equals(evidenceId, EvidenceIds.WINDOW_FRAME, StringComparison.Ordinal)
            ? "window_frame"
            : "lookout_window";

        CrimeSceneEvidenceGrantBridge.ClearPending();

        DialogueManager dm = CrimeSceneEvidenceGrantBridge.FindDialogueManager();
        if (dm == null)
        {
            Debug.LogWarning("RatchettCloseUpSceneController: 找不到 DialogueManager，无法播放窗户相关对话（需已进入过列车走廊等已加载对话 UI 的流程）。");
            return false;
        }

        if (!CrimeSceneEvidenceGrantBridge.DialogueResourceExists(dialogueId))
        {
            Debug.LogWarning($"RatchettCloseUpSceneController: 无对话资源 Dialogue/{dialogueId}。");
            return false;
        }

        dm.StartDialogue(dialogueId);
        if (!dm.IsDialogueUiActive())
        {
            Debug.LogWarning("RatchettCloseUpSceneController: 窗户对话未能启动（检查 DialogueManager UI 绑定）。");
            return false;
        }

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            return true;
        }

        if (string.Equals(evidenceId, EvidenceIds.WINDOW_FRAME, StringComparison.Ordinal))
        {
            gm.RatchettWindowFrameDialogueDone = true;
        }
        else
        {
            gm.RatchettWindowSnowDialogueDone = true;
        }

        return true;
    }

    private static string ResolvePostCollectDialogueId(RatchettCloseUpHotspot hotspot)
    {
        if (hotspot == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(hotspot.dialogueResourceId))
        {
            return hotspot.dialogueResourceId.Trim();
        }

        return ResolveDefaultDialogueIdForEvidence(hotspot.evidenceId);
    }

    private static string ResolveDefaultDialogueIdForEvidence(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }

        if (string.Equals(evidenceId, EvidenceIds.PISTOL, StringComparison.Ordinal))
        {
            return "ratchett_pistol";
        }

        if (string.Equals(evidenceId, EvidenceIds.GOLD_WATCH, StringComparison.Ordinal))
        {
            return "ratchett_gold_watch";
        }

        if (string.Equals(evidenceId, EvidenceIds.BURNED_PAPER, StringComparison.Ordinal))
        {
            return "burned_paper_fragment";
        }

        // 桌上拾取：只收录笔记与线索，不自动播 Dialogue/wine_glasses（剧情可由手枪对话等推进）。
        if (string.Equals(evidenceId, EvidenceIds.WINE_GLASSES, StringComparison.Ordinal))
        {
            return null;
        }

        return evidenceId;
    }

    private void OnBackToCabinClicked()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ForceEndDialogueIfAny();
        }

        CrimeSceneEvidenceGrantBridge.ClearPending();

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(cabinSceneName);
        }
        else
        {
            if (SceneAudioPolicy.ShouldPlayTransitionSfx(cabinSceneName))
            {
                GameAudioManager.EnsureExists();
                if (GameAudioManager.Instance != null)
                {
                    GameAudioManager.Instance.TryPlayTransitionSound();
                }
            }

            EvidencePanelUI.DisableAllEventSystemComponentsBeforeSceneLoad();
            SceneManager.LoadScene(cabinSceneName);
            EvidencePanelUI.EnsureSingleEventSystem();
        }
    }

    private bool IsHotspotExhausted(string evidenceId)
    {
        if (IsDialogueOnlyWindowEvidence(evidenceId))
        {
            return IsWindowCollectionComplete(evidenceId);
        }

        if (!string.IsNullOrEmpty(CrimeSceneEvidenceGrantBridge.PendingEvidenceIdAfterDialogue)
            && string.Equals(CrimeSceneEvidenceGrantBridge.PendingEvidenceIdAfterDialogue, evidenceId, StringComparison.Ordinal))
        {
            return true;
        }

        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        return HasEvidence(evidenceManager, notebookManager, evidenceId);
    }

    private void AddEvidence(string evidenceId)
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        if (notebookManager != null)
        {
            notebookManager.AddEvidence(evidenceId);
        }
        else
        {
            Debug.LogWarning("RatchettCloseUpSceneController: DetectiveNotebookManager 不存在，无法记录侦探笔记。");
        }

        if (evidenceManager == null)
        {
            Debug.LogWarning("RatchettCloseUpSceneController: EvidenceManager 不存在，无法记录证据。");
        }
        else
        {
            evidenceManager.AddClue(evidenceId);
        }
    }

    /// <summary>
    /// 烧焦纸条：在「获得物证」弹窗队列结束后全屏播放 <c>Resources/MOV/find</c>，再进入搜证对话。
    /// </summary>
    private IEnumerator PlayBurnedPaperFindVideoThenDialogue(string dialogueId)
    {
        VideoClip clip = Resources.Load<VideoClip>(BurnedPaperFindVideoResourcesPath);
        if (clip == null)
        {
            Debug.LogWarning(
                $"RatchettCloseUpSceneController: 未找到 VideoClip Resources/{BurnedPaperFindVideoResourcesPath}，将直接开始对话。");
            TryStartBurnedPaperDialogue(dialogueId);
            yield break;
        }

        GameObject overlayRoot = null;
        VideoPlayer player = null;
        RenderTexture rt = null;
        try
        {
            overlayRoot = CreateBurnedPaperVideoOverlayRoot();
            GameObject videoGo = new GameObject("BurnedPaperFindVideo", typeof(RectTransform));
            videoGo.transform.SetParent(overlayRoot.transform, false);
            RectTransform vrt = videoGo.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            RawImage rawImage = videoGo.AddComponent<RawImage>();
            rawImage.raycastTarget = true;
            rawImage.color = Color.white;

            rt = new RenderTexture(1920, 1080, 0);
            player = overlayRoot.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.clip = clip;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            rawImage.texture = rt;

            yield return WaitUntilVideoPrepared(player);
            if (!player.isPrepared)
            {
                Debug.LogWarning("RatchettCloseUpSceneController: find.mov 未能 Prepare，将跳过视频。");
            }
            else
            {
                yield return WaitUntilVideoPlaybackEnds(player, (float)clip.length);
            }
        }
        finally
        {
            if (player != null)
            {
                player.Stop();
            }

            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }

            if (overlayRoot != null)
            {
                Destroy(overlayRoot);
            }
        }

        TryStartBurnedPaperDialogue(dialogueId);
    }

    private static void TryStartBurnedPaperDialogue(string dialogueId)
    {
        if (!CrimeSceneEvidenceGrantBridge.TryStartCollectDialogue(dialogueId))
        {
            Debug.LogWarning($"RatchettCloseUpSceneController: 搜证对话未能启动：{dialogueId}");
        }
    }

    private static GameObject CreateBurnedPaperVideoOverlayRoot()
    {
        GameObject root = new GameObject("BurnedPaperFindVideoOverlay");
        UnityEngine.Object.DontDestroyOnLoad(root);
        RectTransform ort = root.AddComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = BurnedPaperVideoOverlaySortingOrder;
        root.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return root;
    }

    private static IEnumerator WaitUntilVideoPrepared(VideoPlayer player)
    {
        player.Prepare();
        float elapsed = 0f;
        while (!player.isPrepared && elapsed < BurnedPaperVideoPrepareTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static IEnumerator WaitUntilVideoPlaybackEnds(VideoPlayer player, float clipLengthSeconds)
    {
        player.Play();
        float maxWait = Mathf.Max(45f, clipLengthSeconds + BurnedPaperVideoPlaybackPadSeconds);
        float elapsed = 0f;
        while (player.isPlaying && elapsed < maxWait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player.isPlaying)
        {
            player.Stop();
        }
    }

    private HoverHintTrigger AutoBindButton(string buttonName, UnityEngine.Events.UnityAction callback, string hint, bool required = true)
    {
        GameObject buttonObject = GameObject.Find(buttonName);
        if (buttonObject == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCloseUpSceneController: 找不到按钮 {buttonName}");
            }

            return null;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCloseUpSceneController: 对象 {buttonName} 没有 Button 组件");
            }

            return null;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);

        if (string.Equals(buttonName, backButtonObjectName, StringComparison.Ordinal))
        {
            Image image = buttonObject.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }
        }

        HoverHintTrigger trigger = buttonObject.GetComponent<HoverHintTrigger>();
        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<HoverHintTrigger>();
        }

        trigger.hint = hint;
        return trigger;
    }

    private void RefreshHintStates()
    {
        foreach (RatchettCloseUpHotspot hotspot in collectionHotspots)
        {
            if (hotspot == null)
            {
                continue;
            }

            foreach (string name in CollectButtonNames(hotspot))
            {
                if (!hintTriggers.TryGetValue(name, out HoverHintTrigger trigger) || trigger == null)
                {
                    continue;
                }

                bool done = IsHotspotExhausted(hotspot.evidenceId);
                trigger.SetHint(done ? exhaustedHint : hotspot.availableHint);
            }
        }
    }

    private static List<string> CollectButtonNames(RatchettCloseUpHotspot hotspot)
    {
        var names = new List<string>();
        if (hotspot == null)
        {
            return names;
        }

        void AddIfValid(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            s = s.Trim();
            if (names.Contains(s))
            {
                return;
            }

            names.Add(s);
        }

        AddIfValid(hotspot.buttonObjectName);
        if (hotspot.additionalButtonObjectNames != null)
        {
            foreach (string s in hotspot.additionalButtonObjectNames)
            {
                AddIfValid(s);
            }
        }

        return names;
    }

    private static bool HasEvidence(EvidenceManager evidenceManager, DetectiveNotebookManager notebookManager, string evidenceId)
    {
        return (evidenceManager != null && evidenceManager.HasClue(evidenceId))
            || (notebookManager != null && notebookManager.HasEvidence(evidenceId));
    }
}
