using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏内设置遮罩：可在 Prefab 上只挂本脚本（子节点由运行时生成），也可在编辑器里预先摆好同名子物体。
/// 路径约定：<c>Dim</c>、<c>Panel/SaveButton</c>、<c>Panel/ExitMenuButton</c>、<c>Panel/QuitButton</c>、<c>Panel/CloseButton</c>。
/// 音量：<c>Panel/AudioVolumeSection</c> 可在运行时自动创建；每条对应 <see cref="GameAudioChannel"/>。
/// </summary>
public class InGameSettingsOverlayView : MonoBehaviour
{
    private static Sprite s_SolidSprite;

    public static void EnsureSolidSprite(Image image)
    {
        if (image == null || image.sprite != null)
        {
            return;
        }

        if (s_SolidSprite == null)
        {
            Texture2D t = Texture2D.whiteTexture;
            s_SolidSprite = Sprite.Create(
                t,
                new Rect(0f, 0f, t.width, t.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        image.sprite = s_SolidSprite;
    }

    private Button dimCloseButton;
    private Button saveButton;
    private Button exitMenuButton;
    private Button quitButton;
    private Button closeButton;

    private void Awake()
    {
        if (transform.childCount == 0)
        {
            BuildDefaultLayout();
        }

        ResolveButtons();
        ApplySolidSpritesRecursive(gameObject);
        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            ApplyDefaultTmpFont(tmp);
        }
    }

    public void Wire(Action onSave, Action onExitToMenu, Action onQuit, Action onCloseOverlay)
    {
        ResolveButtons();
        ClearClickListeners();

        if (dimCloseButton != null)
        {
            dimCloseButton.onClick.AddListener(() => onCloseOverlay?.Invoke());
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(() => onSave?.Invoke());
        }

        if (exitMenuButton != null)
        {
            exitMenuButton.onClick.AddListener(() => onExitToMenu?.Invoke());
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() => onQuit?.Invoke());
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => onCloseOverlay?.Invoke());
        }
    }

    /// <summary>由 <see cref="InGamePauseMenuController"/> 在打开设置后调用。</summary>
    public void BindAudioVolumeControls()
    {
        GameAudioManager.EnsureExists();
        Transform panel = transform.Find("Panel");
        if (panel == null)
        {
            return;
        }

        Transform hint = panel.Find("HintVolume");
        if (hint != null)
        {
            hint.gameObject.SetActive(false);
        }

        Transform section = panel.Find("AudioVolumeSection");
        if (section == null)
        {
            GameObject sectionGo = CreateAudioVolumeSection(panel);
            section = sectionGo.transform;
            Transform title = panel.Find("Title");
            if (title != null)
            {
                section.SetSiblingIndex(title.GetSiblingIndex() + 1);
            }
        }

        foreach (GameAudioChannel ch in GameAudioManager.AllChannels)
        {
            Transform row = section.Find("VolumeRow_" + ch);
            if (row == null)
            {
                continue;
            }

            Slider slider = row.GetComponentInChildren<Slider>(true);
            if (slider == null)
            {
                continue;
            }

            slider.onValueChanged.RemoveAllListeners();
            float v = GameAudioManager.Instance.GetLinearVolume(ch);
            slider.SetValueWithoutNotify(v);
            GameAudioChannel captured = ch;
            slider.onValueChanged.AddListener(vol => GameAudioManager.Instance.SetLinearVolume(captured, vol));
        }
    }

    public void RefreshAudioVolumeSlidersFromManager()
    {
        GameAudioManager.EnsureExists();
        Transform panel = transform.Find("Panel");
        Transform section = panel != null ? panel.Find("AudioVolumeSection") : null;
        if (section == null)
        {
            return;
        }

        foreach (GameAudioChannel ch in GameAudioManager.AllChannels)
        {
            Transform row = section.Find("VolumeRow_" + ch);
            Slider slider = row != null ? row.GetComponentInChildren<Slider>(true) : null;
            if (slider == null)
            {
                continue;
            }

            slider.SetValueWithoutNotify(GameAudioManager.Instance.GetLinearVolume(ch));
        }
    }

