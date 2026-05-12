using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全屏「获得新线索 / 证词 / 疑点」弹窗。
/// 自定义 UI：Prefab 路径建议 Assets/_Project/Resources/UI/NotebookUnlockOverlay.prefab，
/// Resources.Load("UI/NotebookUnlockOverlay")。根/Dim / Panel/Title CloseButton Icon Name Body；若要先按钮再看详情，
/// 根下增加 PrimerPanel/PrimerPrompt + RevealButton，或留空交由运行时生成。
/// </summary>
public class NotebookUnlockOverlayPresenter : MonoBehaviour
{
    public static NotebookUnlockOverlayPresenter Instance { get; private set; }
    public static bool IsBlockingInput
    {
        get
        {
            return Instance != null
                && Instance.overlayRoot != null
                && Instance.overlayRoot.activeInHierarchy;
        }
    }

    private const string ChildDim = "Dim";
    private const string ChildPanel = "Panel";
    private const string ChildTitle = "Title";
    private const string ChildClose = "CloseButton";
    private const string ChildIcon = "Icon";
    private const string ChildName = "Name";
    private const string ChildBody = "Body";

    private const string ChildPrimerPanel = "PrimerPanel";
    private const string ChildPrimerPrompt = "PrimerPrompt";
    private const string ChildRevealButton = "RevealButton";

    [Header("可选：美术预制体（根下须含 Dim / Panel，Panel 下 Title / CloseButton / Icon / Name / Body）")]
    [SerializeField] private GameObject optionalOverlayPrefab;
    [SerializeField] private int overlaySortingOrder = 280;

    [Header("先按钮再弹出详情")]
    [Tooltip("为 true：先显示 PrimerPanel +「查看详情」按钮，再显示下方详情面板；遮罩在此期间不可一点就关详情。")]
    [SerializeField] private bool requireConfirmBeforeReveal = true;
    [Tooltip("{0}=弹窗大类标题（如「获得新线索」）")]
    [SerializeField] private string primerPromptFormat = "{0}\n点此查看详情";
    [SerializeField] private string primerRevealButtonLabel = "查看详情";

    [Header("可选：中文字体（留空则用 TMP 默认）")]
    [SerializeField] private TMP_FontAsset overrideFont;

    private Canvas rootCanvas;
    private GameObject overlayRoot;
    private TMP_Text titleText;
    private TMP_Text nameText;
    private TMP_Text bodyText;
    private Image iconImage;
    private Button closeButton;
    private Image dimImage;
    private Button dimCloseButton;


    private GameObject detailPanelGo;
    private GameObject primerPanelGo;
    private TMP_Text primerPromptText;
    private Button primerRevealButton;

    private bool detailPhaseActive;

