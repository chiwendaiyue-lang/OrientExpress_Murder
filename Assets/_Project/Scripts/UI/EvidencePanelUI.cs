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
    public GameObject backgroundDim;
    public GameObject physicalPanel;
    public GameObject testimonyPanel;
    public GameObject doubtPanel;
    public Transform testimonyCharacterList;
    public Transform testimonyList;
    public Transform leftDoubtList;
    public Transform rightDoubtList;
    public GameObject physicalDetailPanel;
    public TMP_Text clueDetailText;
    public Image clueDetailIcon;

    [Header("Generated Layout")]
    [SerializeField] private Color generatedPanelColor = new Color(0.93f, 0.88f, 0.78f, 0.86f);
    [SerializeField] private Color generatedInnerPanelColor = new Color(0.74f, 0.67f, 0.56f, 0.30f);
    [SerializeField] private Color generatedSelectedColor = new Color(0.72f, 0.57f, 0.28f, 0.95f);
    [SerializeField] private Color generatedOutlineColor = new Color(0.22f, 0.16f, 0.10f, 1f);
    [SerializeField] private Color generatedTextColor = new Color(0.16f, 0.12f, 0.08f, 1f);
    [SerializeField] private Color generatedAccentColor = new Color(0.46f, 0.16f, 0.14f, 1f);
    [SerializeField] private Vector2 testimonyCharacterListWidth = new Vector2(248f, 0f);
    [SerializeField] private Vector2 testimonyCardHeight = new Vector2(0f, 124f);
    [SerializeField] private Vector2 doubtItemHeight = new Vector2(0f, 62f);

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
    private string selectedTestimonyCharacterId;
    private string selectedDoubtId;
    private TMP_FontAsset generatedFontAsset;
    private Material generatedFontMaterial;

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
        EnsurePanelStructure();
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

        if (backgroundDim != null)
        {
            backgroundDim.SetActive(false);
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

        if (backgroundDim == null)
        {
            backgroundDim = FindChildGameObject("BackgroundDim");
        }

        if (evidenceContainer == null)
        {
            GameObject container = FindChildGameObject("EvidenceGrid")
                ?? FindChildGameObject("Content");
            if (container != null)
            {
                evidenceContainer = container.transform;
            }
        }

        if (physicalPanel == null)
        {
            physicalPanel = FindChildGameObject("EvidencePanel")
                ?? FindChildGameObject("Scroll View");
        }

        if (testimonyPanel == null)
        {
            testimonyPanel = FindChildGameObject("TestimonyPanel");
        }

        if (doubtPanel == null)
        {
            doubtPanel = FindChildGameObject("DoubtPanel");
        }

        if (testimonyCharacterList == null)
        {
            GameObject list = FindChildGameObject("CharacterList");
            if (list != null)
            {
                testimonyCharacterList = list.transform;
            }
        }

        if (testimonyList == null)
        {
            GameObject list = FindChildGameObject("TestimonyList");
            if (list != null)
            {
                testimonyList = list.transform;
            }
        }

        if (leftDoubtList == null)
        {
            GameObject list = FindChildGameObject("LeftDoubtList");
            if (list != null)
            {
                leftDoubtList = list.transform;
            }
        }

        if (rightDoubtList == null)
        {
            GameObject list = FindChildGameObject("RightDoubtList");
            if (list != null)
            {
                rightDoubtList = list.transform;
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

        if (physicalDetailPanel == null)
        {
            physicalDetailPanel = FindChildGameObject("DetailPanel");
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
            if (backgroundDim != null)
            {
                backgroundDim.SetActive(false);
            }
            HideClueDetail();
            return;
        }

        evidencePanel.SetActive(!evidencePanel.activeSelf);
        if (backgroundDim != null)
        {
            backgroundDim.SetActive(evidencePanel.activeSelf);
        }
        if (evidencePanel.activeSelf)
        {
            EnsureCurrentTabAvailable();
            RefreshCurrentTab();
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

        if (NormalizeTab(currentTab) != EvidenceTab.Physical)
        {
            RefreshCurrentTab();
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
            return;
        }

        HideClueDetail();
    }

    private void SetTab(EvidenceTab tab)
    {
        EvidenceTab normalizedTab = NormalizeTab(tab);
        if (!IsTabAvailable(normalizedTab))
        {
            return;
        }

        currentTab = normalizedTab;
        UpdateContentPanels();
        RefreshCurrentTab();
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
        TrySetPhysicalDetailPanelActive(true);

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
        TrySetPhysicalDetailPanelActive(false);

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
            RefreshCurrentTab();
        }
    }

    private void BindButtons()
    {
        BindButtonIfNeeded(evidenceToggleButton, ToggleEvidencePanel, nameof(ToggleEvidencePanel));
        BindButtonIfNeeded(physicalTabButton, ShowPhysicalTab, nameof(ShowPhysicalTab));
        BindButtonIfNeeded(testimonyTabButton, ShowTestimonyTab, nameof(ShowTestimonyTab));
        BindButtonIfNeeded(doubtTabButton, ShowDoubtTab, nameof(ShowDoubtTab));
        BindButtonIfNeeded(inferenceTabButton, ShowInferenceTab, nameof(ShowInferenceTab));
    }

    private static void BindButtonIfNeeded(Button button, UnityEngine.Events.UnityAction action, string methodName)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        if (!HasPersistentBinding(button, methodName))
        {
            button.onClick.AddListener(action);
        }
    }

    private static bool HasPersistentBinding(Button button, string methodName)
    {
        if (button == null || string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }

        return false;
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
            if (backgroundDim != null)
            {
                backgroundDim.SetActive(false);
            }
            HideClueDetail();
        }

        SetTabVisible(physicalTabButton, notebookManager != null && notebookManager.HasUnlockedEvidence());
        SetTabVisible(testimonyTabButton, notebookManager != null && notebookManager.HasUnlockedTestimony());
        SetTabVisible(doubtTabButton, notebookManager != null && notebookManager.HasUnlockedDoubt());
        SetTabVisible(inferenceTabButton, notebookManager != null && notebookManager.HasUnlockedDoubt());
        EnsureCurrentTabAvailable();
        UpdateContentPanels();
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

    private void RefreshCurrentTab()
    {
        UpdateContentPanels();

        EvidenceTab normalizedTab = NormalizeTab(currentTab);
        if (normalizedTab == EvidenceTab.Physical)
        {
            RefreshPhysicalPanel();
            return;
        }

        if (normalizedTab == EvidenceTab.Testimony)
        {
            RefreshTestimonyPanel();
            return;
        }

        RefreshDoubtPanel();
    }

    private void RefreshPhysicalPanel()
    {
        if (physicalPanel != null && !physicalPanel.activeSelf)
        {
            physicalPanel.SetActive(true);
        }

        if (testimonyPanel != null)
        {
            testimonyPanel.SetActive(false);
        }

        if (doubtPanel != null)
        {
            doubtPanel.SetActive(false);
        }

        bool hasDetailText = clueDetailText != null && !string.IsNullOrEmpty(clueDetailText.text);
        if (clueDetailText != null)
        {
            clueDetailText.gameObject.SetActive(hasDetailText);
        }

        if (clueDetailIcon != null)
        {
            bool hasIcon = clueDetailIcon.sprite != null;
            clueDetailIcon.enabled = hasIcon;
            clueDetailIcon.gameObject.SetActive(hasIcon);
            TrySetPhysicalDetailPanelActive(hasDetailText || hasIcon);
        }
        else
        {
            TrySetPhysicalDetailPanelActive(hasDetailText);
        }

        if (notebookManager == null || evidenceContainer == null)
        {
            return;
        }

        if (evidenceSlotPrefab == null)
        {
            Debug.LogWarning("EvidencePanelUI: evidenceSlotPrefab 未指定，无法生成线索条目。请在 Prefab 上拖入 EvidenceSlot.prefab。");
            return;
        }

        foreach (Transform child in evidenceContainer)
        {
            Destroy(child.gameObject);
        }

        List<string> itemIds = GetItemIdsForTab(EvidenceTab.Physical);
        foreach (string itemId in itemIds)
        {
            GameObject slot = Instantiate(evidenceSlotPrefab, evidenceContainer);
            EvidenceSlotUI slotUI = slot.GetComponent<EvidenceSlotUI>();
            if (slotUI != null)
            {
                slotUI.Init(itemId, EvidenceTab.Physical, this);
                continue;
            }

            TMP_Text tmpText = slot.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = GetNotebookDisplayName(itemId, EvidenceTab.Physical);
            }
        }
    }

    private GameObject ResolvePhysicalDetailPanel()
    {
        try
        {
            if (physicalDetailPanel != null)
            {
                return physicalDetailPanel;
            }
        }
        catch (MissingReferenceException)
        {
            physicalDetailPanel = null;
        }

        physicalDetailPanel = FindChildGameObject("DetailPanel");
        return physicalDetailPanel;
    }

    private void TrySetPhysicalDetailPanelActive(bool active)
    {
        GameObject detailPanel = ResolvePhysicalDetailPanel();
        if (detailPanel == null)
        {
            return;
        }

        try
        {
            detailPanel.SetActive(active);
        }
        catch (MissingReferenceException)
        {
            physicalDetailPanel = null;
            detailPanel = ResolvePhysicalDetailPanel();
            if (detailPanel != null)
            {
                detailPanel.SetActive(active);
            }
        }
    }

    private void RefreshTestimonyPanel()
    {
        if (testimonyPanel == null || testimonyCharacterList == null || testimonyList == null)
        {
            return;
        }

        if (physicalPanel != null)
        {
            physicalPanel.SetActive(false);
        }

        testimonyPanel.SetActive(true);

        if (doubtPanel != null)
        {
            doubtPanel.SetActive(false);
        }

        HideClueDetail();
        EnsureTestimonyListUsesVerticalLayout();

        foreach (Transform child in testimonyCharacterList)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in testimonyList)
        {
            Destroy(child.gameObject);
        }

        Dictionary<string, List<TestimonyDefinition>> grouped = BuildUnlockedTestimonyGroups();
        if (grouped.Count == 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(selectedTestimonyCharacterId) || !grouped.ContainsKey(selectedTestimonyCharacterId))
        {
            foreach (var pair in grouped)
            {
                selectedTestimonyCharacterId = pair.Key;
                break;
            }
        }

        List<CharacterDefinition> characters = notebookManager != null ? notebookManager.GetAllCharacters() : new List<CharacterDefinition>();
        HashSet<string> added = new HashSet<string>();
        foreach (CharacterDefinition character in characters)
        {
            if (character == null || string.IsNullOrEmpty(character.id) || !grouped.ContainsKey(character.id))
            {
                continue;
            }

            CreateCharacterButton(character, selectedTestimonyCharacterId == character.id);
            added.Add(character.id);
        }

        foreach (var pair in grouped)
        {
            if (added.Contains(pair.Key))
            {
                continue;
            }

            CharacterDefinition fallbackCharacter = notebookManager != null ? notebookManager.GetCharacter(pair.Key) : null;
            CreateCharacterButton(fallbackCharacter ?? new CharacterDefinition
            {
                id = pair.Key,
                displayName = pair.Key,
                role = "待确认身份"
            }, selectedTestimonyCharacterId == pair.Key);
        }

        List<TestimonyDefinition> selectedItems = grouped[selectedTestimonyCharacterId];
        foreach (TestimonyDefinition testimony in selectedItems)
        {
            CreateTestimonyCard(testimony);
        }
    }

    private void EnsureTestimonyListUsesVerticalLayout()
    {
        if (testimonyList == null)
        {
            return;
        }

        GridLayoutGroup grid = testimonyList.GetComponent<GridLayoutGroup>();
        VerticalLayoutGroup vertical = testimonyList.GetComponent<VerticalLayoutGroup>();
        if (vertical == null && grid != null)
        {
            DestroyImmediate(grid);
        }

        if (vertical == null)
        {
            vertical = testimonyList.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        if (vertical == null)
        {
            return;
        }

        vertical.spacing = 14f;
        vertical.padding = new RectOffset(8, 8, 8, 8);
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
    }

    private void RefreshDoubtPanel()
    {
        if (doubtPanel == null || leftDoubtList == null || rightDoubtList == null)
        {
            return;
        }

        if (physicalPanel != null)
        {
            physicalPanel.SetActive(false);
        }

        if (testimonyPanel != null)
        {
            testimonyPanel.SetActive(false);
        }

        doubtPanel.SetActive(true);
        HideClueDetail();

        foreach (Transform child in leftDoubtList)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform child in rightDoubtList)
        {
            Destroy(child.gameObject);
        }

        List<string> itemIds = GetItemIdsForTab(EvidenceTab.Doubt);
        if (itemIds.Count == 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(selectedDoubtId) || !itemIds.Contains(selectedDoubtId))
        {
            selectedDoubtId = itemIds[0];
        }

        for (int i = 0; i < itemIds.Count; i++)
        {
            Transform parent = i % 2 == 0 ? leftDoubtList : rightDoubtList;
            CreateDoubtButton(parent, itemIds[i], selectedDoubtId == itemIds[i]);
        }
    }

    private void UpdateContentPanels()
    {
        EvidenceTab normalizedTab = NormalizeTab(currentTab);

        if (physicalPanel != null)
        {
            physicalPanel.SetActive(normalizedTab == EvidenceTab.Physical && evidencePanel != null && evidencePanel.activeSelf);
        }

        if (testimonyPanel != null)
        {
            testimonyPanel.SetActive(normalizedTab == EvidenceTab.Testimony && evidencePanel != null && evidencePanel.activeSelf);
        }

        if (doubtPanel != null)
        {
            doubtPanel.SetActive(normalizedTab == EvidenceTab.Doubt && evidencePanel != null && evidencePanel.activeSelf);
        }
    }

    private void EnsurePanelStructure()
    {
        if (physicalPanel == null)
        {
            physicalPanel = FindChildGameObject("EvidencePanel") ?? FindChildGameObject("Scroll View");
        }

        if (testimonyPanel == null)
        {
            testimonyPanel = FindChildGameObject("TestimonyPanel");
        }

        if (doubtPanel == null)
        {
            doubtPanel = FindChildGameObject("DoubtPanel");
        }

        if (testimonyPanel == null && evidencePanel != null)
        {
            testimonyPanel = CreateGeneratedPanel("TestimonyPanel", evidencePanel.transform as RectTransform);
            CreateGeneratedTestimonyLayout(testimonyPanel.transform as RectTransform);
        }

        if (doubtPanel == null && evidencePanel != null)
        {
            doubtPanel = CreateGeneratedPanel("DoubtPanel", evidencePanel.transform as RectTransform);
            CreateGeneratedDoubtLayout(doubtPanel.transform as RectTransform);
        }

        if (testimonyCharacterList == null)
        {
            GameObject obj = FindChildGameObject("CharacterList");
            testimonyCharacterList = obj != null ? obj.transform : null;
        }

        if (testimonyList == null)
        {
            GameObject obj = FindChildGameObject("TestimonyList");
            testimonyList = obj != null ? obj.transform : null;
        }

        if (leftDoubtList == null)
        {
            GameObject obj = FindChildGameObject("LeftDoubtList");
            leftDoubtList = obj != null ? obj.transform : null;
        }

        if (rightDoubtList == null)
        {
            GameObject obj = FindChildGameObject("RightDoubtList");
            rightDoubtList = obj != null ? obj.transform : null;
        }
    }

    private GameObject CreateGeneratedPanel(string panelName, RectTransform parent)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.08f);
        rect.anchorMax = new Vector2(0.96f, 0.92f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = generatedInnerPanelColor;
        image.raycastTarget = true;
        panel.SetActive(false);
        return panel;
    }

    private void CreateGeneratedTestimonyLayout(RectTransform panel)
    {
        GameObject characterListObject = CreateLayoutContainer("CharacterList", panel, new Vector2(0.02f, 0.04f), new Vector2(0.28f, 0.96f));
        VerticalLayoutGroup characterLayout = characterListObject.AddComponent<VerticalLayoutGroup>();
        characterLayout.spacing = 10f;
        characterLayout.padding = new RectOffset(8, 8, 8, 8);
        characterLayout.childControlHeight = false;
        characterLayout.childControlWidth = true;
        characterLayout.childForceExpandHeight = false;
        characterLayout.childForceExpandWidth = true;
        ContentSizeFitter characterFitter = characterListObject.AddComponent<ContentSizeFitter>();
        characterFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        characterFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject testimonyListObject = CreateLayoutContainer("TestimonyList", panel, new Vector2(0.30f, 0.04f), new Vector2(0.98f, 0.96f));
        VerticalLayoutGroup testimonyLayout = testimonyListObject.AddComponent<VerticalLayoutGroup>();
        testimonyLayout.spacing = 12f;
        testimonyLayout.padding = new RectOffset(8, 8, 8, 8);
        testimonyLayout.childControlHeight = false;
        testimonyLayout.childControlWidth = true;
        testimonyLayout.childForceExpandHeight = false;
        testimonyLayout.childForceExpandWidth = true;
        ContentSizeFitter testimonyFitter = testimonyListObject.AddComponent<ContentSizeFitter>();
        testimonyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        testimonyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        testimonyCharacterList = characterListObject.transform;
        testimonyList = testimonyListObject.transform;
    }

    private void CreateGeneratedDoubtLayout(RectTransform panel)
    {
        GameObject leftListObject = CreateLayoutContainer("LeftDoubtList", panel, new Vector2(0.03f, 0.08f), new Vector2(0.47f, 0.94f));
        VerticalLayoutGroup leftLayout = leftListObject.AddComponent<VerticalLayoutGroup>();
        leftLayout.spacing = 12f;
        leftLayout.padding = new RectOffset(8, 8, 8, 8);
        leftLayout.childControlHeight = false;
        leftLayout.childControlWidth = true;
        leftLayout.childForceExpandHeight = false;
        leftLayout.childForceExpandWidth = true;
        ContentSizeFitter leftFitter = leftListObject.AddComponent<ContentSizeFitter>();
        leftFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        leftFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject rightListObject = CreateLayoutContainer("RightDoubtList", panel, new Vector2(0.53f, 0.08f), new Vector2(0.97f, 0.94f));
        VerticalLayoutGroup rightLayout = rightListObject.AddComponent<VerticalLayoutGroup>();
        rightLayout.spacing = 12f;
        rightLayout.padding = new RectOffset(8, 8, 8, 8);
        rightLayout.childControlHeight = false;
        rightLayout.childControlWidth = true;
        rightLayout.childForceExpandHeight = false;
        rightLayout.childForceExpandWidth = true;
        ContentSizeFitter rightFitter = rightListObject.AddComponent<ContentSizeFitter>();
        rightFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rightFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        leftDoubtList = leftListObject.transform;
        rightDoubtList = rightListObject.transform;
    }

    private GameObject CreateLayoutContainer(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject container = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        container.transform.SetParent(parent, false);
        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = container.GetComponent<Image>();
        bool useTransparentBackground =
            name == "CharacterList" ||
            name == "TestimonyList" ||
            name == "LeftDoubtList" ||
            name == "RightDoubtList";
        image.color = useTransparentBackground ? new Color(1f, 1f, 1f, 0f) : generatedPanelColor;
        image.raycastTarget = true;
        return container;
    }

    private Dictionary<string, List<TestimonyDefinition>> BuildUnlockedTestimonyGroups()
    {
        Dictionary<string, List<TestimonyDefinition>> grouped = new Dictionary<string, List<TestimonyDefinition>>();
        if (notebookManager == null)
        {
            return grouped;
        }

        List<string> testimonyIds = notebookManager.GetUnlockedTestimonyIds();
        foreach (string testimonyId in testimonyIds)
        {
            TestimonyDefinition item = notebookManager.GetCurrentTestimony(testimonyId);
            if (item == null)
            {
                continue;
            }

            string characterId = string.IsNullOrEmpty(item.speakerCharacterId) ? "unknown_voice" : item.speakerCharacterId;
            List<TestimonyDefinition> list;
            if (!grouped.TryGetValue(characterId, out list))
            {
                list = new List<TestimonyDefinition>();
                grouped[characterId] = list;
            }

            list.Add(item);
        }

        return grouped;
    }

    public void ApplyGeneratedTextStyle(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        if (generatedFontAsset == null)
        {
            TMP_Text source = clueDetailText != null ? clueDetailText : GetComponentInChildren<TMP_Text>(true);
            if (source != null)
            {
                generatedFontAsset = source.font;
                generatedFontMaterial = source.fontSharedMaterial;
            }
            else
            {
                generatedFontAsset = TMP_Settings.defaultFontAsset;
                generatedFontMaterial = generatedFontAsset != null ? generatedFontAsset.material : null;
            }
        }

        if (generatedFontAsset != null)
        {
            text.font = generatedFontAsset;
        }

        if (generatedFontMaterial != null)
        {
            text.fontSharedMaterial = generatedFontMaterial;
        }
    }

    private void CreateCharacterButton(CharacterDefinition character, bool selected)
    {
        GameObject buttonObject = new GameObject(character.displayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(testimonyCharacterList, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? generatedSelectedColor : generatedPanelColor;
        if (selected)
        {
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = generatedOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 74f;
        layout.flexibleWidth = 1f;

        Button button = buttonObject.GetComponent<Button>();
        string characterId = character.id;
        button.onClick.AddListener(() =>
        {
            selectedTestimonyCharacterId = characterId;
            RefreshTestimonyPanel();
        });

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        ApplyGeneratedTextStyle(label);
        label.fontSize = 28f;
        label.text = $"<b>{character.displayName}</b>\n<size=60%>{character.role}</size>";
        label.color = generatedTextColor;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.margin = new Vector4(18f, 18f, 12f, 8f);

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void CreateTestimonyCard(TestimonyDefinition testimony)
    {
        GameObject cardObject = new GameObject(testimony.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        cardObject.transform.SetParent(testimonyList, false);

        Image image = cardObject.GetComponent<Image>();
        image.color = generatedPanelColor;
        Outline outline = cardObject.AddComponent<Outline>();
        outline.effectColor = generatedOutlineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        layout.minHeight = testimonyCardHeight.y;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;

        GameObject accentObject = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentObject.transform.SetParent(cardObject.transform, false);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = generatedAccentColor;
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.sizeDelta = new Vector2(8f, 0f);
        accentRect.anchoredPosition = new Vector2(10f, 0f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(cardObject.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        ApplyGeneratedTextStyle(text);
        text.text = $"<b>{testimony.name}</b>\n<size=78%>{testimony.summary}</size>";
        text.color = generatedTextColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.margin = new Vector4(28f, 12f, 14f, 12f);

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        RectTransform testimonyListRect = testimonyList as RectTransform;
        float availableWidth = testimonyListRect != null && testimonyListRect.rect.width > 0f
            ? testimonyListRect.rect.width - 56f
            : 560f;
        float preferredTextHeight = text.GetPreferredValues(text.text, availableWidth, 0f).y;
        layout.preferredHeight = Mathf.Max(testimonyCardHeight.y, preferredTextHeight + 24f);
    }

    private void CreateDoubtButton(Transform parent, string doubtId, bool selected)
    {
        DoubtDefinition doubt = notebookManager != null ? notebookManager.GetCurrentDoubt(doubtId) : null;
        string display = doubt != null && !string.IsNullOrEmpty(doubt.question) ? doubt.question : doubtId;

        GameObject buttonObject = new GameObject(doubtId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = selected ? generatedSelectedColor : generatedPanelColor;
        if (selected)
        {
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = generatedOutlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = doubtItemHeight.y;
        layout.flexibleWidth = 1f;

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() =>
        {
            selectedDoubtId = doubtId;
            RefreshDoubtPanel();
        });

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        ApplyGeneratedTextStyle(text);
        text.text = $"<b>{display}</b>";
        text.color = generatedTextColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        text.margin = new Vector4(16f, 8f, 16f, 8f);

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
