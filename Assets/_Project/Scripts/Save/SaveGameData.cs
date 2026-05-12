using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单槽存档根对象（JsonUtility 序列化）。
/// </summary>
[Serializable]
public class SaveGameData
{
    public int saveVersion = 1;
    public long utcTicks;
    public string activeSceneName;
    public string dialogueResourceId;
    public string dialogueNodeId;
    public bool dialogueWasActive;
    public string pendingNotebookRewardsJson;
    public NotebookRuntimeState notebook = new NotebookRuntimeState();
    public List<string> legacyCollectedClues = new List<string>();
    public bool hasInvestigatedCrimeScene;
    public bool hasTalkedToMrsHubbard;
    public bool hasTalkedToCountAndrenyi;
    public string dialogueProgressBridgePendingId;
}
