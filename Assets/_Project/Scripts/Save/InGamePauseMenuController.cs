using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 游戏内设置：保存、退出主菜单、退出游戏；音量占位。
/// 常驻 DontDestroyOnLoad；非主菜单场景会显示「设置」入口。
/// <para><b>自定义设置按钮（推荐）</b>：在关卡 UI 的 Canvas 下放一个 <see cref="Button"/>，
/// 在 Inspector 里调好 <see cref="RectTransform"/> 与 <see cref="Image.sprite"/>（背景图），
/// 物体命名为 <c>InGameSettingsButton</c> 或 <c>Btn_InGameSettings</c>（或名称含 <c>设置</c>/<c>Settings</c>），
/// 运行时即可自动绑定，且<b>不会</b>再生成代码里的占位按钮。</para>
/// <para>也可在任意脚本的 <c>Awake</c>/<c>Start</c> 里调用
/// <see cref="UseManualSettingsButton"/>，并设 <see cref="DisableAutoSpawnSettingsButton"/> 为 <c>true</c>，
/// 表示完全由你提供按钮（未绑定时不再自动生成）。</para>
/// <para>若存在 <c>Resources/UI/InGameSettingsButton</c> 预制体，且场景里尚未摆放同名按钮，则会<b>自动实例化</b>到当前游戏 Canvas（你在 Prefab 里摆好的 RectTransform / 图即可）。</para>
/// </summary>
public class InGamePauseMenuController : MonoBehaviour
{
    public static InGamePauseMenuController Instance { get; private set; }

    /// <summary>为 <c>true</c> 时，若未绑定任何设置按钮，则不再自动生成左上角占位按钮（便于纯手动布置）。</summary>
    public static bool DisableAutoSpawnSettingsButton { get; set; }

    private static Button s_PendingManualSettingsButton;

    /// <summary>最近一次「应挂载关卡 HUD」的场景（来自 sceneLoaded 或当前 ActiveScene），供 <see cref="FindPreferredGameCanvas"/> 优先选择该场景内的 Canvas。</summary>
    private static Scene s_PreferredHudCanvasScene;

    /// <summary>
    /// 由你的场景脚本显式指定设置按钮（可拖到 Inspector 再传入）。在 <see cref="Instance"/> 尚未创建时会排队，稍晚自动挂上。
    /// </summary>
    public static void UseManualSettingsButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (Instance != null)
        {
            Instance.AssignAndWireSettingsButton(button);
            return;
        }

