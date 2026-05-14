using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 疑点覆层内：按 <see cref="DialogueData"/> 在 <c>Bubble/name</c>（speakerName）与 <c>Bubble/detail</c>（text）展示；
/// 无选项时点击 <c>Bubble</c> 区域推进下一句或关闭；有选项时在 Bubble 下生成选项按钮。
/// </summary>
[DisallowMultipleComponent]
public sealed class DoubtInquiryLineScriptPlayer : MonoBehaviour
{
    private TMP_Text nameText;
    private TMP_Text detailText;
    private TMP_Text legacyLineText;

    private Transform optionHost;
    private Button bubbleClickButton;
    private DialogueData dialogue;
    private string currentNodeId;
    private bool isActive;

    public void StartPlayback(TMP_Text nameTmp, TMP_Text detailTmp, TMP_Text legacyLine, string resourcesPathWithoutExtension)
    {
        StopPlayback();
        nameText = nameTmp;
        detailText = detailTmp;
        legacyLineText = legacyLine;

        if (nameText == null && detailText == null && legacyLineText == null)
        {
            Debug.LogWarning("DoubtInquiryLineScriptPlayer: 未找到 Bubble/name、Bubble/detail 或 Bubble/Line 上的 TMP_Text。");
            return;
        }

        if (string.IsNullOrWhiteSpace(resourcesPathWithoutExtension))
        {
            return;
        }

        string path = resourcesPathWithoutExtension.Trim();
        TextAsset asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 找不到 Resources/{path}.json");
            return;
        }

