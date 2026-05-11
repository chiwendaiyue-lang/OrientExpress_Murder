using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvidenceManager : MonoBehaviour
{
    public static EvidenceManager Instance;

    [SerializeField] private List<string> collectedClues = new List<string>();
    private readonly HashSet<string> collectedSet = new HashSet<string>();
    private readonly Dictionary<string, ClueDefinition> clueLookup = new Dictionary<string, ClueDefinition>();
    private readonly List<ClueSynthesisRule> synthesisRules = new List<ClueSynthesisRule>();
    private const string CLUE_DATABASE_PATH = "Evidence/clues";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadClueDatabase();
            RebuildCollectedSet();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static EvidenceManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        EvidenceManager existing = FindObjectOfType<EvidenceManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("EvidenceManager");
        Instance = go.AddComponent<EvidenceManager>();
        return Instance;
    }

    public bool AddClue(string clueId)
    {
        if (string.IsNullOrEmpty(clueId))
        {
            return false;
        }

        if (collectedSet.Contains(clueId))
        {
            return false;
        }

        collectedSet.Add(clueId);
        collectedClues.Add(clueId);
        string clueName = GetClueDisplayName(clueId);
        Debug.Log($"获得线索：{clueName}");
        if (HoverHintUI.Instance != null)
        {
            HoverHintUI.Instance.ShowTemporaryHint($"获得证据：{clueName}", 1.5f);
        }

        TryApplySynthesisRules();
        OnEvidenceUpdated?.Invoke();
        return true;
    }

    public bool RemoveClue(string clueId)
    {
        if (string.IsNullOrEmpty(clueId) || !collectedSet.Contains(clueId))
        {
            return false;
        }

        collectedSet.Remove(clueId);
        collectedClues.Remove(clueId);
        return true;
    }

    public bool HasClue(string clueId)
    {
        return collectedSet.Contains(clueId);
    }

    public List<string> GetAllClues()
    {
        return new List<string>(collectedClues);
    }

    public List<string> GetCluesByType(string type)
    {
        List<string> result = new List<string>();
        foreach (string clueId in collectedClues)
        {
            ClueDefinition clue = GetClueDefinition(clueId);
            string clueType = clue != null && !string.IsNullOrEmpty(clue.type) ? clue.type : ClueTypes.PHYSICAL;
            if (clueType == type)
            {
                result.Add(clueId);
            }
        }

        return result;
    }

    public bool HasAllClues(params string[] clueIds)
    {
        if (clueIds == null || clueIds.Length == 0)
        {
            return false;
        }

        foreach (string clueId in clueIds)
        {
            if (!collectedSet.Contains(clueId))
            {
                return false;
            }
        }

        return true;
    }

    public string GetClueDisplayName(string clueId)
    {
        if (string.IsNullOrEmpty(clueId))
        {
            return clueId;
        }

        // 主数据源：侦探笔记 notebook_database.json（物证 → 证词 → 疑点 顺序回退）
        DetectiveNotebookManager notebook = DetectiveNotebookManager.Instance;
        if (notebook != null)
        {
            PhysicalEvidenceDefinition evidence = notebook.GetCurrentEvidence(clueId);
            if (evidence != null && !string.IsNullOrEmpty(evidence.name))
            {
                return evidence.name;
            }

            TestimonyDefinition testimony = notebook.GetCurrentTestimony(clueId);
            if (testimony != null && !string.IsNullOrEmpty(testimony.name))
            {
                return testimony.name;
            }

            DoubtDefinition doubt = notebook.GetCurrentDoubt(clueId);
            if (doubt != null && !string.IsNullOrEmpty(doubt.name))
            {
                return doubt.name;
            }
        }

        // 兼容旧 Evidence/clues.json（已弃用，文件不存在时直接跳过）
        if (clueLookup.TryGetValue(clueId, out ClueDefinition clue))
        {
            return string.IsNullOrEmpty(clue.name) ? clueId : clue.name;
        }

        return clueId;
    }

    public ClueDefinition GetClueDefinition(string clueId)
    {
        clueLookup.TryGetValue(clueId, out ClueDefinition clue);
        return clue;
    }

    public Sprite GetClueIcon(string clueId)
    {
        // 优先走侦探笔记的统一加载器（路径回退 + id 回退），跟 EvidencePanelUI 保持一致。
        DetectiveNotebookManager notebook = DetectiveNotebookManager.Instance;
        if (notebook != null)
        {
            PhysicalEvidenceDefinition evidence = notebook.GetCurrentEvidence(clueId);
            string iconField = evidence != null ? evidence.icon : null;
            Sprite sprite = EvidencePanelUI.LoadEvidenceIcon(iconField, clueId);
            if (sprite != null)
            {
                return sprite;
            }
        }

        // 兼容旧路径（clues.json 仍存在时）
        ClueDefinition clue = GetClueDefinition(clueId);
        if (clue == null || string.IsNullOrEmpty(clue.icon))
        {
            return null;
        }

        return Resources.Load<Sprite>($"Clues/Icons/{clue.icon}");
    }

    private void RebuildCollectedSet()
    {
        collectedSet.Clear();
        foreach (string clue in collectedClues)
        {
            if (!string.IsNullOrEmpty(clue))
            {
                collectedSet.Add(clue);
            }
        }
    }

    private void LoadClueDatabase()
    {
        clueLookup.Clear();
        TextAsset jsonFile = Resources.Load<TextAsset>(CLUE_DATABASE_PATH);
        if (jsonFile == null)
        {
            // 旧的线索数据库已迁移到 notebook_database.json，文件缺失视为正常。
            return;
        }

        ClueDatabase database = JsonUtility.FromJson<ClueDatabase>(jsonFile.text);
        if (database == null || database.clues == null)
        {
            Debug.LogWarning("EvidenceManager: 线索数据库解析失败或为空。");
            return;
        }

        foreach (ClueDefinition clue in database.clues)
        {
            if (clue == null || string.IsNullOrEmpty(clue.id))
            {
                continue;
            }
            clueLookup[clue.id] = clue;
        }

        synthesisRules.Clear();
        if (database.syntheses != null)
        {
            foreach (ClueSynthesisRule rule in database.syntheses)
            {
                if (rule == null || string.IsNullOrEmpty(rule.outputId) || rule.requiresAll == null || rule.requiresAll.Count == 0)
                {
                    continue;
                }
                synthesisRules.Add(rule);
            }
        }
    }

    private void TryApplySynthesisRules()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (ClueSynthesisRule rule in synthesisRules)
            {
                if (collectedSet.Contains(rule.outputId))
                {
                    continue;
                }

                bool allPresent = true;
                foreach (string requiredId in rule.requiresAll)
                {
                    if (!collectedSet.Contains(requiredId))
                    {
                        allPresent = false;
                        break;
                    }
                }

                if (!allPresent)
                {
                    continue;
                }

                // 合并后删除原线索，只保留新线索 C
                foreach (string requiredId in rule.requiresAll)
                {
                    RemoveClue(requiredId);
                }

                collectedSet.Add(rule.outputId);
                collectedClues.Add(rule.outputId);
                string mergedName = GetClueDisplayName(rule.outputId);
                Debug.Log($"线索自动整理为：{mergedName}");
                if (HoverHintUI.Instance != null)
                {
                    HoverHintUI.Instance.ShowTemporaryHint($"自动整理：{mergedName}", 1.8f);
                }
                changed = true;
                break;
            }
        }
    }

    public System.Action OnEvidenceUpdated;
}

[System.Serializable]
public class ClueDefinition
{
    public string id;
    public string type;
    public string name;
    public string description;
    public string icon;
    public string foundLocation;
}

[System.Serializable]
public class ClueSynthesisRule
{
    public string outputId;
    public List<string> requiresAll;
}

[System.Serializable]
public class ClueDatabase
{
    public List<ClueDefinition> clues;
    public List<ClueSynthesisRule> syntheses;
}

public static class ClueTypes
{
    public const string PHYSICAL = "physical";
    public const string INFERENCE = "inference";
}