using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipTipPanel : BasePanel
{
    private const string WeaponSlot1Key = "WeaponContent1";
    private const string WeaponSlot2Key = "WeaponContent2";

    private Transform mask;
    private Transform tipContent;
    private Transform weaponButton1;
    private Transform weaponButton2;
    private Transform closeButton;

    private EquipPanel equipPanel;
    private InventoryItem pendingInventoryItem;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        mask = transform.Find("Mask");
        tipContent = transform.Find("TipContent");
        weaponButton1 = transform.Find("WeaponButton1");
        weaponButton2 = transform.Find("WeaponButton2");
        closeButton = transform.Find("CloseButton");

        BindButton(weaponButton1, OnClickWeaponButton1);
        BindButton(weaponButton2, OnClickWeaponButton2);
        BindButton(closeButton, OnClickClose);
        DisableMaskClick();
    }

    public override void Show()
    {
        base.Show();
        transform.SetAsLastSibling();
    }

    public void ShowForWeaponReplacement(EquipPanel owner, InventoryItem inventoryItem)
    {
        equipPanel = owner;
        pendingInventoryItem = inventoryItem;
        SetTipContent("\u8981\u66ff\u6362\u54ea\u4ef6\u6b66\u5668");
        Show();
    }

    private static void BindButton(Transform target, UnityEngine.Events.UnityAction callback)
    {
        if (target == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private void OnClickWeaponButton1()
    {
        ReplaceWeapon(WeaponSlot1Key);
    }

    private void OnClickWeaponButton2()
    {
        ReplaceWeapon(WeaponSlot2Key);
    }

    private void OnClickClose()
    {
        ClosePanel();
    }

    private void ReplaceWeapon(string slotKey)
    {
        InventoryItem inventoryItem = pendingInventoryItem;
        EquipPanel targetEquipPanel = equipPanel != null
            ? equipPanel
            : GameMgr.UI.GetPanelWithoutLoad<EquipPanel>();

        ClearPendingItem();

        if (inventoryItem != null && targetEquipPanel != null)
        {
            targetEquipPanel.TryEquipInventoryItemToSlotKey(slotKey, inventoryItem);
        }
        else if (inventoryItem != null)
        {
            TryEquipWeaponToSlotKey(slotKey, inventoryItem);
        }

        GameMgr.UI.HidePanel<EquipTipPanel>();
    }

    private static bool TryEquipWeaponToSlotKey(string slotKey, InventoryItem inventoryItem)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || inventoryItem == null || GameMgr.Package == null)
        {
            return false;
        }

        Item config = GameMgr.Package.GetItemConfig(inventoryItem.itemId);
        if (config is not WeaponItem weaponItem || weaponItem.equipSlot != EquipmentSlot.Weapon)
        {
            return false;
        }

        GameMgr.Package.SetEquippedItem(slotKey, inventoryItem.uid);
        RefreshOpenInventoryPanels();
        return true;
    }

    private static void RefreshOpenInventoryPanels()
    {
        PackagePanel packagePanel = GameMgr.UI.GetPanelWithoutLoad<PackagePanel>();
        if (packagePanel != null && packagePanel.gameObject.activeInHierarchy)
        {
            packagePanel.RefreshUi();
        }

        EquipPanel equipPanel = GameMgr.UI.GetPanelWithoutLoad<EquipPanel>();
        if (equipPanel != null && equipPanel.gameObject.activeInHierarchy)
        {
            equipPanel.RefreshEquipSlots();
        }
    }

    private void ClosePanel()
    {
        ClearPendingItem();
        GameMgr.UI.HidePanel<EquipTipPanel>();
    }

    private void ClearPendingItem()
    {
        equipPanel = null;
        pendingInventoryItem = null;
    }

    private void DisableMaskClick()
    {
        if (mask == null)
        {
            return;
        }

        Button button = mask.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.enabled = false;
        }
    }

    private void SetTipContent(string content)
    {
        if (tipContent == null)
        {
            return;
        }

        Text legacyText = tipContent.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = content;
        }

        TMP_Text tmpText = tipContent.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = content;
        }
    }
}
