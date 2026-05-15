using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 侦探笔记「疑点」条目点击后：全屏暗角 + 实例化 <c>Resources/UI/doubt</c> 预制体展示疑点文案。
/// 与 <see cref="PerceptionMomentPresenter"/> 分离实例，避免察觉流程互相抢 UI。
/// </summary>
public sealed class DoubtInquiryOverlayPresenter : MonoBehaviour
{
    public static DoubtInquiryOverlayPresenter Instance { get; private set; }

    /// <summary>疑点覆层显示且挡游戏内点击时为 true（对话推进等应暂停）。</summary>
    public static bool IsBlockingInput =>
        Instance != null && Instance.overlayRoot != null && Instance.overlayRoot.activeInHierarchy;

    private const string ChromePrefabResourcesPath = "UI/doubt";
    private const int OverlayCanvasSortingOrder = 5600;

    private GameObject overlayRoot;
    private Canvas rootCanvas;
    private GameObject chromeInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (Instance != null)
        {
            return;
        }

        DoubtInquiryOverlayPresenter existing = FindObjectOfType<DoubtInquiryOverlayPresenter>(true);
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject go = new GameObject(nameof(DoubtInquiryOverlayPresenter));
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<DoubtInquiryOverlayPresenter>();
    }

    /// <summary>在笔记中点击某一疑点 id 时调用。</summary>
    public static void ShowForDoubtId(string doubtId)
    {
        EnsureInstance();
        Instance?.ShowInternal(doubtId);
    }

    public static void Hide()
    {
        Instance?.HideInternal();
    }

    private void ShowInternal(string doubtId)
    {
        DetectiveNotebookManager nb = DetectiveNotebookManager.EnsureInstance();
        DoubtDefinition doubt = nb != null ? nb.GetCurrentDoubt(doubtId) : null;
        if (doubt == null)
        {
            Debug.LogWarning($"DoubtInquiryOverlayPresenter: 未找到疑点定义 {doubtId}");
            return;
        }

        EnsureOverlayUi();
        ClearChrome();

        GameObject prefab = Resources.Load<GameObject>(ChromePrefabResourcesPath);
        if (prefab == null)
        {
            Debug.LogError($"DoubtInquiryOverlayPresenter: 缺少 Resources/{ChromePrefabResourcesPath}（应为 doubt.prefab）。");
            return;
        }

        chromeInstance = Instantiate(prefab, overlayRoot.transform, false);
        DoubtInquiryChromeView view = chromeInstance.GetComponent<DoubtInquiryChromeView>();
        if (view == null)
        {
            Debug.LogError(
                "DoubtInquiryOverlayPresenter: Resources/UI/doubt 根物体缺少 DoubtInquiryChromeView。"
                + " 运行时 AddComponent 会丢失 Inspector 上的 doubtOptionButtonPrefab 等引用；请在预制体根上挂 DoubtInquiryChromeView 并保存。");
            Destroy(chromeInstance);
            chromeInstance = null;
            return;
        }

        view.Bind(doubt);

        overlayRoot.SetActive(true);
    }

    private void HideInternal()
    {
        ClearChrome();
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void ClearChrome()
    {
        if (chromeInstance != null)
        {
            Destroy(chromeInstance);
            chromeInstance = null;
        }
    }

    private void EnsureOverlayUi()
    {
        if (overlayRoot != null)
        {
            return;
        }

        overlayRoot = new GameObject("DoubtInquiryOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(transform, false);
        RectTransform ort = overlayRoot.GetComponent<RectTransform>();
        StretchFull(ort);

        rootCanvas = overlayRoot.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = OverlayCanvasSortingOrder;
        overlayRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dim.transform.SetParent(overlayRoot.transform, false);
        StretchFull(dim.GetComponent<RectTransform>());
        Image dimImage = dim.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.45f);
        dimImage.raycastTarget = true;
        Button dimButton = dim.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(HideInternal);

        overlayRoot.SetActive(false);
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
