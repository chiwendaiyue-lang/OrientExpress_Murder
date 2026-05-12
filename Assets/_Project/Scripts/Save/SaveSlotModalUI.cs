using System;
using UnityEngine;

/// <summary>
/// 主菜单存档槽入口。优先实例化 <c>Resources/UI/SaveSlotModal</c> 预制体（可在 Prefab 模式可视化编辑）；
/// 若未放置该预制体则使用运行时生成的兜底 UI。
/// </summary>
public static class SaveSlotModalUI
{
    public enum PickMode
    {
        NewGame,
        Continue
    }

    private const string PrefabResourcesPath = "UI/SaveSlotModal";

    public static void Show(Transform canvasRoot, PickMode pickMode, Action<int> onPicked, Action onCancelled)
    {
        if (canvasRoot == null)
        {
            Debug.LogError("SaveSlotModalUI: canvasRoot 为空。");
            return;
        }

        Canvas parentCanvas = canvasRoot.GetComponent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = canvasRoot.GetComponentInParent<Canvas>();
        }

        Transform parent = parentCanvas != null ? parentCanvas.transform : canvasRoot;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        if (prefab != null)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            RectTransform rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.SetAsLastSibling();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            SaveSlotModalView view = instance.GetComponentInChildren<SaveSlotModalView>(true);
            if (view != null)
            {
                view.Setup(parentCanvas, pickMode, onPicked, onCancelled);
                return;
            }

            Debug.LogWarning($"SaveSlotModalUI: 预制体 {PrefabResourcesPath} 未挂载 {nameof(SaveSlotModalView)}，已回退到运行时 UI。");
            UnityEngine.Object.Destroy(instance);
        }

        SaveSlotModalRuntimeFallback.Show(canvasRoot, pickMode, onPicked, onCancelled);
    }
}
