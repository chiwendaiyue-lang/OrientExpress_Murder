using UnityEngine;

/// <summary>
/// 案发包厢 / 近景搜证：先播对话、在 <see cref="DialogueManager"/> 结束关闭 UI 后再发放物证（避免与对话同帧叠状态）。
/// 同时提供对话资源检测与启动，供 <see cref="RatchettCabinSceneController"/>、<see cref="RatchettCloseUpSceneController"/> 共用。
/// </summary>
public static class CrimeSceneEvidenceGrantBridge
{
    public static string PendingEvidenceIdAfterDialogue { get; private set; }

    public static void QueueGrantAfterDialogue(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return;
        }

        PendingEvidenceIdAfterDialogue = evidenceId;
    }

    public static void ClearPending()
    {
        PendingEvidenceIdAfterDialogue = null;
    }

    public static void GrantPendingIfAny()
    {
        if (string.IsNullOrEmpty(PendingEvidenceIdAfterDialogue))
        {
            return;
        }

        string id = PendingEvidenceIdAfterDialogue;
        PendingEvidenceIdAfterDialogue = null;

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (notebookManager != null)
        {
            notebookManager.AddEvidence(id);
        }
        else
        {
            Debug.LogWarning("CrimeSceneEvidenceGrantBridge: DetectiveNotebookManager 不存在，无法记录侦探笔记。");
        }

        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        if (evidenceManager != null)
        {
            evidenceManager.AddClue(id);
        }
        else
        {
            Debug.LogWarning("CrimeSceneEvidenceGrantBridge: EvidenceManager 不存在，无法记录线索。");
        }

        RatchettCabinSceneController cabinUi = UnityEngine.Object.FindFirstObjectByType<RatchettCabinSceneController>(FindObjectsInactive.Include);
        if (cabinUi != null)
        {
            cabinUi.RefreshHintStates();
        }
    }

    public static DialogueManager FindDialogueManager()
    {
        DialogueManager.EnsureExists();
        if (DialogueManager.Instance != null)
        {
            return DialogueManager.Instance;
        }

        return Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
    }

    public static bool DialogueResourceExists(string dialogueId)
    {
        if (string.IsNullOrEmpty(dialogueId))
        {
            return false;
        }

        return Resources.Load<TextAsset>($"Dialogue/{dialogueId}") != null
            || Resources.Load<TextAsset>($"Notebook/{dialogueId}") != null;
    }

    public static bool TryStartCollectDialogue(string dialogueId)
    {
        DialogueManager dm = FindDialogueManager();
        if (dm == null)
        {
            Debug.LogWarning("CrimeSceneEvidenceGrantBridge: 找不到 DialogueManager，无法播放搜证对话。");
            return false;
        }

        dm.StartDialogue(dialogueId);
        if (!dm.IsDialogueUiActive())
        {
            Debug.LogWarning($"CrimeSceneEvidenceGrantBridge: 对话 {dialogueId} 未能启动（检查 UI 绑定或 JSON）。");
            return false;
        }

        return true;
    }
}
