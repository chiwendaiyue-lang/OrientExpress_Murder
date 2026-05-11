using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EvidencePanelUI : MonoBehaviour
{
    private static EvidencePanelUI persistentInstance;
    private const string NOTEBOOK_PREFAB_PATH = "UI/DetectiveNotebookRoot";

    public enum EvidenceTab
    {
        Physical,
        Testimony,
        Doubt,
        Inference
    }

    [Header("Toggle")]
    public Button evidenceToggleButton;

    [Header("Tabs")]
    public Button physicalTabButton;
    public Button testimonyTabButton;
    public Button doubtTabButton;
    public Button inferenceTabButton;

    [Header("Panel")]
    public GameObject evidenceSlotPrefab;
    public Transform evidenceContainer;
    public GameObject evidencePanel;
    public TMP_Text clueDetailText;
    public Image clueDetailIcon;

    [Header("Drag")]
    [Tooltip("是否让 NotebookPanel 在运行时可被鼠标拖动。")]
    public bool enablePanelDragging = true;
    [Tooltip("拖动手柄。留空表示在 NotebookPanel 任意位置都能拖动。")]
    public RectTransform panelDragHandle;
    [Tooltip("拖动时是否把 NotebookPanel 限制在父级（Canvas）范围内。")]
    public bool clampPanelInsideScreen = true;

    [Header("Category")]
    [SerializeField] private EvidenceTab currentTab = EvidenceTab.Physical;

    // 重构说明：
    //   旧版本会在 Awake/Start 里强制把 RectTransform 锚点拉满全屏、并按硬编码坐标
    //   重置 DetailText/DetailIcon 的 anchor，导致美术在 Prefab 里改了形状也会被运行时
    //   覆盖。新版本只在“缺失”时补齐组件，不再覆盖任何 Transform 数据。
    //   如果你的 Prefab 已自带 Canvas/CanvasScaler/GraphicRaycaster，可以把
    //   autoEnsureCanvas 关掉，让 Prefab 完全控制布局。
    [Header("Overlay (Optional)")]
    [Tooltip("当 Prefab 上缺少 Canvas/CanvasScaler/GraphicRaycaster 时，运行时是否按 ScreenSpaceOverlay 默认值自动补齐。Prefab 已自带这些组件时建议关闭。")]
    [SerializeField] private bool autoEnsureCanvas = true;
    [SerializeField] private int overlaySortingOrder = 100;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1280f, 720f);
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;

    private DetectiveNotebookManager notebookManager;

    public static bool IsNotebookOpen
    {
        get
        {
            return persistentInstance != null
                && persistentInstance.evidencePanel != null
                && persistentInstance.evidencePanel.activeSelf;
        }
    }

    public static bool IsPointerOverNotebookArea()
    {
        if (persistentInstance == null)
        {
            return false;
        }

        return IsPointerInside(persistentInstance.evidenceToggleButton)
            || IsPointerInside(persistentInstance.evidencePanel);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureRuntimeInstance();
    }

    public static EvidencePanelUI EnsureRuntimeInstance()
    {
        if (persistentInstance != null)
        {
            return persistentInstance;
        }

        EvidencePanelUI existingOfficial = FindExistingOfficialInstance();
        if (existingOfficial != null)
        {
            return existingOfficial;
        }

        EvidencePanelUI prefabInstance = InstantiateNotebookPrefab();
        if (prefabInstance != null)
        {
            return prefabInstance;
        }

        EvidencePanelUI existing = FindObjectOfType<EvidencePanelUI>(true);
        if (existing != null)
        {
            return existing;
        }

        Debug.LogWarning(
            $"EvidencePanelUI: 未在场景或 Resources/{NOTEBOOK_PREFAB_PATH}.prefab 中找到可用实例，侦探笔记功能将不可用。");
        return null;
    }

    private void Awake()
    {
        if (persistentInstance != null && persistentInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (!IsOfficialNotebookRoot(this))
        {
            EvidencePanelUI existingOfficial = FindExistingOfficialInstance();
            if (existingOfficial != null && existingOfficial != this)
            {
                Destroy(gameObject);
                return;
            }

            EvidencePanelUI prefabInstance = InstantiateNotebookPrefab();
            if (prefabInstance != null && prefabInstance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        persistentInstance = this;
        DetachFromSceneParent();
        EnsureCanvasIfNeeded();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        EnsureEventSystem();
        TryAutoBindReferences();
        EnsureDraggablePanel();

        notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (notebookManager != null)
        {
            notebookManager.OnNotebookUpdated += HandleNotebookUpdated;
        }

        BindButtons();

        if (evidencePanel != null)
        {
            evidencePanel.SetActive(false);
        }

        HideClueDetail();
        RefreshNotebookVisibility();
    }

    private void OnDestroy()
    {
        if (persistentInstance == this)
        {
            persistentInstance = null;
        }

        if (notebookManager != null)
        {
            notebookManager.OnNotebookUpdated -= HandleNotebookUpdated;
        }
    }

    // ==================== 启动期：只补组件，不动形状 ====================

    private void DetachFromSceneParent()
    {
        // 仅确保根节点位于场景根（DontDestroyOnLoad 需要根对象），不修改 RectTransform 任何数值。
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }
    }

    private void EnsureCanvasIfNeeded()
    {
        if (!autoEnsureCanvas)
        {
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = overlaySortingOrder;
        }

        if (GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private static EvidencePanelUI FindExistingOfficialInstance()
    {
        EvidencePanelUI[] instances = FindObjectsOfType<EvidencePanelUI>(true);
        foreach (EvidencePanelUI instance in instances)
        {
            if (IsOfficialNotebookRoot(instance))
            {
                return instance;
            }
        }

        return null;
    }

    private static EvidencePanelUI InstantiateNotebookPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(NOTEBOOK_PREFAB_PATH);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "DetectiveNotebookRoot";

        EvidencePanelUI panel = instance.GetComponent<EvidencePanelUI>();
        if (panel == null)
        {
            Debug.LogWarning($"EvidencePanelUI: Resources/{NOTEBOOK_PREFAB_PATH}.prefab 缺少 EvidencePanelUI，将自动补挂脚本。");
            panel = instance.AddComponent<EvidencePanelUI>();
        }

        return panel;
    }

    private static bool IsOfficialNotebookRoot(EvidencePanelUI panel)
    {
        return panel != null && panel.gameObject.name.StartsWith("DetectiveNotebookRoot");
    }

    // ==================== 引用回填：仅按名查找，不修改 Transform ====================

    private void TryAutoBindReferences()
    {
        if (evidenceToggleButton == null)
        {
            evidenceToggleButton = FindChildButton("Btn_Notebook")
                ?? FindChildButton("Btn_DetectiveNotebook");
        }

        if (physicalTabButton == null)
        {
            physicalTabButton = FindChildButton("Btn_Physical");
        }
        if (testimonyTabButton == null)
        {
            testimonyTabButton = FindChildButton("Btn_Testimony");
        }
        if (doubtTabButton == null)
        {
            doubtTabButton = FindChildButton("Btn_Doubt");
        }
        if (inferenceTabButton == null)
        {
            inferenceTabButton = FindChildButton("Btn_Inference");
        }

        if (evidencePanel == null)
        {
            evidencePanel = FindChildGameObject("NotebookPanel")
                ?? FindChildGameObject("DetectiveNotebookPanel");
        }

        if (evidenceContainer == null)
        {
            GameObject container = FindChildGameObject("Content");
            if (container != null)
            {
                evidenceContainer = container.transform;
            }
        }

        if (clueDetailText == null)
        {
            GameObject detail = FindChildGameObject("DetailText");
            if (detail != null)
            {
                clueDetailText = detail.GetComponent<TMP_Text>();
            }
        }

        if (clueDetailIcon == null)
        {
            GameObject icon = FindChildGameObject("DetailIcon");
            if (icon != null)
            {
                clueDetailIcon = icon.GetComponent<Image>();
            }
        }

        if (evidenceSlotPrefab == null)
        {
            Debug.LogWarning("EvidencePanelUI: evidenceSlotPrefab 未指定，请在 Prefab 上拖入 EvidenceSlot.prefab。");
        }
    }

    private Button FindChildButton(string childName)
    {
        GameObject obj = FindChildGameObject(childName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    private GameObject FindChildGameObject(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    // ==================== 运行时拖动支持 ====================

    private void EnsureDraggablePanel()
    {
        if (!enablePanelDragging || evidencePanel == null)
        {
            return;
        }

        GameObject handleObject = panelDragHandle != null ? panelDragHandle.gameObject : evidencePanel;
        UIDraggable draggable = handleObject.GetComponent<UIDraggable>();
        if (draggable == null)
        {
            draggable = handleObject.AddComponent<UIDraggable>();
        }

        draggable.target = evidencePanel.transform as RectTransform;
        draggable.clampToParent = clampPanelInsideScreen;
        draggable.bringToFrontOnDrag = true;

        // NotebookPanel 的背景 Image 必须能接收 Pointer 事件，否则在空白处按下不会触发拖动。
        Image background = handleObject.GetComponent<Image>();
        if (background != null && !background.raycastTarget)
        {
            background.raycastTarget = true;
        }
    }

    // ==================== 业务逻辑（保持不变）====================

    public void ToggleEvidencePanel()
    {
        if (evidencePanel == null)
        {
            return;
        }

        if (!HasNotebookContent())
        {
            evidencePanel.SetActive(false);
            HideClueDetail();
            return;
        }

        evidencePanel.SetActive(!evidencePanel.activeSelf);
        if (evidencePanel.activeSelf)
        {
            EnsureCurrentTabAvailable();
            RefreshEvidenceList();
        }
        HideClueDetail();
    }

    public void RefreshEvidenceList()
    {
        if (notebookManager == null)
        {
            notebookManager = DetectiveNotebookManager.EnsureInstance();
        }

        if (notebookManager == null || evidenceContainer == null)
        {
            return;
        }

        RefreshNotebookVisibility();
        if (!HasNotebookContent())
        {
            return;
        }

        if (evidenceSlotPrefab == null)
        {
            Debug.LogWarning("EvidencePanelUI: evidenceSlotPrefab 未指定，无法生成线索条目。请在 Prefab 上拖入 EvidenceSlot.prefab。");
            return;
        }

        EnsureCurrentTabAvailable();
        foreach (Transform child in evidenceContainer)
        {
            Destroy(child.gameObject);
        }

        EvidenceTab tab = NormalizeTab(currentTab);
        List<string> itemIds = GetItemIdsForTab(tab);
        foreach (string itemId in itemIds)
        {
            GameObject slot = Instantiate(evidenceSlotPrefab, evidenceContainer);
            EvidenceSlotUI slotUI = slot.GetComponent<EvidenceSlotUI>();
            if (slotUI != null)
            {
                slotUI.Init(itemId, tab, this);
                continue;
            }

            TMP_Text tmpText = slot.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = GetNotebookDisplayName(itemId, tab);
            }
        }
    }

    public void ShowPhysicalTab()
    {
        SetTab(EvidenceTab.Physical);
    }

    public void ShowTestimonyTab()
    {
        SetTab(EvidenceTab.Testimony);
    }

    public void ShowDoubtTab()
    {
        SetTab(EvidenceTab.Doubt);
    }

    public void ShowInferenceTab()
    {
        SetTab(EvidenceTab.Doubt);
    }

    public void ShowClueDetail(string clueId)
    {
        ShowNotebookDetail(clueId, NormalizeTab(currentTab));
    }

    public void ShowNotebookDetail(string itemId, EvidenceTab tab)
    {
        if (notebookManager == null)
        {
            notebookManager = DetectiveNotebookManager.EnsureInstance();
        }

        if (notebookManager == null)
        {
            return;
        }

        EvidenceTab normalizedTab = NormalizeTab(tab);
        if (normalizedTab == EvidenceTab.Physical)
        {
            ShowEvidenceDetail(itemId);
        }
        else if (normalizedTab == EvidenceTab.Testimony)
        {
            ShowTestimonyDetail(itemId);
        }
        else
        {
            ShowDoubtDetail(itemId);
        }
    }

    private void SetTab(EvidenceTab tab)
    {
        EvidenceTab normalizedTab = NormalizeTab(tab);
        if (!IsTabAvailable(normalizedTab))
        {
            return;
        }

        currentTab = normalizedTab;
        RefreshEvidenceList();
        HideClueDetail();
    }

    private List<string> GetItemIdsForTab(EvidenceTab tab)
    {
        if (tab == EvidenceTab.Physical)
        {
            return notebookManager.GetUnlockedEvidenceIds();
        }

        if (tab == EvidenceTab.Testimony)
        {
            return notebookManager.GetUnlockedTestimonyIds();
        }

        return notebookManager.GetUnlockedDoubtIds();
    }

    private string GetNotebookDisplayName(string itemId, EvidenceTab tab)
    {
        if (tab == EvidenceTab.Physical)
        {
            PhysicalEvidenceDefinition item = notebookManager.GetCurrentEvidence(itemId);
            return item != null && !string.IsNullOrEmpty(item.name) ? item.name : itemId;
        }

        if (tab == EvidenceTab.Testimony)
        {
            TestimonyDefinition item = notebookManager.GetCurrentTestimony(itemId);
            return item != null && !string.IsNullOrEmpty(item.name) ? item.name : itemId;
        }

        DoubtDefinition doubt = notebookManager.GetCurrentDoubt(itemId);
        return doubt != null && !string.IsNullOrEmpty(doubt.name) ? doubt.name : itemId;
    }

    private void ShowEvidenceDetail(string itemId)
    {
        PhysicalEvidenceDefinition item = notebookManager.GetCurrentEvidence(itemId);
        if (clueDetailText != null)
        {
            clueDetailText.gameObject.SetActive(true);
            clueDetailText.text = item == null
                ? itemId
                : $"物证：{item.name}\n\n描述：{item.description}\n\n发现地点：{item.foundLocation}";
        }

        SetDetailIcon(item != null ? item.icon : string.Empty, itemId);
    }

    private void ShowTestimonyDetail(string itemId)
    {
        TestimonyDefinition item = notebookManager.GetCurrentTestimony(itemId);
        if (clueDetailText != null)
        {
            clueDetailText.gameObject.SetActive(true);
            if (item == null)
            {
                clueDetailText.text = itemId;
            }
            else
            {
                clueDetailText.text =
                    $"证词：{item.name}\n\n摘要：{item.summary}\n\n说话人：{item.speakerName}\n来源：{item.source}\n原话：{item.originalText}";
            }
        }

        SetDetailIcon(string.Empty, null);
    }

    private void ShowDoubtDetail(string itemId)
    {
        DoubtDefinition item = notebookManager.GetCurrentDoubt(itemId);
        if (clueDetailText != null)
        {
            clueDetailText.gameObject.SetActive(true);
            if (item == null)
            {
                clueDetailText.text = itemId;
            }
            else
            {
                string status = item.resolved ? "已解决" : "未解决";
                string conclusion = string.IsNullOrEmpty(item.finalConclusion) ? "" : $"\n结论：{item.finalConclusion}";
                clueDetailText.text = $"疑点：{item.name}\n状态：{status}\n\n描述：{item.description}\n问题：{item.question}{conclusion}";
            }
        }

        SetDetailIcon(string.Empty, null);
    }

    private void SetDetailIcon(string iconId, string fallbackId)
    {
        if (clueDetailIcon == null)
        {
            return;
        }

        Sprite icon = LoadEvidenceIcon(iconId, fallbackId);
        clueDetailIcon.sprite = icon;
        clueDetailIcon.enabled = icon != null;
        clueDetailIcon.gameObject.SetActive(icon != null);
    }

    // 物证图标的统一加载入口。EvidenceSlotUI 也会调用它，所以是 public static。
    // 加载顺序：(1) JSON 的 icon 字段；(2) 物证 id；(3) 加 icon_ 前缀的备用名。
    // 路径优先级：Evidence/icons/Physical/ → Evidence/icons/ → Clues/Icons/（兼容旧路径）。
    private static readonly string[] IconResourceFolders =
    {
        "Evidence/icons/Physical",
        "Evidence/icons",
        "Clues/Icons",
        "Clues/icons",
    };

    public static Sprite LoadEvidenceIcon(string iconId, string fallbackId)
    {
        string trimmedIcon = string.IsNullOrWhiteSpace(iconId) ? null : iconId.Trim();
        string trimmedFallback = string.IsNullOrWhiteSpace(fallbackId) ? null : fallbackId.Trim();

        foreach (string candidate in EnumerateIconCandidates(trimmedIcon, trimmedFallback))
        {
            foreach (string folder in IconResourceFolders)
            {
                Sprite sprite = Resources.Load<Sprite>($"{folder}/{candidate}");
                if (sprite != null)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateIconCandidates(string iconId, string fallbackId)
    {
        HashSet<string> seen = new HashSet<string>();

        if (!string.IsNullOrEmpty(iconId) && seen.Add(iconId))
        {
            yield return iconId;
        }

        if (!string.IsNullOrEmpty(fallbackId) && seen.Add(fallbackId))
        {
            yield return fallbackId;
        }

        // 兼容历史命名：图片名带 icon_ 前缀但 JSON 里只写了 id。
        if (!string.IsNullOrEmpty(fallbackId))
        {
            string prefixed = "icon_" + fallbackId;
            if (seen.Add(prefixed))
            {
                yield return prefixed;
            }
        }

        // 兼容历史命名：JSON 里写了 icon_xxx 但图片其实只叫 xxx。
        if (!string.IsNullOrEmpty(iconId) && iconId.StartsWith("icon_"))
        {
            string stripped = iconId.Substring("icon_".Length);
            if (seen.Add(stripped))
            {
                yield return stripped;
            }
        }
    }

    private void HideClueDetail()
    {
        if (clueDetailText != null)
        {
            clueDetailText.text = "";
            clueDetailText.gameObject.SetActive(false);
        }

        if (clueDetailIcon != null)
        {
            clueDetailIcon.sprite = null;
            clueDetailIcon.enabled = false;
            clueDetailIcon.gameObject.SetActive(false);
        }
    }

    private EvidenceTab NormalizeTab(EvidenceTab tab)
    {
        return tab == EvidenceTab.Inference ? EvidenceTab.Doubt : tab;
    }

    private void HandleNotebookUpdated()
    {
        RefreshNotebookVisibility();
        if (evidencePanel != null && evidencePanel.activeSelf)
        {
            RefreshEvidenceList();
        }
    }

    private void BindButtons()
    {
        if (evidenceToggleButton != null)
        {
            evidenceToggleButton.onClick.RemoveListener(ToggleEvidencePanel);
            evidenceToggleButton.onClick.AddListener(ToggleEvidencePanel);
        }

        BindTabButton(physicalTabButton, ShowPhysicalTab);
        BindTabButton(testimonyTabButton, ShowTestimonyTab);
        BindTabButton(doubtTabButton, ShowDoubtTab);
        BindTabButton(inferenceTabButton, ShowInferenceTab);
    }

    private static void BindTabButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void RefreshNotebookVisibility()
    {
        if (notebookManager == null)
        {
            notebookManager = DetectiveNotebookManager.EnsureInstance();
        }

        bool hasContent = HasNotebookContent();
        SetButtonVisible(evidenceToggleButton, hasContent);

        if (!hasContent)
        {
            if (evidencePanel != null)
            {
                evidencePanel.SetActive(false);
            }
            HideClueDetail();
        }

        SetTabVisible(physicalTabButton, notebookManager != null && notebookManager.HasUnlockedEvidence());
        SetTabVisible(testimonyTabButton, notebookManager != null && notebookManager.HasUnlockedTestimony());
        SetTabVisible(doubtTabButton, notebookManager != null && notebookManager.HasUnlockedDoubt());
        SetTabVisible(inferenceTabButton, notebookManager != null && notebookManager.HasUnlockedDoubt());
        EnsureCurrentTabAvailable();
    }

    private bool HasNotebookContent()
    {
        if (notebookManager == null)
        {
            notebookManager = DetectiveNotebookManager.EnsureInstance();
        }

        return notebookManager != null && notebookManager.HasAnyUnlockedItem();
    }

    private bool IsTabAvailable(EvidenceTab tab)
    {
        if (notebookManager == null)
        {
            notebookManager = DetectiveNotebookManager.EnsureInstance();
        }

        if (notebookManager == null)
        {
            return false;
        }

        EvidenceTab normalizedTab = NormalizeTab(tab);
        if (normalizedTab == EvidenceTab.Physical)
        {
            return notebookManager.HasUnlockedEvidence();
        }

        if (normalizedTab == EvidenceTab.Testimony)
        {
            return notebookManager.HasUnlockedTestimony();
        }

        return notebookManager.HasUnlockedDoubt();
    }

    private void EnsureCurrentTabAvailable()
    {
        EvidenceTab normalizedTab = NormalizeTab(currentTab);
        if (IsTabAvailable(normalizedTab))
        {
            currentTab = normalizedTab;
            return;
        }

        if (IsTabAvailable(EvidenceTab.Doubt))
        {
            currentTab = EvidenceTab.Doubt;
            return;
        }

        if (IsTabAvailable(EvidenceTab.Testimony))
        {
            currentTab = EvidenceTab.Testimony;
            return;
        }

        currentTab = EvidenceTab.Physical;
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(visible);
        button.interactable = visible;
    }

    private static void SetTabVisible(Button button, bool visible)
    {
        if (button == null)
        {
            return;
        }

        button.gameObject.SetActive(visible);
        button.interactable = visible;
    }

    private static bool IsPointerInside(Button button)
    {
        return button != null && IsPointerInside(button.gameObject);
    }

    private static bool IsPointerInside(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return false;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }
}
