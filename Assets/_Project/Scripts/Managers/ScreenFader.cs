using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    [SerializeField] private float defaultFadeOutDuration = 0.22f;
    [SerializeField] private float defaultFadeInDuration = 0.22f;
    [SerializeField] private float holdBlackDuration = 0.03f;

    private CanvasGroup fadeCanvasGroup;
    private bool isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    public static ScreenFader EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject go = new GameObject("ScreenFader");
        Instance = go.AddComponent<ScreenFader>();
        DontDestroyOnLoad(go);
        return Instance;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateOverlayIfNeeded();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public static void LoadSceneWithFade(string sceneName)
    {
        bool playTransition = SceneAudioPolicy.ShouldPlayTransitionSfx(sceneName);
        LoadSceneWithFade(sceneName, playTransition);
    }

    public static void LoadSceneWithFade(string sceneName, bool playTransitionSound)
    {
        EnsureInstance().StartTransition(sceneName, playTransitionSound);
    }

    private void StartTransition(string sceneName, bool playTransitionSound)
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName, playTransitionSound));
    }

    private IEnumerator TransitionRoutine(string sceneName, bool playTransitionSound)
    {
        isTransitioning = true;
        CreateOverlayIfNeeded();

        if (playTransitionSound)
        {
            GameAudioManager.EnsureExists();
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.TryPlayTransitionSound();
            }
        }

        yield return FadeTo(1f, defaultFadeOutDuration);
        if (holdBlackDuration > 0f)
        {
            yield return new WaitForSeconds(holdBlackDuration);
        }

        EvidencePanelUI.DisableAllEventSystemComponentsBeforeSceneLoad();
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        EvidencePanelUI.EnsureSingleEventSystem();
        yield return FadeTo(0f, defaultFadeInDuration);
        isTransitioning = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = fadeCanvasGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = Mathf.SmoothStep(0f, 1f, p);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }

    private void CreateOverlayIfNeeded()
    {
        if (fadeCanvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObject = new GameObject("FadeImage", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        fadeCanvasGroup = imageObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.interactable = false;
        fadeCanvasGroup.blocksRaycasts = false;
    }
}
