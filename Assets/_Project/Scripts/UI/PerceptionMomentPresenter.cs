using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 对话 notice 察觉：先播 Resources 视频，再显示波洛立绘与「有猫腻....」提示，直至点对正确热点后清除。
/// </summary>
public class PerceptionMomentPresenter : MonoBehaviour
{
    public static PerceptionMomentPresenter Instance { get; private set; }

    /// <summary>仅察觉开场视频播放期间为 true（挡点击）；热点阶段不挡察觉按钮。</summary>
    public static bool IsBlockingInput => Instance != null && Instance.introBlocking;

    private const string IntroVideoResourcesPath = "test";
    private const string DefaultPortraitResourcesPath = "Characters/poirot_normal";
    private const string PresenterResourcesPath = "UI/PerceptionMomentPresenter";
    private const string NoticeChromeResourcesPath = "UI/PerceptionMomentNoticeChrome";

    [SerializeField] private GameObject optionalOverlayPrefab;
    [SerializeField] private GameObject noticeChromePrefab;
    [SerializeField] private VideoPlayer introVideoPlayer;
    [SerializeField, Min(0.05f)] private float introFallbackDuration = 1f;
    [SerializeField, Min(0.1f)] private float videoPrepareTimeout = 3f;
    [SerializeField, Min(0.1f)] private float videoPlayTimeout = 6f;
    [SerializeField] private int overlaySortingOrder = 3200;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private string noticeLineText = "有猫腻....";
    [SerializeField, Min(1)] private int blinkingTailLength = 1;
    [SerializeField, Min(0.05f)] private float blinkingInterval = 0.45f;

    private bool introBlocking;
    private GameObject overlayRoot;
    private Canvas rootCanvas;
    private GameObject introBlocker;
    private GameObject chromeGroup;
    private VideoPlayer overlayVideoPlayer;
    private RenderTexture videoRenderTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static PerceptionMomentPresenter EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PerceptionMomentPresenter existing = FindObjectOfType<PerceptionMomentPresenter>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject prefab = Resources.Load<GameObject>(PresenterResourcesPath);
        if (prefab != null)
        {
            GameObject go = Instantiate(prefab);
            DontDestroyOnLoad(go);
            Instance = go.GetComponent<PerceptionMomentPresenter>();
            if (Instance != null)
            {
                return Instance;
            }

            Destroy(go);
        }

