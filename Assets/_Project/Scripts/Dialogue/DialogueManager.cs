using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Video;

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

    [Header("对话选项区布局")]
    [Tooltip("叠在 OptionsContainer 预制体上的 anchoredPosition 偏移（像素）。Y 为正通常使整块选项按钮在画面上移；无选项节点会自动还原基准位置。")]
    [SerializeField] private Vector2 optionsContainerAnchoredPositionOffset = new Vector2(0f, 72f);

    [Tooltip("察觉开场（notice + hotspots）结束并解锁热点后，下一次出现对话选项时在选项区额外叠加的 anchoredPosition.y（像素）；只生效一次，不影响未走察觉的对话选项。")]
    [SerializeField] private float noticePerceptionOptionsExtraAnchoredPositionY = 200f;

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
    private Coroutine dialogueVideoRoutine;

    private const float DialogueVideoPrepareTimeoutSeconds = 8f;
    private const float DialogueVideoPlaybackPadSeconds = 0.75f;
    private const int DialogueVideoOverlaySortingOrder = 9650;

    private NotebookRewards pendingDeferredNotebookRewards;

    private string currentDialogueResourceId = string.Empty;

    private Vector2 optionsContainerAnchoredPositionBase;
    private bool optionsContainerLayoutBaseCached;
    private float pendingNoticePerceptionOptionsLayoutBoostY;

    private const string RuntimePrefabResourcePath = "UI/DialogueRuntimeRoot";

    private void CacheOptionsContainerLayoutBaseIfNeeded()
    {
        if (optionsContainerLayoutBaseCached || optionsContainer == null)
        {
            return;
        }

        RectTransform rt = optionsContainer as RectTransform;
        if (rt == null)
        {
            return;
        }

        optionsContainerAnchoredPositionBase = rt.anchoredPosition;
        optionsContainerLayoutBaseCached = true;
    }

    private void ResetOptionsContainerToPrefabLayout()
    {
        if (!optionsContainerLayoutBaseCached || optionsContainer == null)
        {
            return;
        }

        RectTransform rt = optionsContainer as RectTransform;
        if (rt == null)
        {
            return;
        }

        rt.anchoredPosition = optionsContainerAnchoredPositionBase;
    }

    private void ApplyOptionsContainerLayoutOffset(bool consumeNoticePerceptionExtraLayoutBoost = false)
    {
        CacheOptionsContainerLayoutBaseIfNeeded();
        if (!optionsContainerLayoutBaseCached || optionsContainer == null)
        {
            return;
        }

        RectTransform rt = optionsContainer as RectTransform;
        if (rt == null)
        {
            return;
        }

        Vector2 extra = Vector2.zero;
        if (consumeNoticePerceptionExtraLayoutBoost && pendingNoticePerceptionOptionsLayoutBoostY != 0f)
        {
            extra.y = pendingNoticePerceptionOptionsLayoutBoostY;
            pendingNoticePerceptionOptionsLayoutBoostY = 0f;
        }

        rt.anchoredPosition = optionsContainerAnchoredPositionBase + optionsContainerAnchoredPositionOffset + extra;
    }

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
        CacheOptionsContainerLayoutBaseIfNeeded();
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
    /// 案发包厢 <see cref="SceneLoader.SCENE_CRIME_SCENE"/> 与二级近景（桌子 / 窗 / 尸体）搜证对话：
    /// 与 <see cref="DialogueData.mainStoryBlockDeferredRewardSkip"/> 相同，有待收录的笔记奖励时禁止左键跳过，
    /// 须右键收录后方可继续；否则左键会 <see cref="AbandonPendingDeferredNotebookRewards"/>，对话里的奖励不会进笔记也无弹窗。
    /// </summary>
    private bool TreatDeferredNotebookRewardsAsMandatoryCollect()
    {
        if (currentDialogue != null && currentDialogue.mainStoryBlockDeferredRewardSkip)
        {
            return true;
        }

        string scene = SceneManager.GetActiveScene().name;
        return string.Equals(scene, SceneLoader.SCENE_CRIME_SCENE, StringComparison.Ordinal)
            || string.Equals(scene, SceneLoader.SCENE_RATCHETT_TABLE, StringComparison.Ordinal)
            || string.Equals(scene, SceneLoader.SCENE_RATCHETT_WINDOW, StringComparison.Ordinal)
            || string.Equals(scene, SceneLoader.SCENE_RATCHETT_BODY, StringComparison.Ordinal);
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

        if (DoubtInquiryOverlayPresenter.IsBlockingInput)
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

            TryAdvanceCurrentNode();
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
            jsonFile = Resources.Load<TextAsset>($"Notebook/doubt/{characterId}");
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

        if (node.rewards != null)
        {
            DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
            if (notebookManager != null)
            {
                if (!deferNotebook)
                {
                    notebookManager.ApplyRewards(node.rewards);
                }
                else
                {
                    pendingDeferredNotebookRewards = node.rewards;
                }
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
        ResetOptionsContainerToPrefabLayout();
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
                ResolveNextTarget(node, out pendingNextNodeId, out pendingNextDialogueId, out pendingNextSceneName);
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

            ResolveNextTarget(node, out pendingNextNodeId, out pendingNextDialogueId, out pendingNextSceneName);
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

        ApplyOptionsContainerLayoutOffset(true);
    }

    void SelectOption(DialogueOption option)
    {
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (DoubtInquiryOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        AbandonPendingDeferredNotebookRewards();

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (notebookManager != null)
        {
            notebookManager.ApplyRewards(option.rewards);
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

    private void TryAdvanceCurrentNode()
    {
        if (currentDialogue == null
            || currentDialogue.nodeLookup == null
            || string.IsNullOrEmpty(currentNodeId)
            || !currentDialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node))
        {
            AdvanceToNode(pendingNextNodeId, pendingNextDialogueId, pendingNextSceneName);
            return;
        }

        if (string.IsNullOrWhiteSpace(node.dialogueVideoResourcePath))
        {
            AdvanceToNode(pendingNextNodeId, pendingNextDialogueId, pendingNextSceneName);
            return;
        }

        if (dialogueVideoRoutine != null)
        {
            return;
        }

        string videoPath = node.dialogueVideoResourcePath.Trim();
        string nextNodeId = pendingNextNodeId;
        string nextDialogueId = pendingNextDialogueId;
        string nextSceneName = pendingNextSceneName;
        waitingForClickAdvance = false;
        dialogueVideoRoutine = StartCoroutine(PlayDialogueVideoThenAdvance(videoPath, nextNodeId, nextDialogueId, nextSceneName));
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

    private void ResolveNextTarget(DialogueNode node, out string nextNodeId, out string nextDialogueId, out string nextSceneName)
    {
        nextNodeId = null;
        nextDialogueId = null;
        nextSceneName = null;

        DialogueConditionalNext conditionalBranch = GetMatchedConditionalNext(node);
        if (conditionalBranch != null)
        {
            nextNodeId = conditionalBranch.nextNodeId;
            nextDialogueId = conditionalBranch.nextDialogueId;
            nextSceneName = conditionalBranch.nextSceneName;
        }
        else if (node != null)
        {
            nextNodeId = node.nextNodeId;
            nextDialogueId = node.nextDialogueId;
            nextSceneName = node.nextSceneName;
        }

        if (string.IsNullOrEmpty(nextNodeId))
        {
            nextNodeId = "END";
        }
    }

    private DialogueConditionalNext GetMatchedConditionalNext(DialogueNode node)
    {
        if (node == null || node.conditionalNext == null || node.conditionalNext.Count == 0)
        {
            return null;
        }

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.Instance;
        foreach (DialogueConditionalNext branch in node.conditionalNext)
        {
            if (branch == null)
            {
                continue;
            }

            if (branch.requirements == null)
            {
                return branch;
            }

            if (notebookManager != null && notebookManager.MeetsRequirements(branch.requirements))
            {
                return branch;
            }
        }

        return null;
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
        pendingNoticePerceptionOptionsLayoutBoostY = 0f;

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
        FinalizeHotspotAndPresentedImageStack();
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

        FinalizeHotspotAndPresentedImageStack();
    }

    /// <summary>
    /// 与 <see cref="hotspotContainer"/> 同父节点时，后序兄弟会盖住先序兄弟。
    /// 热点容器置顶以便点击；若有展示图则插在其正下方一层，作背景（如察觉 + 房间示意图）。
    /// </summary>
    private void FinalizeHotspotAndPresentedImageStack()
    {
        if (hotspotContainer == null)
        {
            return;
        }

        hotspotContainer.SetAsLastSibling();

        if (presentedImageDisplay == null || !presentedImageDisplay.gameObject.activeSelf)
        {
            return;
        }

        Transform img = presentedImageDisplay.transform;
        Transform hot = hotspotContainer.transform;
        if (img.parent != hot.parent)
        {
            return;
        }

        img.SetSiblingIndex(hot.GetSiblingIndex());
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
        bool fromPrefab = hotspotButtonPrefab != null;
        TryApplyHotspotImageFromResources(hotspotObject, hotspot, fromPrefab);

        TMP_Text tmpText = hotspotObject.GetComponentInChildren<TMP_Text>(true);
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

    private static void TryApplyHotspotImageFromResources(GameObject hotspotObject, DialogueHotspot hotspot, bool instantiatedFromPrefab)
    {
        if (hotspotObject == null || hotspot == null)
        {
            return;
        }

        // 使用 hotspotButtonPrefab 时：保留预制体上的 Image/Sprite 与颜色（含透明度），除非 JSON 里显式写了 imageId 要换图。
        // 否则会用热点 id 去加载 Resources/UI/{id}，若存在测试用 Sprite 会覆盖预制体外观（如 macqueen_agitated_hotspot.png）。
        if (instantiatedFromPrefab && string.IsNullOrWhiteSpace(hotspot.imageId))
        {
            return;
        }

        Image image = hotspotObject.GetComponent<Image>();
        if (image == null)
        {
            image = hotspotObject.GetComponentInChildren<Image>(true);
        }

        if (image == null)
        {
            return;
        }

        Sprite sprite = null;
        string loadKey = null;

        if (!string.IsNullOrWhiteSpace(hotspot.imageId))
        {
            loadKey = hotspot.imageId.Trim();
            sprite = Resources.Load<Sprite>($"UI/{loadKey}");
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>($"backgrounds/{loadKey}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(hotspot.id))
        {
            loadKey = hotspot.id.Trim();
            sprite = Resources.Load<Sprite>($"UI/{loadKey}");
        }

        if (sprite == null)
        {
            if (!string.IsNullOrEmpty(loadKey))
            {
                Debug.LogWarning(
                    $"DialogueManager: 热点 id「{hotspot.id}」未找到 Sprite（查找 key「{loadKey}」；"
                    + (!string.IsNullOrWhiteSpace(hotspot.imageId) ? "imageId 会额外查 backgrounds）。" : "仅查 Resources/UI）。"));
            }

            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
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
        pendingNoticePerceptionOptionsLayoutBoostY = noticePerceptionOptionsExtraAnchoredPositionY;
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

        if (DoubtInquiryOverlayPresenter.IsBlockingInput)
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

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (notebookManager != null)
        {
            notebookManager.ApplyRewards(pendingDeferredNotebookRewards);
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

    /// <summary>
    /// 全屏覆盖播放 Resources 下 <see cref="VideoClip"/>（路径不含扩展名，例如 <c>MOV/open</c>）。
    /// 找不到资源或 Prepare 失败会打日志并尽快结束；结束后销毁临时覆盖层。
    /// </summary>
    public static IEnumerator PlayResourcesVideoFullscreen(string videoResourcePath)
    {
        if (string.IsNullOrWhiteSpace(videoResourcePath))
        {
            yield break;
        }

        GameObject overlayRoot = null;
        VideoPlayer player = null;
        RenderTexture renderTexture = null;

        try
        {
            VideoClip clip = Resources.Load<VideoClip>(videoResourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"DialogueManager: 未找到 VideoClip Resources/{videoResourcePath}，跳过视频。");
                yield break;
            }

            overlayRoot = CreateDialogueVideoOverlayRoot();
            GameObject videoGo = new GameObject("DialogueVideoOverlay", typeof(RectTransform));
            videoGo.transform.SetParent(overlayRoot.transform, false);
            RectTransform videoRect = videoGo.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;

            RawImage rawImage = videoGo.AddComponent<RawImage>();
            rawImage.raycastTarget = true;
            rawImage.color = Color.white;

            renderTexture = new RenderTexture(1920, 1080, 0);
            player = overlayRoot.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = renderTexture;
            player.clip = clip;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            rawImage.texture = renderTexture;

            yield return WaitUntilDialogueVideoPrepared(player);
            if (player.isPrepared)
            {
                yield return WaitUntilDialogueVideoPlaybackEnds(player, (float)clip.length);
            }
            else
            {
                Debug.LogWarning($"DialogueManager: 视频 {videoResourcePath} Prepare 失败，跳过播放。");
            }
        }
        finally
        {
            if (player != null)
            {
                player.Stop();
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }

            if (overlayRoot != null)
            {
                UnityEngine.Object.Destroy(overlayRoot);
            }
        }
    }

    private IEnumerator PlayDialogueVideoThenAdvance(string videoResourcePath, string nextNodeId, string nextDialogueId, string nextSceneName)
    {
        try
        {
            yield return PlayResourcesVideoFullscreen(videoResourcePath);
        }
        finally
        {
            dialogueVideoRoutine = null;
        }

        AdvanceToNode(nextNodeId, nextDialogueId, nextSceneName);
    }

    private static GameObject CreateDialogueVideoOverlayRoot()
    {
        GameObject root = new GameObject("DialogueVideoOverlayRoot");
        DontDestroyOnLoad(root);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = DialogueVideoOverlaySortingOrder;
        root.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return root;
    }

    private static IEnumerator WaitUntilDialogueVideoPrepared(VideoPlayer player)
    {
        player.Prepare();
        float elapsed = 0f;
        while (!player.isPrepared && elapsed < DialogueVideoPrepareTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static IEnumerator WaitUntilDialogueVideoPlaybackEnds(VideoPlayer player, float clipLengthSeconds)
    {
        player.Play();
        float maxWait = Mathf.Max(45f, clipLengthSeconds + DialogueVideoPlaybackPadSeconds);
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
}
