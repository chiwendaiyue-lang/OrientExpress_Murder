using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景转场音效规则：从犯罪现场进入桌子 / 窗户 / 尸体二级近景时不播放 Transition。
/// </summary>
public static class SceneAudioPolicy
{
    public static bool ShouldPlayTransitionSfx(string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            return true;
        }

        string from = SceneManager.GetActiveScene().name;
        if (!string.Equals(from, SceneLoader.SCENE_CRIME_SCENE, System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !IsRatchettCloseUpScene(targetSceneName);
    }

    private static bool IsRatchettCloseUpScene(string sceneName)
    {
        return string.Equals(sceneName, SceneLoader.SCENE_RATCHETT_TABLE, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, SceneLoader.SCENE_RATCHETT_WINDOW, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, SceneLoader.SCENE_RATCHETT_BODY, System.StringComparison.OrdinalIgnoreCase);
    }
}
