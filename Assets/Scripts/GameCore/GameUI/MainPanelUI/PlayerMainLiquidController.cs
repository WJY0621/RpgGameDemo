using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainLiquidController
{
    private readonly Transform root;

    private Transform liquidRoot;
    private Transform liquidIconRoot;
    private Image liquidIconImage;
    private TMP_Text countText;
    private int itemId;
    private int iconLoadVersion;

    public PlayerMainLiquidController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init()
    {
        liquidRoot = root != null ? root.Find("LiquidContent") : null;
        if (liquidRoot == null)
        {
            return;
        }

        liquidIconRoot = liquidRoot.Find("BK/LiquidIcon") ?? liquidRoot.Find("LiquidIcon");
        if (liquidIconRoot != null)
        {
            liquidIconImage = liquidIconRoot.GetComponent<Image>();
            if (liquidIconImage == null)
            {
                liquidIconImage = liquidIconRoot.GetComponentInChildren<Image>(true);
            }
        }

        Transform textRoot = liquidRoot.Find("BK/Text (TMP)") ?? liquidRoot.Find("Text (TMP)");
        if (textRoot != null)
        {
            countText = textRoot.GetComponent<TMP_Text>();
        }

        PlayerMainLiquidSlotUI slotUI = liquidRoot.GetComponent<PlayerMainLiquidSlotUI>();
        if (slotUI == null)
        {
            slotUI = liquidRoot.gameObject.AddComponent<PlayerMainLiquidSlotUI>();
        }

        slotUI.Bind(this);
        Refresh();
    }

    public void Tick()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            TryUseCurrentConsumable();
        }
    }

    public void Dispose()
    {
    }

    public bool TryAssign(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || GameMgr.Package == null)
        {
            return false;
        }

        ConsumableItem consumable = GameMgr.Package.GetItemConfig<ConsumableItem>(inventoryItem.itemId);
        if (consumable == null)
        {
            return false;
        }

        itemId = consumable.id;
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (itemId > 0 && (GameMgr.Package == null || GameMgr.Package.GetAvailableItemCount(itemId) <= 0))
        {
            itemId = 0;
        }

        ConsumableItem consumable = itemId > 0 && GameMgr.Package != null
            ? GameMgr.Package.GetItemConfig<ConsumableItem>(itemId)
            : null;

        if (consumable == null)
        {
            SetEmpty();
            return;
        }

        int count = GameMgr.Package.GetAvailableItemCount(itemId);
        if (count <= 0)
        {
            itemId = 0;
            SetEmpty();
            return;
        }

        if (countText != null)
        {
            countText.text = count > 1 ? count.ToString() : string.Empty;
        }

        LoadIconAsync(consumable, ++iconLoadVersion);
    }

    private void TryUseCurrentConsumable()
    {
        if (itemId <= 0 || GameMgr.Package == null)
        {
            return;
        }

        if (GameMgr.Package.UseConsumable(itemId))
        {
            Refresh();
        }
        else if (GameMgr.Package.GetAvailableItemCount(itemId) <= 0)
        {
            itemId = 0;
            SetEmpty();
        }
    }

    private async void LoadIconAsync(ConsumableItem consumable, int version)
    {
        if (consumable == null || liquidIconRoot == null || liquidIconImage == null || GameMgr.IconAtlas == null)
        {
            return;
        }

        Sprite sprite = await GameMgr.IconAtlas.GetItemIcon(consumable);
        if (version != iconLoadVersion || liquidIconRoot == null || liquidIconImage == null)
        {
            return;
        }

        if (sprite != null)
        {
            liquidIconImage.sprite = sprite;
            liquidIconRoot.gameObject.SetActive(true);
        }
    }

    private void SetEmpty()
    {
        iconLoadVersion++;
        if (liquidIconRoot != null)
        {
            liquidIconRoot.gameObject.SetActive(false);
        }

        if (countText != null)
        {
            countText.text = string.Empty;
        }
    }
}

public sealed class PlayerMainLiquidSlotUI : MonoBehaviour
{
    private PlayerMainLiquidController controller;

    public void Bind(PlayerMainLiquidController owner)
    {
        controller = owner;
    }

    public bool TryAssign(InventoryItem inventoryItem)
    {
        return controller != null && controller.TryAssign(inventoryItem);
    }
}
