using System;
using System.Collections.Generic;
using UnityEngine;

public enum NotebookRevealKind
{
    Evidence,
    Testimony,
    Doubt
}

public enum NotebookRevealType
{
    NewEntry,
    StageUpdate
}

public struct NotebookRevealEvent
{
    public NotebookRevealKind Kind;
    public string ItemId;
    public NotebookRevealType RevealType;

    public NotebookRevealEvent(NotebookRevealKind kind, string itemId, NotebookRevealType revealType)
    {
        Kind = kind;
        ItemId = itemId;
        RevealType = revealType;
    }
}

public class DetectiveNotebookManager : MonoBehaviour
{
    public static DetectiveNotebookManager Instance;

    private const string DATABASE_PATH = "Notebook/notebook_database";

    private NotebookDatabase database;
    private readonly Dictionary<string, PhysicalEvidenceDefinition> evidenceLookup = new Dictionary<string, PhysicalEvidenceDefinition>();
    private readonly Dictionary<string, CharacterDefinition> characterLookup = new Dictionary<string, CharacterDefinition>();
    private readonly Dictionary<string, TestimonyDefinition> testimonyLookup = new Dictionary<string, TestimonyDefinition>();
    private readonly Dictionary<string, DoubtDefinition> doubtLookup = new Dictionary<string, DoubtDefinition>();

    private readonly Dictionary<string, NotebookItemState> evidenceStates = new Dictionary<string, NotebookItemState>();
    private readonly Dictionary<string, NotebookItemState> testimonyStates = new Dictionary<string, NotebookItemState>();
    private readonly Dictionary<string, NotebookItemState> doubtStates = new Dictionary<string, NotebookItemState>();

    public event Action OnNotebookUpdated;
    public event Action<NotebookRevealEvent> OnNotebookItemRevealed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    public static DetectiveNotebookManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        DetectiveNotebookManager existing = FindObjectOfType<DetectiveNotebookManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("DetectiveNotebookManager");
        Instance = go.AddComponent<DetectiveNotebookManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadDatabase();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool AddEvidence(string itemId)
    {
        bool changed = AddItem(itemId, evidenceLookup, evidenceStates, "物证");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Evidence, itemId, NotebookRevealType.NewEntry);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool AddTestimony(string itemId)
    {
        bool changed = AddItem(itemId, testimonyLookup, testimonyStates, "证词");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Testimony, itemId, NotebookRevealType.NewEntry);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool AddDoubt(string itemId)
    {
        bool changed = AddItem(itemId, doubtLookup, doubtStates, "疑点");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Doubt, itemId, NotebookRevealType.NewEntry);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool UpdateEvidenceStage(string itemId, string stageId)
    {
        bool changed = UpdateItemStage(itemId, stageId, evidenceLookup, evidenceStates, EvidenceStageExists, "物证");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Evidence, itemId, NotebookRevealType.StageUpdate);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool UpdateTestimonyStage(string itemId, string stageId)
    {
        bool changed = UpdateItemStage(itemId, stageId, testimonyLookup, testimonyStates, TestimonyStageExists, "证词");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Testimony, itemId, NotebookRevealType.StageUpdate);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool UpdateDoubtStage(string itemId, string stageId)
    {
        bool changed = UpdateItemStage(itemId, stageId, doubtLookup, doubtStates, DoubtStageExists, "疑点");
        if (changed)
        {
            NotifyReveal(NotebookRevealKind.Doubt, itemId, NotebookRevealType.StageUpdate);
        }

        NotifyIfChanged(changed);
        return changed;
    }

    public bool HasEvidence(string itemId)
    {
        return HasUnlockedState(itemId, evidenceStates);
    }

    public bool HasTestimony(string itemId)
    {
        return HasUnlockedState(itemId, testimonyStates);
    }

    public bool HasDoubt(string itemId)
    {
        return HasUnlockedState(itemId, doubtStates);
    }

