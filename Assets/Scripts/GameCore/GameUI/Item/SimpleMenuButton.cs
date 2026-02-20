using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptimizedMenuButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler
{
    [Header("基础设置")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float hoverMoveY = 5f;
    [SerializeField] private float clickScale = 0.9f;
    [SerializeField] private float clickDuration = 0.15f;
    
    [Header("颜色设置")]
    [SerializeField] private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1);
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField] private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1);
    
    [Header("组件引用")]
    [SerializeField] private Image icon;
    
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Color originalIconColor;
    private Color originalTextColor;
    
    private bool isHovering = false;
    private Coroutine clickCoroutine;
    
    void Start()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        
        if (icon != null)
        {
            originalIconColor = icon.color;
            icon.color = normalColor;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        // 如果有正在进行的点击动画，停止它
        if (clickCoroutine != null)
        {
            StopCoroutine(clickCoroutine);
            clickCoroutine = null;
        }
        
        ApplyHoverEffect();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        // 如果有正在进行的点击动画，停止它
        if (clickCoroutine != null)
        {
            StopCoroutine(clickCoroutine);
            clickCoroutine = null;
        }
        
        ApplyNormalEffect();
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (clickCoroutine != null)
            StopCoroutine(clickCoroutine);
        
        clickCoroutine = StartCoroutine(ClickEffect());
    }
    
    private void ApplyHoverEffect()
    {
        // 上浮
        transform.localPosition = originalPosition + Vector3.up * hoverMoveY;
        
        // 放大
        transform.localScale = originalScale * hoverScale;
        
        // 变色
        if (icon != null) icon.color = hoverColor;
    }
    
    private void ApplyNormalEffect()
    {
        // 恢复位置
        transform.localPosition = originalPosition;
        
        // 恢复大小
        transform.localScale = originalScale;
        
        // 恢复颜色
        if (icon != null) icon.color = normalColor;
    }
    
    private void ApplyClickEffect()
    {
        // 缩小
        transform.localScale = originalScale * clickScale;
        
        // 颜色变暗
        if (icon != null) icon.color = clickColor;
    }
    
    IEnumerator ClickEffect()
    {
        // 第一步：应用点击效果
        ApplyClickEffect();
        
        // 等待点击持续时间
        yield return new WaitForSecondsRealtime(clickDuration / 2);
        
        // 第二步：弹回效果
        if (isHovering)
        {
            // 如果还在悬停状态，回到悬停效果
            ApplyHoverEffect();
        }
        else
        {
            // 否则回到正常状态
            ApplyNormalEffect();
        }
        
        clickCoroutine = null;
    }
}