using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeCostItemUI : MonoBehaviour
{
    [SerializeField] private Image costItemIcon;
    [SerializeField] private TMP_Text costItemDesc;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color insufficientTextColor = Color.red;

    private void Awake()
    {
        CacheReferences();
    }

    public void Bind(Sprite iconSprite, string description, bool hasEnoughMaterial)
    {
        CacheReferences();

        if (costItemIcon != null)
        {
            costItemIcon.sprite = iconSprite;
            costItemIcon.enabled = iconSprite != null;
        }

        if (costItemDesc != null)
        {
            costItemDesc.text = description;
            costItemDesc.color = hasEnoughMaterial ? normalTextColor : insufficientTextColor;
        }
    }

    private void CacheReferences()
    {
        if (costItemIcon == null)
        {
            Transform iconTransform = transform.Find("CostItemIcon");
            if (iconTransform != null)
            {
                costItemIcon = iconTransform.GetComponent<Image>();
            }
        }

        if (costItemDesc == null)
        {
            Transform descTransform = transform.Find("CostItemDes");
            if (descTransform != null)
            {
                costItemDesc = descTransform.GetComponent<TMP_Text>();
            }
        }
    }
}
