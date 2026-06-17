using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在 BuffContent 下每个 BuffIcon 上的鼠标事件转发器。
/// 不持有 UI 渲染逻辑，只负责报告 hover/exit。
/// </summary>
public class BuffIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform rect;
    private Action<BuffIconUI> onHover;
    private Action<BuffIconUI> onExit;

    public BuffInstance Instance { get; private set; }
    public RectTransform Rect => rect != null ? rect : rect = GetComponent<RectTransform>();

    public void Bind(BuffInstance instance, Action<BuffIconUI> hoverCallback, Action<BuffIconUI> exitCallback)
    {
        Instance = instance;
        onHover = hoverCallback;
        onExit = exitCallback;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onHover?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onExit?.Invoke(this);
    }
}
