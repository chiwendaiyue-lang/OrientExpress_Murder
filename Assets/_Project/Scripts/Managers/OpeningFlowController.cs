using System.Collections;
using UnityEngine;

public class OpeningFlowController : MonoBehaviour
{
    [SerializeField] private string openingDialogueId = "opening";
    [SerializeField] private bool autoStartOnSceneLoaded = true;
    [SerializeField] private float startDelaySeconds = 0.2f;

    [Header("新游戏开场影片")]
    [Tooltip("进入场景后、主线 opening 对话前播放。Resources 路径不含扩展名，例如 Assets/.../Resources/MOV/open.mp4 → MOV/open")]
    [SerializeField] private string openingIntroVideoResourcePath = "MOV/open";
    [SerializeField] private bool playOpeningIntroVideo = true;

    private bool hasStarted;

    /// <summary>本局是否已播过 <c>MOV/open</c>；场景重载（如从犯罪现场回车厢）不重置。</summary>
    private static bool openingIntroVideoPlayedThisRun;

    public static void ResetOpeningIntroForNewGame()
    {
        openingIntroVideoPlayedThisRun = false;
    }

    void Start()
    {
        if (GameResumeCoordinator.SuppressOpeningFlowOnce)
        {
            GameResumeCoordinator.SuppressOpeningFlowOnce = false;
            hasStarted = true;
            openingIntroVideoPlayedThisRun = true;
            return;
        }

        if (!autoStartOnSceneLoaded)
        {
            return;
        }

        if (!string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            StartCoroutine(PlayPendingDialogueAfterSceneReady());
            return;
        }

        if (hasStarted)
        {
            return;
        }

        StartCoroutine(OpeningFlowRoutine());
    }

    private IEnumerator PlayPendingDialogueAfterSceneReady()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        if (!TryStartPendingDialogue())
        {
            Debug.LogWarning(
                $"OpeningFlowController: 待接对话「{DialogueProgressBridge.PendingDialogueId}」未能启动，将在下一帧重试。");
            yield return null;
            TryStartPendingDialogue();
        }
    }

    private IEnumerator OpeningFlowRoutine()
    {
        if (startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        if (!string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            TryStartPendingDialogue();
            yield break;
        }

        bool shouldPlayIntro = playOpeningIntroVideo
            && !openingIntroVideoPlayedThisRun
            && !string.IsNullOrWhiteSpace(openingIntroVideoResourcePath);
        if (shouldPlayIntro)
        {
            yield return DialogueManager.PlayResourcesVideoFullscreen(openingIntroVideoResourcePath.Trim());
            openingIntroVideoPlayedThisRun = true;
        }

        StartOpeningDialogue();
    }

    public void StartOpeningDialogue()
    {
        if (!string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            TryStartPendingDialogue();
            return;
        }

        if (hasStarted)
        {
            return;
        }

        if (!TryStartDialogue(openingDialogueId))
        {
            return;
        }

        hasStarted = true;
    }

    private static bool TryStartPendingDialogue()
    {
        if (string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            return false;
        }

        string pendingDialogue = DialogueProgressBridge.PendingDialogueId;
        if (!TryStartDialogue(pendingDialogue))
        {
            return false;
        }

        DialogueProgressBridge.PendingDialogueId = null;
        return true;
    }

    private static bool TryStartDialogue(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId))
        {
            return false;
        }

        DialogueManager manager = DialogueManager.EnsureExists();
        if (manager == null)
        {
            Debug.LogWarning("OpeningFlowController: DialogueManager 未就绪，无法开始对话。");
            return false;
        }

        if (manager.IsDialogueUiActive())
        {
            return true;
        }

        manager.StartDialogue(dialogueId);
        if (!manager.IsDialogueUiActive())
        {
            Debug.LogWarning($"OpeningFlowController: 对话「{dialogueId}」未能显示（检查 UI 绑定、JSON 或 Canvas 层级）。");
            return false;
        }

        return true;
    }
}
