using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RecipePanel : BasePanel
{
    private const string RecipeItemPrefabKey = "RecipeItem";

    private bool initialized;
    private bool loadingRecipeItemPrefab;

    private Transform recipeListContentRoot;
    private Transform canRecipeRoot;
    private Transform unRecipeRoot;
    private RecipeItemUI recipeItemTemplate;
    private GameObject recipeItemPrefab;
    private Transform recipeContentRoot;
    private Image targetItemIcon;
    private TMP_Text targetItemNameText;
    private Transform costContentRoot;
    private RecipeCostItemUI costItemTemplate;
    private TMP_Text numberLabelText;
    private TMP_InputField numberInputField;
    private InputField legacyNumberInputField;
    private Button upButton;
    private Button downButton;
    private Button ensureButton;
    private Button closeButton;

    private readonly List<RecipeItemUI> spawnedCanRecipeItems = new List<RecipeItemUI>();
    private readonly List<RecipeItemUI> spawnedUnRecipeItems = new List<RecipeItemUI>();
    private readonly List<RecipeCostItemUI> spawnedCostItems = new List<RecipeCostItemUI>();

    private RecipeData selectedRecipe;
    private int recipeContentRefreshVersion;
    private bool canRecipeExpanded = true;
    private bool unRecipeExpanded = true;
    private int selectedCraftCount = 1;
    private bool hasItemTypeFilter;
    private ItemType itemTypeFilter;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        EnsureRecipeItemSourceReady().Forget();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        CacheUI();
        CacheTemplate();
        CacheRecipeContentTemplate();
        BindGroupClick(canRecipeRoot, ToggleCanRecipeList);
        BindGroupClick(unRecipeRoot, ToggleUnRecipeList);
        BindButtons();
        SyncNumberField();
        EnsureRecipeItemSourceReady().Forget();
    }

    public override void Show()
    {
        base.Show();
        EnsureRecipeItemSourceReady().Forget();
    }

    public void SelectRecipe(RecipeData recipe)
    {
        selectedRecipe = recipe;
        selectedCraftCount = 1;
        SyncNumberField();
        RefreshSelectionState();
        RefreshRecipeContent().Forget();
    }

    public void ShowOnlyItemType(ItemType itemType)
    {
        hasItemTypeFilter = true;
        itemTypeFilter = itemType;
        selectedRecipe = null;
        selectedCraftCount = 1;
        SyncNumberField();
        RefreshRecipeList();
    }

    public void ClearRecipeFilter()
    {
        hasItemTypeFilter = false;
        selectedRecipe = null;
        selectedCraftCount = 1;
        SyncNumberField();
        RefreshRecipeList();
    }

    private void CacheUI()
    {
        Transform content = transform.Find("RecipeListBG/Scroll View/Viewport/Content");
        if (content == null)
        {
            return;
        }

        recipeListContentRoot = content;
        canRecipeRoot = FindDeepChild(content, "CanRecipeList");
        unRecipeRoot = FindDeepChild(content, "UnRecipeList");

        recipeContentRoot = FindDeepChild(transform, "RecipeContent");
        if (recipeContentRoot != null)
        {
            Transform targetItemRoot = FindDeepChild(recipeContentRoot, "TargetItem");
            if (targetItemRoot != null)
            {
                Transform targetIconTransform = FindDeepChild(targetItemRoot, "TargetItemIcon");
                if (targetIconTransform != null)
                {
                    targetItemIcon = targetIconTransform.GetComponent<Image>();
                }

                Transform targetNameTransform = FindDeepChild(targetItemRoot, "TargetItemName");
                if (targetNameTransform != null)
                {
                    targetItemNameText = targetNameTransform.GetComponent<TMP_Text>();
                }
            }

            costContentRoot = FindDeepChild(recipeContentRoot, "CostContent");
            Transform numberRoot = FindDeepChild(recipeContentRoot, "Number");
            if (numberRoot != null)
            {
                Transform numberFieldTransform = FindDeepChild(numberRoot, "UnmbelField");
                if (numberFieldTransform != null)
                {
                    numberLabelText = numberFieldTransform.GetComponent<TMP_Text>();
                    if (numberLabelText == null)
                    {
                        Transform textTransform = FindDeepChild(numberFieldTransform, "Text");
                        if (textTransform != null)
                        {
                            numberLabelText = textTransform.GetComponent<TMP_Text>();
                        }
                    }

                    numberInputField = numberFieldTransform.GetComponent<TMP_InputField>();
                    legacyNumberInputField = numberFieldTransform.GetComponent<InputField>();
                }

                Transform upTransform = FindDeepChild(numberRoot, "UpButton");
                if (upTransform != null)
                {
                    upButton = upTransform.GetComponent<Button>();
                }

                Transform downTransform = FindDeepChild(numberRoot, "DownButton");
                if (downTransform != null)
                {
                    downButton = downTransform.GetComponent<Button>();
                }
            }

            Transform ensureTransform = FindDeepChild(recipeContentRoot, "EnsureButton");
            if (ensureTransform != null)
            {
                ensureButton = ensureTransform.GetComponent<Button>();
            }
        }

        Transform closeTransform = FindDeepChild(transform, "CloseButton");
        if (closeTransform != null)
        {
            closeButton = closeTransform.GetComponent<Button>();
        }
    }

    private void CacheTemplate()
    {
        if (recipeListContentRoot == null)
        {
            return;
        }

        Transform templateTransform = FindDeepChild(recipeListContentRoot, "RecipeItem");
        if (templateTransform == null)
        {
            templateTransform = CreateRecipeItemTemplate(recipeListContentRoot).transform;
        }

        recipeItemTemplate = templateTransform.GetComponent<RecipeItemUI>();
        if (recipeItemTemplate == null)
        {
            recipeItemTemplate = templateTransform.gameObject.AddComponent<RecipeItemUI>();
        }

        recipeItemTemplate.gameObject.SetActive(false);
    }

    private async UniTaskVoid EnsureRecipeItemSourceReady()
    {
        if (!initialized)
        {
            return;
        }

        if (recipeItemPrefab == null && !loadingRecipeItemPrefab && GameMgr.AssetLoader != null)
        {
            loadingRecipeItemPrefab = true;
            GameObject loadedPrefab = await GameMgr.AssetLoader.LoadPrefab(RecipeItemPrefabKey);
            if (loadedPrefab != null)
            {
                recipeItemPrefab = loadedPrefab;
            }

            loadingRecipeItemPrefab = false;
        }

        if (recipeItemTemplate == null)
        {
            CacheTemplate();
        }

        RefreshRecipeList();
    }

    private static GameObject CreateRecipeItemTemplate(Transform parent)
    {
        GameObject root = new GameObject("RecipeItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(RecipeItemUI));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(214.29f, 30f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.183f, 0.191f, 0.207f, 0f);
        rootImage.raycastTarget = true;

        LayoutElement layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 214.29f;
        layoutElement.preferredHeight = 30f;

        GameObject selectImageObject = new GameObject("SelectImage", typeof(RectTransform), typeof(Image));
        selectImageObject.transform.SetParent(root.transform, false);
        RectTransform selectRect = selectImageObject.GetComponent<RectTransform>();
        selectRect.anchorMin = Vector2.zero;
        selectRect.anchorMax = Vector2.one;
        selectRect.offsetMin = Vector2.zero;
        selectRect.offsetMax = Vector2.zero;
        Image selectImage = selectImageObject.GetComponent<Image>();
        selectImage.color = new Color(1f, 1f, 1f, 0.12f);
        selectImage.raycastTarget = false;

        GameObject highlightImageObject = new GameObject("HightlightImage", typeof(RectTransform), typeof(Image));
        highlightImageObject.transform.SetParent(root.transform, false);
        RectTransform highlightRect = highlightImageObject.GetComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = Vector2.zero;
        highlightRect.offsetMax = Vector2.zero;
        Image highlightImage = highlightImageObject.GetComponent<Image>();
        highlightImage.color = new Color(1f, 1f, 1f, 0.08f);
        highlightImage.raycastTarget = false;

        GameObject textObject = new GameObject("ItemName", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "Recipe";
        text.fontSize = 20f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;

        return root;
    }

    private void CacheRecipeContentTemplate()
    {
        if (costContentRoot == null)
        {
            return;
        }

        Transform templateTransform = costContentRoot.Find("CostItem");
        if (templateTransform == null)
        {
            return;
        }

        costItemTemplate = templateTransform.GetComponent<RecipeCostItemUI>();
        if (costItemTemplate == null)
        {
            costItemTemplate = templateTransform.gameObject.AddComponent<RecipeCostItemUI>();
        }

        costItemTemplate.gameObject.SetActive(false);
    }

    private void BindButtons()
    {
        if (upButton != null)
        {
            upButton.onClick.RemoveListener(OnClickIncreaseCount);
            upButton.onClick.AddListener(OnClickIncreaseCount);
        }

        if (downButton != null)
        {
            downButton.onClick.RemoveListener(OnClickDecreaseCount);
            downButton.onClick.AddListener(OnClickDecreaseCount);
        }

        if (ensureButton != null)
        {
            ensureButton.onClick.RemoveListener(OnClickEnsureCraft);
            ensureButton.onClick.AddListener(OnClickEnsureCraft);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnClickClose);
            closeButton.onClick.AddListener(OnClickClose);
        }

        if (numberInputField != null)
        {
            numberInputField.onEndEdit.RemoveListener(OnNumberInputChanged);
            numberInputField.onEndEdit.AddListener(OnNumberInputChanged);
        }

        if (legacyNumberInputField != null)
        {
            legacyNumberInputField.onEndEdit.RemoveListener(OnNumberInputChanged);
            legacyNumberInputField.onEndEdit.AddListener(OnNumberInputChanged);
        }
    }

    private void SubscribeEvents()
    {
        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
            GameMgr.Package.OnInventoryChanged += HandleInventoryChanged;
        }

        if (GameMgr.Craft != null)
        {
            GameMgr.Craft.OnRecipeStateChanged -= HandleInventoryChanged;
            GameMgr.Craft.OnRecipeStateChanged += HandleInventoryChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= HandleInventoryChanged;
        }

        if (GameMgr.Craft != null)
        {
            GameMgr.Craft.OnRecipeStateChanged -= HandleInventoryChanged;
        }
    }

    private void HandleInventoryChanged()
    {
        RefreshRecipeList();
    }

    private void RefreshRecipeList()
    {
        if (!initialized || recipeListContentRoot == null || canRecipeRoot == null || unRecipeRoot == null || GameMgr.Craft == null)
        {
            return;
        }

        if (recipeItemPrefab == null && recipeItemTemplate == null)
        {
            return;
        }

        ClearSpawnedItems();

        List<RecipeData> matchingRecipes = GameMgr.Craft.AllRecipes
            .Where(RecipeMatchesFilter)
            .ToList();
        List<RecipeData> craftableRecipes = matchingRecipes
            .Where(recipe => GameMgr.Craft.CanCraft(recipe))
            .ToList();
        List<RecipeData> uncraftableRecipes = matchingRecipes
            .Where(recipe => !GameMgr.Craft.CanCraft(recipe))
            .ToList();

        for (int i = 0; i < craftableRecipes.Count; i++)
        {
            spawnedCanRecipeItems.Add(CreateRecipeItem(craftableRecipes[i], recipeListContentRoot, true));
        }

        for (int i = 0; i < uncraftableRecipes.Count; i++)
        {
            spawnedUnRecipeItems.Add(CreateRecipeItem(uncraftableRecipes[i], recipeListContentRoot, false));
        }

        LayoutRecipeItems();
        ApplyGroupVisibility();
        ValidateSelection(craftableRecipes, uncraftableRecipes);
        RefreshSelectionState();
        RefreshRecipeContent().Forget();
    }

    private RecipeItemUI CreateRecipeItem(RecipeData recipe, Transform parent, bool craftable)
    {
        GameObject sourceObject = recipeItemPrefab != null ? recipeItemPrefab : recipeItemTemplate.gameObject;
        GameObject itemObject = Instantiate(sourceObject, parent);
        itemObject.name = $"RecipeItem_{recipe.recipeId}";
        itemObject.SetActive(true);

        RecipeItemUI item = itemObject.GetComponent<RecipeItemUI>();
        if (item == null)
        {
            item = itemObject.AddComponent<RecipeItemUI>();
        }

        item.Bind(this, recipe, craftable);
        return item;
    }

    private void ValidateSelection(List<RecipeData> craftableRecipes, List<RecipeData> uncraftableRecipes)
    {
        if (selectedRecipe == null)
        {
            selectedRecipe = craftableRecipes.FirstOrDefault() ?? uncraftableRecipes.FirstOrDefault();
            selectedCraftCount = 1;
            return;
        }

        bool stillExists = craftableRecipes.Any(r => r.recipeId == selectedRecipe.recipeId) ||
                           uncraftableRecipes.Any(r => r.recipeId == selectedRecipe.recipeId);

        if (!stillExists)
        {
            selectedRecipe = craftableRecipes.FirstOrDefault() ?? uncraftableRecipes.FirstOrDefault();
            selectedCraftCount = 1;
        }
    }

    private bool RecipeMatchesFilter(RecipeData recipe)
    {
        if (!hasItemTypeFilter)
        {
            return true;
        }

        if (recipe == null || recipe.targetItemId <= 0)
        {
            return false;
        }

        Item targetItem = ItemJsonDatabase.GetItem(recipe.targetItemId);
        return targetItem != null && targetItem.itemType == itemTypeFilter;
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindDeepChild(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void RefreshSelectionState()
    {
        for (int i = 0; i < spawnedCanRecipeItems.Count; i++)
        {
            RecipeItemUI item = spawnedCanRecipeItems[i];
            item?.SetSelected(selectedRecipe != null && item.RecipeId == selectedRecipe.recipeId);
        }

        for (int i = 0; i < spawnedUnRecipeItems.Count; i++)
        {
            RecipeItemUI item = spawnedUnRecipeItems[i];
            item?.SetSelected(selectedRecipe != null && item.RecipeId == selectedRecipe.recipeId);
        }
    }

    private void ToggleCanRecipeList()
    {
        canRecipeExpanded = !canRecipeExpanded;
        ApplyGroupVisibility();
    }

    private void ToggleUnRecipeList()
    {
        unRecipeExpanded = !unRecipeExpanded;
        ApplyGroupVisibility();
    }

    private void ApplyGroupVisibility()
    {
        SetItemsVisible(spawnedCanRecipeItems, canRecipeExpanded);
        SetItemsVisible(spawnedUnRecipeItems, unRecipeExpanded);
    }

    private static void SetItemsVisible(List<RecipeItemUI> items, bool visible)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                items[i].gameObject.SetActive(visible);
            }
        }
    }

    private void LayoutRecipeItems()
    {
        if (recipeListContentRoot == null || canRecipeRoot == null || unRecipeRoot == null)
        {
            return;
        }

        int insertIndex = canRecipeRoot.GetSiblingIndex() + 1;
        for (int i = 0; i < spawnedCanRecipeItems.Count; i++)
        {
            RecipeItemUI item = spawnedCanRecipeItems[i];
            if (item != null)
            {
                item.transform.SetSiblingIndex(insertIndex++);
            }
        }

        insertIndex = unRecipeRoot.GetSiblingIndex() + 1;
        for (int i = 0; i < spawnedUnRecipeItems.Count; i++)
        {
            RecipeItemUI item = spawnedUnRecipeItems[i];
            if (item != null)
            {
                item.transform.SetSiblingIndex(insertIndex++);
            }
        }
    }

    private void ClearSpawnedItems()
    {
        DestroySpawnedList(spawnedCanRecipeItems);
        DestroySpawnedList(spawnedUnRecipeItems);
    }

    private void ClearSpawnedCostItems()
    {
        for (int i = 0; i < spawnedCostItems.Count; i++)
        {
            if (spawnedCostItems[i] != null)
            {
                Destroy(spawnedCostItems[i].gameObject);
            }
        }

        spawnedCostItems.Clear();
    }

    private static void DestroySpawnedList(List<RecipeItemUI> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                Destroy(list[i].gameObject);
            }
        }

        list.Clear();
    }

    private static void BindGroupClick(Transform target, System.Action callback)
    {
        if (target == null)
        {
            return;
        }

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry => entry != null && entry.eventID == EventTriggerType.PointerClick);

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        entry.callback.AddListener(_ => callback?.Invoke());
        trigger.triggers.Add(entry);
    }

    private async UniTaskVoid RefreshRecipeContent()
    {
        int refreshVersion = ++recipeContentRefreshVersion;

        if (recipeContentRoot == null)
        {
            return;
        }

        bool hasRecipe = selectedRecipe != null;
        recipeContentRoot.gameObject.SetActive(hasRecipe);

        if (!hasRecipe)
        {
            ClearSpawnedCostItems();
            return;
        }

        Item targetItem = ItemJsonDatabase.GetItem(selectedRecipe.targetItemId);
        if (targetItemNameText != null)
        {
            targetItemNameText.text = targetItem != null ? targetItem.name : GameMgr.Craft.GetRecipeDisplayName(selectedRecipe);
        }

        if (targetItemIcon != null)
        {
            Sprite targetSprite = targetItem != null ? await GameMgr.IconAtlas.GetItemIcon(targetItem) : null;
            if (refreshVersion != recipeContentRefreshVersion)
            {
                return;
            }

            targetItemIcon.sprite = targetSprite;
            targetItemIcon.enabled = targetSprite != null;
        }

        ClearSpawnedCostItems();
        await BuildCostItemList(refreshVersion);
        if (refreshVersion != recipeContentRefreshVersion)
        {
            return;
        }

        RefreshEnsureButtonState();
    }

    private async UniTask BuildCostItemList(int refreshVersion)
    {
        if (costContentRoot == null || costItemTemplate == null || selectedRecipe == null)
        {
            return;
        }

        for (int i = 0; i < selectedRecipe.materials.Count; i++)
        {
            RecipeMaterialData material = selectedRecipe.materials[i];
            if (material == null || material.itemId <= 0 || material.count <= 0)
            {
                continue;
            }

            Item materialItem = ItemJsonDatabase.GetItem(material.itemId);
            int ownedCount = GameMgr.Package != null ? GameMgr.Package.GetAvailableItemCount(material.itemId) : 0;
            int requiredCount = material.count * selectedCraftCount;
            bool hasEnoughMaterial = ownedCount >= requiredCount;
            string materialName = materialItem != null ? materialItem.name : $"Item {material.itemId}";
            string description = $"{materialName}({ownedCount}/{requiredCount})";

            Sprite materialSprite = materialItem != null ? await GameMgr.IconAtlas.GetItemIcon(materialItem) : null;
            if (refreshVersion != recipeContentRefreshVersion)
            {
                return;
            }

            GameObject costItemObject = Instantiate(costItemTemplate.gameObject, costContentRoot);
            costItemObject.name = $"CostItem_{material.itemId}";
            costItemObject.SetActive(true);

            RecipeCostItemUI costItemUI = costItemObject.GetComponent<RecipeCostItemUI>();
            if (costItemUI == null)
            {
                costItemUI = costItemObject.AddComponent<RecipeCostItemUI>();
            }

            costItemUI.Bind(materialSprite, description, hasEnoughMaterial);
            spawnedCostItems.Add(costItemUI);
        }
    }

    private void OnClickIncreaseCount()
    {
        if (selectedRecipe == null)
        {
            return;
        }

        selectedCraftCount = Mathf.Max(1, selectedCraftCount + 1);
        HandleCraftCountChanged();
    }

    private void OnClickDecreaseCount()
    {
        if (selectedRecipe == null)
        {
            return;
        }

        selectedCraftCount = Mathf.Max(1, selectedCraftCount - 1);
        HandleCraftCountChanged();
    }

    private void OnNumberInputChanged(string value)
    {
        if (selectedRecipe == null)
        {
            SyncNumberField();
            return;
        }

        if (!int.TryParse(value, out int parsedValue))
        {
            parsedValue = 1;
        }

        selectedCraftCount = Mathf.Max(1, parsedValue);
        HandleCraftCountChanged();
    }

    private void HandleCraftCountChanged()
    {
        SyncNumberField();
        RefreshRecipeContent().Forget();
    }

    private void SyncNumberField()
    {
        string displayValue = Mathf.Max(1, selectedCraftCount).ToString();

        if (numberLabelText != null)
        {
            numberLabelText.text = displayValue;
        }

        if (numberInputField != null && numberInputField.text != displayValue)
        {
            numberInputField.SetTextWithoutNotify(displayValue);
        }

        if (legacyNumberInputField != null && legacyNumberInputField.text != displayValue)
        {
            legacyNumberInputField.SetTextWithoutNotify(displayValue);
        }
    }

    private void RefreshEnsureButtonState()
    {
        if (ensureButton == null)
        {
            return;
        }

        ensureButton.interactable = selectedRecipe != null && GameMgr.Craft != null && GameMgr.Craft.CanCraft(selectedRecipe, selectedCraftCount);
    }

    private async void OnClickEnsureCraft()
    {
        if (selectedRecipe == null || GameMgr.Craft == null)
        {
            return;
        }

        Item targetItem = ItemJsonDatabase.GetItem(selectedRecipe.targetItemId);
        string targetName = targetItem != null ? targetItem.name : GameMgr.Craft.GetRecipeDisplayName(selectedRecipe);
        string tipText = $"确认要合成{targetName}x{selectedCraftCount}吗？";

        TipPanel tipPanel = await GameMgr.UI.ShowPanel<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip(tipText, () =>
        {
            CraftSelectedRecipe().Forget();
        }, () => { });
    }

    private async UniTaskVoid CraftSelectedRecipe()
    {
        if (selectedRecipe == null || GameMgr.Craft == null)
        {
            return;
        }

        bool success = await GameMgr.Craft.Craft(selectedRecipe.recipeId, selectedCraftCount);
        if (!success)
        {
            return;
        }

        int maxCraftCount = GameMgr.Craft.GetMaxCraftCount(selectedRecipe);
        selectedCraftCount = Mathf.Clamp(selectedCraftCount, 1, Mathf.Max(1, maxCraftCount));
        SyncNumberField();
        RefreshRecipeList();
    }

    private void OnClickClose()
    {
        GameMgr.UI.HidePanel<RecipePanel>();
    }
}
