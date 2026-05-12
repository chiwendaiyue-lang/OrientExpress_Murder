using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RatchettCabinSceneController : MonoBehaviour
{
    [SerializeField] private string tableSceneName = SceneLoader.SCENE_RATCHETT_TABLE;
    [SerializeField] private string postInvestigationSceneName = SceneLoader.SCENE_TRAIN_CORRIDOR;
    [SerializeField] private string postInvestigationDialogueId = "mr_MacQueen";
    [SerializeField] private string exhaustedHint = "没有什么值得注意的了";
    [SerializeField] private string cannotLeaveHint = "好像还有什么值得注意的地方";

    [Header("场景热点（Hierarchy 中物体名，需带 Button）")]
    [SerializeField] private string bodyHotspotObjectName = "Hotspot_Body_Button";
    [SerializeField] private string floorHotspotObjectName = "Hotspot_Floor_Button";
    [SerializeField] private string tableHotspotObjectName = "Hotspot_Table_Button";
    [SerializeField] private string daggerHotspotObjectName = "Hotspot_Dagger_Button";
    [SerializeField] private string leaveHotspotObjectName = "Hotspot_Leave_Button";

    private HoverHintTrigger bodyHintTrigger;
    private HoverHintTrigger floorHintTrigger;
    private HoverHintTrigger tableHintTrigger;
    private HoverHintTrigger daggerHintTrigger;

    void Start()
    {
        bodyHintTrigger = AutoBindButton(bodyHotspotObjectName, OnBodyClicked, "点击查看");
        floorHintTrigger = AutoBindButton(floorHotspotObjectName, OnFloorClicked, "点击查看");
        tableHintTrigger = AutoBindButton(tableHotspotObjectName, OnTableClicked, "点击查看");
        daggerHintTrigger = AutoBindButton(daggerHotspotObjectName, OnDaggerClicked, "点击查看");
        AutoBindButton(leaveHotspotObjectName, OnLeaveCabinClicked, "离开房间", false);
        RefreshHintStates();
    }

    public void OnBodyClicked()
    {
        AddEvidence(EvidenceIds.KNIFE_WOUND);
        TryMarkCrimeSceneFinished();
        RefreshHintStates();
    }

    public void OnFloorClicked()
    {
        AddEvidence(EvidenceIds.H_HANDKERCHIEF);
        TryMarkCrimeSceneFinished();
        RefreshHintStates();
    }

    public void OnDaggerClicked()
    {
        AddEvidence(EvidenceIds.DAGGER);
        TryMarkCrimeSceneFinished();
        RefreshHintStates();
    }

    public void OnTableClicked()
    {
        LoadScene(tableSceneName);
    }

    public void OnLeaveCabinClicked()
    {
        if (!IsCrimeSceneFinished())
        {
            if (HoverHintUI.Instance != null)
            {
                HoverHintUI.Instance.ShowTemporaryHint(cannotLeaveHint, 1.6f);
            }
            return;
        }

        LoadScene(postInvestigationSceneName);
    }

    private void AddEvidence(string evidenceId)
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        if (notebookManager != null)
        {
            notebookManager.AddEvidence(evidenceId);
        }
        else
        {
            Debug.LogWarning("RatchettCabinSceneController: DetectiveNotebookManager 不存在，无法记录侦探笔记。");
        }

        if (evidenceManager == null)
        {
            Debug.LogWarning("RatchettCabinSceneController: EvidenceManager 不存在，无法记录证据。");
        }
        else
        {
            evidenceManager.AddClue(evidenceId);
        }
    }

    private void TryMarkCrimeSceneFinished()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        bool done = IsCrimeSceneFinished(notebookManager);

        if (done)
        {
            GameManager.Instance.HasInvestigatedCrimeScene = true;
            Debug.Log("案发包厢证据已收集完毕。");
        }
    }

    private void LoadScene(string sceneName)
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }

        if (!string.IsNullOrEmpty(postInvestigationDialogueId))
        {
            DialogueProgressBridge.PendingDialogueId = postInvestigationDialogueId;
        }
    }

    private HoverHintTrigger AutoBindButton(string buttonName, UnityEngine.Events.UnityAction callback, string hint, bool required = true)
    {
        GameObject buttonObject = GameObject.Find(buttonName);
        if (buttonObject == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCabinSceneController: 找不到按钮 {buttonName}");
            }
            return null;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            if (required)
            {
                Debug.LogWarning($"RatchettCabinSceneController: 对象 {buttonName} 没有 Button 组件");
            }
            return null;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);

        HoverHintTrigger trigger = buttonObject.GetComponent<HoverHintTrigger>();
        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<HoverHintTrigger>();
        }
        trigger.hint = hint;
        return trigger;
    }

    private void RefreshHintStates()
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        bool bodyDone = HasOldClue(evidenceManager, EvidenceIds.KNIFE_WOUND)
            || HasNotebookEvidence(notebookManager, EvidenceIds.KNIFE_WOUND);
        bool floorDone = HasOldClue(evidenceManager, EvidenceIds.H_HANDKERCHIEF)
            || HasNotebookEvidence(notebookManager, EvidenceIds.H_HANDKERCHIEF);
        bool tableDone = HasOldClue(evidenceManager, EvidenceIds.BURNED_PAPER)
            || HasNotebookEvidence(notebookManager, EvidenceIds.BURNED_PAPER);
        bool daggerDone = HasOldClue(evidenceManager, EvidenceIds.DAGGER)
            || HasNotebookEvidence(notebookManager, EvidenceIds.DAGGER);

        if (bodyHintTrigger != null)
        {
            bodyHintTrigger.SetHint(bodyDone ? exhaustedHint : "点击查看");
        }
        if (floorHintTrigger != null)
        {
            floorHintTrigger.SetHint(floorDone ? exhaustedHint : "点击查看");
        }
        if (tableHintTrigger != null)
        {
            tableHintTrigger.SetHint(tableDone ? exhaustedHint : "点击查看");
        }
        if (daggerHintTrigger != null)
        {
            daggerHintTrigger.SetHint(daggerDone ? exhaustedHint : "点击查看");
        }
    }

    private bool HasOldClue(EvidenceManager evidenceManager, string evidenceId)
    {
        return evidenceManager != null && evidenceManager.HasClue(evidenceId);
    }

    private bool HasNotebookEvidence(DetectiveNotebookManager notebookManager, string evidenceId)
    {
        return notebookManager != null && notebookManager.HasEvidence(evidenceId);
    }

    private bool IsCrimeSceneFinished()
    {
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        return IsCrimeSceneFinished(notebookManager);
    }

    private bool IsCrimeSceneFinished(DetectiveNotebookManager notebookManager)
    {
        return notebookManager != null
            && notebookManager.HasEvidence(EvidenceIds.KNIFE_WOUND)
            && notebookManager.HasEvidence(EvidenceIds.H_HANDKERCHIEF)
            && notebookManager.HasEvidence(EvidenceIds.DAGGER)
            && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER);
    }
}
