using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在察觉「立绘 + 气泡」预制体根上，在 Inspector 里摆好位置与气泡形状（Image 九宫格 / 自定义 Sprite），
/// 由 <see cref="PerceptionMomentPresenter"/> 在显示察觉 UI 时调用 <see cref="Apply"/>。
/// </summary>
[DisallowMultipleComponent]
public class PerceptionMomentNoticeChromeView : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text noticeLineText;
    [SerializeField] private BlinkingTextTail lineBlink;

    public void Apply(string line, Sprite portrait, int tailLength, float blinkInterval)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (noticeLineText == null)
        {
            return;
        }

        noticeLineText.text = line;

        BlinkingTextTail blink = lineBlink;
        if (blink == null)
        {
            blink = noticeLineText.GetComponent<BlinkingTextTail>();
        }

        if (blink == null)
        {
            blink = noticeLineText.gameObject.AddComponent<BlinkingTextTail>();
        }

        blink.BindLabel(noticeLineText);
        blink.Configure(line, tailLength, blinkInterval);
    }
}
