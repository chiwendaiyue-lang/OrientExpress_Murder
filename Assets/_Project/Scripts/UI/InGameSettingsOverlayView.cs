using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 游戏内设置遮罩：可在 Prefab 上只挂本脚本（子节点由运行时生成），也可在编辑器里预先摆好同名子物体。
/// 路径约定：<c>Dim</c>、<c>Panel/SaveButton</c>、<c>Panel/ExitMenuButton</c>、<c>Panel/QuitButton</c>、<c>Panel/CloseButton</c>。
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
        pr.sizeDelta = new Vector2(440f, 400f);
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
        AddLabel(panelTf, "HintVolume", "音量调节（后续在此接入）", 18f, new Color(0.65f, 0.6f, 0.55f));
        AddLabel(panelTf, "HintEsc", "按 ESC 也可开关本面板", 16f, new Color(0.55f, 0.5f, 0.48f));

        AddPanelButton(panelTf, "SaveButton", "保存当前进度");
        AddPanelButton(panelTf, "ExitMenuButton", "退出到主菜单");
        AddPanelButton(panelTf, "QuitButton", "退出游戏");
        AddPanelButton(panelTf, "CloseButton", "关闭");
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
