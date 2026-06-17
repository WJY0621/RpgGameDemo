using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform targetRect;
    private RectTransform parentRect;
    private Vector2 dragOffset;

    public void Setup(RectTransform target)
    {
        targetRect = target;

        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            Image image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            graphic = image;
        }

        graphic.raycastTarget = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetRect == null)
        {
            targetRect = transform.parent as RectTransform;
        }

        parentRect = targetRect != null ? targetRect.parent as RectTransform : null;
        if (targetRect == null || parentRect == null || eventData == null)
        {
            return;
        }

        targetRect.SetAsLastSibling();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            dragOffset = targetRect.anchoredPosition - localPointerPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetRect == null || parentRect == null || eventData == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPointerPosition))
        {
            targetRect.anchoredPosition = localPointerPosition + dragOffset;
        }
    }
}