    public bool IsDoubtResolved(string itemId)
    {
        if (!HasDoubt(itemId))
        {
            return false;
        }

        DoubtDefinition current = GetCurrentDoubt(itemId);
        return current != null && current.resolved;
    }

    public bool HasAnyUnlockedItem()
    {
        return HasAnyUnlockedState(evidenceStates)
            || HasAnyUnlockedState(testimonyStates)
            || HasAnyUnlockedState(doubtStates);
    }

    public bool HasUnlockedEvidence()
    {
        return HasAnyUnlockedState(evidenceStates);
    }

    public bool HasUnlockedTestimony()
    {
        return HasAnyUnlockedState(testimonyStates);
    }

    public bool HasUnlockedDoubt()
    {
        return HasAnyUnlockedState(doubtStates);
    }

    public void ApplyRewards(NotebookRewards rewards)
    {
        if (rewards == null)
        {
            return;
        }

        bool changed = false;
        changed |= AddEvidenceBatch(rewards.evidenceToAdd);
        changed |= AddTestimonyBatch(rewards.testimonyToAdd);
        changed |= AddDoubtBatch(rewards.doubtToAdd);
        changed |= UpdateEvidenceStagesWithNotify(rewards.evidenceStageToUpdate);
        changed |= UpdateTestimonyStagesWithNotify(rewards.testimonyStageToUpdate);
        changed |= UpdateDoubtStagesWithNotify(rewards.doubtStageToUpdate);
        NotifyIfChanged(changed);
    }

    private void NotifyReveal(NotebookRevealKind kind, string itemId, NotebookRevealType revealType)
    {
        if (OnNotebookItemRevealed == null)
        {
            return;
        }

        OnNotebookItemRevealed.Invoke(new NotebookRevealEvent(kind, itemId, revealType));
    }

    private bool AddEvidenceBatch(List<string> itemIds)
    {
        if (itemIds == null)
        {
            return false;
        }

        bool changed = false;
        foreach (string itemId in itemIds)
        {
            if (AddItem(itemId, evidenceLookup, evidenceStates, "物证"))
            {
                NotifyReveal(NotebookRevealKind.Evidence, itemId, NotebookRevealType.NewEntry);
                changed = true;
            }
        }

        return changed;
    }

    private bool AddTestimonyBatch(List<string> itemIds)
    {
        if (itemIds == null)
        {
            return false;
        }

        bool changed = false;
        foreach (string itemId in itemIds)
        {
            if (AddItem(itemId, testimonyLookup, testimonyStates, "证词"))
            {
                NotifyReveal(NotebookRevealKind.Testimony, itemId, NotebookRevealType.NewEntry);
                changed = true;
            }
        }

        return changed;
    }

    private bool AddDoubtBatch(List<string> itemIds)
    {
        if (itemIds == null)
        {
            return false;
        }

        bool changed = false;
        foreach (string itemId in itemIds)
        {
            if (AddItem(itemId, doubtLookup, doubtStates, "疑点"))
            {
                NotifyReveal(NotebookRevealKind.Doubt, itemId, NotebookRevealType.NewEntry);
                changed = true;
            }
        }

        return changed;
    }

    private bool UpdateEvidenceStagesWithNotify(List<NotebookStageUpdate> updates)
    {
        if (updates == null)
        {
            return false;
        }

        bool changed = false;
        foreach (NotebookStageUpdate update in updates)
        {
            if (update == null)
            {
                continue;
            }

            if (UpdateItemStage(update.itemId, update.stageId, evidenceLookup, evidenceStates, EvidenceStageExists, "物证"))
            {
                NotifyReveal(NotebookRevealKind.Evidence, update.itemId, NotebookRevealType.StageUpdate);
                changed = true;
            }
        }

        return changed;
    }

    private bool UpdateTestimonyStagesWithNotify(List<NotebookStageUpdate> updates)
    {
        if (updates == null)
        {
            return false;
        }

        bool changed = false;
        foreach (NotebookStageUpdate update in updates)
        {
            if (update == null)
            {
                continue;
            }

            if (UpdateItemStage(update.itemId, update.stageId, testimonyLookup, testimonyStates, TestimonyStageExists, "证词"))
            {
                NotifyReveal(NotebookRevealKind.Testimony, update.itemId, NotebookRevealType.StageUpdate);
                changed = true;
            }
        }

        return changed;
    }

