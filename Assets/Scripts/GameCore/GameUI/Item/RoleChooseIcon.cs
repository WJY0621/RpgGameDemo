using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RoleChooseIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Transform UIRoleIcon;
    private Transform UISelectIcon;
    private CanvasGroup canvasGroup;

    // 当前图标对应的角色数据
    private RoleData roleData;
    private string roleModelName;

    // 是否被选中
    private bool isSelected = false;

    // 悬停缩放设置
    [Header("悬停缩放设置")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float scaleSpeed = 20f;

    private Vector3 defaultScale = Vector3.one;
    private Coroutine scaleCoroutine;

    // 点击事件
    public System.Action<RoleChooseIcon> onIconClick;

    public RoleData GetRoleData() => roleData;
    public string GetRoleModelName() => roleModelName;
    public bool IsSelected() => isSelected;

    void Awake()
    {
        UIRoleIcon = transform.Find("RoleIcon");
        UISelectIcon = transform.Find("SelectIcon");
        canvasGroup = GetComponent<CanvasGroup>();

        // 如果没有 CanvasGroup，添加一个
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 默认隐藏
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        // 默认隐藏选中图标
        if (UISelectIcon != null)
            UISelectIcon.gameObject.SetActive(false);

        // 记录初始缩放
        defaultScale = transform.localScale;
    }

    /// <summary>
    /// 设置角色数据
    /// </summary>
    public void SetData(RoleData data, string modelName)
    {
        roleData = data;
        roleModelName = modelName;
    }

    /// <summary>
    /// 设置选中状态
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (UISelectIcon != null)
        {
            UISelectIcon.gameObject.SetActive(selected);
        }
    }

    // 使用 IPointerClickHandler 处理点击
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[RoleChooseIcon] OnPointerClick: {gameObject.name}");
        onIconClick?.Invoke(this);
    }

    // 指针进入
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleToTarget(defaultScale * hoverScale));
    }

    // 指针退出
    public void OnPointerExit(PointerEventData eventData)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleToTarget(defaultScale));
    }

    // 缩放动画协程
    private IEnumerator ScaleToTarget(Vector3 targetScale)
    {
        while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            yield return null;
        }
        transform.localScale = targetScale;
    }

    /// <summary>
    /// 设置图标是否可见（使用 CanvasGroup 保持可交互）
    /// </summary>
    public void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;

        // 隐藏时重置缩放
        if (!visible)
        {
            transform.localScale = defaultScale;
        }
    }
}
