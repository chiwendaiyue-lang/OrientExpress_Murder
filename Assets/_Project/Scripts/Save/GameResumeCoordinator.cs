using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 读档：在目标场景加载完成后应用存档；并通知 <see cref="OpeningFlowController"/> 跳过自动开场对话。
/// </summary>
public class GameResumeCoordinator : MonoBehaviour
{
    public static GameResumeCoordinator Instance { get; private set; }

    public static SaveGameData PendingResume;

    /// <summary>下一帧 <see cref="OpeningFlowController"/> 消费后清零。</summary>
    public static bool SuppressOpeningFlowOnce;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(GameResumeCoordinator));
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameResumeCoordinator>();
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (PendingResume == null)
        {
            return;
        }

        if (!string.Equals(scene.name, PendingResume.activeSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SaveGameData data = PendingResume;
        PendingResume = null;
        GameSaveService.ApplySnapshotToGame(data, restoreDialogue: true);
        SuppressOpeningFlowOnce = true;
    }
}