        dialogue = JsonUtility.FromJson<DialogueData>(asset.text);
        if (dialogue == null || string.IsNullOrEmpty(dialogue.startNodeId))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: JSON 无效或缺少 startNodeId：{path}");
            return;
        }

        dialogue.BuildLookup();
        if (dialogue.nodeLookup == null || !dialogue.nodeLookup.ContainsKey(dialogue.startNodeId))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 缺少起始节点 {dialogue.startNodeId}（{path}）");
            return;
        }

        EnsureOptionHost();
        EnsureBubbleClickSurface();
        currentNodeId = dialogue.startNodeId;
        isActive = true;
        RenderCurrentNode();
    }

    public void StopPlayback()
    {
        isActive = false;
        dialogue = null;
        currentNodeId = null;
        ClearOptionRows();
        TeardownBubbleClick();
    }

    private void OnDestroy()
    {
        StopPlayback();
    }

    private void EnsureBubbleClickSurface()
    {
        Transform bubble = transform.Find("Bubble");
        if (bubble == null)
        {
            return;
        }

        Image bg = bubble.GetComponent<Image>();
        if (bg != null)
        {
            bg.raycastTarget = true;
        }

        bubbleClickButton = bubble.GetComponent<Button>();
        if (bubbleClickButton == null)
        {
            bubbleClickButton = bubble.gameObject.AddComponent<Button>();
        }

        if (bg != null)
        {
            bubbleClickButton.targetGraphic = bg;
        }

        bubbleClickButton.transition = Selectable.Transition.None;
        bubbleClickButton.onClick.RemoveAllListeners();
    }

    private void TeardownBubbleClick()
    {
        if (bubbleClickButton != null)
        {
            bubbleClickButton.onClick.RemoveAllListeners();
            bubbleClickButton.interactable = false;
        }
    }

    private void EnsureOptionHost()
    {
        if (optionHost != null)
        {
            return;
        }

        Transform bubble = transform.Find("Bubble");
        if (bubble == null)
        {
            bubble = transform;
        }

        Transform existing = bubble.Find("DoubtInquiryOptionHost");
        if (existing != null)
        {
            optionHost = existing;
            return;
        }

        GameObject host = new GameObject(
            "DoubtInquiryOptionHost",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        host.transform.SetParent(bubble, false);
        RectTransform rt = host.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(-40f, 0f);

        VerticalLayoutGroup vlg = host.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(8, 8, 4, 8);
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        ContentSizeFitter fitter = host.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        optionHost = host.transform;
    }

    private void ClearOptionRows()
    {
        if (optionHost == null)
        {
            return;
        }

        for (int i = optionHost.childCount - 1; i >= 0; i--)
        {
            Destroy(optionHost.GetChild(i).gameObject);
        }
    }

    private void RenderCurrentNode()
    {
        ClearOptionRows();
        if (!isActive || dialogue == null || dialogue.nodeLookup == null)
        {
            return;
        }

        if (!dialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node) || node == null)
        {
            EndScriptPlayback();
            return;
        }

        DetectiveNotebookManager nb = DetectiveNotebookManager.EnsureInstance();
        if (node.rewards != null && nb != null)
        {
            nb.ApplyRewards(node.rewards);
        }

        ApplyNodeToLabels(node);

        if (IsNoticeLikeNode(node))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 节点「{currentNodeId}」为 notice 类型，疑点覆层中跳过热点，直接结束。");
            EndScriptPlayback();
            return;
        }

        List<DialogueOption> usableOptions = CollectUsableOptions(node);
        if (usableOptions.Count > 0)
        {
            if (bubbleClickButton != null)
            {
                bubbleClickButton.interactable = false;
                bubbleClickButton.onClick.RemoveAllListeners();
            }

            foreach (DialogueOption opt in usableOptions)
            {
                DialogueOption captured = opt;
                AddOptionButton(captured.text, () => OnOptionChosen(captured));
            }

            return;
        }

        string nextId;
        string nextDialogueId;
        string nextSceneName;
        ResolveNextTarget(node, out nextId, out nextDialogueId, out nextSceneName);
        if (!string.IsNullOrEmpty(nextDialogueId))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 节点「{currentNodeId}」要求切换对话资源 {nextDialogueId}，疑点覆层不支持，已结束。");
            EndScriptPlayback();
            return;
        }

        WireBubbleClickAdvance();
    }

    private void ApplyNodeToLabels(DialogueNode node)
    {
        string speaker = !string.IsNullOrEmpty(node.speakerName)
            ? node.speakerName
            : (!string.IsNullOrEmpty(node.speaker) ? node.speaker : "……");
        string body = node.text ?? string.Empty;

        TMP_Text bodyTarget = detailText != null ? detailText : legacyLineText;

        if (nameText != null)
        {
            nameText.richText = true;
            nameText.text = speaker;
            nameText.raycastTarget = false;
        }

        if (bodyTarget != null)
        {
            bodyTarget.richText = true;
            if (nameText != null)
            {
                bodyTarget.text = body;
            }
            else
            {
                bodyTarget.text = $"<size=95%><color=#8A8074>{speaker}</color></size>\n\n<size=110%><b>{body}</b></size>";
            }

            bodyTarget.raycastTarget = false;
        }
    }

    private void WireBubbleClickAdvance()
    {
        EnsureBubbleClickSurface();
        if (bubbleClickButton == null)
        {
            return;
        }

        bubbleClickButton.onClick.RemoveAllListeners();
        bubbleClickButton.interactable = true;
        bubbleClickButton.onClick.AddListener(OnBubbleClicked);
    }

    private void OnBubbleClicked()
    {
        if (!isActive || dialogue == null || dialogue.nodeLookup == null)
        {
            return;
        }

        if (!dialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node) || node == null)
        {
            EndScriptPlayback();
            return;
        }

        if (CollectUsableOptions(node).Count > 0)
        {
            return;
        }

        ResolveNextTarget(node, out string nextId, out string nextDialogueId, out string nextSceneName);
        if (!string.IsNullOrEmpty(nextDialogueId))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 要求切换对话 {nextDialogueId}，已结束疑点脚本。");
            EndScriptPlayback();
            return;
        }

        if (string.IsNullOrEmpty(nextId) || nextId == "END")
        {
            EndScriptPlayback();
            return;
        }

        AdvanceTo(nextId, nextDialogueId, nextSceneName);
    }

    private static bool IsNoticeLikeNode(DialogueNode node)
    {
        if (node == null || string.IsNullOrEmpty(node.nodeType))
        {
            return false;
        }

        return string.Equals(node.nodeType, "notice", System.StringComparison.OrdinalIgnoreCase);
    }

    private List<DialogueOption> CollectUsableOptions(DialogueNode node)
    {
        var list = new List<DialogueOption>();
        if (node.options == null || node.options.Count == 0)
        {
            return list;
        }

        DetectiveNotebookManager nb = DetectiveNotebookManager.Instance;
        foreach (DialogueOption option in node.options)
        {
            if (option == null || string.IsNullOrEmpty(option.text))
            {
                continue;
            }

            if (nb != null && !nb.MeetsRequirements(option.requirements))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(option.requiredClueId)
                && (EvidenceManager.Instance == null || !EvidenceManager.Instance.HasClue(option.requiredClueId)))
            {
                continue;
            }

            list.Add(option);
        }

        return list;
    }

    private void OnOptionChosen(DialogueOption option)
    {
        if (!isActive || option == null)
        {
            return;
        }

        DetectiveNotebookManager nb = DetectiveNotebookManager.EnsureInstance();
        if (option.rewards != null && nb != null)
        {
            nb.ApplyRewards(option.rewards);
        }

        if (!string.IsNullOrEmpty(option.clueToAdd) && EvidenceManager.Instance != null)
        {
            EvidenceManager.Instance.AddClue(option.clueToAdd);
        }

        AdvanceTo(option.nextNodeId, option.nextDialogueId, option.nextSceneName);
    }

    private void AdvanceTo(string nextNodeId, string nextDialogueId, string nextSceneName)
    {
        if (!isActive)
        {
            return;
        }

        if (!string.IsNullOrEmpty(nextDialogueId))
        {
            Debug.LogWarning($"DoubtInquiryLineScriptPlayer: 选项/跳转要求切换对话 {nextDialogueId}，已结束疑点脚本。");
            EndScriptPlayback();
            return;
        }

        if (string.IsNullOrEmpty(nextNodeId) || nextNodeId == "END")
        {
            EndScriptPlayback();
            return;
        }

        if (dialogue.nodeLookup == null || !dialogue.nodeLookup.ContainsKey(nextNodeId))
        {
            EndScriptPlayback();
            return;
        }

        currentNodeId = nextNodeId;
        RenderCurrentNode();
    }

    private void EndScriptPlayback()
    {
        StopPlayback();
        DoubtInquiryOverlayPresenter.Hide();
    }

    private void ResolveNextTarget(DialogueNode node, out string nextNodeId, out string nextDialogueId, out string nextSceneName)
    {
        nextNodeId = null;
        nextDialogueId = null;
        nextSceneName = null;

        DialogueConditionalNext branch = GetMatchedConditionalNext(node);
        if (branch != null)
        {
            nextNodeId = branch.nextNodeId;
            nextDialogueId = branch.nextDialogueId;
            nextSceneName = branch.nextSceneName;
        }
        else if (node != null)
        {
            nextNodeId = node.nextNodeId;
            nextDialogueId = node.nextDialogueId;
            nextSceneName = node.nextSceneName;
        }

        if (string.IsNullOrEmpty(nextNodeId))
        {
            nextNodeId = "END";
        }

        if (nextNodeId == "END" && string.IsNullOrEmpty(nextSceneName) && node != null && !string.IsNullOrEmpty(node.nextSceneName))
        {
            nextSceneName = node.nextSceneName;
        }
    }

    private DialogueConditionalNext GetMatchedConditionalNext(DialogueNode node)
    {
        if (node == null || node.conditionalNext == null || node.conditionalNext.Count == 0)
        {
            return null;
        }

        DetectiveNotebookManager notebookManager = DetectiveNotebookManager.Instance;
        foreach (DialogueConditionalNext branch in node.conditionalNext)
        {
            if (branch == null)
            {
                continue;
            }

            if (branch.requirements == null)
            {
                return branch;
            }

            if (notebookManager != null && notebookManager.MeetsRequirements(branch.requirements))
            {
                return branch;
            }
        }

        return null;
    }

    private void AddOptionButton(string label, UnityAction onClick)
    {
        if (optionHost == null)
        {
            return;
        }

        GameObject row = new GameObject("Option", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(optionHost, false);

        LayoutElement le = row.GetComponent<LayoutElement>();
        le.minHeight = 36f;
        le.preferredHeight = 40f;

        Image img = row.GetComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        if (img.sprite == null)
        {
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        }

        img.type = Image.Type.Sliced;
        img.color = new Color(0.25f, 0.22f, 0.2f, 0.92f);
        img.raycastTarget = true;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(row.transform, false);
        RectTransform trt = textGo.GetComponent<RectTransform>();
        StretchFull(trt);

        TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Midline;
        tmp.color = new Color(0.95f, 0.92f, 0.88f);
        tmp.raycastTarget = false;
        TMP_Text fontSource = detailText != null ? detailText : legacyLineText;
        if (fontSource != null && fontSource.font != null)
        {
            tmp.font = fontSource.font;
        }

        Button btn = row.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        btn.onClick.AddListener(onClick);
    }

    private static void StretchFull(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 4f);
        rect.offsetMax = new Vector2(-12f, -4f);
    }
}
