public static class RatchettCrimeSceneProgress
{
    /// <summary>
    /// 窗户支线：旧流程为两物证；新流程为两段 Notebook 对话（不记入物证），由 <see cref="GameManager"/> 标记。
    /// 仍兼容已解锁两物证的存档。
    /// </summary>
    public static bool IsWindowBranchSatisfied(DetectiveNotebookManager notebookManager)
    {
        if (notebookManager != null
            && notebookManager.HasEvidence(EvidenceIds.WINDOW_FRAME)
            && notebookManager.HasEvidence(EvidenceIds.SNOW_NO_FOOTPRINTS))
        {
            return true;
        }

        GameManager gm = GameManager.Instance;
        return gm != null
            && gm.RatchettWindowFrameDialogueDone
            && gm.RatchettWindowSnowDialogueDone;
    }

    public static bool IsInvestigationComplete(DetectiveNotebookManager notebookManager)
    {
        return notebookManager != null
            && notebookManager.HasEvidence(EvidenceIds.KNIFE_WOUND)
            && notebookManager.HasEvidence(EvidenceIds.H_HANDKERCHIEF)
            && notebookManager.HasEvidence(EvidenceIds.PIPE_CLEANER)
            && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER)
            && IsWindowBranchSatisfied(notebookManager)
            && notebookManager.HasEvidence(EvidenceIds.GOLD_WATCH)
            && notebookManager.HasEvidence(EvidenceIds.PISTOL);
    }

    public static void TryMarkFinished()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (!IsInvestigationComplete(notebookManager))
        {
            return;
        }

        GameManager.Instance.HasInvestigatedCrimeScene = true;
        UnityEngine.Debug.Log("案发包厢证据已收集完毕。");
    }
}
