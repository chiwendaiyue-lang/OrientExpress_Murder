using System.Collections;
using System.Collections.Generic;
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
        // 添加线索
        EvidenceManager.Instance.AddClue(clueId);

        // 显示提示
        ShowTooltip();

        // 禁用物品（防止重复获取）
        interactButton.interactable = false;
        if (highlightEffect != null) highlightEffect.SetActive(false);

        // 可选：播放音效
        // AudioManager.Instance.Play("clue_found");
    }

    void ShowTooltip()
    {
        // 简单起见，用Debug，后续可接入弹窗
        Debug.Log($"🔍 获得【{clueId}】：{clueDescription}");
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