    private bool UpdateDoubtStagesWithNotify(List<NotebookStageUpdate> updates)
    {
        if (updates == null)
        {
            return false;
        }

        bool changed = false;
        foreach (NotebookStageUpdate update in updates)
        {
            if (update == null)
            {
                continue;
            }

            if (UpdateItemStage(update.itemId, update.stageId, doubtLookup, doubtStates, DoubtStageExists, "疑点"))
            {
                NotifyReveal(NotebookRevealKind.Doubt, update.itemId, NotebookRevealType.StageUpdate);
                changed = true;
            }
        }

        return changed;
    }

    public bool MeetsRequirements(NotebookRequirements requirements)
    {
        if (requirements == null)
        {
            return true;
        }

        return HasAll(requirements.requiredEvidenceIds, HasEvidence)
            && HasAll(requirements.requiredTestimonyIds, HasTestimony)
            && HasAll(requirements.requiredDoubtIds, HasDoubt)
            && HasAll(requirements.requiredResolvedDoubtIds, IsDoubtResolved);
    }

    public List<string> GetUnlockedEvidenceIds()
    {
        return GetUnlockedIds(database != null ? database.evidence : null, evidenceStates);
    }

    public List<string> GetUnlockedTestimonyIds()
    {
        return GetUnlockedIds(database != null ? database.testimonies : null, testimonyStates);
    }

    public List<string> GetUnlockedDoubtIds()
    {
        return GetUnlockedIds(database != null ? database.doubts : null, doubtStates);
    }

    public PhysicalEvidenceDefinition GetCurrentEvidence(string itemId)
    {
        PhysicalEvidenceDefinition definition;
        if (!evidenceLookup.TryGetValue(itemId, out definition))
        {
            return null;
        }

        PhysicalEvidenceDefinition current = CopyEvidence(definition);
        NotebookItemState state;
        if (!evidenceStates.TryGetValue(itemId, out state) || string.IsNullOrEmpty(state.currentStageId) || definition.updates == null)
        {
            return current;
        }

        PhysicalEvidenceUpdate update = definition.updates.Find(item => item != null && item.stageId == state.currentStageId);
        if (update == null)
        {
            return current;
        }

        ApplyEvidenceUpdate(current, update);
        return current;
    }

    public TestimonyDefinition GetCurrentTestimony(string itemId)
    {
        TestimonyDefinition definition;
        if (!testimonyLookup.TryGetValue(itemId, out definition))
        {
            return null;
        }

        TestimonyDefinition current = CopyTestimony(definition);
        NotebookItemState state;
        if (!testimonyStates.TryGetValue(itemId, out state) || string.IsNullOrEmpty(state.currentStageId) || definition.updates == null)
        {
            return current;
        }

        TestimonyUpdate update = definition.updates.Find(item => item != null && item.stageId == state.currentStageId);
        if (update == null)
        {
            return current;
        }

        ApplyTestimonyUpdate(current, update);
        return current;
    }

