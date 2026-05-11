using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 通用 UI 拖动组件。挂在哪个物体上，鼠标在该物体上按下并拖动就会移动 <see cref="target"/>。
/// 如果 <see cref="target"/> 为空，则拖动自身。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("UI/UI Draggable")]
public class UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    [Tooltip("被拖动的目标 RectTransform。留空表示拖动自身。")]
    public RectTransform target;

    [Tooltip("是否限制目标在父级 RectTransform 范围内。")]
    public bool clampToParent = true;

    [Tooltip("拖动开始时把目标提到父级最末位（视觉上显示在最上层）。")]
    public bool bringToFrontOnDrag = true;

    private RectTransform self;
    private Canvas rootCanvas;
    private Vector2 pointerStartLocal;
    private Vector2 dragStartAnchored;
    private bool dragging;

    private void Awake()
    {
        self = (RectTransform)transform;
        if (target == null)
        {
            target = self;
        }
    }

    private void OnEnable()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        EnsureRaycastTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (bringToFrontOnDrag && target != null)
        {
            target.SetAsLastSibling();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        RectTransform parent = target.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera cam = GetEventCamera();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out pointerStartLocal))
        {
            dragStartAnchored = target.anchoredPosition;
            dragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || target == null)
        {
            return;
        }

        RectTransform parent = target.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Camera cam = GetEventCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, cam, out Vector2 currentLocal))
        {
            return;
        }

        Vector2 delta = currentLocal - pointerStartLocal;
        target.anchoredPosition = dragStartAnchored + delta;

        if (clampToParent)
        {
            ClampToParent(parent);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    private void ClampToParent(RectTransform parent)
    {
        Vector3[] parentCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        Vector3[] selfCorners = new Vector3[4];
        target.GetWorldCorners(selfCorners);

        Vector3 offset = Vector3.zero;
        if (selfCorners[0].x < parentCorners[0].x) offset.x += parentCorners[0].x - selfCorners[0].x;
        if (selfCorners[2].x > parentCorners[2].x) offset.x += parentCorners[2].x - selfCorners[2].x;
        if (selfCorners[0].y < parentCorners[0].y) offset.y += parentCorners[0].y - selfCorners[0].y;
        if (selfCorners[2].y > parentCorners[2].y) offset.y += parentCorners[2].y - selfCorners[2].y;

        if (offset != Vector3.zero)
        {
            target.position += offset;
        }
    }

    private Camera GetEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
        }

        if (rootCanvas == null)
        {
            return null;
        }

        return rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
    }

    private void EnsureRaycastTarget()
    {
        // 没有 Graphic 或 raycastTarget=false 的物体接收不到 Pointer 事件
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null && !graphic.raycastTarget)
        {
            graphic.raycastTarget = true;
        }
    }
}
