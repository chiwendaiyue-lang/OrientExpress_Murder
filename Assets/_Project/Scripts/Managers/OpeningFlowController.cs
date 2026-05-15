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

    void Start()
    {
        if (GameResumeCoordinator.SuppressOpeningFlowOnce)
        {
            GameResumeCoordinator.SuppressOpeningFlowOnce = false;
            hasStarted = true;
            return;
        }

        if (!autoStartOnSceneLoaded || hasStarted)
        {
            return;
        }

        StartCoroutine(OpeningFlowRoutine());
    }

    private IEnumerator OpeningFlowRoutine()
    {
        if (startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        if (playOpeningIntroVideo && !string.IsNullOrWhiteSpace(openingIntroVideoResourcePath))
        {
            yield return DialogueManager.PlayResourcesVideoFullscreen(openingIntroVideoResourcePath.Trim());
        }

        StartOpeningDialogue();
    }

    public void StartOpeningDialogue()
    {
        if (hasStarted)
        {
            return;
        }

        DialogueManager.EnsureExists();
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("OpeningFlowController: DialogueManager 未就绪，无法开始 opening 对话。");
            return;
        }

        hasStarted = true;
        if (!string.IsNullOrEmpty(DialogueProgressBridge.PendingDialogueId))
        {
            string pendingDialogue = DialogueProgressBridge.PendingDialogueId;
            DialogueProgressBridge.PendingDialogueId = null;
            DialogueManager.Instance.StartDialogue(pendingDialogue);
            return;
        }

        DialogueManager.Instance.StartDialogue(openingDialogueId);
    }
}