    public CharacterDefinition GetCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return null;
        }

        CharacterDefinition definition;
        return characterLookup.TryGetValue(characterId, out definition) ? definition : null;
    }

    public List<CharacterDefinition> GetAllCharacters()
    {
        List<CharacterDefinition> result = new List<CharacterDefinition>();
        if (database == null || database.characters == null)
        {
            return result;
        }

        foreach (CharacterDefinition character in database.characters)
        {
            if (character != null && !string.IsNullOrEmpty(character.id))
            {
                result.Add(character);
            }
        }

        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    public DoubtDefinition GetCurrentDoubt(string itemId)
    {
        DoubtDefinition definition;
        if (!doubtLookup.TryGetValue(itemId, out definition))
        {
            return null;
        }

        DoubtDefinition current = CopyDoubt(definition);
        NotebookItemState state;
        if (!doubtStates.TryGetValue(itemId, out state) || string.IsNullOrEmpty(state.currentStageId) || definition.updates == null)
        {
            return current;
        }

        DoubtUpdate update = definition.updates.Find(item => item != null && item.stageId == state.currentStageId);
        if (update == null)
        {
            return current;
        }

        ApplyDoubtUpdate(current, update);
        return current;
    }

    private void LoadDatabase()
    {
        evidenceLookup.Clear();
        characterLookup.Clear();
        testimonyLookup.Clear();
        doubtLookup.Clear();

        TextAsset jsonFile = Resources.Load<TextAsset>(DATABASE_PATH);
        if (jsonFile == null)
        {
            Debug.LogWarning($"DetectiveNotebookManager: 找不到数据库 Resources/{DATABASE_PATH}.json");
            database = new NotebookDatabase();
            return;
        }

        database = JsonUtility.FromJson<NotebookDatabase>(jsonFile.text);
        if (database == null)
        {
            Debug.LogWarning("DetectiveNotebookManager: 数据库解析失败。");
            database = new NotebookDatabase();
            return;
        }

        BuildEvidenceLookup();
        BuildCharacterLookup();
        BuildTestimonyLookup();
        BuildDoubtLookup();
    }

    private void BuildEvidenceLookup()
    {
        if (database.evidence == null)
        {
            return;
        }

        foreach (PhysicalEvidenceDefinition item in database.evidence)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                continue;
            }
            evidenceLookup[item.id] = item;
        }
    }

    private void BuildTestimonyLookup()
    {
        if (database.testimonies == null)
        {
            return;
        }

        foreach (TestimonyDefinition item in database.testimonies)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                continue;
            }
            testimonyLookup[item.id] = item;
        }
    }

    private void BuildCharacterLookup()
    {
        if (database.characters == null)
        {
            return;
        }

        foreach (CharacterDefinition item in database.characters)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                continue;
            }
            characterLookup[item.id] = item;
        }
    }

    private void BuildDoubtLookup()
    {
        if (database.doubts == null)
        {
            return;
        }

        foreach (DoubtDefinition item in database.doubts)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                continue;
            }
            doubtLookup[item.id] = item;
        }
    }

    private static bool AddItem<TDefinition>(
        string itemId,
        Dictionary<string, TDefinition> lookup,
        Dictionary<string, NotebookItemState> states,
        string label)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if (!lookup.ContainsKey(itemId))
        {
            Debug.LogWarning($"DetectiveNotebookManager: 未找到{label}定义 {itemId}");
            return false;
        }

        NotebookItemState state = GetOrCreateState(itemId, states);
        if (state.unlocked)
        {
            return false;
        }

        state.unlocked = true;
        Debug.Log($"获得{label}：{itemId}");
        return true;
    }

    private static bool UpdateItemStage<TDefinition>(
        string itemId,
        string stageId,
        Dictionary<string, TDefinition> lookup,
        Dictionary<string, NotebookItemState> states,
        Func<TDefinition, string, bool> stageExists,
        string label)
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(stageId))
        {
            return false;
        }

        TDefinition definition;
        if (!lookup.TryGetValue(itemId, out definition))
        {
            Debug.LogWarning($"DetectiveNotebookManager: 未找到{label}定义 {itemId}");
            return false;
        }

        if (stageExists != null && !stageExists(definition, stageId))
        {
            Debug.LogWarning($"DetectiveNotebookManager: {label} {itemId} 不存在更新阶段 {stageId}");
            return false;
        }

        NotebookItemState state = GetOrCreateState(itemId, states);
        if (state.unlocked && state.currentStageId == stageId)
        {
            return false;
        }

        state.unlocked = true;
        state.currentStageId = stageId;
        Debug.Log($"{label}更新：{itemId}/{stageId}");
        return true;
    }

    private static NotebookItemState GetOrCreateState(string itemId, Dictionary<string, NotebookItemState> states)
    {
        NotebookItemState state;
        if (states.TryGetValue(itemId, out state))
        {
            return state;
        }

        state = new NotebookItemState
        {
            itemId = itemId,
            unlocked = false,
            currentStageId = string.Empty
        };
        states[itemId] = state;
        return state;
    }

    private static bool HasUnlockedState(string itemId, Dictionary<string, NotebookItemState> states)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        NotebookItemState state;
        return states.TryGetValue(itemId, out state) && state.unlocked;
    }

    private static bool HasAnyUnlockedState(Dictionary<string, NotebookItemState> states)
    {
        foreach (NotebookItemState state in states.Values)
        {
            if (state != null && state.unlocked)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAll(List<string> itemIds, Func<string, bool> predicate)
    {
        if (itemIds == null || itemIds.Count == 0)
        {
            return true;
        }

        foreach (string itemId in itemIds)
        {
            if (!predicate(itemId))
            {
                return false;
            }
        }
        return true;
    }

    private static List<string> GetUnlockedIds<TDefinition>(List<TDefinition> definitions, Dictionary<string, NotebookItemState> states)
        where TDefinition : class
    {
        List<string> result = new List<string>();
        if (definitions == null)
        {
            return result;
        }

        foreach (TDefinition definition in definitions)
        {
            string id = GetDefinitionId(definition);
            if (HasUnlockedState(id, states))
            {
                result.Add(id);
            }
        }
        return result;
    }

    private static string GetDefinitionId<TDefinition>(TDefinition definition)
        where TDefinition : class
    {
        PhysicalEvidenceDefinition evidence = definition as PhysicalEvidenceDefinition;
        if (evidence != null)
        {
            return evidence.id;
        }

        TestimonyDefinition testimony = definition as TestimonyDefinition;
        if (testimony != null)
        {
            return testimony.id;
        }

        DoubtDefinition doubt = definition as DoubtDefinition;
        return doubt != null ? doubt.id : string.Empty;
    }

    private static PhysicalEvidenceDefinition CopyEvidence(PhysicalEvidenceDefinition source)
    {
        return new PhysicalEvidenceDefinition
        {
            id = source.id,
            type = source.type,
            name = source.name,
            description = source.description,
            unlockBrief = source.unlockBrief,
            foundLocation = source.foundLocation,
            icon = source.icon,
            updates = source.updates
        };
    }

    private static TestimonyDefinition CopyTestimony(TestimonyDefinition source)
    {
        return new TestimonyDefinition
        {
            id = source.id,
            type = source.type,
            name = source.name,
            isConfirmed = source.isConfirmed,
            unlockBrief = source.unlockBrief,
            speakerCharacterId = source.speakerCharacterId,
            speakerName = source.speakerName,
            summary = source.summary,
            originalText = source.originalText,
            source = source.source,
            topic = source.topic,
            updates = source.updates
        };
    }

    private static DoubtDefinition CopyDoubt(DoubtDefinition source)
    {
        return new DoubtDefinition
        {
            id = source.id,
            type = source.type,
            name = source.name,
            unlockBrief = source.unlockBrief,
            question = source.question,
            description = source.description,
            finalConclusion = source.finalConclusion,
            resolved = source.resolved,
            doubtInquiryLineScriptResourcePath = source.doubtInquiryLineScriptResourcePath,
            updates = source.updates
        };
    }

    private static void ApplyEvidenceUpdate(PhysicalEvidenceDefinition target, PhysicalEvidenceUpdate update)
    {
        OverrideIfNotEmpty(ref target.name, update.name);
        OverrideIfNotEmpty(ref target.description, update.description);
        OverrideIfNotEmpty(ref target.unlockBrief, update.unlockBrief);
        OverrideIfNotEmpty(ref target.foundLocation, update.foundLocation);
        OverrideIfNotEmpty(ref target.icon, update.icon);
    }

    private static void ApplyTestimonyUpdate(TestimonyDefinition target, TestimonyUpdate update)
    {
        OverrideIfNotEmpty(ref target.name, update.name);
        OverrideIfNotEmpty(ref target.unlockBrief, update.unlockBrief);
        OverrideIfNotEmpty(ref target.summary, update.summary);
        OverrideIfNotEmpty(ref target.originalText, update.originalText);
        OverrideIfNotEmpty(ref target.source, update.source);
        OverrideIfNotEmpty(ref target.topic, update.topic);
    }

    private static void ApplyDoubtUpdate(DoubtDefinition target, DoubtUpdate update)
    {
        OverrideIfNotEmpty(ref target.name, update.name);
        OverrideIfNotEmpty(ref target.unlockBrief, update.unlockBrief);
        OverrideIfNotEmpty(ref target.question, update.question);
        OverrideIfNotEmpty(ref target.description, update.description);
        OverrideIfNotEmpty(ref target.finalConclusion, update.finalConclusion);
        if (update.resolved || !string.IsNullOrEmpty(update.finalConclusion))
        {
            target.resolved = true;
        }
    }

    private static bool EvidenceStageExists(PhysicalEvidenceDefinition definition, string stageId)
    {
        return definition != null
            && definition.updates != null
            && definition.updates.Exists(item => item != null && item.stageId == stageId);
    }

    private static bool TestimonyStageExists(TestimonyDefinition definition, string stageId)
    {
        return definition != null
            && definition.updates != null
            && definition.updates.Exists(item => item != null && item.stageId == stageId);
    }

    private static bool DoubtStageExists(DoubtDefinition definition, string stageId)
    {
        return definition != null
            && definition.updates != null
            && definition.updates.Exists(item => item != null && item.stageId == stageId);
    }

    private static void OverrideIfNotEmpty(ref string target, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            target = value;
        }
    }

    private void NotifyIfChanged(bool changed)
    {
        if (changed && OnNotebookUpdated != null)
        {
            OnNotebookUpdated.Invoke();
        }
    }

    public NotebookRuntimeState CaptureRuntimeState()
    {
        NotebookRuntimeState snapshot = new NotebookRuntimeState();
        CopyStatesToList(evidenceStates, snapshot.evidenceStates);
        CopyStatesToList(testimonyStates, snapshot.testimonyStates);
        CopyStatesToList(doubtStates, snapshot.doubtStates);
        return snapshot;
    }

    public void RestoreRuntimeState(NotebookRuntimeState snapshot)
    {
        if (snapshot == null)
        {
            ResetAllNotebookProgress();
            return;
        }

        evidenceStates.Clear();
        testimonyStates.Clear();
        doubtStates.Clear();

        RestoreStatesFromList(snapshot.evidenceStates, evidenceStates);
        RestoreStatesFromList(snapshot.testimonyStates, testimonyStates);
        RestoreStatesFromList(snapshot.doubtStates, doubtStates);

        OnNotebookUpdated?.Invoke();
    }

    public void ResetAllNotebookProgress()
    {
        evidenceStates.Clear();
        testimonyStates.Clear();
        doubtStates.Clear();
        OnNotebookUpdated?.Invoke();
    }

    private static void CopyStatesToList(
        Dictionary<string, NotebookItemState> source,
        List<NotebookItemState> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        foreach (KeyValuePair<string, NotebookItemState> pair in source)
        {
            if (pair.Value == null || !pair.Value.unlocked)
            {
                continue;
            }

            destination.Add(new NotebookItemState
            {
                itemId = pair.Value.itemId,
                unlocked = pair.Value.unlocked,
                currentStageId = pair.Value.currentStageId ?? string.Empty
            });
        }
    }

    private static void RestoreStatesFromList(
        List<NotebookItemState> source,
        Dictionary<string, NotebookItemState> destination)
    {
        if (source == null)
        {
            return;
        }

        foreach (NotebookItemState entry in source)
        {
            if (entry == null || string.IsNullOrEmpty(entry.itemId) || !entry.unlocked)
            {
                continue;
            }

            NotebookItemState state = GetOrCreateState(entry.itemId, destination);
            state.unlocked = true;
            state.currentStageId = entry.currentStageId ?? string.Empty;
        }
    }
}
