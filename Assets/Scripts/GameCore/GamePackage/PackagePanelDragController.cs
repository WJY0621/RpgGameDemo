using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PackagePanelDragController
{
    private readonly PackagePanel owner;

    private PackageItemUI draggingUI;
    private Transform draggingIcon;

    public bool IsDragging => draggingUI != null;

    public PackagePanelDragController(PackagePanel ownerPanel)
    {
        owner = ownerPanel;
    }

    public void BeginDrag(PackageItemUI ui)
    {
        if (ui == null || ui.InventoryItem == null || ui.PackageItem == null)
        {
            return;
        }

        ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
        infoPanel?.TryHideFrom(ItemInfoPanel.InfoOwner.PackageHover);

        draggingUI = ui;
        CreateDraggingIcon(ui.transform as RectTransform);
        ui.SetDraggingState(true);
    }

    public void Drag(PointerEventData eventData)
    {
        if (draggingIcon == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                draggingIcon.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector3 worldPosition))
        {
            draggingIcon.position = worldPosition;
        }
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (draggingUI == null)
        {
            ClearDraggingState();
            return;
        }

        PackageItemUI sourceUI = draggingUI;
        owner.RefreshPackageSlotVisual(sourceUI);

        if (owner.ResolveDropTargets(
                eventData,
                sourceUI,
                out PackageItemUI targetUI,
                out EquipSlotUI equipSlotUI,
                out PlayerMainLiquidSlotUI liquidSlotUI))
        {
            if (liquidSlotUI != null)
            {
                liquidSlotUI.TryAssign(sourceUI.InventoryItem);
            }
            else if (equipSlotUI != null)
            {
                EquipPanel equipPanel = GameMgr.UI.GetPanelWithoutLoad<EquipPanel>();
                equipPanel?.TryEquipItemToSlot(equipSlotUI, sourceUI.InventoryItem);
            }
            else if (targetUI != null && targetUI.ItemIndex != sourceUI.ItemIndex)
            {
                GameMgr.Package.SwapItemSlots(sourceUI.PackageItem.itemType, sourceUI.ItemIndex, targetUI.ItemIndex);
            }
        }

        owner.RefreshUi();
        ClearDraggingState();
    }

    private void CreateDraggingIcon(RectTransform sourceRect)
    {
        ClearDraggingIconOnly();

        GameObject dragObject = Object.Instantiate(sourceRect.gameObject);
        dragObject.name = "DraggingItem";

        PackageItemUI itemUI = dragObject.GetComponent<PackageItemUI>();
        if (itemUI != null)
        {
            Object.Destroy(itemUI);
        }

        Button button = dragObject.GetComponent<Button>();
        if (button != null)
        {
            Object.Destroy(button);
        }

        Canvas rootCanvas = owner.GetComponentInParent<Canvas>();
        dragObject.transform.SetParent(rootCanvas != null ? rootCanvas.transform : owner.transform, true);

        RectTransform dragRect = dragObject.GetComponent<RectTransform>();
        dragRect.anchorMin = new Vector2(0.5f, 0.5f);
        dragRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragRect.pivot = new Vector2(0.5f, 0.5f);
        dragRect.sizeDelta = sourceRect.sizeDelta;
        dragRect.position = sourceRect.position;
        dragRect.localScale = Vector3.one;

        CanvasGroup canvasGroup = dragObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = dragObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0.85f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        dragObject.transform.SetAsLastSibling();

        draggingIcon = dragObject.transform;
    }

    private void ClearDraggingIconOnly()
    {
        if (draggingIcon != null)
        {
            Object.Destroy(draggingIcon.gameObject);
            draggingIcon = null;
        }
    }

    private void ClearDraggingState()
    {
        ClearDraggingIconOnly();
        draggingUI = null;
    }
}
