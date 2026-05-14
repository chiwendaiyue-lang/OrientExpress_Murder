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

    /// <summary>绑定气泡文案与可选立绘覆盖；<paramref name="portrait"/> 为 null 时不改立绘图。</summary>
    /// <param name="portrait">非 null 时写入立绘；为 null 时保留预制体 / 场景中 <see cref="portraitImage"/> 原有 Sprite。</param>
    public void Apply(string line, Sprite portrait, int tailLength, float blinkInterval)
    {
        if (portraitImage != null)
        {
            if (portrait != null)
            {
                portraitImage.sprite = portrait;
            }

            portraitImage.enabled = portraitImage.sprite != null;
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
