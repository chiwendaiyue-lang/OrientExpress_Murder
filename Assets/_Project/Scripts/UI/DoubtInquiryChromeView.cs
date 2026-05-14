using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 挂在 <c>Resources/UI/doubt</c> 预制体上：疑点脚本走 <c>Bubble/name</c>（说话者）与 <c>Bubble/detail</c>（正文）；
/// 无脚本时可选把疑点标题写入 <c>Bubble/detail</c>（或旧版 <c>Bubble/Line</c>），并关闭察觉用闪烁脚本。
/// </summary>
[DisallowMultipleComponent]
public sealed class DoubtInquiryChromeView : MonoBehaviour
{
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text detailText;

    /// <summary>旧预制体仅有 <c>Bubble/Line</c> 时作 detail 回退；兼容原 Inspector 字段 <c>bodyText</c>。</summary>
    [SerializeField, FormerlySerializedAs("bodyText")]
    private TMP_Text legacyLineText;

    private void Reset()
    {
        ResolveTextsIfNeeded();
    }

    [SerializeField, FormerlySerializedAs("writeQuestionToLine")]
    private bool writeQuestionToDetail = true;

    public void Bind(DoubtDefinition doubt)
    {
        ResolveTextsIfNeeded();

        DoubtInquiryLineScriptPlayer linePlayer = GetComponent<DoubtInquiryLineScriptPlayer>();
        if (linePlayer != null)
        {
            linePlayer.StopPlayback();
            Destroy(linePlayer);
        }

        if (doubt != null && !string.IsNullOrWhiteSpace(doubt.doubtInquiryLineScriptResourcePath))
        {
            DoubtInquiryLineScriptPlayer player = gameObject.AddComponent<DoubtInquiryLineScriptPlayer>();
            player.StartPlayback(speakerNameText, detailText, legacyLineText, doubt.doubtInquiryLineScriptResourcePath);
            DisableNoticeChromeBehaviours();
            return;
        }

        TMP_Text target = detailText != null ? detailText : legacyLineText;
        if (writeQuestionToDetail && target != null && doubt != null)
        {
            string title = !string.IsNullOrEmpty(doubt.question) ? doubt.question : doubt.name;
            target.richText = true;
            target.text = $"<size=110%><b>{title}</b></size>";
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = string.Empty;
        }

        DisableNoticeChromeBehaviours();
    }

    private void ResolveTextsIfNeeded()
    {
        if (speakerNameText == null)
        {
            Transform t = transform.Find("Bubble/name");
            if (t != null)
            {
                speakerNameText = t.GetComponent<TMP_Text>();
            }
        }

        if (detailText == null)
        {
            Transform t = transform.Find("Bubble/detail");
            if (t != null)
            {
                detailText = t.GetComponent<TMP_Text>();
            }
        }

        if (legacyLineText == null)
        {
            Transform line = transform.Find("Bubble/Line");
            if (line != null)
            {
                legacyLineText = line.GetComponent<TMP_Text>();
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

        DisableBlinkOn(speakerNameText);
        DisableBlinkOn(detailText);
        DisableBlinkOn(legacyLineText);
    }

    private static void DisableBlinkOn(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        foreach (BlinkingTextTail blink in text.GetComponents<BlinkingTextTail>())
        {
            blink.enabled = false;
        }

        text.raycastTarget = false;
    }
}
