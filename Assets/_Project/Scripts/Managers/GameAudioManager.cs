using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 全局 BGM / UI 点击 / 证据弹窗 / 场景转场 / 察觉等音效。Resources/MP3 下加载；各通道音量见 <see cref="GameAudioChannel"/> 与 <see cref="GameAudioSettings"/>。
/// </summary>
[DefaultExecutionOrder(5000)]
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    private const string ResourceBgm = "MP3/Background Music_Main Interface";
    private const string ResourceClick = "MP3/Click";
    private const string ResourceClueCollection = "MP3/ClueCollection";
    private const string ResourceTransition = "MP3/Transition";
    private const string ResourcePerceive = "MP3/Perceive";

    private const float DefaultBgmVolume = 0.38f;
    private const float DefaultSfxVolume = 0.85f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private AudioClip clipBgm;
    private AudioClip clipClick;
    private AudioClip clipClueCollection;
    private AudioClip clipTransition;
    private AudioClip clipPerceive;

    /// <summary>与 <see cref="GameAudioChannel"/> 枚举顺序一致，供设置 UI 遍历。</summary>
    public static readonly GameAudioChannel[] AllChannels =
    {
        GameAudioChannel.BgmMainInterface,
        GameAudioChannel.UiClick,
        GameAudioChannel.ClueCollection,
        GameAudioChannel.SceneTransition,
        GameAudioChannel.Perceive
    };

    /// <summary>与 <see cref="GameAudioChannel"/> 与 Resources 路径对应，供 UI 显示名称。</summary>
    public static string GetChannelDisplayName(GameAudioChannel channel)
    {
        switch (channel)
        {
            case GameAudioChannel.BgmMainInterface:
                return "背景音乐（主界面）";
            case GameAudioChannel.UiClick:
                return "界面点击";
            case GameAudioChannel.ClueCollection:
                return "线索收录";
            case GameAudioChannel.SceneTransition:
                return "场景转场";
            case GameAudioChannel.Perceive:
                return "察觉时刻";
            default:
                return channel.ToString();
        }
    }

    private static float DefaultLinearVolume(GameAudioChannel channel)
    {
        return channel == GameAudioChannel.BgmMainInterface ? DefaultBgmVolume : DefaultSfxVolume;
    }

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
        bgmSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = 1f;
        sfxSource.spatialBlend = 0f;

        clipBgm = Resources.Load<AudioClip>(ResourceBgm);
        clipClick = Resources.Load<AudioClip>(ResourceClick);
        clipClueCollection = Resources.Load<AudioClip>(ResourceClueCollection);
        clipTransition = Resources.Load<AudioClip>(ResourceTransition);
        clipPerceive = Resources.Load<AudioClip>(ResourcePerceive);

        if (clipBgm == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourceBgm}（AudioClip）。");
        }
        else
        {
            bgmSource.clip = clipBgm;
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

        if (clipPerceive == null)
        {
            Debug.LogWarning($"GameAudioManager: 未找到 Resources/{ResourcePerceive}。");
        }

        ApplyAllSavedVolumes();
        TryStartBgmIfReady();
    }

    public float GetLinearVolume(GameAudioChannel channel)
    {
        return GameAudioSettings.LoadVolume(channel, DefaultLinearVolume(channel));
    }

    public void SetLinearVolume(GameAudioChannel channel, float volume01)
    {
        volume01 = Mathf.Clamp01(volume01);
        GameAudioSettings.SaveVolume(channel, volume01);
        ApplyChannelVolume(channel);
    }

    private void ApplyAllSavedVolumes()
    {
        foreach (GameAudioChannel ch in AllChannels)
        {
            ApplyChannelVolume(ch);
        }
    }

    private void ApplyChannelVolume(GameAudioChannel channel)
    {
        float v = GetLinearVolume(channel);
        if (channel == GameAudioChannel.BgmMainInterface && bgmSource != null)
        {
            bgmSource.volume = v;
        }
    }

    private void TryStartBgmIfReady()
    {
        if (bgmSource == null || clipBgm == null)
        {
            return;
        }

        if (!bgmSource.isPlaying)
        {
            bgmSource.Play();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Log 音频载入状态（需在 Play 下）")]
    private void EditorLogLoadStatus()
    {
        Debug.Log(
            $"[GameAudioManager] BGM clip={(clipBgm != null ? clipBgm.name : "null")} playing={bgmSource != null && bgmSource.isPlaying} | "
            + $"Click={(clipClick != null)} Clue={(clipClueCollection != null)} Transition={(clipTransition != null)} Perceive={(clipPerceive != null)} | "
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

        sfxSource.PlayOneShot(clipClick, GetLinearVolume(GameAudioChannel.UiClick));
    }

    public void TryPlayClueCollectionSound()
    {
        SuppressUiClickSoundOnThisFrame();
        if (clipClueCollection == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipClueCollection, GetLinearVolume(GameAudioChannel.ClueCollection));
    }

    public void TryPlayTransitionSound()
    {
        SuppressUiClickSoundOnThisFrame();
        if (clipTransition == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipTransition, GetLinearVolume(GameAudioChannel.SceneTransition));
    }

    /// <summary>察觉等剧情用短音效；音量由「察觉时刻」滑条控制。</summary>
    public void TryPlayPerceiveSound()
    {
        if (clipPerceive == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clipPerceive, GetLinearVolume(GameAudioChannel.Perceive));
    }
}
