using UnityEngine;

public class OpeningFlowController : MonoBehaviour
{
    [SerializeField] private string openingDialogueId = "opening";
    [SerializeField] private bool autoStartOnSceneLoaded = true;
    [SerializeField] private float startDelaySeconds = 0.2f;

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

        Invoke(nameof(StartOpeningDialogue), startDelaySeconds);
    }

    public void StartOpeningDialogue()
    {
        if (hasStarted)
        {
            return;
        }

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
