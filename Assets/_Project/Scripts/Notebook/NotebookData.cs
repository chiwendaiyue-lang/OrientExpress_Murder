using System;
using System.Collections.Generic;

[Serializable]
public class NotebookDatabase
{
    public List<PhysicalEvidenceDefinition> evidence;
    public List<CharacterDefinition> characters;
    public List<TestimonyDefinition> testimonies;
    public List<DoubtDefinition> doubts;
}

[Serializable]
public class CharacterDefinition
{
    public string id;
    public string displayName;
    public string role;
    public string portrait;
    public int sortOrder;
}

[Serializable]
public class PhysicalEvidenceDefinition
{
    public string id;
    public string type;
    public string name;
    public string description;
    public string foundLocation;
    public string icon;
    public List<PhysicalEvidenceUpdate> updates;
}

[Serializable]
public class PhysicalEvidenceUpdate
{
    public string stageId;
    public string name;
    public string description;
    public string foundLocation;
    public string icon;
}

[Serializable]
public class TestimonyDefinition
{
    public string id;
    public string type;
    public string name;
    public string speakerCharacterId;
    public string speakerName;
    public string summary;
    public string originalText;
    public string source;
    public string topic;
    public List<TestimonyUpdate> updates;
}

[Serializable]
public class TestimonyUpdate
{
    public string stageId;
    public string name;
    public string summary;
    public string originalText;
    public string source;
    public string topic;
}

[Serializable]
public class DoubtDefinition
{
    public string id;
    public string type;
    public string name;
    public string question;
    public string description;
    public string finalConclusion;
    public bool resolved;
    public List<DoubtUpdate> updates;
}

[Serializable]
public class DoubtUpdate
{
    public string stageId;
    public string name;
    public string question;
    public string description;
    public string finalConclusion;
    public bool resolved;
}

[Serializable]
public class NotebookItemState
{
    public string itemId;
    public bool unlocked;
    public string currentStageId;
}

[Serializable]
public class NotebookRuntimeState
{
    public List<NotebookItemState> evidenceStates = new List<NotebookItemState>();
    public List<NotebookItemState> testimonyStates = new List<NotebookItemState>();
    public List<NotebookItemState> doubtStates = new List<NotebookItemState>();
}

[Serializable]
public class NotebookRewards
{
    public List<string> evidenceToAdd;
    public List<string> testimonyToAdd;
    public List<string> doubtToAdd;
    public List<NotebookStageUpdate> evidenceStageToUpdate;
    public List<NotebookStageUpdate> testimonyStageToUpdate;
    public List<NotebookStageUpdate> doubtStageToUpdate;
}

[Serializable]
public class NotebookStageUpdate
{
    public string itemId;
    public string stageId;
}

[Serializable]
public class NotebookRequirements
{
    public List<string> requiredEvidenceIds;
    public List<string> requiredTestimonyIds;
    public List<string> requiredDoubtIds;
    public List<string> requiredResolvedDoubtIds;
}
