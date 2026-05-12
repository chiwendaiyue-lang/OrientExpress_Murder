using UnityEngine;
using UnityEngine.UI;

public class InteractableItem : MonoBehaviour
{
    public string clueId;
    public string clueDescription;
    public Button interactButton;
    public GameObject highlightEffect;

    void Start()
    {
        if (interactButton == null)
            interactButton = GetComponent<Button>();

        if (interactButton != null)
            interactButton.onClick.AddListener(OnInteract);

        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }

    void OnInteract()
    {
        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.EnsureInstance();
        if (notebookManager != null)
        {
            notebookManager.AddEvidence(clueId);
        }

        EvidenceManager evidenceManager = EvidenceManager.EnsureInstance();
        if (evidenceManager != null)
        {
            evidenceManager.AddClue(clueId);
        }

        if (interactButton != null)
        {
            interactButton.interactable = false;
        }
        if (highlightEffect != null) highlightEffect.SetActive(false);

        // 可选：播放音效
        // AudioManager.Instance.Play("clue_found");
    }

    void OnMouseEnter()
    {
        if (highlightEffect != null && interactButton.interactable)
            highlightEffect.SetActive(true);
    }

    void OnMouseExit()
    {
        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }
}