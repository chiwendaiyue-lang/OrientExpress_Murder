using UnityEngine;

/// <summary>与 Resources/MP3 下各文件一一对应的音量通道（设置里可独立拖动）。</summary>
public enum GameAudioChannel
{
    BgmMainInterface,
    UiClick,
    ClueCollection,
    SceneTransition,
    Perceive
}

/// <summary>
/// 与 <see cref="GameAudioManager"/> 对应的各条 Resources/MP3 音量（0~1），存 PlayerPrefs。
/// </summary>
public static class GameAudioSettings
{
    private const string KeyPrefix = "OrientExpress.Audio.v1.";

    public static string PlayerPrefsKey(GameAudioChannel channel)
    {
        return KeyPrefix + channel;
    }

    public static float LoadVolume(GameAudioChannel channel, float defaultVolume)
    {
        string key = PlayerPrefsKey(channel);
        if (!PlayerPrefs.HasKey(key))
        {
            return Mathf.Clamp01(defaultVolume);
        }

        return Mathf.Clamp01(PlayerPrefs.GetFloat(key));
    }

    public static void SaveVolume(GameAudioChannel channel, float volume01)
    {
        volume01 = Mathf.Clamp01(volume01);
        PlayerPrefs.SetFloat(PlayerPrefsKey(channel), volume01);
        PlayerPrefs.Save();
    }
}