    private void ClearClickListeners()
    {
        foreach (Button b in new[] { dimCloseButton, saveButton, exitMenuButton, quitButton, closeButton })
        {
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
            }
        }
    }

    private void ResolveButtons()
    {
        dimCloseButton = transform.Find("Dim")?.GetComponent<Button>();
        Transform panel = transform.Find("Panel");
        if (panel == null)
        {
            return;
        }

        saveButton = panel.Find("SaveButton")?.GetComponent<Button>();
        exitMenuButton = panel.Find("ExitMenuButton")?.GetComponent<Button>();
        quitButton = panel.Find("QuitButton")?.GetComponent<Button>();
        closeButton = panel.Find("CloseButton")?.GetComponent<Button>();
    }

    private static void ApplySolidSpritesRecursive(GameObject root)
    {
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            EnsureSolidSprite(img);
        }
    }

    private static void ApplyDefaultTmpFont(TMP_Text tmp)
    {
        if (tmp == null)
        {
            return;
        }

        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void BuildDefaultLayout()
    {
        RectTransform ort = GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero;
        ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = Vector2.zero;

        GameObject dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dimGo.transform.SetParent(transform, false);
        RectTransform dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;
        dimRt.SetAsFirstSibling();

        Image dimImg = dimGo.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget = true;
        EnsureSolidSprite(dimImg);

        Button dimBtn = dimGo.GetComponent<Button>();
        dimBtn.targetGraphic = dimImg;

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(transform, false);
        RectTransform pr = panelGo.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(480f, 620f);
        pr.SetAsLastSibling();

        Image panelImg = panelGo.GetComponent<Image>();
        panelImg.color = new Color(0.14f, 0.12f, 0.1f, 1f);
        panelImg.raycastTarget = true;
        EnsureSolidSprite(panelImg);

        VerticalLayoutGroup v = panelGo.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 20, 20);
        v.spacing = 12f;
        v.childAlignment = TextAnchor.UpperCenter;

        Transform panelTf = panelGo.transform;
        AddLabel(panelTf, "Title", "设置", 28f, null);
        CreateAudioVolumeSection(panelTf);
        AddLabel(panelTf, "HintEsc", "按 ESC 也可开关本面板", 16f, new Color(0.55f, 0.5f, 0.48f));

        AddPanelButton(panelTf, "SaveButton", "保存当前进度");
        AddPanelButton(panelTf, "ExitMenuButton", "退出到主菜单");
        AddPanelButton(panelTf, "QuitButton", "退出游戏");
        AddPanelButton(panelTf, "CloseButton", "关闭");
    }

    private static GameObject CreateAudioVolumeSection(Transform panel)
    {
        GameObject section = new GameObject("AudioVolumeSection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        section.transform.SetParent(panel, false);
        VerticalLayoutGroup vg = section.GetComponent<VerticalLayoutGroup>();
        vg.spacing = 8f;
        vg.childAlignment = TextAnchor.UpperCenter;
        vg.childControlWidth = true;
        vg.childForceExpandWidth = true;
        vg.childControlHeight = false;
        vg.childForceExpandHeight = false;

        LayoutElement sectionLe = section.AddComponent<LayoutElement>();
        sectionLe.minHeight = 260f;
        sectionLe.flexibleWidth = 1f;

        foreach (GameAudioChannel ch in GameAudioManager.AllChannels)
        {
            AddVolumeSliderRow(section.transform, ch);
        }

        return section;
    }

    private static void AddVolumeSliderRow(Transform section, GameAudioChannel channel)
    {
        string rowName = "VolumeRow_" + channel;
        GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(section, false);
        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        h.childControlHeight = true;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(0, 0, 0, 0);

        LayoutElement rowLe = row.GetComponent<LayoutElement>();
        rowLe.minHeight = 44f;
        rowLe.flexibleWidth = 1f;

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGo.transform.SetParent(row.transform, false);
        TMP_Text tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = GameAudioManager.GetChannelDisplayName(channel);
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = new Color(0.88f, 0.84f, 0.78f);
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        LayoutElement labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.preferredWidth = 210f;
        labelLe.minWidth = 180f;
        labelLe.flexibleWidth = 0f;

        BuildVolumeSlider(row.transform);
    }

    private static void BuildVolumeSlider(Transform row)
    {
        GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        root.transform.SetParent(row, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 32f);

        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = true;
        slider.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = slider.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.35f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);
        slider.colors = colors;

        LayoutElement sliderLe = root.GetComponent<LayoutElement>();
        sliderLe.flexibleWidth = 1f;
        sliderLe.minWidth = 120f;
        sliderLe.minHeight = 32f;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.GetComponent<Image>();
        EnsureSolidSprite(bgImg);
        bgImg.color = new Color(0.22f, 0.19f, 0.16f, 1f);
        bgImg.raycastTarget = true;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(6f, 6f);
        fillAreaRt.offsetMax = new Vector2(-6f, -6f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImg = fill.GetComponent<Image>();
        EnsureSolidSprite(fillImg);
        fillImg.color = new Color(0.52f, 0.45f, 0.32f, 1f);
        fillImg.raycastTarget = false;

        GameObject handleSlide = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleSlide.transform.SetParent(root.transform, false);
        RectTransform hsRt = handleSlide.GetComponent<RectTransform>();
        hsRt.anchorMin = Vector2.zero;
        hsRt.anchorMax = Vector2.one;
        hsRt.offsetMin = new Vector2(6f, 6f);
        hsRt.offsetMax = new Vector2(-6f, -6f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleSlide.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(20f, 26f);
        Image handleImg = handle.GetComponent<Image>();
        EnsureSolidSprite(handleImg);
        handleImg.color = new Color(0.94f, 0.9f, 0.82f, 1f);
        handleImg.raycastTarget = true;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private static void AddLabel(Transform parent, string objectName, string text, float size, Color? color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color ?? new Color(0.92f, 0.88f, 0.82f);
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        go.GetComponent<LayoutElement>().minHeight = size + 12f;
    }

    private static void AddPanelButton(Transform parent, string objectName, string label)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.32f, 0.26f, 0.2f, 1f);
        img.raycastTarget = true;
        EnsureSolidSprite(img);

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.transform.SetParent(go.transform, false);
        TMP_Text tmp = t.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        RectTransform tr = t.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8f, 6f);
        tr.offsetMax = new Vector2(-8f, -6f);

        go.GetComponent<LayoutElement>().minHeight = 48f;
    }
}
