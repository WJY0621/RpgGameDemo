using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RecipeItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private GameObject selectImage;
    [SerializeField] private GameObject highlightingImage;
    [SerializeField] private Image backgroundImage;

    private RecipePanel owner;
    private RecipeData recipeData;
    private bool isSelected;
    private bool isCraftable = true;

    public int RecipeId => recipeData != null ? recipeData.recipeId : -1;

    private void Awake()
    {
        CacheReferences();
    }

    public void Bind(RecipePanel recipePanel, RecipeData recipe, bool craftable)
    {
        owner = recipePanel;
        recipeData = recipe;
        isCraftable = craftable;
        CacheReferences();

        if (itemNameText != null)
        {
            itemNameText.text = GameMgr.Craft != null ? GameMgr.Craft.GetRecipeDisplayName(recipe) : string.Empty;
        }

        ApplyCraftableVisual();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        CacheReferences();

        if (selectImage != null)
        {
            selectImage.SetActive(selected);
        }

        if (highlightingImage != null && !selected)
        {
            highlightingImage.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlightingImage != null)
        {
            highlightingImage.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlightingImage != null)
        {
            highlightingImage.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner?.SelectRecipe(recipeData);
    }

    private void CacheReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (itemNameText == null)
        {
            Transform textTransform = transform.Find("ItemName");
            if (textTransform == null)
            {
                textTransform = transform.Find("TargetName");
            }

            if (textTransform != null)
            {
                itemNameText = textTransform.GetComponent<TMP_Text>();
            }
        }

        if (selectImage == null)
        {
            Transform selectTransform = transform.Find("SelectImage");
            if (selectTransform != null)
            {
                selectImage = selectTransform.gameObject;
            }
        }

        if (highlightingImage == null)
        {
            Transform highlightTransform = transform.Find("HightlightingImage");
            if (highlightTransform == null)
            {
                highlightTransform = transform.Find("HightlightImage");
            }

            if (highlightTransform == null)
            {
                highlightTransform = transform.Find("HighlightImage");
            }

            if (highlightTransform != null)
            {
                highlightingImage = highlightTransform.gameObject;
            }
        }

        if (selectImage != null && !isSelected)
        {
            selectImage.SetActive(false);
        }

        if (highlightingImage != null)
        {
            highlightingImage.SetActive(false);
        }
    }

    private void ApplyCraftableVisual()
    {
        if (backgroundImage == null)
        {
            return;
        }

        Color color = backgroundImage.color;
        color.a = isCraftable ? 1f : 0.4f;
        backgroundImage.color = color;
    }
}
