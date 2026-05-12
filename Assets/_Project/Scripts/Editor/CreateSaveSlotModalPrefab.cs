#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成可编辑的存档槽 UI 预制体到 Resources/UI/SaveSlotModal.prefab。
/// </summary>
public static class CreateSaveSlotModalPrefab
{
    private const string PrefabPath = "Assets/_Project/Resources/UI/SaveSlotModal.prefab";

    [MenuItem("Tools/东方快车/生成 SaveSlotModal 预制体（存档槽 UI）")]
    public static void CreatePrefab()
    {
        string dir = Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(PrefabPath))
        {
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        GameObject root = new GameObject("SaveSlotModal");
        Undo.RegisterCreatedObjectUndo(root, "Create SaveSlotModal");

        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Canvas cv = root.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.overrideSorting = true;
        cv.sortingOrder = 400;

        root.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        Button dimBtn = root.AddComponent<Button>();
        dimBtn.targetGraphic = dim;

        GameObject panel = new GameObject("Panel");
        Undo.RegisterCreatedObjectUndo(panel, "Create Panel");
        panel.transform.SetParent(root.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f);
        pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = Vector2.zero;
        pr.sizeDelta = new Vector2(680f, 560f);

        Image pBg = panel.AddComponent<Image>();
        pBg.color = new Color(0.12f, 0.1f, 0.09f, 0.98f);
        pBg.raycastTarget = true;

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 24);
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TMP_Text titleTmp = CreateTmpTitle(panel.transform, "Title", "选择存档槽");

        Button[] slotButtons = new Button[4];
        TMP_Text[] slotTexts = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            CreateSlotRow(panel.transform, $"Slot{i}", out slotButtons[i], out slotTexts[i]);
        }

        Button backBtn = CreateBackRow(panel.transform);

        SaveSlotModalView view = root.AddComponent<SaveSlotModalView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("titleText").objectReferenceValue = titleTmp;
        for (int i = 0; i < 4; i++)
        {
            so.FindProperty("slotButtons").GetArrayElementAtIndex(i).objectReferenceValue = slotButtons[i];
            so.FindProperty("slotLineTexts").GetArrayElementAtIndex(i).objectReferenceValue = slotTexts[i];
        }

        so.FindProperty("backButton").objectReferenceValue = backBtn;
        so.FindProperty("dimCloseButton").objectReferenceValue = dimBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        Debug.Log($"已生成可编辑预制体：{PrefabPath}（双击在 Prefab 模式中调整布局与尺寸）");
    }

    private static TMP_Text CreateTmpTitle(Transform parent, string name, string text)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.9f, 0.82f);
        tmp.raycastTarget = false;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 48f;
        le.preferredHeight = 48f;
        return tmp;
    }

    private static void CreateSlotRow(Transform parent, string name, out Button button, out TMP_Text lineText)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.28f, 0.22f, 0.18f, 1f);
        img.raycastTarget = true;

        button = go.AddComponent<Button>();
        button.targetGraphic = img;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 84f;
        le.preferredHeight = 84f;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        lineText = textGo.AddComponent<TextMeshProUGUI>();
        lineText.text = "槽位文案（运行时填入）";
        lineText.fontSize = 22f;
        lineText.alignment = TextAlignmentOptions.Center;
        lineText.color = Color.white;
        lineText.enableWordWrapping = true;
        lineText.raycastTarget = false;
        if (lineText.font == null && TMP_Settings.defaultFontAsset != null)
        {
            lineText.font = TMP_Settings.defaultFontAsset;
        }

        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(14f, 10f);
        tr.offsetMax = new Vector2(-14f, -10f);
    }

    private static Button CreateBackRow(Transform parent)
    {
        GameObject go = new GameObject("BackButton");
        Undo.RegisterCreatedObjectUndo(go, "BackButton");
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.2f, 0.17f, 1f);
        img.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 52f;
        le.preferredHeight = 52f;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "返回";
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.92f, 0.88f, 0.8f);
        tmp.raycastTarget = false;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 8f);
        tr.offsetMax = new Vector2(-12f, -8f);

        return btn;
    }
}
#endif
