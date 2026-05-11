using UnityEngine;
using UnityEngine.EventSystems;

public class HoverHintTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea] public string hint = "";

    public void SetHint(string newHint)
    {
        hint = newHint;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (HoverHintUI.Instance != null && !string.IsNullOrEmpty(hint))
        {
            HoverHintUI.Instance.ShowHint(hint);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (HoverHintUI.Instance != null)
        {
            HoverHintUI.Instance.HideHint();
        }
    }

    void OnDisable()
    {
        if (HoverHintUI.Instance != null)
        {
            HoverHintUI.Instance.HideHint();
        }
    }
}
