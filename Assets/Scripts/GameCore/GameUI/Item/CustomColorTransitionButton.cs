using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CustomColorTransitionButton : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerExitHandler,
    IPointerDownHandler
{
    [Header("颜色过渡")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.black;
    
    [Header("移动效果")]
    [SerializeField] private Vector3 hoverOffset = new Vector3(-5, 0, 0);
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float moveSpeed = 8f;
    
    [Header("点击效果")]
    [SerializeField] private float clickScale = 0.9f;
    [SerializeField] private float clickDuration = 0.1f;
    
    [Header("组件引用")]
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text text;
    
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private bool isHovering;
    private bool isAnimatingClick;
    
    void Start()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
        
        if (background != null)
            background.color = new Color(1, 1, 1, 0);
    }
    
    void Update()
    {
        // 只在没有点击动画时更新悬停效果
        if (!isAnimatingClick)
        {
            UpdateHoverEffect();
        }
    }
    
    void UpdateHoverEffect()
    {
        Vector3 targetPos = isHovering ? originalPosition + hoverOffset : originalPosition;
        Vector3 targetScale = isHovering ? originalScale * hoverScale : originalScale;
        
        transform.localPosition = Vector3.Lerp(
            transform.localPosition, 
            targetPos, 
            Time.unscaledDeltaTime * moveSpeed
        );
        
        transform.localScale = Vector3.Lerp(
            transform.localScale, 
            targetScale, 
            Time.unscaledDeltaTime * moveSpeed
        );
        
        // 平滑更新颜色
        if (text != null)
        {
            Color targetColor = isHovering ? hoverTextColor : normalTextColor;
            text.color = Color.Lerp(text.color, targetColor, Time.unscaledDeltaTime * moveSpeed);
        }
        
        if (background != null)
        {
            float targetAlpha = isHovering ? 1f : 0f;
            Color bgColor = background.color;
            bgColor.a = Mathf.Lerp(bgColor.a, targetAlpha, Time.unscaledDeltaTime * moveSpeed);
            background.color = bgColor;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // 触发点击动画
        StartCoroutine(ClickAnimation());
    }
    
    IEnumerator ClickAnimation()
    {
        isAnimatingClick = true;
        
        // 记录初始状态
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * clickScale;
        
        // 缩放缩小效果
        float elapsed = 0f;
        while (elapsed < clickDuration / 2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (clickDuration / 2);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        
        // 缩放恢复效果
        elapsed = 0f;
        while (elapsed < clickDuration / 2)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (clickDuration / 2);
            transform.localScale = Vector3.Lerp(targetScale, isHovering ? 
                originalScale * hoverScale : originalScale, t);
            yield return null;
        }
        
        isAnimatingClick = false;
    }
}
