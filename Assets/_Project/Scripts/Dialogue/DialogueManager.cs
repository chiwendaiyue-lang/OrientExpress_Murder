using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.SceneManagement;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI")]
    public GameObject dialoguePanel;
    public Image speakerPortrait;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;
    public CharacterStageController stageController;
    [Header("Presented Image")]
    public Image presentedImageDisplay;
    public RectTransform hotspotContainer;
    public GameObject hotspotButtonPrefab;
    [Header("Font Override (Optional)")]
    public TMP_FontAsset dialogueFontOverride;
    [Header("Portrait Display")]
    public bool useLeftSpeakerPortrait = false;

    private DialogueData currentDialogue;
    private string currentNodeId;
    private bool waitingForClickAdvance;
    private bool waitMouseReleaseAfterEnter;
    private string pendingNextNodeId;
    private string pendingNextDialogueId;
    private string pendingNextSceneName;
    private readonly List<GameObject> activeHotspots = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!waitingForClickAdvance || !dialoguePanel.activeSelf)
        {
            return;
        }

        if (EvidencePanelUI.IsNotebookOpen)
        {
            return;
        }

        if (EvidencePanelUI.IsPointerOverNotebookArea())
        {
            return;
        }
        
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (waitMouseReleaseAfterEnter)
        {
            if (!Input.GetMouseButton(0))
            {
                waitMouseReleaseAfterEnter = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            AdvanceToNode(pendingNextNodeId, pendingNextDialogueId, pendingNextSceneName);
        }
    }

    public void StartDialogue(string characterId)
    {
        if (!ValidateUIBindings())
        {
            return;
        }

        if (speakerPortrait != null && !useLeftSpeakerPortrait)
        {
            speakerPortrait.sprite = null;
            speakerPortrait.enabled = false;
        }

        // ????JSON???
        TextAsset jsonFile = Resources.Load<TextAsset>($"Dialogue/{characterId}");
        if (jsonFile == null)
        {
            Debug.LogError($"???????????: {characterId}");
            return;
        }

        currentDialogue = JsonUtility.FromJson<DialogueData>(jsonFile.text);
        if (currentDialogue == null)
        {
            Debug.LogError($"??????????: {characterId}");
            return;
        }

        currentDialogue.BuildLookup();
        currentNodeId = currentDialogue.startNodeId;

        if (currentDialogue.nodeLookup == null || !currentDialogue.nodeLookup.ContainsKey(currentNodeId))
        {
            Debug.LogError($"?????????????: {characterId}/{currentNodeId}");
            return;
        }

        dialoguePanel.SetActive(true);
        EnsureRuntimeVisualContainers();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Dialogue);
        }
        ShowCurrentNode();
    }

    void ShowCurrentNode()
    {
        if (currentDialogue == null || currentDialogue.nodeLookup == null || !currentDialogue.nodeLookup.TryGetValue(currentNodeId, out DialogueNode node))
        {
            Debug.LogError($"??????????????: {currentNodeId}");
            EndDialogue();
            return;
        }

        if (DetectiveNotebookManager.Instance != null)
        {
            DetectiveNotebookManager.Instance.ApplyRewards(node.rewards);
        }

        speakerNameText.text = GetDisplaySpeakerName(node);
        dialogueText.text = GetDisplayText(node);
        ApplyFontIfNeeded(speakerNameText);
        ApplyFontIfNeeded(dialogueText);
        ApplyPresentedImage(node.presentedImage);
        if (stageController != null)
        {
            if (node.stage != null)
            {
                stageController.ApplyStage(node.stage, node.speakerCharacterId);
            }
            else
            {
                stageController.ExecuteCommands(node.stageCommands);
                if (!string.IsNullOrEmpty(node.focusSlotId))
                {
                    stageController.FocusSlot(node.focusSlotId);
                }
            }
        }

        // ????????
        if (useLeftSpeakerPortrait && !string.IsNullOrEmpty(node.portrait))
        {
            Sprite portrait = Resources.Load<Sprite>($"Characters/{node.portrait}");
            if (portrait != null)
            {
                if (speakerPortrait != null)
                {
                    speakerPortrait.sprite = portrait;
                    speakerPortrait.enabled = true;
                }
            }
        }
        else
        {
            // 节点明确没有头像时，清空上一个节点残留
            if (speakerPortrait != null)
            {
                speakerPortrait.sprite = null;
                speakerPortrait.enabled = false;
            }
        }

        // ????????
        foreach (Transform child in optionsContainer)
            Destroy(child.gameObject);
        ClearHotspots();

        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;

        if (IsNoticeNode(node))
        {
            CreateHotspots(node.hotspots, true);
            if (activeHotspots.Count == 0)
            {
                pendingNextNodeId = string.IsNullOrEmpty(node.nextNodeId) ? "END" : node.nextNodeId;
                pendingNextDialogueId = node.nextDialogueId;
                pendingNextSceneName = node.nextSceneName;
                waitingForClickAdvance = true;
                waitMouseReleaseAfterEnter = Input.GetMouseButton(0);
            }
            return;
        }

        CreateHotspots(node.hotspots, false);

        // 无选项时，改为点击对话框继续
        if (node.options == null || node.options.Count == 0)
        {
            if (activeHotspots.Count > 0 && string.IsNullOrEmpty(node.nextNodeId))
            {
                return;
            }

            pendingNextNodeId = string.IsNullOrEmpty(node.nextNodeId) ? "END" : node.nextNodeId;
            pendingNextDialogueId = node.nextDialogueId;
            pendingNextSceneName = node.nextSceneName;
            waitingForClickAdvance = true;
            // 防止由上一次点击带来的误触发
            waitMouseReleaseAfterEnter = Input.GetMouseButton(0);
            return;
        }

        foreach (var option in node.options)
        {
            if (DetectiveNotebookManager.Instance != null && !DetectiveNotebookManager.Instance.MeetsRequirements(option.requirements))
            {
                continue;
            }

            // ????????????????
            if (!string.IsNullOrEmpty(option.requiredClueId))
            {
                if (EvidenceManager.Instance == null || !EvidenceManager.Instance.HasClue(option.requiredClueId))
                    continue;
            }

            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            TMP_Text tmpText = btnObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = option.text;
                ApplyFontIfNeeded(tmpText);
            }
            else
            {
                Text legacyText = btnObj.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = option.text;
                }
            }
            btnObj.GetComponent<Button>().onClick.AddListener(() => SelectOption(option));
        }
    }

    void SelectOption(DialogueOption option)
    {
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (DetectiveNotebookManager.Instance != null)
        {
            DetectiveNotebookManager.Instance.ApplyRewards(option.rewards);
        }

        // ???????
        if (!string.IsNullOrEmpty(option.clueToAdd))
        {
            if (EvidenceManager.Instance != null)
            {
                EvidenceManager.Instance.AddClue(option.clueToAdd);
            }
        }

        // ????????
        AdvanceToNode(option.nextNodeId, option.nextDialogueId, option.nextSceneName);
    }

    private void AdvanceToNode(string nextNodeId)
    {
        AdvanceToNode(nextNodeId, null, null);
    }

    private void AdvanceToNode(string nextNodeId, string nextDialogueId)
    {
        AdvanceToNode(nextNodeId, nextDialogueId, null);
    }

    private void AdvanceToNode(string nextNodeId, string nextDialogueId, string nextSceneName)
    {
        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;

        if (nextNodeId == "END")
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                LoadScene(nextSceneName);
                return;
            }

            if (!string.IsNullOrEmpty(nextDialogueId))
            {
                StartDialogue(nextDialogueId);
                return;
            }

            EndDialogue();
            return;
        }

        currentNodeId = nextNodeId;
        ShowCurrentNode();
    }

    private void ApplyFontIfNeeded(TMP_Text textComponent)
    {
        if (dialogueFontOverride == null || textComponent == null)
        {
            return;
        }

        textComponent.font = dialogueFontOverride;
    }

    void EndDialogue()
    {
        waitingForClickAdvance = false;
        pendingNextNodeId = null;
        pendingNextDialogueId = null;
        pendingNextSceneName = null;
        waitMouseReleaseAfterEnter = false;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        ClearHotspots();
        HidePresentedImage();
        if (stageController != null)
        {
            stageController.HideAll();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.Exploring);
        }

        // ????????
        if (GameManager.Instance != null && currentDialogue != null)
        {
            string dialogueKey = GetCurrentDialogueKey();
            if (dialogueKey == "mrs_hubbard")
                GameManager.Instance.HasTalkedToMrsHubbard = true;
            else if (dialogueKey == "count_andrenyi")
                GameManager.Instance.HasTalkedToCountAndrenyi = true;
        }
    }

    private string GetCurrentDialogueKey()
    {
        if (currentDialogue == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(currentDialogue.characterId))
        {
            return currentDialogue.characterId;
        }

        if (!string.IsNullOrEmpty(currentDialogue.sceneId))
        {
            return currentDialogue.sceneId;
        }

        return currentDialogue.dialogueId;
    }

    private bool ValidateUIBindings()
    {
        if (dialoguePanel == null || speakerNameText == null || dialogueText == null || optionsContainer == null || optionButtonPrefab == null)
        {
            Debug.LogError("DialogueManager: UI 引用缺失，请检查 dialoguePanel/speakerNameText/dialogueText/optionsContainer/optionButtonPrefab。");
            return false;
        }

        if (speakerPortrait == null)
        {
            Debug.LogWarning("DialogueManager: speakerPortrait 未绑定，将跳过头像显示。");
        }

        return true;
    }

    private void EnsureRuntimeVisualContainers()
    {
        Transform root = dialoguePanel != null && dialoguePanel.transform.parent != null
            ? dialoguePanel.transform.parent
            : transform;

        if (presentedImageDisplay == null)
        {
            GameObject imageObject = new GameObject("PresentedImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.25f);
            rect.anchorMax = new Vector2(0.8f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            presentedImageDisplay = imageObject.GetComponent<Image>();
            presentedImageDisplay.preserveAspect = true;
            presentedImageDisplay.raycastTarget = false;
            imageObject.SetActive(false);

            if (dialoguePanel != null)
            {
                imageObject.transform.SetSiblingIndex(dialoguePanel.transform.GetSiblingIndex());
            }
        }

        if (hotspotContainer == null)
        {
            GameObject hotspotObject = new GameObject("DialogueHotspots", typeof(RectTransform));
            hotspotObject.transform.SetParent(root, false);
            hotspotContainer = hotspotObject.GetComponent<RectTransform>();
            hotspotContainer.anchorMin = Vector2.zero;
            hotspotContainer.anchorMax = Vector2.one;
            hotspotContainer.offsetMin = Vector2.zero;
            hotspotContainer.offsetMax = Vector2.zero;
            hotspotObject.transform.SetAsLastSibling();
        }
    }

    private void ApplyPresentedImage(PresentedImageData imageData)
    {
        EnsureRuntimeVisualContainers();
        if (imageData == null || string.IsNullOrEmpty(imageData.imageId))
        {
            HidePresentedImage();
            return;
        }

        Sprite sprite = LoadPresentedSprite(imageData.imageId);
        if (sprite == null)
        {
            Debug.LogWarning($"DialogueManager: 找不到展示图片 {imageData.imageId}");
            HidePresentedImage();
            return;
        }

        presentedImageDisplay.sprite = sprite;
        ApplyPresentedImagePosition(imageData.position);
        presentedImageDisplay.gameObject.SetActive(true);
    }

    private Sprite LoadPresentedSprite(string imageId)
    {
        string[] paths =
        {
            $"PresentedImages/{imageId}",
            $"UI/{imageId}",
            $"backgrounds/{imageId}",
            $"Evidence/{imageId}",
            $"Characters/{imageId}",
            imageId
        };

        foreach (string path in paths)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private void ApplyPresentedImagePosition(string position)
    {
        RectTransform rect = presentedImageDisplay.rectTransform;
        string p = string.IsNullOrEmpty(position) ? "center" : position.ToLowerInvariant();

        if (p == "full")
        {
            SetAnchors(rect, 0.05f, 0.08f, 0.95f, 0.95f);
        }
        else if (p == "left")
        {
            SetAnchors(rect, 0.05f, 0.25f, 0.48f, 0.85f);
        }
        else if (p == "right")
        {
            SetAnchors(rect, 0.52f, 0.25f, 0.95f, 0.85f);
        }
        else
        {
            SetAnchors(rect, 0.2f, 0.25f, 0.8f, 0.85f);
        }
    }

    private void HidePresentedImage()
    {
        if (presentedImageDisplay == null)
        {
            return;
        }

        presentedImageDisplay.sprite = null;
        presentedImageDisplay.gameObject.SetActive(false);
    }

    private bool IsNoticeNode(DialogueNode node)
    {
        return node != null && node.nodeType == "notice";
    }

    private string GetDisplaySpeakerName(DialogueNode node)
    {
        if (node == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(node.speakerName))
        {
            return node.speakerName;
        }

        if (!string.IsNullOrEmpty(node.speaker))
        {
            return node.speaker;
        }

        return IsNoticeNode(node) ? "系统" : "";
    }

    private string GetDisplayText(DialogueNode node)
    {
        if (node == null)
        {
            return "";
        }

        if (IsNoticeNode(node) && !string.IsNullOrEmpty(node.prompt))
        {
            return node.prompt;
        }

        return node.text;
    }

    private void CreateHotspots(List<DialogueHotspot> hotspots, bool allowFailure)
    {
        if (hotspots == null || hotspots.Count == 0)
        {
            return;
        }

        EnsureRuntimeVisualContainers();
        for (int i = 0; i < hotspots.Count; i++)
        {
            DialogueHotspot hotspot = hotspots[i];
            if (hotspot == null)
            {
                continue;
            }

            GameObject hotspotObject = CreateHotspotObject(hotspot, i);
            Button button = hotspotObject.GetComponent<Button>();
            if (button == null)
            {
                button = hotspotObject.AddComponent<Button>();
            }

            DialogueHotspot capturedHotspot = hotspot;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnHotspotClicked(capturedHotspot, allowFailure));
            activeHotspots.Add(hotspotObject);
        }
    }

    private GameObject CreateHotspotObject(DialogueHotspot hotspot, int index)
    {
        GameObject hotspotObject;
        if (hotspotButtonPrefab != null)
        {
            hotspotObject = Instantiate(hotspotButtonPrefab, hotspotContainer);
        }
        else
        {
            hotspotObject = new GameObject($"Hotspot_{hotspot.id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            hotspotObject.transform.SetParent(hotspotContainer, false);
            Image image = hotspotObject.GetComponent<Image>();
            image.color = new Color(1f, 0.82f, 0.12f, 0.75f);
        }

        hotspotObject.name = string.IsNullOrEmpty(hotspot.id) ? $"Hotspot_{index}" : $"Hotspot_{hotspot.id}";
        RectTransform rect = hotspotObject.GetComponent<RectTransform>();
        ApplyHotspotRect(rect, hotspot, index);

        TMP_Text tmpText = hotspotObject.GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = string.IsNullOrEmpty(hotspot.label) ? "" : hotspot.label;
            ApplyFontIfNeeded(tmpText);
        }

        Text legacyText = hotspotObject.GetComponentInChildren<Text>();
        if (legacyText != null)
        {
            legacyText.text = string.IsNullOrEmpty(hotspot.label) ? "" : hotspot.label;
        }

        return hotspotObject;
    }

    private void ApplyHotspotRect(RectTransform rect, DialogueHotspot hotspot, int index)
    {
        if (rect == null)
        {
            return;
        }

        if (hotspot.width > 0f && hotspot.height > 0f)
        {
            SetAnchors(rect, hotspot.x, hotspot.y, hotspot.x + hotspot.width, hotspot.y + hotspot.height);
            return;
        }

        Vector2 center = GetDefaultHotspotCenter(hotspot, index);
        float size = 0.065f;
        SetAnchors(rect, center.x - size * 0.5f, center.y - size * 0.5f, center.x + size * 0.5f, center.y + size * 0.5f);
    }

    private Vector2 GetDefaultHotspotCenter(DialogueHotspot hotspot, int index)
    {
        string targetType = hotspot != null && !string.IsNullOrEmpty(hotspot.targetType)
            ? hotspot.targetType
            : "presented_image";

        if (targetType == "dialogue_text")
        {
            return new Vector2(0.78f, 0.22f + index * 0.07f);
        }

        if (targetType == "portrait")
        {
            return new Vector2(0.72f, 0.6f - index * 0.08f);
        }

        return new Vector2(0.5f + index * 0.08f, 0.55f);
    }

    private void OnHotspotClicked(DialogueHotspot hotspot, bool allowFailure)
    {
        if (NotebookUnlockOverlayPresenter.IsBlockingInput)
        {
            return;
        }

        if (EvidencePanelUI.IsNotebookOpen)
        {
            return;
        }

        if (hotspot == null)
        {
            return;
        }

        string nextNodeId = hotspot.successNodeId;
        if (allowFailure && !hotspot.correct)
        {
            nextNodeId = hotspot.failureNodeId;
        }

        if (string.IsNullOrEmpty(nextNodeId))
        {
            Debug.LogWarning($"DialogueManager: 热点 {hotspot.id} 没有可跳转节点。");
            return;
        }

        AdvanceToNode(nextNodeId);
    }

    private void ClearHotspots()
    {
        foreach (GameObject hotspot in activeHotspots)
        {
            if (hotspot != null)
            {
                Destroy(hotspot);
            }
        }
        activeHotspots.Clear();
    }

    private void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
    {
        rect.anchorMin = new Vector2(Mathf.Clamp01(minX), Mathf.Clamp01(minY));
        rect.anchorMax = new Vector2(Mathf.Clamp01(maxX), Mathf.Clamp01(maxY));
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void LoadScene(string sceneName)
    {
        EndDialogue();
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
