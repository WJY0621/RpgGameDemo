using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class PackagePanelHoverController
{
    private readonly PackagePanel owner;

    public PackagePanelHoverController(PackagePanel ownerPanel)
    {
        owner = ownerPanel;
    }

    public async void Show(InventoryItem item, PackageItemUI itemUI)
    {
        if (item == null || itemUI == null)
        {
            return;
        }

        Item itemData = GameMgr.Package.GetItemConfig(item.itemId);
        if (itemData == null)
        {
            return;
        }

        ItemInfoPanel infoPanel = await GameMgr.UI.ShowPanel<ItemInfoPanel>();
        if (infoPanel == null)
        {
            return;
        }

        infoPanel.SetOwner(ItemInfoPanel.InfoOwner.PackageHover);
        infoPanel.UpdatePanelInfo(itemData);
        infoPanel.transform.SetAsLastSibling();
        owner.PositionItemInfoPanel(infoPanel, itemUI);
    }

    public void MaintainVisibility()
    {
        if (!owner.gameObject.activeInHierarchy || owner.IsInDeleteMode || owner.IsDragging)
        {
            return;
        }

        ItemInfoPanel infoPanel = GameMgr.UI.GetPanelWithoutLoad<ItemInfoPanel>();
        if (infoPanel == null || infoPanel.CurrentOwner != ItemInfoPanel.InfoOwner.PackageHover)
        {
            return;
        }

        if (IsPointerOverPackageItemWithItem())
        {
            return;
        }

        infoPanel.TryHideFrom(ItemInfoPanel.InfoOwner.PackageHover);
    }

    private static bool IsPointerOverPackageItemWithItem()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        for (int i = 0; i < results.Count; i++)
        {
            PackageItemUI itemUI = results[i].gameObject != null
                ? results[i].gameObject.GetComponentInParent<PackageItemUI>()
                : null;
            if (itemUI != null && itemUI.InventoryItem != null && itemUI.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }
}
