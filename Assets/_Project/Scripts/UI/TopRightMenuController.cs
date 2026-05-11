using UnityEngine;
using UnityEngine.UI;

public class TopRightMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button evidenceButton;
    [SerializeField] private Button mapButton;
    [SerializeField] private Button interrogationButton;

    [Header("Panels")]
    [SerializeField] private GameObject evidencePanel;
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private GameObject interrogationPanel;

    [Header("Optional Components")]
    [SerializeField] private EvidencePanelUI evidencePanelUI;
    [SerializeField] private InterrogationPanelController interrogationPanelController;

    void Start()
    {
        BindButtons();
        CloseAllPanels();
    }

    public void OnEvidenceButtonClicked()
    {
        ToggleTargetPanel(evidencePanel, true);
    }

    public void OnMapButtonClicked()
    {
        ToggleTargetPanel(mapPanel, false);
    }

    public void OnInterrogationButtonClicked()
    {
        ToggleTargetPanel(interrogationPanel, false);
        if (interrogationPanel != null && interrogationPanel.activeSelf && interrogationPanelController != null)
        {
            interrogationPanelController.RefreshEntries();
        }
    }

    public void CloseAllPanels()
    {
        SetPanelActive(evidencePanel, false);
        SetPanelActive(mapPanel, false);
        SetPanelActive(interrogationPanel, false);
    }

    private void BindButtons()
    {
        if (evidenceButton != null)
        {
            evidenceButton.onClick.RemoveListener(OnEvidenceButtonClicked);
            evidenceButton.onClick.AddListener(OnEvidenceButtonClicked);
        }

        if (mapButton != null)
        {
            mapButton.onClick.RemoveListener(OnMapButtonClicked);
            mapButton.onClick.AddListener(OnMapButtonClicked);
        }

        if (interrogationButton != null)
        {
            interrogationButton.onClick.RemoveListener(OnInterrogationButtonClicked);
            interrogationButton.onClick.AddListener(OnInterrogationButtonClicked);
        }
    }

    private void ToggleTargetPanel(GameObject targetPanel, bool useEvidencePanelApi)
    {
        if (targetPanel == null)
        {
            return;
        }

        bool willOpen = !targetPanel.activeSelf;
        CloseAllPanels();

        if (!willOpen)
        {
            return;
        }

        if (useEvidencePanelApi && evidencePanelUI != null && targetPanel == evidencePanel)
        {
            evidencePanelUI.ToggleEvidencePanel();
            return;
        }

        SetPanelActive(targetPanel, true);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