        s_PendingManualSettingsButton = button;
    }

    /// <summary>设置面板打开时，阻止对话等用 Input 监听左键的逻辑误触。</summary>
    public static bool IsBlockingGameInput()
    {
        return Instance != null && Instance.overlayRoot != null && Instance.overlayRoot.activeInHierarchy;
    }

    private const string SettingsOverlayResourcesPath = "UI/InGameSettingsOverlay";
    private const string SettingsEntryButtonResourcesPath = "UI/InGameSettingsButton";

    [Header("可选：在场景 Canvas 上绑定一个「设置」按钮")]
    [SerializeField] private Button settingsButton;

    [Header("快捷键")]
    [SerializeField] private bool openWithEscape = true;

    private GameObject overlayRoot;
    private Coroutine saveFeedbackRoutine;
    private GameObject customSettingsEntryRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(InGamePauseMenuController));
        DontDestroyOnLoad(go);
        go.AddComponent<InGamePauseMenuController>();
        Debug.LogWarning("[InGamePause] Bootstrap：已创建 DontDestroyOnLoad 上的 InGamePauseMenuController（用此条确认脚本已编译进当前运行）。");
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
        ApplyPendingManualSettingsButton();
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyPendingManualSettingsButton();
        RefreshForSceneContext(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        if (!openWithEscape || IsMainMenuScene())
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (SaveSlotModalUITypeCheck.IsModalBlocking())
        {
            return;
        }

        ToggleOrOpenSettings();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearRuntimeUiRefs();
        RefreshForSceneContext(scene);
    }

    private void ClearRuntimeUiRefs()
    {
        if (settingsButton != null && settingsButton.name == "RuntimeSettingsButton")
        {
            settingsButton = null;
        }

        if (settingsButton != null && !settingsButton)
        {
            settingsButton = null;
        }

        if (customSettingsEntryRoot != null && !customSettingsEntryRoot)
        {
            customSettingsEntryRoot = null;
        }

        if (overlayRoot != null && !overlayRoot)
        {
            overlayRoot = null;
        }
    }

    /// <param name="contextScene">用 <see cref="SceneManager.sceneLoaded"/> 传入的「刚加载的场景」判断主菜单，避免加载体场景时 <see cref="SceneManager.GetActiveScene"/> 仍指向旧场景。</param>
    private void RefreshForSceneContext(Scene contextScene)
    {
        Scene hudContext = contextScene.IsValid() ? contextScene : SceneManager.GetActiveScene();

        if (!hudContext.IsValid() || IsMainMenuSceneName(hudContext.name))
        {
            s_PreferredHudCanvasScene = default;
            DestroyCustomInstantiatedSettingsEntryIfAny();
            DestroyRuntimeSettingsButtonIfAny();
            // 主菜单 EventSystem 上的 Binder 可能把「预制体资源上的 Button」登记进来；该引用在切关卡后仍会“非空”，
            // 但已不在任何加载场景中，会导致 TrainCorridor 误判已绑定而跳过实例化。
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
                settingsButton = null;
            }

            if (overlayRoot != null && overlayRoot)
            {
                overlayRoot.SetActive(false);
            }

            Debug.LogWarning(
                $"[InGamePause] Refresh：按主菜单或无效场景处理，已跳过关卡设置入口（scene='{hudContext.name}' valid={hudContext.IsValid()}）。");
            return;
        }

        s_PreferredHudCanvasScene = hudContext;
        InvalidateSettingsButtonIfNotInHudContext(hudContext);
        Debug.LogWarning($"[InGamePause] Refresh：进入关卡分支（scene='{hudContext.name}'），将尝试绑定/实例化设置入口。");
        TryBindSettingsButton();
        EnsureFloatingSettingsButton();
        DestroyOrphanRuntimeSettingsButtonIfUnused();
        StartCoroutine(DeferEnsureHudIfNeeded());
    }

    /// <summary>
    /// 清掉「预制体资源上的 Button」或「已卸载场景里」的绑定；否则会一直 <c>settingsButton != null</c>，关卡里误判已绑定而不再实例化。
    /// </summary>
    private void InvalidateSettingsButtonIfNotInHudContext(Scene hudContext)
    {
        if (settingsButton == null || !settingsButton)
        {
            settingsButton = null;
            return;
        }

        Scene btnScene = settingsButton.gameObject.scene;
        if (!btnScene.IsValid())
        {
            settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
            settingsButton = null;
            return;
        }

        bool sameGameplay = hudContext.IsValid() && btnScene == hudContext;
        bool ddol = string.Equals(btnScene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
        if (sameGameplay || ddol)
        {
            return;
        }

        settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
        settingsButton = null;
    }

    private IEnumerator DeferEnsureHudIfNeeded()
    {
        yield return null;
        if (!s_PreferredHudCanvasScene.IsValid() || IsMainMenuSceneName(s_PreferredHudCanvasScene.name))
        {
            yield break;
        }

        if (IsMainMenuScene())
        {
            yield break;
        }

        if (settingsButton != null && settingsButton)
        {
            yield break;
        }

        InvalidateSettingsButtonIfNotInHudContext(s_PreferredHudCanvasScene);
        TryBindSettingsButton();
        EnsureFloatingSettingsButton();
        DestroyOrphanRuntimeSettingsButtonIfUnused();
    }

    private void DestroyOrphanRuntimeSettingsButtonIfUnused()
    {
        GameObject found = GameObject.Find("RuntimeSettingsButton");
        if (found == null)
        {
            return;
        }

        Button runtimeBtn = found.GetComponent<Button>();
        if (runtimeBtn == null)
        {
            return;
        }

        if (settingsButton != null && settingsButton == runtimeBtn)
        {
            return;
        }

        Destroy(found);
    }

    private void ApplyPendingManualSettingsButton()
    {
        if (s_PendingManualSettingsButton == null)
        {
            return;
        }

        Button b = s_PendingManualSettingsButton;
        s_PendingManualSettingsButton = null;
        AssignAndWireSettingsButton(b);
    }

    private void AssignAndWireSettingsButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (settingsButton != null && settingsButton != button)
        {
            settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
        }

        // Button 在预制体根上与 customSettingsEntryRoot 为同一 Transform 时，IsChildOf(自身) 为 false，
        // 不能误判为「外来按钮」而销毁刚实例化的 Resources 入口。
        if (customSettingsEntryRoot != null && button != null && button.transform != null)
        {
            Transform ct = customSettingsEntryRoot.transform;
            Transform bt = button.transform;
            bool belongsToCustomEntry = bt == ct || bt.IsChildOf(ct);
            if (!belongsToCustomEntry)
            {
                DestroyCustomInstantiatedSettingsEntryIfAny();
            }
        }

        settingsButton = button;
        DestroyRuntimeSettingsButtonIfAny();

        settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
        settingsButton.onClick.AddListener(OpenSettingsOverlay);
    }

    private static bool IsMainMenuSceneName(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName)
            && string.Equals(sceneName, SceneLoader.SCENE_MAIN_MENU, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMainMenuScene()
    {
        Scene s = SceneManager.GetActiveScene();
        return s.IsValid() && IsMainMenuSceneName(s.name);
    }

    private void DestroyRuntimeSettingsButtonIfAny()
    {
        GameObject found = GameObject.Find("RuntimeSettingsButton");
        if (found != null)
        {
            Destroy(found);
        }

        if (settingsButton != null && settingsButton.name == "RuntimeSettingsButton")
        {
            settingsButton = null;
        }
    }

    private void DestroyCustomInstantiatedSettingsEntryIfAny()
    {
        if (customSettingsEntryRoot == null)
        {
            return;
        }

        if (settingsButton != null
            && settingsButton.transform != null
            && customSettingsEntryRoot != null)
        {
            Transform st = settingsButton.transform;
            Transform ct = customSettingsEntryRoot.transform;
            if (st == ct || st.IsChildOf(ct))
            {
                settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
                settingsButton = null;
            }
        }

        if (customSettingsEntryRoot)
        {
            Destroy(customSettingsEntryRoot);
        }

        customSettingsEntryRoot = null;
    }

    private void TryBindSettingsButton()
    {
        if (settingsButton != null)
        {
            return;
        }

        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn == null)
            {
                continue;
            }

            if (IsButtonUnderDetectiveNotebookUi(btn))
            {
                continue;
            }

            Canvas btnCanvas = btn.GetComponentInParent<Canvas>(true);
            if (!CanvasIsUsableForHud(btnCanvas))
            {
                continue;
            }

            if (string.Equals(btn.gameObject.name, "RuntimeSettingsButton", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string n = btn.gameObject.name.ToLowerInvariant();
            if (IsPreferredSettingsButtonName(n))
            {
                settingsButton = btn;
                break;
            }
        }
    }

    private static bool IsPreferredSettingsButtonName(string lowerName)
    {
        if (string.IsNullOrEmpty(lowerName))
        {
            return false;
        }

        // 代码生成的占位按钮名含 "settings"，必须排除，否则会永远绑到占位按钮上。
        if (lowerName == "runtimesettingsbutton")
        {
            return false;
        }

        if (lowerName == "ingamesettingsbutton" || lowerName == "btn_ingamesettings")
        {
            return true;
        }

        if (lowerName.Contains("btn_settings") || lowerName.Contains("gamesettingsbutton"))
        {
            return true;
        }

        if (lowerName.Contains("setting") || lowerName.Contains("设置"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 在指定根节点下查找第一个符合「手动设置按钮」命名的 <see cref="Button"/>（不含代码占位、不含侦探笔记 UI）。
    /// 供 <see cref="InGameSettingsButtonBinder"/> 等在未拖引用时自动绑定。
    /// </summary>
    public static Button FindPreferredSettingsButtonUnder(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Button btn in root.GetComponentsInChildren<Button>(true))
        {
            if (btn == null)
            {
                continue;
            }

            if (IsButtonUnderDetectiveNotebookUi(btn))
            {
                continue;
            }

            Canvas btnCanvas = btn.GetComponentInParent<Canvas>(true);
            if (!CanvasIsUsableForHud(btnCanvas))
            {
                continue;
            }

            if (string.Equals(btn.gameObject.name, "RuntimeSettingsButton", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string n = btn.gameObject.name.ToLowerInvariant();
            if (IsPreferredSettingsButtonName(n))
            {
                return btn;
            }
        }

        return null;
    }

    private void TryInstantiateCustomSettingsEntryFromResources()
    {
        if (customSettingsEntryRoot != null && customSettingsEntryRoot)
        {
            Button existing = customSettingsEntryRoot.GetComponent<Button>()
                ?? customSettingsEntryRoot.GetComponentInChildren<Button>(true);
            if (existing != null)
            {
                AssignAndWireSettingsButton(existing);
                return;
            }

            Destroy(customSettingsEntryRoot);
            customSettingsEntryRoot = null;
        }

        GameObject prefab = Resources.Load<GameObject>(SettingsEntryButtonResourcesPath);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"[InGamePause] 未找到 Resources/{SettingsEntryButtonResourcesPath}，无法自动放置设置入口按钮。",
                this);
            return;
        }

        Canvas canvas = FindPreferredGameCanvas();
        if (canvas == null)
        {
            Debug.LogWarning(
                "[InGamePause] 未找到可用的游戏 UI Canvas（需 Screen Space、已激活、非侦探笔记/淡入淡出层，且 RectTransform 缩放不能为 0）。"
                + " 请检查场景里主 Canvas 的 Scale 是否为 1。",
                this);
            return;
        }

        GameObject inst = Instantiate(prefab, canvas.transform, false);
        inst.name = prefab.name;
        customSettingsEntryRoot = inst;
        inst.transform.SetAsLastSibling();

        Button btn = inst.GetComponent<Button>() ?? inst.GetComponentInChildren<Button>(true);
        if (btn == null)
        {
            Debug.LogWarning(
                $"[InGamePause] Resources/{SettingsEntryButtonResourcesPath} 预制体上未找到 Button，已销毁实例。",
                inst);
            Destroy(inst);
            customSettingsEntryRoot = null;
            return;
        }

        foreach (Image img in inst.GetComponentsInChildren<Image>(true))
        {
            InGameSettingsOverlayView.EnsureSolidSprite(img);
        }

        AssignAndWireSettingsButton(btn);
        Debug.LogWarning(
            $"[InGamePause] 已从 Resources 实例化设置入口：父 Canvas「{canvas.name}」场景「{canvas.gameObject.scene.name}」，实例「{inst.name}」。",
            inst);
    }

    private void EnsureFloatingSettingsButton()
    {
        if (settingsButton != null)
        {
            Debug.LogWarning(
                $"[InGamePause] EnsureFloating：已存在设置按钮「{settingsButton.name}」，跳过 Resources 实例化与占位生成。",
                settingsButton);
            if (settingsButton.name == "RuntimeSettingsButton")
            {
                ReparentRuntimeSettingsButtonToPreferredCanvas();
            }

            settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
            settingsButton.onClick.AddListener(OpenSettingsOverlay);
            return;
        }

        TryInstantiateCustomSettingsEntryFromResources();
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
            settingsButton.onClick.AddListener(OpenSettingsOverlay);
            return;
        }

        if (DisableAutoSpawnSettingsButton)
        {
            Debug.LogWarning(
                "[InGamePause] DisableAutoSpawnSettingsButton=true，且未绑定设置按钮：不会生成 RuntimeSettingsButton 占位。",
                this);
            return;
        }

        Canvas canvas = FindPreferredGameCanvas();
        if (canvas == null)
        {
            Debug.LogWarning(
                "[InGamePause] 未找到可用 Canvas，无法生成左上角占位「设置」按钮。",
                this);
            return;
        }

        GameObject go = new GameObject("RuntimeSettingsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(28f, -28f);
        rt.sizeDelta = new Vector2(132f, 48f);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.22f, 0.18f, 0.15f, 0.95f);
        img.raycastTarget = true;
        InGameSettingsOverlayView.EnsureSolidSprite(img);

        GameObject label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        TMP_Text tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.text = "设置";
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.88f, 0.8f);
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        RectTransform lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero;
        lr.offsetMax = Vector2.zero;

        settingsButton = go.GetComponent<Button>();
        settingsButton.targetGraphic = img;
        settingsButton.onClick.AddListener(OpenSettingsOverlay);
    }

    private void ReparentRuntimeSettingsButtonToPreferredCanvas()
    {
        if (settingsButton == null || settingsButton.name != "RuntimeSettingsButton")
        {
            return;
        }

        Canvas preferred = FindPreferredGameCanvas();
        if (preferred == null)
        {
            return;
        }

        Transform target = preferred.transform;
        if (settingsButton.transform.parent == target)
        {
            return;
        }

        settingsButton.transform.SetParent(target, false);
        settingsButton.transform.SetAsLastSibling();

        RectTransform rt = settingsButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(28f, -28f);
            rt.sizeDelta = new Vector2(132f, 48f);
        }
    }

    private static Canvas FindPreferredGameCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        Scene primary = SceneManager.GetActiveScene();
        if (s_PreferredHudCanvasScene.IsValid() && !IsMainMenuSceneName(s_PreferredHudCanvasScene.name))
        {
            primary = s_PreferredHudCanvasScene;
        }

        Canvas bestInPrimary = null;
        int bestOrderInPrimary = int.MinValue;
        foreach (Canvas c in canvases)
        {
            if (!CanvasIsUsableForHud(c))
            {
                continue;
            }

            if (!primary.IsValid() || c.gameObject.scene != primary)
            {
                continue;
            }

            if (c.sortingOrder >= bestOrderInPrimary)
            {
                bestInPrimary = c;
                bestOrderInPrimary = c.sortingOrder;
            }
        }

        if (bestInPrimary != null)
        {
            return bestInPrimary;
        }

        Canvas bestAny = null;
        int bestOrderAny = int.MinValue;
        foreach (Canvas c in canvases)
        {
            if (!CanvasIsUsableForHud(c))
            {
                continue;
            }

            if (c.sortingOrder >= bestOrderAny)
            {
                bestAny = c;
                bestOrderAny = c.sortingOrder;
            }
        }

        return bestAny;
    }

    /// <summary>用于挂载 HUD 类 UI：排除笔记/淡入淡出层，且父级链须激活、Canvas 可用、整体缩放不能接近 0。</summary>
    private static bool CanvasIsUsableForHud(Canvas canvas)
    {
        if (canvas == null || !canvas.enabled)
        {
            return false;
        }

        if (!canvas.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            return false;
        }

        if (IsDetectiveNotebookCanvas(canvas) || IsScreenFaderCanvas(canvas) || IsNotebookUnlockPresenterCanvas(canvas))
        {
            return false;
        }

        Vector3 s = canvas.transform.lossyScale;
        if (s.sqrMagnitude < 1e-8f || Mathf.Abs(s.x * s.y * s.z) < 1e-8f)
        {
            return false;
        }

        return true;
    }

    /// <summary>侦探笔记 UI 树内任意 Canvas（含子物体上的 Canvas）均不可挂 HUD。</summary>
    private static bool IsDetectiveNotebookCanvas(Canvas canvas)
    {
        return canvas != null && canvas.GetComponentInParent<EvidencePanelUI>(true) != null;
    }

    private static bool IsScreenFaderCanvas(Canvas canvas)
    {
        return canvas != null && canvas.GetComponentInParent<ScreenFader>(true) != null;
    }

    private static bool IsNotebookUnlockPresenterCanvas(Canvas canvas)
    {
        return canvas != null && canvas.GetComponentInParent<NotebookUnlockOverlayPresenter>(true) != null;
    }

    private static bool IsButtonUnderDetectiveNotebookUi(Button button)
    {
        return button != null && button.GetComponentInParent<EvidencePanelUI>(true) != null;
    }

    private static void ApplyDefaultTmpFont(TMP_Text tmp)
    {
        if (tmp == null)
        {
            return;
        }

        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void ToggleOrOpenSettings()
    {
        if (overlayRoot != null && overlayRoot)
        {
            overlayRoot.SetActive(!overlayRoot.activeSelf);
            return;
        }

        OpenSettingsOverlay();
    }

    public void OpenSettingsOverlay()
    {
        if (IsMainMenuScene())
        {
            return;
        }

        if (overlayRoot != null)
        {
            if (!overlayRoot)
            {
                overlayRoot = null;
            }
            else
            {
                overlayRoot.SetActive(true);
                overlayRoot.transform.SetAsLastSibling();
                return;
            }
        }

        Canvas canvas = FindPreferredGameCanvas();
        if (canvas == null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(SettingsOverlayResourcesPath);
        GameObject root = prefab != null
            ? Instantiate(prefab, canvas.transform, false)
            : new GameObject(
                "InGameSettingsOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasScaler),
                typeof(InGameSettingsOverlayView));

        overlayRoot = root;
        ConfigureOverlayHierarchy(canvas, root);
    }

    private void CloseSettingsOverlay()
    {
        if (overlayRoot != null && overlayRoot)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void DisposeSettingsOverlay()
    {
        if (saveFeedbackRoutine != null)
        {
            StopCoroutine(saveFeedbackRoutine);
            saveFeedbackRoutine = null;
        }

        if (overlayRoot == null)
        {
            return;
        }

        if (overlayRoot)
        {
            overlayRoot.SetActive(false);
            Destroy(overlayRoot);
        }

        overlayRoot = null;
    }

    private void ConfigureOverlayHierarchy(Canvas canvas, GameObject root)
    {
        RectTransform ort = root.GetComponent<RectTransform>();
        ort.SetParent(canvas.transform, false);
        ort.SetAsLastSibling();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        Canvas ocv = root.GetComponent<Canvas>();
        if (ocv != null)
        {
            ocv.renderMode = canvas.renderMode;
            ocv.worldCamera = canvas.worldCamera;
            ocv.planeDistance = canvas.planeDistance;
            ocv.overrideSorting = true;
            ocv.sortingOrder = Mathf.Max(canvas.sortingOrder + 50, 400);
        }

        CanvasScaler parentScaler = canvas.GetComponent<CanvasScaler>();
        if (parentScaler != null)
        {
            CanvasScaler copy = root.GetComponent<CanvasScaler>();
            if (copy == null)
            {
                copy = root.AddComponent<CanvasScaler>();
            }

            copy.uiScaleMode = parentScaler.uiScaleMode;
            copy.referencePixelsPerUnit = parentScaler.referencePixelsPerUnit;
            copy.referenceResolution = parentScaler.referenceResolution;
            copy.screenMatchMode = parentScaler.screenMatchMode;
            copy.matchWidthOrHeight = parentScaler.matchWidthOrHeight;
        }

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = root.AddComponent<CanvasGroup>();
        }

        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;

        InGameSettingsOverlayView view = root.GetComponent<InGameSettingsOverlayView>();
        if (view != null)
        {
            view.Wire(OnSaveClicked, OnExitToMenuClicked, OnQuitGameClicked, CloseSettingsOverlay);
        }
    }

    private void OnSaveClicked()
    {
        int slot = GameSaveService.GetEffectiveSaveSlotIndex();
        string path = GameSaveService.GetSlotFilePath(slot);
        try
        {
            SaveGameData snap = GameSaveService.BuildSnapshotFromCurrentGame();
            if (snap == null)
            {
                ShowOverlayToast("存档失败：未能生成存档数据。");
                Debug.LogError("[InGamePause] BuildSnapshotFromCurrentGame 返回 null。");
                return;
            }

            GameSaveService.WriteSlot(slot, snap);
            GameSaveService.ActiveSaveSlot = slot;
            Debug.Log($"[InGamePause] 已写入存档槽 {slot + 1}：\n{path}");
            ShowOverlayToast($"已保存到存档槽 {slot + 1}。\n（完整路径见控制台）");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ShowOverlayToast($"存档失败：{e.Message}");
        }
    }

    private void ShowOverlayToast(string message, float durationSeconds = 2.8f)
    {
        if (overlayRoot == null || !overlayRoot)
        {
            return;
        }

        if (saveFeedbackRoutine != null)
        {
            StopCoroutine(saveFeedbackRoutine);
        }

        saveFeedbackRoutine = StartCoroutine(OverlayToastRoutine(message, durationSeconds));
    }

    private IEnumerator OverlayToastRoutine(string message, float durationSeconds)
    {
        Transform parent = overlayRoot.transform;
        Transform old = parent.Find("RuntimeSaveToast");
        if (old != null)
        {
            Destroy(old.gameObject);
        }

        GameObject go = new GameObject("RuntimeSaveToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(780f, 140f);

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.93f, 0.88f, 1f);
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);

        go.transform.SetAsLastSibling();

        yield return new WaitForSecondsRealtime(durationSeconds);

        if (go != null)
        {
            Destroy(go);
        }

        saveFeedbackRoutine = null;
    }

    private void OnExitToMenuClicked()
    {
        DisposeSettingsOverlay();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ForceEndDialogueIfAny();
        }

        if (EvidencePanelUI.IsNotebookOpen)
        {
            EvidencePanelUI panel = EvidencePanelUI.EnsureRuntimeInstance();
            if (panel != null)
            {
                panel.ToggleEvidencePanel();
            }
        }

        // 从暂停返回主菜单：直接切场景，避免 ScreenFader 叠在已关闭的面板上造成「关得慢」的感觉。
        EvidencePanelUI.DisableAllEventSystemComponentsBeforeSceneLoad();
        SceneManager.LoadScene(SceneLoader.SCENE_MAIN_MENU);
        EvidencePanelUI.EnsureSingleEventSystem();
    }

    private void OnQuitGameClicked()
    {
        try
        {
#if UNITY_EDITOR
            Debug.Log("[InGamePause] 退出游戏：停止编辑器播放模式。");
            EditorApplication.ExitPlaymode();
#else
            Debug.Log("[InGamePause] 退出游戏：Application.Quit()");
            Application.Quit();
#endif
        }
        catch (Exception e)
        {
            Debug.LogException(e);
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

/// <summary>ESC 打开设置时，若存档槽弹窗打开则不打断。</summary>
internal static class SaveSlotModalUITypeCheck
{
    internal static bool IsModalBlocking()
    {
        return UnityEngine.Object.FindObjectOfType<SaveSlotModalView>(true) != null
            || UnityEngine.Object.FindObjectOfType<SaveSlotModalRuntimeFallbackHost>(true) != null;
    }
}
