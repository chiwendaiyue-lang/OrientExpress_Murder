using System;
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

    [Tooltip("疑点脚本分支选项按钮；未指定时疑点脚本内会回退为简易按钮样式。")]
    [SerializeField] private GameObject doubtOptionButtonPrefab;

    public void Bind(DoubtDefinition doubt)
    {
        ResolveTextsIfNeeded();

        if (doubt != null && !string.IsNullOrWhiteSpace(doubt.doubtInquiryLineScriptResourcePath))
        {
            DoubtInquiryLineScriptPlayer player = GetComponent<DoubtInquiryLineScriptPlayer>();
            if (player == null)
            {
                player = gameObject.AddComponent<DoubtInquiryLineScriptPlayer>();
            }
            else
            {
                player.StopPlayback();
            }

            if (doubtOptionButtonPrefab == null)
            {
                Debug.LogWarning(
                    "DoubtInquiryChromeView: doubtOptionButtonPrefab 未赋值，疑点选项会使用简易样式。"
                    + " 请在 Resources/UI/doubt 根物体的 DoubtInquiryChromeView 上指定 Assets/_Project/Prefabs/UI/OptionButton.prefab。");
            }

            player.SetOptionButtonPrefab(doubtOptionButtonPrefab);
            player.StartPlayback(speakerNameText, detailText, legacyLineText, doubt.doubtInquiryLineScriptResourcePath);
            DisableNoticeChromeBehaviours();
            return;
        }

        DoubtInquiryLineScriptPlayer orphan = GetComponent<DoubtInquiryLineScriptPlayer>();
        if (orphan != null)
        {
            orphan.StopPlayback();
            Destroy(orphan);
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
        Transform bubble = FindDirectChildByName(transform, "Bubble");

        if (speakerNameText == null)
        {
            Transform t = bubble != null ? FindDirectChildByName(bubble, "name") : null;
            if (t != null)
            {
                speakerNameText = t.GetComponent<TMP_Text>();
            }
        }

        if (detailText == null)
        {
            Transform t = bubble != null ? FindDirectChildByName(bubble, "detail") : null;
            if (t != null)
            {
                detailText = t.GetComponent<TMP_Text>();
            }
        }

        if (legacyLineText == null)
        {
            Transform line = bubble != null ? FindDirectChildByName(bubble, "Line") : null;
            if (line != null)
            {
                legacyLineText = line.GetComponent<TMP_Text>();
            }
        }
    }

    /// <summary>
    /// 遍历直接子节点（含未激活），避免 <see cref="Transform.Find(string)"/> 不搜索未激活子物体导致解析失败。
    /// </summary>
    private static Transform FindDirectChildByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (string.Equals(c.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }

        return null;
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
