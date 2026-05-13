using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RatchettCabinSceneController : MonoBehaviour
{
    [SerializeField] private string tableSceneName = SceneLoader.SCENE_RATCHETT_TABLE;
    [SerializeField] private string windowSceneName = SceneLoader.SCENE_RATCHETT_WINDOW;
    [SerializeField] private string bodySceneName = SceneLoader.SCENE_RATCHETT_BODY;
    [SerializeField] private string postInvestigationSceneName = SceneLoader.SCENE_TRAIN_CORRIDOR;
    [SerializeField] private string postInvestigationDialogueId = "mr_MacQueen";
    [SerializeField] private string exhaustedHint = "没有什么值得注意的了";
    [SerializeField] private string cannotLeaveHint = "好像还有什么值得注意的地方";

    [Header("场景热点（Hierarchy 中物体名，需带 Button）")]
    [SerializeField] private string bodyKnifeWoundHotspotObjectName = "Hotspot_Body1_Button";
    [SerializeField] private string bodyCloseUpHotspotObjectName = "Hotspot_Body2_Button";
    [SerializeField] private string windowHotspotObjectName = "Hotspot_Window_Button";
    [SerializeField] private string floorHotspotObjectName = "Hotspot_Floor_Button";
    [SerializeField] private string tableHotspotObjectName = "Hotspot_Table_Button";
    [SerializeField] private string pipeCleanerHotspotObjectName = "Hotspot_Dagger_Button";
    [SerializeField] private string leaveHotspotObjectName = "Hotspot_Leave_Button";

    [Header("二级近景悬停")]
    [SerializeField] private string tableCloseUpHint = "凑近查看桌子";
    [SerializeField] private string bodyCloseUpHint = "凑近查看尸体";
    [SerializeField] private string windowCloseUpHint = "凑近查看窗户";

    private HoverHintTrigger bodyKnifeWoundHintTrigger;
    private HoverHintTrigger bodyCloseUpHintTrigger;
    private HoverHintTrigger windowHintTrigger;
    private HoverHintTrigger floorHintTrigger;
    private HoverHintTrigger tableHintTrigger;
    private HoverHintTrigger pipeCleanerHintTrigger;

    void Start()
    {
        bodyKnifeWoundHintTrigger = AutoBindButton(bodyKnifeWoundHotspotObjectName, OnBodyKnifeWoundClicked, "点击查看");
        bodyCloseUpHintTrigger = AutoBindButton(bodyCloseUpHotspotObjectName, OnBodyCloseUpClicked, bodyCloseUpHint);
        windowHintTrigger = AutoBindButton(windowHotspotObjectName, OnWindowClicked, windowCloseUpHint, false);
        floorHintTrigger = AutoBindButton(floorHotspotObjectName, OnFloorClicked, "点击查看");
        tableHintTrigger = AutoBindButton(tableHotspotObjectName, OnTableClicked, tableCloseUpHint);
        pipeCleanerHintTrigger = AutoBindButton(pipeCleanerHotspotObjectName, OnPipeCleanerClicked, "点击查看");
        AutoBindButton(leaveHotspotObjectName, OnLeaveCabinClicked, "离开房间", false);
        RefreshHintStates();
    }

    public void OnBodyKnifeWoundClicked()
    {
        AddEvidence(EvidenceIds.KNIFE_WOUND);
        RatchettCrimeSceneProgress.TryMarkFinished();
        RefreshHintStates();
    }

    public void OnBodyCloseUpClicked()
    {
        LoadScene(bodySceneName);
    }

    public void OnWindowClicked()
    {
        LoadScene(windowSceneName);
    }

    public void OnFloorClicked()
    {
        AddEvidence(EvidenceIds.H_HANDKERCHIEF);
        RatchettCrimeSceneProgress.TryMarkFinished();
        RefreshHintStates();
    }

    public void OnPipeCleanerClicked()
    {
        AddEvidence(EvidenceIds.PIPE_CLEANER);
        RatchettCrimeSceneProgress.TryMarkFinished();
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

        if (!string.IsNullOrEmpty(postInvestigationDialogueId))
        {
            DialogueProgressBridge.PendingDialogueId = postInvestigationDialogueId;
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

        bool bodyKnifeWoundDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.KNIFE_WOUND);
        bool bodyCloseUpDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.GOLD_WATCH)
            && HasEvidence(evidenceManager, notebookManager, EvidenceIds.PISTOL);
        bool windowDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.WINDOW_FRAME)
            && HasEvidence(evidenceManager, notebookManager, EvidenceIds.SNOW_NO_FOOTPRINTS);
        bool floorDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.H_HANDKERCHIEF);
        bool tableDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.BURNED_PAPER);
        bool pipeCleanerDone = HasEvidence(evidenceManager, notebookManager, EvidenceIds.PIPE_CLEANER);

        if (bodyKnifeWoundHintTrigger != null)
        {
            bodyKnifeWoundHintTrigger.SetHint(bodyKnifeWoundDone ? exhaustedHint : "点击查看");
        }

        if (bodyCloseUpHintTrigger != null)
        {
            bodyCloseUpHintTrigger.SetHint(bodyCloseUpDone ? exhaustedHint : bodyCloseUpHint);
        }

        if (windowHintTrigger != null)
        {
            windowHintTrigger.SetHint(windowDone ? exhaustedHint : windowCloseUpHint);
        }

        if (floorHintTrigger != null)
        {
            floorHintTrigger.SetHint(floorDone ? exhaustedHint : "点击查看");
        }

        if (tableHintTrigger != null)
        {
            tableHintTrigger.SetHint(tableDone ? exhaustedHint : tableCloseUpHint);
        }

        if (pipeCleanerHintTrigger != null)
        {
            pipeCleanerHintTrigger.SetHint(pipeCleanerDone ? exhaustedHint : "点击查看");
        }
    }

    private static bool HasEvidence(EvidenceManager evidenceManager, DetectiveNotebookManager notebookManager, string evidenceId)
    {
        return (evidenceManager != null && evidenceManager.HasClue(evidenceId))
            || (notebookManager != null && notebookManager.HasEvidence(evidenceId));
    }

    private bool IsCrimeSceneFinished()
    {
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        return RatchettCrimeSceneProgress.IsInvestigationComplete(notebookManager);
    }
}