    private readonly Queue<Action> pendingShows = new Queue<Action>();
    private bool isShowing;
    private DetectiveNotebookManager subscribedNotebookManager;
    private EvidenceManager subscribedEvidenceManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        NotebookUnlockOverlayPresenter existing = FindObjectOfType<NotebookUnlockOverlayPresenter>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject go = new GameObject(nameof(NotebookUnlockOverlayPresenter));
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<NotebookUnlockOverlayPresenter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureViewBuilt();
        HideImmediate();
        EnsureRuntimeSubscriptions();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRuntimeSubscriptions();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        DetachAllSubscriptions();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRuntimeSubscriptions();
    }

    private void EnsureRuntimeSubscriptions()
    {
        DetectiveNotebookManager currentNotebook = DetectiveNotebookManager.Instance;
        if (subscribedNotebookManager != currentNotebook)
        {
            if (subscribedNotebookManager != null)
            {
                subscribedNotebookManager.OnNotebookItemRevealed -= HandleNotebookRevealed;
            }

            subscribedNotebookManager = currentNotebook;
            if (subscribedNotebookManager != null)
            {
                subscribedNotebookManager.OnNotebookItemRevealed += HandleNotebookRevealed;
            }
        }

        EvidenceManager currentEvidence = EvidenceManager.Instance;
        if (subscribedEvidenceManager != currentEvidence)
        {
            if (subscribedEvidenceManager != null)
            {
                subscribedEvidenceManager.OnClueFirstCollected -= HandleClueOnlyCollected;
            }

            subscribedEvidenceManager = currentEvidence;
            if (subscribedEvidenceManager != null)
            {
                subscribedEvidenceManager.OnClueFirstCollected += HandleClueOnlyCollected;
            }
        }
    }

    private void DetachAllSubscriptions()
    {
        if (subscribedNotebookManager != null)
        {
            subscribedNotebookManager.OnNotebookItemRevealed -= HandleNotebookRevealed;
            subscribedNotebookManager = null;
        }

        if (subscribedEvidenceManager != null)
        {
            subscribedEvidenceManager.OnClueFirstCollected -= HandleClueOnlyCollected;
            subscribedEvidenceManager = null;
        }
    }

    public static void NotifySynthesisClue(string outputClueId)
    {
        if (Instance == null)
        {
            Bootstrap();
        }

        Instance?.EnqueueSynthesis(outputClueId);
    }

    private void HandleNotebookRevealed(NotebookRevealEvent e)
    {
        Enqueue(() => PresentNotebookReveal(e));
    }

    private void HandleClueOnlyCollected(string clueId)
    {
        DetectiveNotebookManager nb = DetectiveNotebookManager.Instance;
        if (nb != null && nb.HasEvidence(clueId))
        {
            return;
        }

        if (nb != null && nb.HasTestimony(clueId))
        {
            return;
        }

        if (nb != null && nb.HasDoubt(clueId))
        {
            return;
        }

        Enqueue(() => PresentLegacyClue(clueId));
    }

    private void Enqueue(Action showAction)
    {
        if (showAction == null)
        {
            return;
        }

        pendingShows.Enqueue(showAction);
        if (!isShowing)
        {
            TryShowNext();
        }
    }

    private void EnqueueSynthesis(string clueId)
    {
        Enqueue(() => PresentSynthesis(clueId));
    }

    private void TryShowNext()
    {
        if (pendingShows.Count == 0)
        {
            isShowing = false;
            HideImmediate();
            return;
        }

        isShowing = true;
        Action next = pendingShows.Dequeue();
        next.Invoke();
    }

    private void PresentNotebookReveal(NotebookRevealEvent e)
    {
        EnsureViewBuilt();
        DetectiveNotebookManager nb = DetectiveNotebookManager.Instance;
        if (nb == null)
        {
            TryShowNext();
            return;
        }

        string title = ResolveTitle(e.Kind, e.RevealType);
        string displayName;
        string body;
        Sprite sprite = null;

        switch (e.Kind)
        {
            case NotebookRevealKind.Evidence:
                PhysicalEvidenceDefinition ev = nb.GetCurrentEvidence(e.ItemId);
                displayName = ev != null && !string.IsNullOrEmpty(ev.name) ? ev.name : e.ItemId;
                body = BuildEvidenceBody(ev);
                if (ev != null)
                {
                    sprite = EvidencePanelUI.LoadEvidenceIcon(ev.icon, e.ItemId);
                }

                break;
            case NotebookRevealKind.Testimony:
                TestimonyDefinition te = nb.GetCurrentTestimony(e.ItemId);
                displayName = te != null && !string.IsNullOrEmpty(te.name) ? te.name : e.ItemId;
                body = BuildTestimonyBody(te);
                break;
            case NotebookRevealKind.Doubt:
                DoubtDefinition du = nb.GetCurrentDoubt(e.ItemId);
                displayName = du != null && !string.IsNullOrEmpty(du.name) ? du.name : e.ItemId;
                body = BuildDoubtBody(du);
                break;
            default:
                displayName = e.ItemId;
                body = string.Empty;
                break;
        }

        PopulateAndShow(title, displayName, body, sprite);
    }

    private void PresentLegacyClue(string clueId)
    {
        EnsureViewBuilt();
        string name = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueDisplayName(clueId)
            : clueId;
        string body = string.Empty;
        ClueDefinition def = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueDefinition(clueId)
            : null;
        if (def != null)
        {
            if (!string.IsNullOrWhiteSpace(def.unlockBrief))
            {
                body = def.unlockBrief.Trim();
            }
            else if (!string.IsNullOrEmpty(def.description))
            {
                body = def.description;
            }
        }

        if (def != null
            && string.IsNullOrWhiteSpace(def.unlockBrief)
            && !string.IsNullOrEmpty(def.foundLocation))
        {
            body = string.IsNullOrEmpty(body)
                ? $"发现地点：{def.foundLocation}"
                : $"{body}\n\n发现地点：{def.foundLocation}";
        }

        Sprite sprite = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueIcon(clueId)
            : null;

        PopulateAndShow("获得新线索", name, body, sprite);
    }

    private void PresentSynthesis(string outputClueId)
    {
        EnsureViewBuilt();
        string name = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueDisplayName(outputClueId)
            : outputClueId;
        string body = string.Empty;
        ClueDefinition def = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueDefinition(outputClueId)
            : null;
        if (def != null)
        {
            if (!string.IsNullOrWhiteSpace(def.unlockBrief))
            {
                body = def.unlockBrief.Trim();
            }
            else if (!string.IsNullOrEmpty(def.description))
            {
                body = def.description;
            }
        }

        Sprite sprite = EvidenceManager.Instance != null
            ? EvidenceManager.Instance.GetClueIcon(outputClueId)
            : null;

        PopulateAndShow("线索整理", name, body, sprite);
    }

    private static string ResolveTitle(NotebookRevealKind kind, NotebookRevealType revealType)
    {
        bool stage = revealType == NotebookRevealType.StageUpdate;
        switch (kind)
        {
            case NotebookRevealKind.Evidence:
                return stage ? "线索更新" : "获得新线索";
            case NotebookRevealKind.Testimony:
                return stage ? "证词更新" : "获得新证词";
            case NotebookRevealKind.Doubt:
                return stage ? "疑点更新" : "获得新疑点";
            default:
                return "提示";
        }
    }

    private static string BuildEvidenceBody(PhysicalEvidenceDefinition ev)
    {
        if (ev == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(ev.unlockBrief))
        {
            return ev.unlockBrief.Trim();
        }

        string body = ev.description ?? string.Empty;
        if (!string.IsNullOrEmpty(ev.foundLocation))
        {
            body = string.IsNullOrEmpty(body)
                ? $"发现地点：{ev.foundLocation}"
                : $"{body}\n\n发现地点：{ev.foundLocation}";
        }

        return body;
    }

    private static string BuildTestimonyBody(TestimonyDefinition te)
    {
        if (te == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(te.unlockBrief))
        {
            return te.unlockBrief.Trim();
        }

        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(te.summary))
        {
            parts.Add(te.summary);
        }

        if (!string.IsNullOrEmpty(te.originalText))
        {
            parts.Add($"原文：{te.originalText}");
        }

        if (!string.IsNullOrEmpty(te.source))
        {
            parts.Add($"来源：{te.source}");
        }

        return string.Join("\n\n", parts);
    }

    private static string BuildDoubtBody(DoubtDefinition du)
    {
        if (du == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(du.unlockBrief))
        {
            return du.unlockBrief.Trim();
        }

        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(du.question))
        {
            parts.Add(du.question);
        }

        if (!string.IsNullOrEmpty(du.description))
        {
            parts.Add(du.description);
        }

        return string.Join("\n\n", parts);
    }

    private void PopulateDetailContent(string title, string displayName, string body, Sprite sprite)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (nameText != null)
        {
            nameText.text = $"【{displayName}】";
        }

        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            iconImage.gameObject.SetActive(sprite != null);
        }
    }

    private void PopulateAndShow(string title, string displayName, string body, Sprite sprite)
    {
        PopulateDetailContent(title, displayName, body, sprite);
        ShowAfterPopulate(title);
    }

    private void ShowAfterPopulate(string title)
    {
        if (overlayRoot == null)
        {
            return;
        }

        overlayRoot.SetActive(true);

        bool usePrimer = requireConfirmBeforeReveal
                         && primerPanelGo != null
                         && primerPromptText != null
                         && primerRevealButton != null;

        if (!usePrimer)
        {
            MoveToDetailPhase();
            return;
        }

        detailPhaseActive = false;

        try
        {
            primerPromptText.text = string.Format(primerPromptFormat ?? "{0}", title);
        }
        catch (FormatException)
        {
            primerPromptText.text = title ?? string.Empty;
        }

        ApplyPrimerRevealButtonLabel();

        primerPanelGo.SetActive(true);

        if (detailPanelGo != null)
        {
            detailPanelGo.SetActive(false);
        }

        SetDimDismissable(false);
    }

    private void ApplyPrimerRevealButtonLabel()
    {
        TMP_Text labelTmp = primerRevealButton != null
            ? primerRevealButton.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (labelTmp != null && !string.IsNullOrEmpty(primerRevealButtonLabel))
        {
            labelTmp.text = primerRevealButtonLabel;
        }
    }

    private void MoveToDetailPhase()
    {
        detailPhaseActive = true;

        if (primerPanelGo != null)
        {
            primerPanelGo.SetActive(false);
        }

        if (detailPanelGo != null)
        {
            detailPanelGo.SetActive(true);
        }

        SetDimDismissable(true);
    }

    private void OnCloseClicked()
    {
        HideImmediate();
        TryShowNext();
    }

    private void OnPrimerRevealClicked()
    {
        MoveToDetailPhase();
    }

    private void HideImmediate()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        detailPhaseActive = false;
    }

    private void EnsureViewBuilt()
    {
        if (overlayRoot != null)
        {
            return;
        }

        GameObject prefabSource = optionalOverlayPrefab;
        if (prefabSource == null)
        {
            prefabSource = Resources.Load<GameObject>("UI/NotebookUnlockOverlay");
        }

        if (prefabSource != null && prefabSource.GetComponent<NotebookUnlockOverlayPresenter>() != null)
        {
            // 防止把 Presenter 脚本也做进 UI prefab，导致实例化后触发单例互斥而自毁。
            Debug.LogWarning("NotebookUnlockOverlayPresenter: UI prefab 上不应挂载 NotebookUnlockOverlayPresenter，已回退到运行时生成 UI。");
            prefabSource = null;
        }

        if (prefabSource != null)
        {
            overlayRoot = Instantiate(prefabSource, transform);
            overlayRoot.name = "NotebookUnlockOverlay";
        }
        else
        {
            BuildRuntimeHierarchy();
        }

        WireFromHierarchy(overlayRoot.transform);
        EnsurePrimerBlockWired();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        EnsureDimCloseButtonAttached();
        SetDimDismissable(false);
        ApplyFontRecursive(overlayRoot.transform);
    }

    private void EnsureDimCloseButtonAttached()
    {
        if (dimImage == null)
        {
            dimCloseButton = null;
            return;
        }

        dimCloseButton = dimImage.GetComponent<Button>();
        if (dimCloseButton == null)
        {
            dimCloseButton = dimImage.gameObject.AddComponent<Button>();
            dimCloseButton.targetGraphic = dimImage;
        }
    }

    private void SetDimDismissable(bool dismissable)
    {
        EnsureDimCloseButtonAttached();
        if (dimCloseButton == null)
        {
            return;
        }

        dimCloseButton.onClick.RemoveListener(OnCloseClicked);
        if (dismissable)
        {
            dimCloseButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void EnsurePrimerBlockWired()
    {
        if (overlayRoot == null)
        {
            return;
        }

        Transform rootTf = overlayRoot.transform;
        Transform primerTf = FindChildDeep(rootTf, ChildPrimerPanel);
        if (primerTf == null && requireConfirmBeforeReveal)
        {
            BuildRuntimePrimerPanel(rootTf);
            primerTf = FindChildDeep(rootTf, ChildPrimerPanel);
        }

        if (primerTf == null)
        {
            primerPanelGo = null;
            primerPromptText = null;
            primerRevealButton = null;
            return;
        }

        primerPanelGo = primerTf.gameObject;
        primerPromptText = FindChildDeep(primerTf, ChildPrimerPrompt)?.GetComponent<TMP_Text>();
        primerRevealButton = FindChildDeep(primerTf, ChildRevealButton)?.GetComponent<Button>();

        if (primerRevealButton != null)
        {
            primerRevealButton.onClick.RemoveListener(OnPrimerRevealClicked);
            primerRevealButton.onClick.AddListener(OnPrimerRevealClicked);
        }

        primerPanelGo.SetActive(false);
    }

    private void BuildRuntimePrimerPanel(Transform rootTf)
    {
        GameObject primer = CreateUiObject(ChildPrimerPanel, rootTf);
        RectTransform primerRect = primer.GetComponent<RectTransform>();
        StretchFull(primerRect);

        Image primerBg = primer.AddComponent<Image>();
        primerBg.sprite = GetWhiteSprite();
        primerBg.color = new Color(0f, 0f, 0f, 0.25f);
        primerBg.raycastTarget = true;

        GameObject promptGo = CreateUiObject(ChildPrimerPrompt, primer.transform);
        RectTransform promptRt = promptGo.GetComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0.1f, 0.55f);
        promptRt.anchorMax = new Vector2(0.9f, 0.85f);
        promptRt.offsetMin = Vector2.zero;
        promptRt.offsetMax = Vector2.zero;

        TMP_Text promptTmp = promptGo.AddComponent<TextMeshProUGUI>();
        promptTmp.alignment = TextAlignmentOptions.Center;
        promptTmp.fontSize = 28f;
        promptTmp.enableWordWrapping = true;

        GameObject revealGo = CreateUiObject(ChildRevealButton, primer.transform);
        RectTransform revealRt = revealGo.GetComponent<RectTransform>();
        revealRt.anchorMin = new Vector2(0.5f, 0.32f);
        revealRt.anchorMax = new Vector2(0.5f, 0.32f);
        revealRt.pivot = new Vector2(0.5f, 0.5f);
        revealRt.sizeDelta = new Vector2(200f, 52f);

        Image revealImg = revealGo.AddComponent<Image>();
        revealImg.sprite = GetWhiteSprite();
        revealImg.color = new Color(0.45f, 0.32f, 0.18f, 0.95f);

        Button revealBtn = revealGo.AddComponent<Button>();
        revealBtn.targetGraphic = revealImg;

        GameObject revealLabelGo = CreateUiObject("Label", revealGo.transform);
        StretchFull(revealLabelGo.GetComponent<RectTransform>());
        TMP_Text revealLabel = revealLabelGo.AddComponent<TextMeshProUGUI>();
        revealLabel.alignment = TextAlignmentOptions.Center;
        revealLabel.fontSize = 24f;
        revealLabel.color = Color.white;
        revealLabel.text = primerRevealButtonLabel;
    }

    /// <summary>供 Unity 场景的 Presenter：修改「先按钮后详情」文案与开关。</summary>
    public void SetRevealConfirmUi(bool enabled, string promptFormat, string revealLabel)
    {
        requireConfirmBeforeReveal = enabled;
        if (!string.IsNullOrEmpty(promptFormat))
        {
            primerPromptFormat = promptFormat;
        }

        if (!string.IsNullOrEmpty(revealLabel))
        {
            primerRevealButtonLabel = revealLabel;
        }
    }

    /// <summary>运行时关闭「先看按钮」一步时调用。</summary>
    public static void SetRequireConfirmGlobally(bool requireConfirm)
    {
        if (Instance == null)
        {
            Bootstrap();
        }

        if (Instance != null)
        {
            Instance.requireConfirmBeforeReveal = requireConfirm;
        }
    }

    private void WireFromHierarchy(Transform root)
    {
        Transform dim = FindChildDeep(root, ChildDim);
        if (dim != null)
        {
            dimImage = dim.GetComponent<Image>();
        }

        Transform panel = FindChildDeep(root, ChildPanel);
        if (panel == null)
        {
            Debug.LogWarning("NotebookUnlockOverlayPresenter: 预制体缺少 Panel 子节点。");
            return;
        }

        detailPanelGo = panel.gameObject;

        titleText = FindChildDeep(panel, ChildTitle)?.GetComponent<TMP_Text>();
        nameText = FindChildDeep(panel, ChildName)?.GetComponent<TMP_Text>();
        bodyText = FindChildDeep(panel, ChildBody)?.GetComponent<TMP_Text>();
        iconImage = FindChildDeep(panel, ChildIcon)?.GetComponent<Image>();
        closeButton = FindChildDeep(panel, ChildClose)?.GetComponent<Button>();

        rootCanvas = root.GetComponentInChildren<Canvas>(true);
        if (rootCanvas == null)
        {
            rootCanvas = root.gameObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = overlaySortingOrder;
    }

    private void BuildRuntimeHierarchy()
    {
        GameObject root = new GameObject("NotebookUnlockOverlay");
        root.transform.SetParent(transform, false);
        overlayRoot = root;

        rootCanvas = root.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = overlaySortingOrder;

        root.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dimGo = CreateUiObject(ChildDim, root.transform);
        RectTransform dimRect = dimGo.GetComponent<RectTransform>();
        StretchFull(dimRect);
        dimImage = dimGo.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.sprite = GetWhiteSprite();
        dimImage.raycastTarget = true;

        GameObject panelGo = CreateUiObject(ChildPanel, root.transform);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 480f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.sprite = GetWhiteSprite();
        panelBg.color = new Color(0.94f, 0.9f, 0.82f, 0.98f);

        GameObject titleGo = CreateUiObject(ChildTitle, panelGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.82f);
        titleRt.anchorMax = new Vector2(0.95f, 0.95f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 32f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.2f, 0.12f, 0.08f, 1f);
        titleText.text = "获得新线索";

        GameObject closeGo = CreateUiObject(ChildClose, panelGo.transform);
        RectTransform closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.pivot = new Vector2(1f, 1f);
        closeRt.sizeDelta = new Vector2(44f, 44f);
        closeRt.anchoredPosition = new Vector2(-12f, -12f);
        Image closeImg = closeGo.AddComponent<Image>();
        closeImg.sprite = GetWhiteSprite();
        closeImg.color = new Color(0.35f, 0.3f, 0.25f, 0.9f);
        closeButton = closeGo.AddComponent<Button>();
        closeButton.targetGraphic = closeImg;

        GameObject closeLabel = CreateUiObject("Label", closeGo.transform);
        StretchFull(closeLabel.GetComponent<RectTransform>());
        TMP_Text closeTmp = closeLabel.AddComponent<TextMeshProUGUI>();
        closeTmp.alignment = TextAlignmentOptions.Center;
        closeTmp.fontSize = 28f;
        closeTmp.text = "×";
        closeTmp.color = Color.white;

        GameObject iconGo = CreateUiObject(ChildIcon, panelGo.transform);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.42f);
        iconRt.anchorMax = new Vector2(0.5f, 0.68f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(200f, 200f);
        iconRt.anchoredPosition = Vector2.zero;
        iconImage = iconGo.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.enabled = false;
        iconImage.gameObject.SetActive(false);

        GameObject nameGo = CreateUiObject(ChildName, panelGo.transform);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.05f, 0.72f);
        nameRt.anchorMax = new Vector2(0.95f, 0.8f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;
        nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontSize = 26f;
        nameText.color = new Color(0.55f, 0.35f, 0.1f, 1f);
        nameText.text = "【】";

        GameObject bodyGo = CreateUiObject(ChildBody, panelGo.transform);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.06f, 0.08f);
        bodyRt.anchorMax = new Vector2(0.94f, 0.4f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;
        bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyText.alignment = TextAlignmentOptions.TopJustified;
        bodyText.fontSize = 22f;
        bodyText.color = new Color(0.15f, 0.1f, 0.06f, 1f);
        bodyText.enableWordWrapping = true;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;
        return go;
    }

    private static Transform FindChildDeep(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
            {
                return t;
            }
        }

        return null;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void ApplyFontRecursive(Transform t)
    {
        if (overrideFont == null)
        {
            return;
        }

        foreach (TMP_Text tmp in t.GetComponentsInChildren<TMP_Text>(true))
        {
            tmp.font = overrideFont;
        }
    }

    private static Sprite s_whiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
        {
            return s_whiteSprite;
        }

        Texture2D tex = Texture2D.whiteTexture;
        s_whiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return s_whiteSprite;
    }
}
