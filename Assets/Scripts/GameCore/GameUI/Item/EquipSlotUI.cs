using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class EquipSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private static readonly Color CommonColor = Color.white;
    private static readonly Color AdvancedColor = new Color(0.20f, 0.80f, 0.30f, 1f);
    private static readonly Color RareColor = new Color(0.25f, 0.55f, 1f, 1f);
    private static readonly Color EpicColor = new Color(0.70f, 0.35f, 0.95f, 1f);
    private static readonly Color LegendaryColor = new Color(1f, 0.58f, 0.15f, 1f);
    private static readonly Color HoverHighlightColor = new Color(1f, 0.92f, 0.25f, 1f);

    private Image equipBG;
    private Image equipIcon;
    private Image defaultEquipIcon;
    private Image hoverImage;
    private Image fallbackPlaceholderIcon;
    private Color defaultBGColor = Color.white;
    private Color defaultHoverImageColor = Color.white;

    public EquipPanel Owner { get; private set; }
    public string SlotKey { get; private set; }
    public EquipmentSlot AcceptedSlot { get; private set; }

    public void Setup(EquipPanel owner, string slotKey, EquipmentSlot acceptedSlot)
    {
        Owner = owner;
        SlotKey = slotKey;
        AcceptedSlot = acceptedSlot;

        if (equipBG == null)
        {
            equipBG = transform.Find("EquipBG")?.GetComponent<Image>();
            equipIcon = transform.Find("EquipIcon")?.GetComponent<Image>();
            defaultEquipIcon = transform.Find("DefaultEquipIcon")?.GetComponent<Image>();
            hoverImage = transform.Find("Image")?.GetComponent<Image>();
            fallbackPlaceholderIcon = defaultEquipIcon == null ? hoverImage : null;

            if (equipBG != null)
            {
                defaultBGColor = equipBG.color;
            }

            if (hoverImage != null)
            {
                defaultHoverImageColor = hoverImage.color;
            }
        }
    }

    public bool CanEquip(Item item)
    {
        if (item is not WeaponItem weaponItem)
        {
            return false;
        }

        if (AcceptedSlot == EquipmentSlot.None)
        {
            return false;
        }

        return weaponItem.equipSlot == AcceptedSlot;
    }

    public async void Refresh()
    {
        Item equippedItem = GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedItem(SlotKey) : null;
        WeaponItem weaponItem = equippedItem as WeaponItem;

        if (equipBG == null || equipIcon == null)
        {
            Setup(Owner, SlotKey, AcceptedSlot);
        }

        if (weaponItem == null)
        {
            if (equipBG != null)
            {
                equipBG.color = defaultBGColor;
                equipBG.gameObject.SetActive(false);
            }

            if (equipIcon != null)
            {
                equipIcon.sprite = null;
                equipIcon.gameObject.SetActive(false);
            }

            SetDefaultIconVisible(true);
            SetHoverHighlight(false);

            return;
        }

        if (equipBG != null)
        {
            equipBG.gameObject.SetActive(true);
            equipBG.color = GetQualityColor(weaponItem.quality);
        }

        SetDefaultIconVisible(false);
        SetHoverHighlight(false);

        if (equipIcon != null)
        {
            Sprite icon = await GameMgr.IconAtlas.GetItemIcon(weaponItem);
            if (icon != null)
            {
                equipIcon.sprite = icon;
                equipIcon.gameObject.SetActive(true);
            }
            else
            {
                equipIcon.sprite = null;
                equipIcon.gameObject.SetActive(false);
                SetDefaultIconVisible(true);
            }
        }
    }

    public Item GetEquippedItem()
    {
        return GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedItem(SlotKey) : null;
    }

    public bool HasEquippedItem()
    {
        return GetEquippedItem() != null;
    }

    public Image GetEquipIconImage()
    {
        return equipIcon;
    }

    public void SetEquipIconVisible(bool visible)
    {
        if (equipIcon != null)
        {
            equipIcon.gameObject.SetActive(visible);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoverHighlight(true);
        Owner?.HandleSlotPointerEnter(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoverHighlight(false);
        Owner?.HandleSlotPointerExit(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Owner?.TryUnequipSlot(this);
            return;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Owner?.OnBeginDragSlot(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.OnDragSlot(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Owner?.OnEndDragSlot(eventData);
    }

    private void SetDefaultIconVisible(bool visible)
    {
        if (defaultEquipIcon != null)
        {
            defaultEquipIcon.gameObject.SetActive(visible);
        }
        else if (fallbackPlaceholderIcon != null)
        {
            fallbackPlaceholderIcon.gameObject.SetActive(visible);
        }
    }

    private void SetHoverHighlight(bool isHovering)
    {
        if (hoverImage == null)
        {
            return;
        }

        hoverImage.color = isHovering ? HoverHighlightColor : defaultHoverImageColor;
    }

    private static Color GetQualityColor(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.Common => CommonColor,
            ItemQuality.Advanced => AdvancedColor,
            ItemQuality.Rare => RareColor,
            ItemQuality.Epic => EpicColor,
            ItemQuality.Legendary => LegendaryColor,
            _ => CommonColor
        };
    }
}
