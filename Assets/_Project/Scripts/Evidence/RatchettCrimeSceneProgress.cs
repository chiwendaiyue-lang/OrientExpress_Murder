public static class RatchettCrimeSceneProgress
{
    public static bool IsInvestigationComplete(DetectiveNotebookManager notebookManager)
    {
        return notebookManager != null
            && notebookManager.HasEvidence(EvidenceIds.KNIFE_WOUND)
            && notebookManager.HasEvidence(EvidenceIds.H_HANDKERCHIEF)
            && notebookManager.HasEvidence(EvidenceIds.PIPE_CLEANER)
            && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER)
            && notebookManager.HasEvidence(EvidenceIds.WINDOW_FRAME)
            && notebookManager.HasEvidence(EvidenceIds.SNOW_NO_FOOTPRINTS)
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
