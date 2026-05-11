using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EvidenceSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text previewText;

    private string itemId;
    private EvidencePanelUI.EvidenceTab itemTab;
    private EvidencePanelUI ownerPanel;

    public void Init(string id, EvidencePanelUI panel)
    {
        Init(id, EvidencePanelUI.EvidenceTab.Physical, panel);
    }

    public void Init(string id, EvidencePanelUI.EvidenceTab tab, EvidencePanelUI panel)
    {
        itemId = id;
        itemTab = tab;
        ownerPanel = panel;

        ApplyDisplay();
        BindButton();
    }

    private void ApplyDisplay()
    {
        EnsureTextBindings();
        ConfigureLayoutForPreview();

        DetectiveNotebookManager notebook = DetectiveNotebookManager.EnsureInstance();
        string title = itemId;
        string preview = string.Empty;
        string iconId = string.Empty;

        if (notebook != null)
        {
            if (itemTab == EvidencePanelUI.EvidenceTab.Physical)
            {
                PhysicalEvidenceDefinition item = notebook.GetCurrentEvidence(itemId);
                if (item != null)
                {
                    title = string.IsNullOrEmpty(item.name) ? itemId : item.name;
                    preview = item.description;
                    iconId = item.icon;
                }
            }
            else if (itemTab == EvidencePanelUI.EvidenceTab.Testimony)
            {
                TestimonyDefinition item = notebook.GetCurrentTestimony(itemId);
                if (item != null)
                {
                    title = string.IsNullOrEmpty(item.name) ? itemId : item.name;
                    preview = item.summary;
                }
            }
            else
            {
                DoubtDefinition item = notebook.GetCurrentDoubt(itemId);
                if (item != null)
                {
                    title = string.IsNullOrEmpty(item.name) ? itemId : item.name;
                    preview = item.description;
                }
            }
        }

        if (previewText != null && previewText != titleText)
        {
            titleText.text = title;
            previewText.text = preview;
        }
        else if (titleText != null)
        {
            titleText.richText = true;
            titleText.text = string.IsNullOrEmpty(preview)
                ? title
                : $"{title}\n<size=75%><color=#CFC6B2>{preview}</color></size>";
        }

        SetIcon(iconId);
    }

    private void SetIcon(string iconId)
    {
        if (iconImage == null)
        {
            return;
        }

        // 仅 Physical 物证有图标，其它分类直接隐藏
        Sprite icon = itemTab == EvidencePanelUI.EvidenceTab.Physical
            ? EvidencePanelUI.LoadEvidenceIcon(iconId, itemId)
            : null;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.gameObject.SetActive(icon != null);
    }

    private void BindButton()
    {
        Button btn = GetComponent<Button>();
        if (btn == null)
        {
            return;
        }

        btn.onClick.RemoveListener(OnClickSlot);
        btn.onClick.AddListener(OnClickSlot);
    }

    private void OnClickSlot()
    {
        if (ownerPanel != null)
        {
            ownerPanel.ShowNotebookDetail(itemId, itemTab);
        }
    }

    private void EnsureTextBindings()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0)
        {
            titleText = texts[0];
        }

        if (previewText == null && texts.Length > 1)
        {
            previewText = texts[1];
        }

        if (titleText == null)
        {
            GameObject textObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            titleText = textObject.GetComponent<TMP_Text>();
        }

        if (titleText != null)
        {
            RectTransform rect = titleText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, -8f);
            titleText.fontSize = 20f;
            titleText.alignment = TextAlignmentOptions.TopLeft;
            titleText.enableWordWrapping = true;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (previewText != null && previewText != titleText)
        {
            previewText.fontSize = 16f;
            previewText.alignment = TextAlignmentOptions.TopLeft;
            previewText.enableWordWrapping = true;
            previewText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void ConfigureLayoutForPreview()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 96f);
        }

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 96f;
        layoutElement.preferredHeight = 96f;
        layoutElement.flexibleHeight = 0f;
    }
}
