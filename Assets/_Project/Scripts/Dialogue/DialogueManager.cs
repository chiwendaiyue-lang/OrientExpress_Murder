using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI")]
    public GameObject dialoguePanel;
    public Image speakerPortrait;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;
    public CharacterStageController stageController;
    [Header("Presented Image")]
    public Image presentedImageDisplay;
    public RectTransform hotspotContainer;
    public GameObject hotspotButtonPrefab;
    [Header("Font Override (Optional)")]
    public TMP_FontAsset dialogueFontOverride;
    [Header("Portrait Display")]
    public bool useLeftSpeakerPortrait = false;

    [Header("对话中的笔记奖励（弹丸式）")]
    [Tooltip("无选项的点击推进节点上，若带有笔记 rewards 且未勾选 dialogueSkipDeferNotebookRewards，则默认延迟到右键收录；左键继续为跳过。")]
    [SerializeField] private bool deferNotebookRewardsByDefaultWhenRewardsPresent = true;

    [Tooltip("延迟收录时在正文下方追加的提示（TMP rich text）。")]
    [SerializeField] private string deferNotebookRewardsHintRichText =
        "<size=88%><color=#D4C4A8>右键：收录到侦探笔记</color><color=#8A8074> · 左键：继续（跳过线索）</color></size>";

    [Tooltip("DialogueData.mainStoryBlockDeferredRewardSkip 为 true 且本句有待收录奖励时使用的提示（不可左键跳过）。")]
    [SerializeField] private string mainStoryDeferNotebookRewardsHintRichText =
        "<size=88%><color=#D4C4A8>右键：收录到侦探笔记</color><color=#8A8074> · 收录后方可继续</color></size>";

    private DialogueData currentDialogue;
    private string currentNodeId;
    private bool waitingForClickAdvance;
    private bool waitMouseReleaseAfterEnter;
    private string pendingNextNodeId;
    private string pendingNextDialogueId;
    private string pendingNextSceneName;
    private readonly List<GameObject> activeHotspots = new List<GameObject>();
    private readonly HashSet<string> noticeIntroPlayedNodeIds = new HashSet<string>();
    private Coroutine noticeMomentRoutine;

    private NotebookRewards pendingDeferredNotebookRewards;

    private string currentDialogueResourceId = string.Empty;

    private const string RuntimePrefabResourcePath = "UI/DialogueRuntimeRoot";

    /// <summary>
    /// 全屏 UI 排序带（Screen Space Overlay）：背景 &lt; 对话 &lt; 弹窗 &lt; ScreenFader。
    /// 与 <see cref="NotebookUnlockOverlayPresenter"/>、<see cref="EvidencePanelUI"/> 中的层级对齐。
    /// </summary>
    private const int DialogueCanvasSortingOrder = 4000;

    public string GetCurrentDialogueResourceId()
    {
        return currentDialogueResourceId ?? string.Empty;
    }

    public string GetCurrentNodeId()
    {
        return currentNodeId ?? string.Empty;
    }

    public bool IsDialogueUiActive()
    {
        return dialoguePanel != null && dialoguePanel.activeSelf;
    }

    public NotebookRewards GetPendingDeferredNotebookRewards()
    {
        return pendingDeferredNotebookRewards;
    }

    public void ForceEndDialogueIfAny()
    {
        if (dialoguePanel != null && dialoguePanel.activeSelf)
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 确保存在可用的 DialogueManager（含 UI）。优先从 Resources/UI/DialogueRuntimeRoot 实例化（见菜单烘焙 Prefab）。
    /// </summary>
    public static DialogueManager EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        DialogueManager found = UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        if (found != null)
        {
            return found;
        }

        GameObject prefabAsset = Resources.Load<GameObject>(RuntimePrefabResourcePath);
        if (prefabAsset == null)
        {
            Debug.LogError(
                "DialogueManager: 缺少 Resources/UI/DialogueRuntimeRoot.prefab。请在 Unity 菜单执行：Tools/东方快车/烘焙 DialogueRuntimeRoot（对话 UI Prefab）。");
            return null;
        }

        // Prefab 内常带 EventSystem；Instantiate 时其 OnEnable 会与场景里仍启用的 EventSystem 冲突。
        EvidencePanelUI.DisableAllEventSystemComponentsBeforeSceneLoad();
        GameObject instance = UnityEngine.Object.Instantiate(prefabAsset);
        instance.name = "DialogueRuntimeRoot";

        if (Instance == null)
        {
            UnityEngine.Object.Destroy(instance);
            Debug.LogError(
                "DialogueManager: 已实例化 DialogueRuntimeRoot，但未找到 DialogueManager 组件。请检查 Prefab 是否包含 DialogueManager。");
            return null;
        }

        EvidencePanelUI.EnsureRuntimeInstance();
        EvidencePanelUI.EnsureSingleEventSystem();
        EvidencePanelUI.EnsureSingleAudioListener();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (dialogueText != null)
        {
            dialogueText.richText = true;
        }

        EnsureDialogueCanvasesRenderOnTop();
        PassThroughDecorativeCanvasImages();
        HideRuntimeDialoguePrefabBackgroundLayer();
    }

    /// <summary>
    /// DialogueRuntimeRoot 下部分工程里 Canvas 会带名为 <c>background</c> 的装饰底图；与场景里 CrimeScene 等自己摆的背景无关。
    /// 进场景时若保留该物体，会盖住画面；与手动在运行时删掉该子物体效果一致，这里在 Awake 统一关掉。
    /// </summary>
    private void HideRuntimeDialoguePrefabBackgroundLayer()
    {
        Transform root = transform.root;
        if (root.GetComponent<DialogueRuntimeRootMarker>() == null)
        {
            return;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && string.Equals(t.name, "background", StringComparison.OrdinalIgnoreCase))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 全屏底图 / 立绘槽位上的 <see cref="Image"/> 若开启 raycast，会在 <see cref="dialoguePanel"/> 关闭时仍挡住下层走廊等可点物体。
    /// 这些层不负责接收输入，统一关闭射线目标。
    /// </summary>
    private void PassThroughDecorativeCanvasImages()
    {
        Transform root = dialoguePanel != null ? dialoguePanel.transform.root : transform.root;
        SetImageRaycastOnNamedChild(root, "background", false);
        SetImageRaycastOnNamedChild(root, "StageLeftImage", false);
        SetImageRaycastOnNamedChild(root, "StageRightImage", false);
        SetImageRaycastOnNamedChild(root, "StageCenterImage", false);
    }

    private static void SetImageRaycastOnNamedChild(Transform root, string objectName, bool raycastTarget)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || !string.Equals(t.name, objectName, StringComparison.Ordinal))
            {
                continue;
            }

            Image image = t.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = raycastTarget;
            }

            return;
        }
    }

    private void EnsureDialogueCanvasesRenderOnTop()
    {
        // DialogueManager 与主 Canvas 在 DialogueRuntimeRoot 下常为兄弟节点，不能只扫本物体子级。
        Transform scanRoot = dialoguePanel != null ? dialoguePanel.transform.root : transform.root;
        Canvas[] canvases = scanRoot.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null)
            {
                continue;
            }

            canvas.overrideSorting = true;
            if (canvas.sortingOrder < DialogueCanvasSortingOrder)
            {
                canvas.sortingOrder = DialogueCanvasSortingOrder;
            }
        }
    }

    /// <summary>
    /// 对话 END 带 nextSceneName 时：已在目标场景则不再 Load；在桌子/窗/尸体近景时不自动跳回案发包厢（JSON 里多为 CrimeScene）。
    /// </summary>
    private static bool ShouldSuppressDialogueAutoSceneLoad(string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            return false;
        }

        string activeName = SceneManager.GetActiveScene().name;
        if (string.Equals(activeName, targetSceneName, StringComparison.Ordinal))
        {
            return true;
        }

        bool returningToCabin = string.Equals(targetSceneName, SceneLoader.SCENE_CRIME_SCENE, StringComparison.Ordinal);
        if (!returningToCabin)
        {
            return false;
        }

        return string.Equals(activeName, SceneLoader.SCENE_RATCHETT_TABLE, StringComparison.Ordinal)
            || string.Equals(activeName, SceneLoader.SCENE_RATCHETT_WINDOW, StringComparison.Ordinal)
            || string.Equals(activeName, SceneLoader.SCENE_RATCHETT_BODY, StringComparison.Ordinal);
    }

    /// <summary>
    /// CrimeScene 搜证对话：与 <see cref="DialogueData.mainStoryBlockDeferredRewardSkip"/> 相同，
    /// 有待收录的笔记奖励时禁止左键跳过，须右键收录后方可继续。
    /// </summary>
    private bool TreatDeferredNotebookRewardsAsMandatoryCollect()
    {
        if (currentDialogue != null && currentDialogue.mainStoryBlockDeferredRewardSkip)
        {
            return true;
        }

        return string.Equals(SceneManager.GetActiveScene().name, SceneLoader.SCENE_CRIME_SCENE, StringComparison.Ordinal);
    }

    void Update()
    {
        if (!waitingForClickAdvance || !dialoguePanel.activeSelf)
        {
            return;
        }

        if (EvidencePanelUI.IsNotebookOpen)
        {
            return;
        }

        if (EvidencePanelUI.IsPointerOverNotebookArea())
        {
            return;
        }
        
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (PerceptionMomentPresenter.IsBlockingInput)
        {
            return;
        }

        if (InGamePauseMenuController.IsBlockingGameInput())
        {
            return;
        }

        if (waitMouseReleaseAfterEnter)
        {
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                waitMouseReleaseAfterEnter = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryApplyDeferredNotebookRewardsFromRightClick();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (pendingDeferredNotebookRewards != null
                && TreatDeferredNotebookRewardsAsMandatoryCollect())
            {
                return;
            }

            AdvanceToNode(pendingNextNodeId, pendingNextDialogueId, pendingNextSceneName);
        }
    }

    public void StartDialogue(string characterId)
    {
        StartDialogueInternal(characterId, null);
    }

    /// <param name="restoredPendingRewards">读档时恢复未收录的延迟奖励；平常传 null。</param>
    public void StartDialogueAtNode(string characterId, string nodeId, NotebookRewards restoredPendingRewards = null)
    {
        StartDialogueInternal(characterId, string.IsNullOrEmpty(nodeId) ? null : nodeId);
        if (restoredPendingRewards != null && NotebookRewardsHasAny(restoredPendingRewards))
        {
            pendingDeferredNotebookRewards = restoredPendingRewards;
            RefreshDialogueBodyForDeferredHints();
        }
    }

    private void StartDialogueInternal(string characterId, string overrideStartNodeId)
    {
        if (!ValidateUIBindings())
        {
            return;
        }

        if (speakerPortrait != null && !useLeftSpeakerPortrait)
        {
            speakerPortrait.sprite = null;
            speakerPortrait.enabled = false;
        }

        // ????JSON???
        TextAsset jsonFile = Resources.Load<TextAsset>($"Dialogue/{characterId}");
        if (jsonFile == null)
        {
            jsonFile = Resources.Load<TextAsset>($"Notebook/{characterId}");
        }

        if (jsonFile == null)
        {
            Debug.LogError($"???????????: Dialogue/{characterId} 或 Notebook/{characterId}");
            return;
        }

        currentDialogue = JsonUtility.FromJson<DialogueData>(jsonFile.text);
        if (currentDialogue == null)
        {
            Debug.LogError($"??????????: {characterId}");
            return;
        }

        currentDialogue.BuildLookup();
        currentDialogueResourceId = characterId ?? string.Empty;
        ResetNoticeIntroPlaybackState();
        AbandonPendingDeferredNotebookRewards();

        DialogueSceneBackdropBinder.ApplyIfAny(currentDialogue.backdropImageId);

        currentNodeId = string.IsNullOrEmpty(overrideStartNodeId)
            ? currentDialogue.startNodeId
            : overrideStartNodeId;

        if (currentDialogue.nodeLookup == null || !currentDialogue.nodeLookup.ContainsKey(currentNodeId))
        {
            Debug.LogError($"?????????????: {characterId}/{currentNodeId}");
            return;
        }

        dialoguePanel.SetActive(true);
        EvidencePanelUI.EnsureSingleEventSystem();
        EnsureRuntimeVisualContainers();
        EnsureDialogueCanvasesRenderOnTop();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Dialogue);
        }
        ShowCurrentNode();
    }

    void ShowCurrentNode()
    {
        if (currentDialogue == null || currentDialogue.nodeLookup == null || !currentDialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node))
        {
            Debug.LogError($"??????????????: {currentNodeId}");
            EndDialogue();
            return;
        }

        pendingDeferredNotebookRewards = null;

        bool noticeHotspotNav = IsNoticeNode(node)
            && node.hotspots != null
            && node.hotspots.Count > 0;
        bool deferNotebook = ShouldDeferNotebookRewards(node) && !noticeHotspotNav;

        if (DetectiveNotebookManager.Instance != null && node.rewards != null)
        {
            if (!deferNotebook)
            {
                DetectiveNotebookManager.Instance.ApplyRewards(node.rewards);
            }
            else
            {
                pendingDeferredNotebookRewards = node.rewards;
            }
        }

        speakerNameText.text = GetDisplaySpeakerName(node);
        bool showPickupHintRow = pendingDeferredNotebookRewards != null;
        dialogueText.text = BuildDialogueDisplayText(node, deferNotebook || noticeHotspotNav, showPickupHintRow);
        ApplyFontIfNeeded(speakerNameText);
        ApplyFontIfNeeded(dialogueText);
        ApplyPresentedImage(node.presentedImage);
        if (stageController != null)
        {
            if (node.stage != null)
            {
                stageController.ApplyStage(node.stage, node.speakerCharacterId);
            }
            else
            {
                stageController.ExecuteCommands(node.stageCommands);
                if (!string.IsNullOrEmpty(node.focusSlotId))
                {
                    stageController.FocusSlot(node.focusSlotId);
                }
            }
        }

        // ????????
        if (useLeftSpeakerPortrait && !string.IsNullOrEmpty(node.portrait))
        {
            Sprite portrait = Resources.Load<Sprite>($"Characters/{node.portrait}");
            if (portrait != null)
            {
                if (speakerPortrait != null)
                {
                    speakerPortrait.sprite = portrait;
                    speakerPortrait.enabled = true;
                    ApplySpeakerPortraitAnxiousShake(node.portrait);
                }
            }
        }
        else
        {
            if (speakerPortrait != null)
            {
                speakerPortrait.sprite = null;
                speakerPortrait.enabled = false;
                ApplySpeakerPortraitAnxiousShake(null);
            }
        }

        // ????????
        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);
        ClearHotspots();

        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;

        if (IsNoticeNode(node))
        {
            CreateHotspots(node.hotspots, true);
            SetNoticeHotspotsInteractable(false);
            StopNoticeMomentRoutineIfAny();
            if (activeHotspots.Count > 0)
            {
                Debug.LogWarning($"[PerceptionMoment] notice 节点「{currentNodeId}」将播放察觉开场，热点数 {activeHotspots.Count}。");
                noticeMomentRoutine = StartCoroutine(PlayNoticeMomentThenUnlock());
            }
            else
            {
                Debug.LogWarning(
                    $"[PerceptionMoment] notice 节点「{currentNodeId}」没有 hotspots，不会播放察觉开场/立绘气泡。");
            }

            if (activeHotspots.Count == 0)
            {
                pendingNextNodeId = string.IsNullOrEmpty(node.nextNodeId) ? "END" : node.nextNodeId;
                pendingNextDialogueId = node.nextDialogueId;
                pendingNextSceneName = node.nextSceneName;
                waitingForClickAdvance = true;
                waitMouseReleaseAfterEnter = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            }
            return;
        }

        CreateHotspots(node.hotspots, false);

        // 无选项时，改为点击对话框继续
        if (node.options == null || node.options.Count == 0)
        {
            if (activeHotspots.Count > 0 && string.IsNullOrEmpty(node.nextNodeId))
            {
                return;
            }

            pendingNextNodeId = string.IsNullOrEmpty(node.nextNodeId) ? "END" : node.nextNodeId;
            pendingNextDialogueId = node.nextDialogueId;
            pendingNextSceneName = node.nextSceneName;
            waitingForClickAdvance = true;
            // 防止由上一次点击带来的误触发
            waitMouseReleaseAfterEnter = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            return;
        }

        foreach (var option in node.options)
        {
            if (DetectiveNotebookManager.Instance != null && !DetectiveNotebookManager.Instance.MeetsRequirements(option.requirements))
            {
                continue;
            }

            // ????????????????
            if (!string.IsNullOrEmpty(option.requiredClueId))
            {
                if (EvidenceManager.Instance == null || !EvidenceManager.Instance.HasClue(option.requiredClueId))
                    continue;
            }

            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            TMP_Text tmpText = btnObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = option.text;
                ApplyFontIfNeeded(tmpText);
            }
            else
            {
                Text legacyText = btnObj.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = option.text;
                }
            }
            btnObj.GetComponent<Button>().onClick.AddListener(() => SelectOption(option));
        }
    }

    void SelectOption(DialogueOption option)
    {
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        AbandonPendingDeferredNotebookRewards();

        if (DetectiveNotebookManager.Instance != null)
        {
            DetectiveNotebookManager.Instance.ApplyRewards(option.rewards);
        }

        // ???????
        if (!string.IsNullOrEmpty(option.clueToAdd))
        {
            if (EvidenceManager.Instance != null)
            {
                EvidenceManager.Instance.AddClue(option.clueToAdd);
            }
        }

        // ????????
        AdvanceToNode(option.nextNodeId, option.nextDialogueId, option.nextSceneName);
    }

    private void AdvanceToNode(string nextNodeId)
    {
        AdvanceToNode(nextNodeId, null, null);
    }

    private void AdvanceToNode(string nextNodeId, string nextDialogueId)
    {
        AdvanceToNode(nextNodeId, nextDialogueId, null);
    }

    private void AdvanceToNode(string nextNodeId, string nextDialogueId, string nextSceneName)
    {
        AbandonPendingDeferredNotebookRewards();

        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;

        if (nextNodeId == "END")
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (ShouldSuppressDialogueAutoSceneLoad(nextSceneName))
                {
                    EndDialogue();
                    return;
                }

                LoadScene(nextSceneName);
                return;
            }

            if (!string.IsNullOrEmpty(nextDialogueId))
            {
                StartDialogue(nextDialogueId);
                return;
            }

            EndDialogue();
            return;
        }

        currentNodeId = nextNodeId;
        ShowCurrentNode();
    }

    private void ApplySpeakerPortraitAnxiousShake(string portraitName)
    {
        if (speakerPortrait == null)
        {
            return;
        }

        PortraitAnxiousShake shake = speakerPortrait.GetComponent<PortraitAnxiousShake>();
        if (shake == null)
        {
            shake = speakerPortrait.gameObject.AddComponent<PortraitAnxiousShake>();
        }

        shake.CaptureBasePose();
        shake.ApplyPortrait(portraitName);
    }

    private void ApplyFontIfNeeded(TMP_Text textComponent)
    {
        if (dialogueFontOverride == null || textComponent == null)
        {
            return;
        }

        textComponent.font = dialogueFontOverride;
    }

    void EndDialogue()
    {
        AbandonPendingDeferredNotebookRewards();
        ResetNoticeIntroPlaybackState();

        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        ClearHotspots();
        HidePresentedImage();
        if (stageController != null)
        {
            stageController.HideAll();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Exploring);
        }

        // ????????
        if (GameManager.Instance != null && currentDialogue != null)
        {
            string dialogueKey = GetCurrentDialogueKey();
            if (dialogueKey == "mrs_hubbard")
                GameManager.Instance.HasTalkedToMrsHubbard = true;
            else if (dialogueKey == "count_andrenyi")
                GameManager.Instance.HasTalkedToCountAndrenyi = true;
        }

        CrimeSceneEvidenceGrantBridge.GrantPendingIfAny();
        EvidencePanelUI.EnsureSingleEventSystem();
        DialogueSceneBackdropBinder.ApplyIfAny(null);
        PassThroughDecorativeCanvasImages();
    }

    private string GetCurrentDialogueKey()
    {
        if (currentDialogue == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(currentDialogue.characterId))
        {
            return currentDialogue.characterId;
        }

        if (!string.IsNullOrEmpty(currentDialogue.sceneId))
        {
            return currentDialogue.sceneId;
        }

        return currentDialogue.dialogueId;
    }

    private bool ValidateUIBindings()
    {
        if (dialoguePanel == null || speakerNameText == null || dialogueText == null || optionsContainer == null || optionButtonPrefab == null)
        {
            Debug.LogError("DialogueManager: UI 引用缺失，请检查 dialoguePanel/speakerNameText/dialogueText/optionsContainer/optionButtonPrefab。");
            return false;
        }

        if (speakerPortrait == null)
        {
            Debug.LogWarning("DialogueManager: speakerPortrait 未绑定，将跳过头像显示。");
        }

        return true;
    }

    private void EnsureRuntimeVisualContainers()
    {
        Transform root = dialoguePanel != null && dialoguePanel.transform.parent != null
            ? dialoguePanel.transform.parent
            : transform;

        if (presentedImageDisplay == null)
        {
            GameObject imageObject = new GameObject("PresentedImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.25f);
            rect.anchorMax = new Vector2(0.8f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            presentedImageDisplay = imageObject.GetComponent<Image>();
            presentedImageDisplay.preserveAspect = true;
            presentedImageDisplay.raycastTarget = false;
            imageObject.SetActive(false);

            if (dialoguePanel != null)
            {
                imageObject.transform.SetSiblingIndex(dialoguePanel.transform.GetSiblingIndex());
            }
        }

        if (hotspotContainer == null)
        {
            GameObject hotspotObject = new GameObject("DialogueHotspots", typeof(RectTransform));
            hotspotObject.transform.SetParent(root, false);
            hotspotContainer = hotspotObject.GetComponent<RectTransform>();
            hotspotContainer.anchorMin = Vector2.zero;
            hotspotContainer.anchorMax = Vector2.one;
            hotspotContainer.offsetMin = Vector2.zero;
            hotspotContainer.offsetMax = Vector2.zero;
            hotspotObject.transform.SetAsLastSibling();
        }
    }

    private void ApplyPresentedImage(PresentedImageData imageData)
    {
        EnsureRuntimeVisualContainers();
        if (imageData == null || string.IsNullOrEmpty(imageData.imageId))
        {
            HidePresentedImage();
            return;
        }

        Sprite sprite = LoadPresentedSprite(imageData.imageId);
        if (sprite == null)
        {
            Debug.LogWarning($"DialogueManager: 找不到展示图片 {imageData.imageId}");
            HidePresentedImage();
            return;
        }

        presentedImageDisplay.sprite = sprite;
        ApplyPresentedImagePosition(imageData.position);
        presentedImageDisplay.gameObject.SetActive(true);
    }

    private Sprite LoadPresentedSprite(string imageId)
    {
        string[] paths =
        {
            $"PresentedImages/{imageId}",
            $"UI/{imageId}",
            $"backgrounds/{imageId}",
            $"Evidence/{imageId}",
            $"Characters/{imageId}",
            imageId
        };

        foreach (string path in paths)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private void ApplyPresentedImagePosition(string position)
    {
        RectTransform rect = presentedImageDisplay.rectTransform;
        string p = string.IsNullOrEmpty(position) ? "center" : position.ToLowerInvariant();

        if (p == "full")
        {
            SetAnchors(rect, 0.05f, 0.08f, 0.95f, 0.95f);
        }
        else if (p == "left")
        {
            SetAnchors(rect, 0.05f, 0.25f, 0.48f, 0.85f);
        }
        else if (p == "right")
        {
            SetAnchors(rect, 0.52f, 0.25f, 0.95f, 0.85f);
        }
        else
        {
            SetAnchors(rect, 0.2f, 0.25f, 0.8f, 0.85f);
        }
    }

    private void HidePresentedImage()
    {
        if (presentedImageDisplay == null)
        {
            return;
        }

        presentedImageDisplay.sprite = null;
        presentedImageDisplay.gameObject.SetActive(false);
    }

    private bool IsNoticeNode(DialogueNode node)
    {
        return node != null && node.nodeType == "notice";
    }

    private string GetDisplaySpeakerName(DialogueNode node)
    {
        if (node == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(node.speakerName))
        {
            return node.speakerName;
        }

        if (!string.IsNullOrEmpty(node.speaker))
        {
            return node.speaker;
        }

        return IsNoticeNode(node) ? "系统" : "";
    }

    private string GetDisplayText(DialogueNode node)
    {
        if (node == null)
        {
            return "";
        }

        if (IsNoticeNode(node) && !string.IsNullOrEmpty(node.prompt))
        {
            if (!string.IsNullOrEmpty(node.text))
            {
                return node.text + "\n\n" + node.prompt;
            }

            return node.prompt;
        }

        return node.text;
    }

    private void CreateHotspots(List<DialogueHotspot> hotspots, bool allowFailure)
    {
        if (hotspots == null || hotspots.Count == 0)
        {
            return;
        }

        EnsureRuntimeVisualContainers();
        for (int i = 0; i < hotspots.Count; i++)
        {
            DialogueHotspot hotspot = hotspots[i];
            if (hotspot == null)
            {
                continue;
            }

            GameObject hotspotObject = CreateHotspotObject(hotspot, i);
            Button button = hotspotObject.GetComponent<Button>();
            if (button == null)
            {
                button = hotspotObject.AddComponent<Button>();
            }

            DialogueHotspot capturedHotspot = hotspot;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnHotspotClicked(capturedHotspot, allowFailure));
            activeHotspots.Add(hotspotObject);
        }
    }

    private GameObject CreateHotspotObject(DialogueHotspot hotspot, int index)
    {
        GameObject hotspotObject;
        if (hotspotButtonPrefab != null)
        {
            hotspotObject = Instantiate(hotspotButtonPrefab, hotspotContainer);
        }
        else
        {
            hotspotObject = new GameObject($"Hotspot_{hotspot.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            hotspotObject.transform.SetParent(hotspotContainer, false);
            Image image = hotspotObject.GetComponent<Image>();
            image.color = new Color(1f, 0.82f, 0.12f, 0.75f);
        }

        hotspotObject.name = string.IsNullOrEmpty(hotspot.id) ? $"Hotspot_{index}" : $"Hotspot_{hotspot.id}";
        RectTransform rect = hotspotObject.GetComponent<RectTransform>();
        ApplyHotspotRect(rect, hotspot, index);

        TMP_Text tmpText = hotspotObject.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = string.IsNullOrEmpty(hotspot.label) ? "" : hotspot.label;
            ApplyFontIfNeeded(tmpText);
        }

        Text legacyText = hotspotObject.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = string.IsNullOrEmpty(hotspot.label) ? "" : hotspot.label;
        }

        return hotspotObject;
    }

    private void ApplyHotspotRect(RectTransform rect, DialogueHotspot hotspot, int index)
    {
        if (rect == null)
        {
            return;
        }

        if (hotspot.width > 0f && hotspot.height > 0f)
        {
            SetAnchors(rect, hotspot.x, hotspot.y, hotspot.x + hotspot.width, hotspot.y + hotspot.height);
            return;
        }

        Vector2 center = GetDefaultHotspotCenter(hotspot, index);
        float size = 0.065f;
        SetAnchors(rect, center.x - size * 0.5f, center.y - size * 0.5f, center.x + size * 0.5f, center.y + size * 0.5f);
    }

    private Vector2 GetDefaultHotspotCenter(DialogueHotspot hotspot, int index)
    {
        string targetType = hotspot != null && !string.IsNullOrEmpty(hotspot.targetType)
            ? hotspot.targetType
            : "presented_image";

        if (targetType == "dialogue_text")
        {
            return new Vector2(0.78f, 0.22f + index * 0.07f);
        }

        if (targetType == "portrait")
        {
            return new Vector2(0.72f, 0.6f - index * 0.08f);
        }

        return new Vector2(0.5f + index * 0.08f, 0.55f);
    }

    private IEnumerator PlayNoticeMomentThenUnlock()
    {
        string noticeNodeId = currentNodeId;
        if (!noticeIntroPlayedNodeIds.Contains(noticeNodeId))
        {
            yield return PerceptionMomentPresenter.PlayIntroVideoRoutine();
            noticeIntroPlayedNodeIds.Add(noticeNodeId);
        }

        PerceptionMomentPresenter.ShowNoticeChrome();
        SetNoticeHotspotsInteractable(true);
        noticeMomentRoutine = null;
    }

    private void ResetNoticeIntroPlaybackState()
    {
        noticeIntroPlayedNodeIds.Clear();
    }

    private void StopNoticeMomentRoutineIfAny()
    {
        if (noticeMomentRoutine == null)
        {
            return;
        }

        StopCoroutine(noticeMomentRoutine);
        noticeMomentRoutine = null;
        PerceptionMomentPresenter.DismissNoticeChrome();
    }

    private void SetNoticeHotspotsInteractable(bool interactable)
    {
        foreach (GameObject hotspot in activeHotspots)
        {
            if (hotspot == null)
            {
                continue;
            }

            Button button = hotspot.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = interactable;
            }

            Image image = hotspot.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = interactable;
            }
        }
    }

    private void OnHotspotClicked(DialogueHotspot hotspot, bool allowFailure)
    {
        if (PerceptionMomentPresenter.IsBlockingInput)
        {
            return;
        }

        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (EvidencePanelUI.IsNotebookOpen)
        {
            return;
        }

        if (hotspot == null)
        {
            return;
        }

        string nextNodeId = hotspot.successNodeId;
        if (allowFailure && !hotspot.correct)
        {
            nextNodeId = hotspot.failureNodeId;
        }

        if (string.IsNullOrEmpty(nextNodeId))
        {
            Debug.LogWarning($"DialogueManager: 热点 {hotspot.id} 没有可跳转节点。");
            return;
        }

        if (!allowFailure || hotspot.correct)
        {
            PerceptionMomentPresenter.DismissNoticeChrome();
        }

        AdvanceToNode(nextNodeId);
    }

    private void ClearHotspots()
    {
        StopNoticeMomentRoutineIfAny();
        PerceptionMomentPresenter.DismissNoticeChrome();
        foreach (GameObject hotspot in activeHotspots)
        {
            if (hotspot != null)
            {
                Destroy(hotspot);
            }
        }
        activeHotspots.Clear();
    }

    private void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(Mathf.Clamp01(minX), Mathf.Clamp01(minY));
        rect.anchorMax = new Vector2(Mathf.Clamp01(maxX), Mathf.Clamp01(maxY));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static bool NotebookRewardsHasAny(NotebookRewards rewards)
    {
        if (rewards == null)
        {
            return false;
        }

        if (rewards.evidenceToAdd != null && rewards.evidenceToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.testimonyToAdd != null && rewards.testimonyToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.doubtToAdd != null && rewards.doubtToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.evidenceStageToUpdate != null && rewards.evidenceStageToUpdate.Count > 0)
        {
            return true;
        }

        if (rewards.testimonyStageToUpdate != null && rewards.testimonyStageToUpdate.Count > 0)
        {
            return true;
        }

        if (rewards.doubtStageToUpdate != null && rewards.doubtStageToUpdate.Count > 0)
        {
            return true;
        }

        return false;
    }

    private bool ShouldDeferNotebookRewards(DialogueNode node)
    {
        if (node == null || !NotebookRewardsHasAny(node.rewards))
        {
            return false;
        }

        if (node.dialogueSkipDeferNotebookRewards)
        {
            return false;
        }

        if (node.options != null && node.options.Count > 0)
        {
            return false;
        }

        if (node.dialogueDeferNotebookRewards)
        {
            return true;
        }

        return deferNotebookRewardsByDefaultWhenRewardsPresent;
    }

    private void RefreshDialogueBodyForDeferredHints()
    {
        if (dialogueText == null || currentDialogue == null || currentDialogue.nodeLookup == null)
        {
            return;
        }

        if (!currentDialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node))
        {
            return;
        }

        bool noticeHotspotNav = IsNoticeNode(node)
            && node.hotspots != null
            && node.hotspots.Count > 0;
        bool ruleDefer = ShouldDeferNotebookRewards(node) && !noticeHotspotNav;
        bool showPickupHintRow = pendingDeferredNotebookRewards != null;
        speakerNameText.text = GetDisplaySpeakerName(node);
        dialogueText.text = BuildDialogueDisplayText(node, ruleDefer || noticeHotspotNav, showPickupHintRow);
        ApplyFontIfNeeded(dialogueText);
    }

    private string BuildDialogueDisplayText(DialogueNode node, bool wrapHighlightPhrase, bool showPickupHintRow)
    {
        string raw = GetDisplayText(node) ?? string.Empty;

        if (wrapHighlightPhrase && !string.IsNullOrEmpty(node.dialogueHighlightPhrase))
        {
            string phrase = node.dialogueHighlightPhrase;
            int idx = raw.IndexOf(phrase, StringComparison.Ordinal);
            if (idx >= 0)
            {
                raw = raw.Substring(0, idx) + "<b>" + phrase + "</b>" + raw.Substring(idx + phrase.Length);
            }
        }

        if (showPickupHintRow)
        {
            string hintLine;
            if (string.IsNullOrWhiteSpace(node.dialogueRewardInteractHint))
            {
                bool mainStoryBlock = TreatDeferredNotebookRewardsAsMandatoryCollect();
                hintLine = mainStoryBlock
                    ? mainStoryDeferNotebookRewardsHintRichText
                    : deferNotebookRewardsHintRichText;
            }
            else
            {
                string custom = node.dialogueRewardInteractHint.Trim();
                bool mainStoryBlock = TreatDeferredNotebookRewardsAsMandatoryCollect();
                if (custom.Length > 0 && custom[0] == '<')
                {
                    hintLine = custom;
                }
                else if (mainStoryBlock)
                {
                    hintLine = $"<size=88%><color=#D4C4A8>{custom}</color><color=#8A8074> · 收录后方可继续</color></size>";
                }
                else
                {
                    hintLine = $"<size=88%><color=#D4C4A8>{custom}</color><color=#8A8074> · 左键：继续（跳过线索）</color></size>";
                }
            }

            raw = raw + "\n" + hintLine;
        }

        return raw;
    }

    private void TryApplyDeferredNotebookRewardsFromRightClick()
    {
        if (pendingDeferredNotebookRewards == null)
        {
            return;
        }

        if (DetectiveNotebookManager.Instance != null)
        {
            DetectiveNotebookManager.Instance.ApplyRewards(pendingDeferredNotebookRewards);
        }

        pendingDeferredNotebookRewards = null;
        RefreshDialogueBodyForDeferredHints();
    }

    private void AbandonPendingDeferredNotebookRewards()
    {
        pendingDeferredNotebookRewards = null;
    }

    private void LoadScene(string sceneName)
    {
        EndDialogue();
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            ScreenFader.LoadSceneWithFade(sceneName);
        }
    }
}
