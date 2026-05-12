using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 未放置 <c>Resources/UI/SaveSlotModal.prefab</c> 时的运行时兜底 UI（不可视化编辑）。
/// </summary>
internal static class SaveSlotModalRuntimeFallback
{
    internal static void Show(Transform canvasRoot, SaveSlotModalUI.PickMode pickMode, Action<int> onPicked, Action onCancelled)
    {
        Canvas parentCanvas = canvasRoot.GetComponent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = canvasRoot.GetComponentInParent<Canvas>();
        }

        Transform parent = parentCanvas != null ? parentCanvas.transform : canvasRoot;

        GameObject root = new GameObject("SaveSlotModal_Runtime", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(CanvasScaler), typeof(SaveSlotModalRuntimeFallbackHost));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Canvas cv = root.GetComponent<Canvas>();
        cv.renderMode = parentCanvas != null ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        cv.worldCamera = parentCanvas != null ? parentCanvas.worldCamera : null;
        cv.planeDistance = parentCanvas != null ? parentCanvas.planeDistance : 100f;
        cv.overrideSorting = true;
        cv.sortingOrder = 400;

        CanvasScaler parentScaler = parentCanvas != null ? parentCanvas.GetComponent<CanvasScaler>() : null;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        if (parentScaler != null)
        {
            scaler.uiScaleMode = parentScaler.uiScaleMode;
            scaler.referencePixelsPerUnit = parentScaler.referencePixelsPerUnit;
            scaler.referenceResolution = parentScaler.referenceResolution;
            scaler.screenMatchMode = parentScaler.screenMatchMode;
            scaler.matchWidthOrHeight = parentScaler.matchWidthOrHeight;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        Image dim = root.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        Button dimBtn = root.GetComponent<Button>();
        if (dimBtn == null)
        {
            dimBtn = root.AddComponent<Button>();
        }

        dimBtn.targetGraphic = dim;

        SaveSlotModalRuntimeFallbackHost host = root.GetComponent<SaveSlotModalRuntimeFallbackHost>();
        host.Begin(pickMode, onPicked, onCancelled, dimBtn);
    }
}

/// <summary>仅用于运行时兜底，内部实现与旧版 SaveSlotModalUI 一致。</summary>
internal class SaveSlotModalRuntimeFallbackHost : MonoBehaviour
{
    private Action<int> onPickedSlot;
    private Action onCancel;

    public void Begin(SaveSlotModalUI.PickMode pickMode, Action<int> onPicked, Action onCancelled, Button dimBtn)
    {
        onPickedSlot = onPicked;
        onCancel = onCancelled;
        dimBtn.onClick.AddListener(OnClickCancel);
        BuildContent(pickMode);
    }

    private void BuildContent(SaveSlotModalUI.PickMode mode)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.SetParent(transform, false);
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(640f, 520f);

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.1f, 0.09f, 0.98f);
        bg.raycastTarget = true;

        VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(28, 28, 24, 24);
        v.spacing = 14f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        AddTitle(panel.transform, mode == SaveSlotModalUI.PickMode.NewGame ? "选择存档槽（新游戏）" : "选择存档槽（继续游戏）");

        for (int i = 0; i < GameSaveService.SlotCount; i++)
        {
            int slot = i;
            SaveGameData preview = GameSaveService.TryLoadSlot(slot);
            bool exists = preview != null;
            string line2 = exists
                ? $"存档时间 {new DateTime(preview.utcTicks, DateTimeKind.Utc).ToLocalTime():yyyy-MM-dd HH:mm} · {preview.activeSceneName}"
                : "空槽";

            if (mode == SaveSlotModalUI.PickMode.Continue && !exists)
            {
                AddDisabledRow(panel.transform, $"槽位 {slot + 1}", line2);
            }
            else
            {
                string row1 = mode == SaveSlotModalUI.PickMode.NewGame && exists
                    ? $"槽位 {slot + 1}（将覆盖已有存档）"
                    : $"槽位 {slot + 1}";
                AddButtonRow(panel.transform, row1, line2, () => OnPick(slot));
            }
        }

        AddButtonRow(panel.transform, "返回", string.Empty, OnClickCancel);
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

    private static void AddTitle(Transform parent, string text)
    {
        GameObject go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.9f, 0.82f);
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;
    }

    private void AddButtonRow(Transform parent, string line1, string line2, Action onClick)
    {
        GameObject go = new GameObject("RowButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.18f, 1f);
        img.raycastTarget = true;
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrEmpty(line2) ? line1 : $"{line1}\n<size=78%><color=#C8B8A8>{line2}</color></size>";
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 10f);
        tr.offsetMax = new Vector2(-12f, -10f);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = string.IsNullOrEmpty(line2) ? 52f : 80f;
        le.preferredHeight = le.minHeight;
    }

    private void AddDisabledRow(Transform parent, string line1, string line2)
    {
        GameObject go = new GameObject("RowDisabled", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.14f, 0.13f, 0.9f);
        img.raycastTarget = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = $"{line1}\n<size=78%><color=#6A625C>{line2}</color></size>";
        tmp.fontSize = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.5f, 0.48f, 0.45f);
        tmp.raycastTarget = false;
        ApplyDefaultTmpFont(tmp);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 10f);
        tr.offsetMax = new Vector2(-12f, -10f);

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.minHeight = 80f;
        le.preferredHeight = 80f;
    }

    private void OnPick(int slot)
    {
        onPickedSlot?.Invoke(slot);
        Destroy(gameObject);
    }

    private void OnClickCancel()
    {
        onCancel?.Invoke();
        Destroy(gameObject);
    }
}
