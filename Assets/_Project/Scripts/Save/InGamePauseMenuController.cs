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
/// 首次场景加载后即常驻 DontDestroyOnLoad；仅在非主菜单场景显示「设置」按钮。
/// 也可在场景里手动放按钮并拖到 <see cref="settingsButton"/>；按 ESC 可打开/关闭设置面板。
/// 设置界面优先从 Resources 路径 <c>UI/InGameSettingsOverlay</c> 加载 Prefab（根物体挂 <see cref="InGameSettingsOverlayView"/>）。
/// </summary>
public class InGamePauseMenuController : MonoBehaviour
{
    public static InGamePauseMenuController Instance { get; private set; }

    /// <summary>设置面板打开时，阻止对话等用 Input 监听左键的逻辑误触。</summary>
    public static bool IsBlockingGameInput()
    {
        return Instance != null && Instance.overlayRoot != null && Instance.overlayRoot.activeInHierarchy;
    }

    private const string SettingsOverlayResourcesPath = "UI/InGameSettingsOverlay";

    [Header("可选：在场景 Canvas 上绑定一个「设置」按钮")]
    [SerializeField] private Button settingsButton;

    [Header("快捷键")]
    [SerializeField] private bool openWithEscape = true;

    private GameObject overlayRoot;
    private Coroutine saveFeedbackRoutine;

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
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshForCurrentScene();
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
        RefreshForCurrentScene();
    }

    private void ClearRuntimeUiRefs()
    {
        if (settingsButton != null && settingsButton.name == "RuntimeSettingsButton")
        {
            settingsButton = null;
        }

        if (overlayRoot != null && !overlayRoot)
        {
            overlayRoot = null;
        }
    }

    private void RefreshForCurrentScene()
    {
        if (IsMainMenuScene())
        {
            DestroyRuntimeSettingsButtonIfAny();
            if (overlayRoot != null && overlayRoot)
            {
                overlayRoot.SetActive(false);
            }

            return;
        }

        TryBindSettingsButton();
        EnsureFloatingSettingsButton();
    }

    private static bool IsMainMenuScene()
    {
        Scene s = SceneManager.GetActiveScene();
        return s.IsValid()
            && string.Equals(s.name, SceneLoader.SCENE_MAIN_MENU, StringComparison.OrdinalIgnoreCase);
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

            string n = btn.gameObject.name.ToLowerInvariant();
            if (n.Contains("setting") || n.Contains("设置"))
            {
                settingsButton = btn;
                break;
            }
        }
    }

    private void EnsureFloatingSettingsButton()
    {
        if (settingsButton != null)
        {
            if (settingsButton.name == "RuntimeSettingsButton")
            {
                ReparentRuntimeSettingsButtonToPreferredCanvas();
            }

            settingsButton.onClick.RemoveListener(OpenSettingsOverlay);
            settingsButton.onClick.AddListener(OpenSettingsOverlay);
            return;
        }

        Canvas canvas = FindPreferredGameCanvas();
        if (canvas == null)
        {
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
        Canvas best = null;
        int bestOrder = int.MinValue;
        foreach (Canvas c in canvases)
        {
            if (c == null || !c.isActiveAndEnabled || c.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (IsDetectiveNotebookCanvas(c))
            {
                continue;
            }

            if (IsScreenFaderCanvas(c))
            {
                continue;
            }

            if (IsNotebookUnlockPresenterCanvas(c))
            {
                continue;
            }

            if (c.sortingOrder >= bestOrder)
            {
                best = c;
                bestOrder = c.sortingOrder;
            }
        }

        if (best != null)
        {
            return best;
        }

        foreach (Canvas c in canvases)
        {
            if (c == null || !c.isActiveAndEnabled || c.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (IsDetectiveNotebookCanvas(c))
            {
                continue;
            }

            if (IsScreenFaderCanvas(c))
            {
                continue;
            }

            if (IsNotebookUnlockPresenterCanvas(c))
            {
                continue;
            }

            return c;
        }

        return null;
    }

    /// <summary>侦探笔记根物体自带高 sorting 的 Canvas；不能把设置按钮或设置遮罩挂到该 Canvas 上，否则会盖住/抢占笔记入口按钮。</summary>
    private static bool IsDetectiveNotebookCanvas(Canvas canvas)
    {
        return canvas != null && canvas.GetComponent<EvidencePanelUI>() != null;
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
        SceneManager.LoadScene(SceneLoader.SCENE_MAIN_MENU);
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
