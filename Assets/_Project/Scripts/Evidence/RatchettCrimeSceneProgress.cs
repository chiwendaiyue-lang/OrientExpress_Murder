public static class RatchettCrimeSceneProgress
{
    /// <summary>
    /// 离开案发包厢：以侦探笔记中是否已收录下列 8 条物证为准（与玩家理解的「集齐线索」一致）。
    /// 刀伤、手帕、通条、烧焦纸条、酒杯、金表、手枪、窗外雪景。
    /// </summary>
    public static bool IsInvestigationComplete(DetectiveNotebookManager notebookManager)
    {
        if (notebookManager == null)
        {
            return false;
        }

        return notebookManager.HasEvidence(EvidenceIds.KNIFE_WOUND)
            && notebookManager.HasEvidence(EvidenceIds.H_HANDKERCHIEF)
            && notebookManager.HasEvidence(EvidenceIds.PIPE_CLEANER)
            && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER)
            && notebookManager.HasEvidence(EvidenceIds.WINE_GLASSES)
            && notebookManager.HasEvidence(EvidenceIds.GOLD_WATCH)
            && notebookManager.HasEvidence(EvidenceIds.PISTOL)
            && notebookManager.HasEvidence(EvidenceIds.SNOW_VIEW);
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
