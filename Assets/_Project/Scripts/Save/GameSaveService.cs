using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 4 槽存档读写与快照构建；当前使用的槽位由 <see cref="ActiveSaveSlot"/> 记录。
/// </summary>
public static class GameSaveService
{
    public const int SlotCount = 4;
    public const int CurrentSaveVersion = 1;

    /// <summary>0–3，-1 表示尚未从主菜单选择槽位（游戏内存档将落到槽 0）。</summary>
    public static int ActiveSaveSlot = -1;

    public static string GetSlotFilePath(int slotIndex)
    {
        int i = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
        return Path.Combine(Application.persistentDataPath, $"save_slot_{i}.json");
    }

    public static bool SlotFileExists(int slotIndex)
    {
        return File.Exists(GetSlotFilePath(slotIndex));
    }

    public static SaveGameData TryLoadSlot(int slotIndex)
    {
        string path = GetSlotFilePath(slotIndex);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonUtility.FromJson<SaveGameData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveService: 读取存档失败 {path}\n{e.Message}");
            return null;
        }
    }

    public static void WriteSlot(int slotIndex, SaveGameData data)
    {
        if (data == null)
        {
            return;
        }

        data.saveVersion = CurrentSaveVersion;
        data.utcTicks = DateTime.UtcNow.Ticks;
        string path = GetSlotFilePath(slotIndex);
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    public static void DeleteSlot(int slotIndex)
    {
        string path = GetSlotFilePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static SaveGameData BuildSnapshotFromCurrentGame()
    {
        SaveGameData data = new SaveGameData
        {
            activeSceneName = SceneManager.GetActiveScene().name,
            dialogueProgressBridgePendingId = DialogueProgressBridge.PendingDialogueId
        };

        if (GameManager.Instance != null)
        {
            data.hasInvestigatedCrimeScene = GameManager.Instance.HasInvestigatedCrimeScene;
            data.hasTalkedToMrsHubbard = GameManager.Instance.HasTalkedToMrsHubbard;
            data.hasTalkedToCountAndrenyi = GameManager.Instance.HasTalkedToCountAndrenyi;
        }

        DetectiveNotebookManager nb = DetectiveNotebookManager.Instance;
        if (nb != null)
        {
            data.notebook = nb.CaptureRuntimeState();
        }

        if (EvidenceManager.Instance != null)
        {
            data.legacyCollectedClues = EvidenceManager.Instance.GetAllClues();
        }

        DialogueManager dm = DialogueManager.Instance;
        if (dm != null && dm.IsDialogueUiActive())
        {
            data.dialogueWasActive = true;
            data.dialogueResourceId = dm.GetCurrentDialogueResourceId();
            data.dialogueNodeId = dm.GetCurrentNodeId();
            NotebookRewards pending = dm.GetPendingDeferredNotebookRewards();
            if (pending != null && NotebookRewardsHasAny(pending))
            {
                data.pendingNotebookRewardsJson = JsonUtility.ToJson(pending);
            }
        }

        return data;
    }

    public static void ApplySnapshotToGame(SaveGameData data, bool restoreDialogue)
    {
        if (data == null)
        {
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HasInvestigatedCrimeScene = data.hasInvestigatedCrimeScene;
            GameManager.Instance.HasTalkedToMrsHubbard = data.hasTalkedToMrsHubbard;
            GameManager.Instance.HasTalkedToCountAndrenyi = data.hasTalkedToCountAndrenyi;
        }

        DetectiveNotebookManager nb = DetectiveNotebookManager.Instance;
        if (nb != null)
        {
            if (data.notebook != null)
            {
                nb.RestoreRuntimeState(data.notebook);
            }
            else
            {
                nb.ResetAllNotebookProgress();
            }
        }

        if (EvidenceManager.Instance != null)
        {
            EvidenceManager.Instance.ReplaceCollectedClues(data.legacyCollectedClues);
        }

        if (!string.IsNullOrEmpty(data.dialogueProgressBridgePendingId))
        {
            DialogueProgressBridge.PendingDialogueId = data.dialogueProgressBridgePendingId;
        }
        else
        {
            DialogueProgressBridge.PendingDialogueId = null;
        }

        if (!restoreDialogue || DialogueManager.Instance == null)
        {
            return;
        }

        if (data.dialogueWasActive
            && !string.IsNullOrEmpty(data.dialogueResourceId)
            && !string.IsNullOrEmpty(data.dialogueNodeId))
        {
            NotebookRewards restoredPending = null;
            if (!string.IsNullOrWhiteSpace(data.pendingNotebookRewardsJson))
            {
                try
                {
                    restoredPending = JsonUtility.FromJson<NotebookRewards>(data.pendingNotebookRewardsJson);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"GameSaveService: 无法解析 pendingNotebookRewardsJson\n{e.Message}");
                }
            }

            DialogueManager.Instance.StartDialogueAtNode(data.dialogueResourceId, data.dialogueNodeId, restoredPending);
        }
    }

    private static bool NotebookRewardsHasAny(NotebookRewards rewards)
    {
        if (rewards == null)
        {
            return false;
        }

        if (rewards.evidenceToAdd != null && rewards.evidenceToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.testimonyToAdd != null && rewards.testimonyToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.doubtToAdd != null && rewards.doubtToAdd.Count > 0)
        {
            return true;
        }

        if (rewards.evidenceStageToUpdate != null && rewards.evidenceStageToUpdate.Count > 0)
        {
            return true;
        }

        if (rewards.testimonyStageToUpdate != null && rewards.testimonyStageToUpdate.Count > 0)
        {
            return true;
        }

        if (rewards.doubtStageToUpdate != null && rewards.doubtStageToUpdate.Count > 0)
        {
            return true;
        }

        return false;
    }

    public static void ResetAllProgressForNewGame()
    {
        DialogueProgressBridge.PendingDialogueId = null;

        if (DetectiveNotebookManager.Instance != null)
        {
            DetectiveNotebookManager.Instance.ResetAllNotebookProgress();
        }

        if (EvidenceManager.Instance != null)
        {
            EvidenceManager.Instance.ReplaceCollectedClues(null);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HasInvestigatedCrimeScene = false;
            GameManager.Instance.HasTalkedToMrsHubbard = false;
            GameManager.Instance.HasTalkedToCountAndrenyi = false;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ForceEndDialogueIfAny();
        }
    }

    public static int GetEffectiveSaveSlotIndex()
    {
        if (ActiveSaveSlot >= 0 && ActiveSaveSlot < SlotCount)
        {
            return ActiveSaveSlot;
        }

        return 0;
    }
}
