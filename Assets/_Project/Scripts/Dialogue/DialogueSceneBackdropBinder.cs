using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在列车走廊等「对话与场景共用一景」的场景里，把某张 <see cref="Image"/> 绑定为背景。
/// 对话 JSON 根级可填 <see cref="DialogueData.backdropImageId"/>，在 <see cref="DialogueManager"/> 开始一段对话时切换贴图。
/// 资源查找顺序：<c>Resources/backgrounds/{id}</c>，再 <c>Resources/UI/{id}</c>。
/// </summary>
public class DialogueSceneBackdropBinder : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite defaultSprite;

    public void ApplyBackdrop(string imageId)
    {
        if (targetImage == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(imageId))
        {
            if (defaultSprite != null)
            {
                targetImage.sprite = defaultSprite;
            }

            return;
        }

        Sprite sprite = Resources.Load<Sprite>($"backgrounds/{imageId}");
        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>($"UI/{imageId}");
        }

        if (sprite != null)
        {
            targetImage.sprite = sprite;
        }
    }

    public static void ApplyIfAny(string imageId)
    {
        DialogueSceneBackdropBinder binder = FindFirstObjectByType<DialogueSceneBackdropBinder>(FindObjectsInactive.Include);
        if (binder == null)
        {
            return;
        }

        binder.ApplyBackdrop(imageId);
    }
}
