using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PackageTabItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PackagePanel owner;
    [SerializeField] private ItemType itemType = ItemType.Weapon;
    [SerializeField] private GameObject selectImageObject;
    [SerializeField] private GameObject highlightImageObject;

    private bool isSelected;

    public ItemType ItemType => itemType;

    private void Awake()
    {
        FindReferences();
        DisableChildRaycasts();
        // 不在 Awake 里调用 RefreshVisual，避免覆盖 PackagePanel 已通过 SetSelected 设置的状态
    }

    /// <summary>
    /// 由 PackagePanel 在 BindClicks 时调用，设置归属和类型。
    /// </summary>
    public void Setup(PackagePanel panelOwner, ItemType type)
    {
        owner = panelOwner;
        itemType = type;
        // 以防 Awake 尚未执行（Panel 先于子节点初始化时），补充一次引用查找
        FindReferences();

        // 若 GameObject 上有 Button，关闭其默认过渡效果，避免干扰自定义视觉
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
        }
    }

    /// <summary>
    /// 由 PackagePanel.UpdateTabHighlight 调用，控制选中态视觉。
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
        {
            return;
        }

        isSelected = selected;
        RefreshVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        owner?.SelectTab(itemType);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightImageObject != null)
        {
            highlightImageObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightImageObject != null)
        {
            highlightImageObject.SetActive(false);
        }
    }

    private void RefreshVisual()
    {
        if (selectImageObject != null)
        {
            selectImageObject.SetActive(isSelected);
        }
    }

    private void FindReferences()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<PackagePanel>(true);
        }

        if (selectImageObject == null)
        {
            selectImageObject = transform.Find("SelectImage")?.gameObject;
        }

        if (highlightImageObject == null)
        {
            highlightImageObject = transform.Find("HighLightImage")?.gameObject
                ?? transform.Find("HighLightingImage")?.gameObject
                ?? transform.Find("HighlightImage")?.gameObject;
        }

        // 确保自身 Image 可以接收射线检测（接口事件的前提）
        Image image = GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
            image.color = Color.clear; // 不可见，仅用于接收点击
        }

        image.raycastTarget = true;
    }

    private void DisableChildRaycasts()
    {
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject != gameObject)
            {
                graphic.raycastTarget = false;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (selectImageObject == null)
        {
            selectImageObject = transform.Find("SelectImage")?.gameObject;
        }

        if (highlightImageObject == null)
        {
            highlightImageObject = transform.Find("HighLightImage")?.gameObject
                ?? transform.Find("HighLightingImage")?.gameObject
                ?? transform.Find("HighlightImage")?.gameObject;
        }
    }
#endif
}
