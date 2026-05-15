using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景待播对话 id（如离开犯罪现场后接 <c>mr_MacQueen</c>）。
/// </summary>
public static class DialogueProgressBridge
{
    public static string PendingDialogueId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SubscribeSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrEmpty(PendingDialogueId))
        {
            return;
        }

        if (!string.Equals(scene.name, SceneLoader.SCENE_TRAIN_CORRIDOR, System.StringComparison.Ordinal))
        {
            return;
        }

        SceneLoadDialogueBootstrap.EnsureInstance().TryConsumePendingDialogue();
    }
}

/// <summary>
/// 场景加载后兜底启动 <see cref="DialogueProgressBridge.PendingDialogueId"/>（防止 OpeningFlowController 未跑或 Instance 未就绪）。
/// </summary>
public sealed class SceneLoadDialogueBootstrap : MonoBehaviour
{
    private static SceneLoadDialogueBootstrap instance;

    public static SceneLoadDialogueBootstrap EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        SceneLoadDialogueBootstrap existing = Object.FindFirstObjectByType<SceneLoadDialogueBootstrap>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject(nameof(SceneLoadDialogueBootstrap));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneLoadDialogueBootstrap>();
        return instance;
    }

    public void TryConsumePendingDialogue()
    {
        if (string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            return;
        }

        StartCoroutine(ConsumePendingRoutine());
    }

    private IEnumerator ConsumePendingRoutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            yield break;
        }

        string dialogueId = DialogueProgressBridge.PendingDialogueId;
        DialogueManager manager = DialogueManager.EnsureExists();
        if (manager == null)
        {
            Debug.LogWarning("SceneLoadDialogueBootstrap: DialogueManager 未就绪，无法播放待接对话。");
            yield break;
        }

        if (manager.IsDialogueUiActive())
        {
            DialogueProgressBridge.PendingDialogueId = null;
            yield break;
        }

        manager.StartDialogue(dialogueId);
        if (!manager.IsDialogueUiActive())
        {
            Debug.LogWarning($"SceneLoadDialogueBootstrap: 对话「{dialogueId}」未能显示。");
            yield break;
        }

        DialogueProgressBridge.PendingDialogueId = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
