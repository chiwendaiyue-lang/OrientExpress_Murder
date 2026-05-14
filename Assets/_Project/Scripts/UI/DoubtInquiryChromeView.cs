using TMPro;
using UnityEngine;

/// <summary>
/// 挂在 <c>Resources/UI/doubt</c> 预制体上：不改动立绘与布局；可选仅把疑点标题写入 <c>Bubble/Line</c>，并关闭察觉用的闪烁脚本。
/// </summary>
[DisallowMultipleComponent]
public sealed class DoubtInquiryChromeView : MonoBehaviour
{
    [SerializeField] private TMP_Text bodyText;

    private void Reset()
    {
        ResolveBodyTextIfNeeded();
    }

    /// <summary>
    /// 子物体 <c>Bubble/Line</c>：仅写入疑点标题（<c>question</c> 或 <c>name</c>），
    /// 不写 <c>description</c>/<c>unlockBrief</c>（此前与标题拼在同一段 TMP 里，看起来像 prefab 多出一行「下方说明」）。
    /// 若整段都不希望由代码改，可在 Inspector 取消勾选 <see cref="writeQuestionToLine"/>。
    /// </summary>
    [SerializeField] private bool writeQuestionToLine = true;

    public void Bind(DoubtDefinition doubt)
    {
        ResolveBodyTextIfNeeded();

        if (writeQuestionToLine && bodyText != null && doubt != null)
        {
            string title = !string.IsNullOrEmpty(doubt.question) ? doubt.question : doubt.name;
            bodyText.richText = true;
            bodyText.text = $"<size=110%><b>{title}</b></size>";
        }

        DisableNoticeChromeBehaviours();
    }

    private void ResolveBodyTextIfNeeded()
    {
        if (bodyText == null)
        {
            Transform line = transform.Find("Bubble/Line");
            if (line != null)
            {
                bodyText = line.GetComponent<TMP_Text>();
            }
        }
    }

    private void DisableNoticeChromeBehaviours()
    {
        PerceptionMomentNoticeChromeView notice = GetComponent<PerceptionMomentNoticeChromeView>();
        if (notice != null)
        {
            notice.enabled = false;
        }

        if (bodyText != null)
        {
            foreach (BlinkingTextTail blink in bodyText.GetComponents<BlinkingTextTail>())
            {
                blink.enabled = false;
            }
        }
    }
}
