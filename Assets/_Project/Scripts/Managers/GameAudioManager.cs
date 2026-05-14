using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 全局 BGM / UI 点击 / 证据弹窗 / 场景转场音效。Resources/MP3 下加载，不额外挂 AudioListener。
/// </summary>
[DefaultExecutionOrder(5000)]
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    private const string ResourceBgm = "MP3/Background Music_Main Interface";
    private const string ResourceClick = "MP3/Click";
    private const string ResourceClueCollection = "MP3/ClueCollection";
    private const string ResourceTransition = "MP3/Transition";

    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.38f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.85f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private AudioClip clipBgm;
    private AudioClip clipClick;
    private AudioClip clipClueCollection;
    private AudioClip clipTransition;

    /// <summary>与「证据收录」「转场」音效发生在同一帧的 UI 左键，不叠播通用点击音。</summary>
    private int suppressUiClickSoundOnFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static GameAudioManager EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameAudioManager existing = FindFirstObjectByType<GameAudioManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject(nameof(GameAudioManager));
        DontDestroyOnLoad(go);
        return go.AddComponent<GameAudioManager>();
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

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;
        // 资源若按「3D 音效」导入，而本物体在 (0,0,0)、监听器在摄像机上，距离衰减会导致几乎听不到。
        bgmSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
        sfxSource.spatialBlend = 0f;

        clipBgm = Resources.Load<AudioClip>(ResourceBgm);
        clipClick = Resources.Load<AudioClip>(ResourceClick);
        clipClueCollection = Resources.Load<AudioClip>(ResourceClueCollection);
        clipTransition = Resources.Load<AudioClip>(ResourceTransition);

        if (clipBgm == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourceBgm}（AudioClip）。");
        }
        else
        {
            bgmSource.clip = clipBgm;
            if (!bgmSource.isPlaying)
            {
                bgmSource.Play();
            }
        }

        if (clipClick == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourceClick}。");
        }

        if (clipClueCollection == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourceClueCollection}。");
        }

        if (clipTransition == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourceTransition}。");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Log 音频载入状态（需在 Play 下）")]
    private void EditorLogLoadStatus()
    {
        Debug.Log(
            $"[GameAudioManager] BGM clip={(clipBgm != null ? clipBgm.name : "null")} playing={bgmSource != null && bgmSource.isPlaying} | "
            + $"Click={(clipClick != null)} Clue={(clipClueCollection != null)} Transition={(clipTransition != null)} | "
            + $"spatialBlend bgm={bgmSource?.spatialBlend} sfx={sfxSource?.spatialBlend}");
    }
#endif

    private void LateUpdate()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(-1))
        {
            return;
        }

        if (Time.frameCount == suppressUiClickSoundOnFrame)
        {
            return;
        }

        TryPlayUiClickSound();
    }

    /// <summary>与「证据弹窗」「转场」同一次点击：在播放这些音效前调用，避免叠一层点击音。</summary>
    public void SuppressUiClickSoundOnThisFrame()
    {
        suppressUiClickSoundOnFrame = Time.frameCount;
    }

    public void TryPlayUiClickSound()
    {
        if (clipClick == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipClick);
    }

    public void TryPlayClueCollectionSound()
    {
        SuppressUiClickSoundOnThisFrame();
        if (clipClueCollection == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipClueCollection);
    }

    public void TryPlayTransitionSound()
    {
        SuppressUiClickSoundOnThisFrame();
        if (clipTransition == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipTransition);
    }
}