        GameObject runtimeGo = new GameObject(nameof(PerceptionMomentPresenter));
        DontDestroyOnLoad(runtimeGo);
        Instance = runtimeGo.AddComponent<PerceptionMomentPresenter>();
        return Instance;
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
        EnsureIntroVideoPlayerResolved();
        Debug.LogWarning("[PerceptionMoment] Presenter 已就绪；察觉开场仅在进入带 hotspots 的 notice 对话节点时触发。");
    }

    public static IEnumerator PlayIntroVideoRoutine()
    {
        yield return EnsureInstance().PlayIntroVideoInternal();
    }

    public static void ShowNoticeChrome()
    {
        EnsureInstance().ShowNoticeChromeInternal();
    }

    public static void DismissNoticeChrome()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.DismissNoticeChromeInternal();
    }

    private IEnumerator PlayIntroVideoInternal()
    {
        introBlocking = true;
        Debug.Log("[PerceptionMoment] 开始察觉开场。");
        try
        {
            EnsureOverlayRoot();
            if (overlayRoot == null)
            {
                Debug.LogWarning("[PerceptionMoment] 未能创建察觉 Overlay，已中止。");
                yield break;
            }

            if (optionalOverlayPrefab != null)
            {
                PerceptionMomentOverlayView view = optionalOverlayPrefab.GetComponentInChildren<PerceptionMomentOverlayView>(true);
                if (view != null)
                {
                    yield return view.PlayRoutine();
                    yield break;
                }
            }

            SetChromeVisible(false);
            SetIntroBlockerActive(true);

            VideoClip clip = LoadIntroVideoClip();
            if (clip == null)
            {
                Debug.LogWarning(
                    $"[PerceptionMoment] 未找到可用的 VideoClip「{IntroVideoResourcesPath}」。"
                    + $" 将等待 {introFallbackDuration:0.##}s 后进入立绘与气泡。"
                    + " 请把 test.mov（H.264）放在 Resources 根目录，并在 Inspector 中按 Video Clip 重新导入。");
                yield return new WaitForSecondsRealtime(introFallbackDuration);
                yield break;
            }

            Debug.Log($"[PerceptionMoment] 已加载 VideoClip「{clip.name}」，时长 {clip.length:0.##}s。");
            EnsureIntroVideoPlayerResolved();
            yield return PlayOverlayHostedVideoRoutine(clip);
        }
        finally
        {
            introBlocking = false;
            SetIntroBlockerActive(false);
            HideVideoDisplay();
            Debug.Log("[PerceptionMoment] 察觉开场结束。");
        }
    }

    private static VideoClip LoadIntroVideoClip()
    {
        VideoClip clip = Resources.Load<VideoClip>(IntroVideoResourcesPath);
        if (clip != null)
        {
            return clip;
        }

        Object asset = Resources.Load(IntroVideoResourcesPath);
        if (asset != null)
        {
            Debug.LogWarning(
                $"[PerceptionMoment] Resources/{IntroVideoResourcesPath} 存在，但类型是 {asset.GetType().Name}，不是 VideoClip。"
                + " 请在 Project 里选中该视频并确认 Importer 为 Video Clip。");
        }

        return null;
    }

    private IEnumerator PlayOverlayHostedVideoRoutine(VideoClip clip)
    {
        GameObject videoGo = CreateUiObject("IntroVideo", overlayRoot.transform);
        StretchFull(videoGo.GetComponent<RectTransform>());
        RawImage rawImage = videoGo.AddComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.color = Color.white;
        videoGo.transform.SetAsLastSibling();
        if (introBlocker != null)
        {
            introBlocker.transform.SetAsFirstSibling();
        }

        if (videoRenderTexture == null)
        {
            videoRenderTexture = new RenderTexture(1920, 1080, 0);
        }

        VideoPlayer player = ResolveIntroVideoPlayer();
        VideoPlayerPlaybackSettings playbackSettings = CapturePlaybackSettings(player);
        try
        {
            player.gameObject.SetActive(true);
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = videoRenderTexture;
            player.clip = clip;
            rawImage.texture = videoRenderTexture;

            Debug.Log("[PerceptionMoment] 在察觉 Overlay 上播放开场视频。");
            yield return WaitUntilPrepared(player);
            if (!player.isPrepared)
            {
                yield break;
            }

            yield return WaitUntilPlaybackFinished(player, (float)clip.length);
        }
        finally
        {
            RestorePlaybackSettings(player, playbackSettings);
            Destroy(videoGo);
        }
    }

    private VideoPlayer ResolveIntroVideoPlayer()
    {
        if (introVideoPlayer != null)
        {
            return introVideoPlayer;
        }

        if (overlayVideoPlayer == null)
        {
            overlayVideoPlayer = overlayRoot.AddComponent<VideoPlayer>();
        }

        return overlayVideoPlayer;
    }

    private struct VideoPlayerPlaybackSettings
    {
        public VideoRenderMode RenderMode;
        public RenderTexture TargetTexture;
        public Camera TargetCamera;
        public bool PlayOnAwake;
    }

    private static VideoPlayerPlaybackSettings CapturePlaybackSettings(VideoPlayer player)
    {
        return new VideoPlayerPlaybackSettings
        {
            RenderMode = player.renderMode,
            TargetTexture = player.targetTexture,
            TargetCamera = player.targetCamera,
            PlayOnAwake = player.playOnAwake
        };
    }

    private static void RestorePlaybackSettings(VideoPlayer player, VideoPlayerPlaybackSettings settings)
    {
        player.Stop();
        player.renderMode = settings.RenderMode;
        player.targetTexture = settings.TargetTexture;
        player.targetCamera = settings.TargetCamera;
        player.playOnAwake = settings.PlayOnAwake;
    }

    private IEnumerator WaitUntilPrepared(VideoPlayer player)
    {
        player.Prepare();
        float prepareElapsed = 0f;
        while (!player.isPrepared && prepareElapsed < videoPrepareTimeout)
        {
            prepareElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!player.isPrepared)
        {
            Debug.LogWarning(
                $"[PerceptionMoment] VideoPlayer 在 {videoPrepareTimeout:0.##}s 内未能 Prepare。"
                + " 请检查视频编码/平台支持，或改用 H.264 MP4。");
        }
    }

    private IEnumerator WaitUntilPlaybackFinished(VideoPlayer player, float clipLengthSeconds)
    {
        player.Play();
        float playElapsed = 0f;
        float maxWait = Mathf.Max(videoPlayTimeout, clipLengthSeconds + 0.5f);
        while (player.isPlaying && playElapsed < maxWait)
        {
            playElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player.isPlaying)
        {
            Debug.LogWarning(
                $"[PerceptionMoment] 视频播放超过 {maxWait:0.##}s，已强制结束并进入立绘与气泡。");
            player.Stop();
        }
    }

    private void EnsureIntroVideoPlayerResolved()
    {
        if (introVideoPlayer != null)
        {
            introVideoPlayer.playOnAwake = false;
            return;
        }

        introVideoPlayer = GetComponentInChildren<VideoPlayer>(true);
        if (introVideoPlayer != null)
        {
            introVideoPlayer.playOnAwake = false;
            return;
        }

        GameObject videoGo = new GameObject("IntroVideoPlayer");
        videoGo.transform.SetParent(transform, false);
        introVideoPlayer = videoGo.AddComponent<VideoPlayer>();
        introVideoPlayer.playOnAwake = false;
        introVideoPlayer.isLooping = false;
        introVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        introVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoGo.SetActive(false);
    }

    private void ShowNoticeChromeInternal()
    {
        EnsureOverlayRoot();
        if (overlayRoot == null)
        {
            Debug.LogWarning("[PerceptionMoment] ShowNoticeChrome 失败：Overlay 不存在。");
            return;
        }

        SetIntroBlockerActive(false);
        HideVideoDisplay();

        if (chromeGroup != null)
        {
            chromeGroup.SetActive(true);
            Debug.Log("[PerceptionMoment] 已重新显示察觉立绘与气泡。");
            return;
        }

        GameObject chromePrefabSource = noticeChromePrefab != null
            ? noticeChromePrefab
            : Resources.Load<GameObject>(NoticeChromeResourcesPath);

        if (chromePrefabSource != null)
        {
            chromeGroup = Instantiate(chromePrefabSource, overlayRoot.transform, false);
            // 保留预制体根 RectTransform 的锚点/位置/尺寸，不在此处 StretchFull，否则会覆盖你在 Prefab 里的摆放。

            PerceptionMomentNoticeChromeView chromeView = chromeGroup.GetComponent<PerceptionMomentNoticeChromeView>();
            if (chromeView == null)
            {
                chromeView = chromeGroup.GetComponentInChildren<PerceptionMomentNoticeChromeView>(true);
            }

            if (chromeView != null)
            {
                chromeView.Apply(noticeLineText, ResolvePortraitSprite(), blinkingTailLength, blinkingInterval);
            }
            else
            {
                Debug.LogWarning(
                    "[PerceptionMoment] 察觉气泡预制体上未找到 PerceptionMomentNoticeChromeView。"
                    + " 请在预制体根或子物体上挂载该脚本，并绑定 Portrait Image 与 Notice Line (TMP)。");
            }

            EnsureChromeCanvasGroup();
            SetOverlayRaycastBlocking(false);
            Debug.Log("[PerceptionMoment] 已用预制体显示察觉立绘与气泡。");
            return;
        }

        chromeGroup = new GameObject("NoticeChrome", typeof(RectTransform));
        chromeGroup.transform.SetParent(overlayRoot.transform, false);
        StretchFull(chromeGroup.GetComponent<RectTransform>());

        GameObject portraitGo = CreateUiObject("PoirotPortrait", chromeGroup.transform);
        RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
        portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRect.pivot = new Vector2(0.5f, 0f);
        portraitRect.anchoredPosition = new Vector2(-40f, 120f);
        portraitRect.sizeDelta = new Vector2(420f, 560f);
        Image portrait = portraitGo.AddComponent<Image>();
        portrait.sprite = ResolvePortraitSprite();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        if (portrait.sprite == null)
        {
            Debug.LogWarning(
                "[PerceptionMoment] 未找到波洛立绘 Sprite（默认 Characters/poirot_normal）。"
                + " 气泡仍会显示，可在 PerceptionMomentPresenter 上指定 defaultPortrait。");
        }

        GameObject bubbleGo = CreateUiObject("NoticeBubble", chromeGroup.transform);
        RectTransform bubbleRect = bubbleGo.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.pivot = new Vector2(0f, 0.5f);
        bubbleRect.anchoredPosition = new Vector2(220f, 360f);
        bubbleRect.sizeDelta = new Vector2(420f, 120f);
        Image bubbleBg = bubbleGo.AddComponent<Image>();
        bubbleBg.sprite = GetWhiteSprite();
        bubbleBg.color = new Color(0.96f, 0.93f, 0.86f, 0.96f);
        bubbleBg.raycastTarget = false;

        GameObject textGo = CreateUiObject("NoticeLine", bubbleGo.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        TMP_Text line = textGo.AddComponent<TextMeshProUGUI>();
        line.text = noticeLineText;
        line.fontSize = 34f;
        line.alignment = TextAlignmentOptions.MidlineLeft;
        line.color = new Color(0.18f, 0.14f, 0.1f);
        line.raycastTarget = false;
        line.richText = true;
        if (TMP_Settings.defaultFontAsset != null)
        {
            line.font = TMP_Settings.defaultFontAsset;
        }

        BlinkingTextTail blink = textGo.AddComponent<BlinkingTextTail>();
        blink.Configure(noticeLineText, blinkingTailLength, blinkingInterval);

        EnsureChromeCanvasGroup();
        SetOverlayRaycastBlocking(false);
        Debug.Log("[PerceptionMoment] 已显示察觉立绘与气泡。");
    }

    private void EnsureChromeCanvasGroup()
    {
        if (chromeGroup == null)
        {
            return;
        }

        CanvasGroup chromeCg = chromeGroup.GetComponent<CanvasGroup>();
        if (chromeCg == null)
        {
            chromeCg = chromeGroup.AddComponent<CanvasGroup>();
        }

        chromeCg.alpha = 1f;
        chromeCg.interactable = false;
        chromeCg.blocksRaycasts = false;
    }

    private void DismissNoticeChromeInternal()
    {
        introBlocking = false;
        HideOverlay();
    }

    private void EnsureOverlayRoot()
    {
        if (overlayRoot != null)
        {
            return;
        }

        overlayRoot = new GameObject("PerceptionMomentOverlay", typeof(RectTransform));
        overlayRoot.transform.SetParent(transform, false);
        StretchFull(overlayRoot.GetComponent<RectTransform>());

        rootCanvas = overlayRoot.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.overrideSorting = true;
        rootCanvas.sortingOrder = overlaySortingOrder;
        overlayRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        introBlocker = CreateUiObject("IntroBlocker", overlayRoot.transform);
        StretchFull(introBlocker.GetComponent<RectTransform>());
        Image blockerImage = introBlocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;

        SetIntroBlockerActive(false);
        SetOverlayRaycastBlocking(false);
    }

    private void SetIntroBlockerActive(bool active)
    {
        if (introBlocker != null)
        {
            introBlocker.SetActive(active);
        }

        SetOverlayRaycastBlocking(active);
    }

    private void SetChromeVisible(bool visible)
    {
        if (chromeGroup != null)
        {
            chromeGroup.SetActive(visible);
        }
    }

    private void SetOverlayRaycastBlocking(bool block)
    {
        if (overlayRoot == null)
        {
            return;
        }

        CanvasGroup cg = overlayRoot.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = overlayRoot.AddComponent<CanvasGroup>();
        }

        cg.blocksRaycasts = block;
        cg.interactable = false;
    }

    private void HideVideoDisplay()
    {
        if (introVideoPlayer != null)
        {
            introVideoPlayer.Stop();
        }

        if (overlayVideoPlayer != null)
        {
            overlayVideoPlayer.Stop();
        }
    }

    private void HideOverlay()
    {
        if (overlayRoot != null)
        {
            Destroy(overlayRoot);
            overlayRoot = null;
        }

        introBlocker = null;
        chromeGroup = null;
        overlayVideoPlayer = null;

        if (videoRenderTexture != null)
        {
            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
            videoRenderTexture = null;
        }

        rootCanvas = null;
    }

    private Sprite ResolvePortraitSprite()
    {
        if (defaultPortrait != null)
        {
            return defaultPortrait;
        }

        return Resources.Load<Sprite>(DefaultPortraitResourcesPath);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite s_WhiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (s_WhiteSprite != null)
        {
            return s_WhiteSprite;
        }

        Texture2D tex = Texture2D.whiteTexture;
        s_WhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return s_WhiteSprite;
    }
}
