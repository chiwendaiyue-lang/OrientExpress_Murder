using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RatchettTableSceneController : MonoBehaviour
{
    [SerializeField] private string cabinSceneName = SceneLoader.SCENE_CRIME_SCENE;
    [SerializeField] private string exhaustedHint = "没有什么值得注意的了";
    private HoverHintTrigger burnedPaperHintTrigger;

    void Start()
    {
        burnedPaperHintTrigger = AutoBindButton("Hotspot_BurnedPaper_Button", OnBurnedPaperClicked, "点击查看");
        AutoBindButton("Hotspot_Back_Button", OnBackToCabinClicked, "返回包厢");
        RefreshHintState();
    }

    public void OnBurnedPaperClicked()
    {
        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        if (notebookManager == null)
        {
            Debug.LogWarning("RatchettTableSceneController: DetectiveNotebookManager 不存在，无法记录侦探笔记。");
        }
        else
        {
            notebookManager.AddEvidence(EvidenceIds.BURNED_PAPER);
        }

        if (evidenceManager == null)
        {
            Debug.LogWarning("RatchettTableSceneController: EvidenceManager 不存在，无法记录证据。");
        }
        else
        {
            evidenceManager.AddClue(EvidenceIds.BURNED_PAPER);
        }

        TryMarkCrimeSceneFinished();
        RefreshHintState();
    }

    public void OnBackToCabinClicked()
    {
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(cabinSceneName);
        }
        else
        {
            SceneManager.LoadScene(cabinSceneName);
        }
    }

    private void TryMarkCrimeSceneFinished()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();

        bool done = notebookManager != null
            && notebookManager.HasEvidence(EvidenceIds.KNIFE_WOUND)
            && notebookManager.HasEvidence(EvidenceIds.H_HANDKERCHIEF)
            && notebookManager.HasEvidence(EvidenceIds.DAGGER)
            && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER);

        if (done)
        {
            GameManager.Instance.HasInvestigatedCrimeScene = true;
            Debug.Log("案发包厢证据已收集完毕。");
        }
    }

    private HoverHintTrigger AutoBindButton(string buttonName, UnityEngine.Events.UnityAction callback, string hint)
    {
        GameObject buttonObject = GameObject.Find(buttonName);
        if (buttonObject == null)
        {
            Debug.LogWarning($"RatchettTableSceneController: 找不到按钮 {buttonName}");
            return null;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"RatchettTableSceneController: 对象 {buttonName} 没有 Button 组件");
            return null;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);

        if (buttonName == "Hotspot_Back_Button")
        {
            ConfigureBackButton(buttonObject);
        }

        HoverHintTrigger trigger = buttonObject.GetComponent<HoverHintTrigger>();
        if (trigger == null)
        {
            trigger = buttonObject.AddComponent<HoverHintTrigger>();
        }
        trigger.hint = hint;
        return trigger;
    }

    private void ConfigureBackButton(GameObject buttonObject)
    {
        // 布局、文案、颜色均以场景 / Prefab 为准；仅保证可点击。
        Image image = buttonObject.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }
    }

    private void RefreshHintState()
    {
        if (burnedPaperHintTrigger == null)
        {
            return;
        }

        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        bool done = (evidenceManager != null && evidenceManager.HasClue(EvidenceIds.BURNED_PAPER))
            || (notebookManager != null && notebookManager.HasEvidence(EvidenceIds.BURNED_PAPER));

        burnedPaperHintTrigger.SetHint(done ? exhaustedHint : "点击查看");
    }
}
