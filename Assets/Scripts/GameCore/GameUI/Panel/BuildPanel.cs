using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildPanel : BasePanel
{
    private const string FallbackItemNamePrefix = "物品";
    private const string NoCostText = "无需材料";
    private const string InfiniteBuildableCountText = "还可以建造(无限)";
    private const string BuildableCountFormat = "还可以建造({0})";

    private Transform itemContentRoot;
    private GameObject itemTemplate;
    private TMP_Text buildConsumText;
    private Text legacyBuildConsumText;

    private readonly List<BuildItemView> itemViews = new List<BuildItemView>();
    private bool initialized;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        CacheReferences();
        BuildItems();
        RefreshAll();
    }

    public override void Show()
    {
        base.Show();
        RefreshAll();
    }

    private void CacheReferences()
    {
        Transform content = FindDeepChild(transform, "BuildItemContent");
        itemContentRoot = content != null ? content : transform;

        Transform templateTransform = FindDeepChild(itemContentRoot, "BuildItem");
        if (templateTransform == null)
        {
            templateTransform = FindDeepChildContains(itemContentRoot, "BuildItem");
        }

        if (templateTransform == null)
        {
            templateTransform = CreateFallbackItemTemplate(itemContentRoot).transform;
        }

        if (templateTransform != null)
        {
            itemTemplate = templateTransform.gameObject;
            itemTemplate.SetActive(false);
        }

        Transform consumTextTransform = FindDeepChild(transform, "BuildConsumText");
        if (consumTextTransform != null)
        {
            buildConsumText = consumTextTransform.GetComponent<TMP_Text>();
            legacyBuildConsumText = consumTextTransform.GetComponent<Text>();
        }
    }

    private void Subscribe()
    {
        if (GameMgr.Build != null)
        {
            GameMgr.Build.OnBuildStateChanged -= RefreshAll;
            GameMgr.Build.OnBuildStateChanged += RefreshAll;
            GameMgr.Build.OnBuildRecipeChanged -= RefreshSelection;
            GameMgr.Build.OnBuildRecipeChanged += RefreshSelection;
        }

        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= RefreshAll;
            GameMgr.Package.OnInventoryChanged += RefreshAll;
        }
    }

    private void Unsubscribe()
    {
        if (GameMgr.Build != null)
        {
            GameMgr.Build.OnBuildStateChanged -= RefreshAll;
            GameMgr.Build.OnBuildRecipeChanged -= RefreshSelection;
        }

        if (GameMgr.Package != null)
        {
            GameMgr.Package.OnInventoryChanged -= RefreshAll;
        }
    }

    private void BuildItems()
    {
        ClearSpawnedItems();

        if (itemTemplate == null)
        {
            CacheReferences();
        }

        if (itemTemplate == null || GameMgr.Build == null)
        {
            Debug.LogWarning(
                itemTemplate == null
                    ? "[BuildPanel] Build item template is missing."
                    : "[BuildPanel] GameMgr.Build is missing.",
                this);
            return;
        }

        IReadOnlyList<BuildRecipeSO> recipes = GameMgr.Build.Recipes;
        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogWarning("[BuildPanel] No build recipes are available.", this);
            return;
        }

        for (int i = 0; i < recipes.Count; i++)
        {
            BuildRecipeSO recipe = recipes[i];
            if (recipe == null)
            {
                continue;
            }

            GameObject itemObject = Instantiate(itemTemplate, itemContentRoot);
            itemObject.name = $"BuildItem_{recipe.recipeId}";
            itemObject.SetActive(true);

            BuildItemView view = new BuildItemView(itemObject, recipe, i);
            view.BindClick(OnClickItem);
            itemViews.Add(view);
        }
    }

    private void ClearSpawnedItems()
    {
        for (int i = 0; i < itemViews.Count; i++)
        {
            if (itemViews[i].Root != null)
            {
                Destroy(itemViews[i].Root);
            }
        }

        itemViews.Clear();
    }

    private void OnClickItem(int index)
    {
        GameMgr.Build?.SelectRecipeByIndex(index);
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (!initialized)
        {
            return;
        }

        if (itemViews.Count == 0)
        {
            BuildItems();
        }

        RefreshSelection();
        RefreshCostText();
    }

    private void RefreshSelection()
    {
        BuildRecipeSO selectedRecipe = GameMgr.Build != null ? GameMgr.Build.ActiveRecipe : null;
        for (int i = 0; i < itemViews.Count; i++)
        {
            itemViews[i].Refresh(selectedRecipe);
        }

        RefreshCostText();
    }

    private void RefreshCostText()
    {
        BuildRecipeSO recipe = GameMgr.Build != null ? GameMgr.Build.ActiveRecipe : null;
        string content = recipe != null ? FormatCostText(recipe) : string.Empty;

        if (buildConsumText != null)
        {
            buildConsumText.text = content;
        }

        if (legacyBuildConsumText != null)
        {
            legacyBuildConsumText.text = content;
        }
    }

    private static string FormatCostText(BuildRecipeSO recipe)
    {
        if (recipe == null)
        {
            return string.Empty;
        }

        List<string> lines = new List<string>();
        if (recipe.costs != null)
        {
            for (int i = 0; i < recipe.costs.Count; i++)
            {
                BuildCost cost = recipe.costs[i];
                if (cost == null || cost.itemId <= 0 || cost.count <= 0)
                {
                    continue;
                }

                Item item = GameMgr.Package != null ? GameMgr.Package.GetItemConfig(cost.itemId) : ItemJsonDatabase.GetItem(cost.itemId);
                string itemName = item != null && !string.IsNullOrWhiteSpace(item.name) ? item.name : $"{FallbackItemNamePrefix}{cost.itemId}";
                int ownedCount = GameMgr.Package != null ? GameMgr.Package.GetAvailableItemCount(cost.itemId) : 0;
                lines.Add($"{itemName}({ownedCount}/{cost.count})");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(NoCostText);
        }

        int buildableCount = GameMgr.Build != null ? GameMgr.Build.GetBuildableCount(recipe) : 0;
        lines.Add(buildableCount == int.MaxValue ? InfiniteBuildableCountText : string.Format(BuildableCountFormat, buildableCount));
        return string.Join("\n", lines);
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == targetName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static Transform FindDeepChildContains(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name.Contains(targetName))
            {
                return children[i];
            }
        }

        return null;
    }

    private static GameObject CreateFallbackItemTemplate(Transform parent)
    {
        GameObject item = new GameObject("BuildItem");
        item.transform.SetParent(parent, false);

        RectTransform rectTransform = item.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(160f, 40f);

        Image background = item.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject nameObject = new GameObject("BuildItemName");
        nameObject.transform.SetParent(item.transform, false);
        RectTransform nameRect = nameObject.AddComponent<RectTransform>();
        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(34f, 4f);
        nameRect.offsetMax = new Vector2(-8f, -4f);
        TMP_Text nameText = nameObject.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 18f;
        nameText.color = Color.white;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject numberObject = new GameObject("BuildItemNumbel");
        numberObject.transform.SetParent(item.transform, false);
        RectTransform numberRect = numberObject.AddComponent<RectTransform>();
        numberRect.anchorMin = new Vector2(0f, 0f);
        numberRect.anchorMax = new Vector2(0f, 1f);
        numberRect.sizeDelta = new Vector2(28f, 0f);
        numberRect.anchoredPosition = Vector2.zero;
        TMP_Text numberText = numberObject.AddComponent<TextMeshProUGUI>();
        numberText.fontSize = 16f;
        numberText.color = Color.white;
        numberText.alignment = TextAlignmentOptions.Center;

        GameObject selectObject = new GameObject("SelectImage");
        selectObject.transform.SetParent(item.transform, false);
        RectTransform selectRect = selectObject.AddComponent<RectTransform>();
        selectRect.anchorMin = Vector2.zero;
        selectRect.anchorMax = Vector2.one;
        selectRect.offsetMin = Vector2.zero;
        selectRect.offsetMax = Vector2.zero;
        Image selectImage = selectObject.AddComponent<Image>();
        selectImage.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        selectObject.SetActive(false);

        return item;
    }

    private sealed class BuildItemView
    {
        public GameObject Root { get; }

        private readonly BuildRecipeSO recipe;
        private readonly int index;
        private readonly Image icon;
        private readonly TMP_Text nameText;
        private readonly Text legacyNameText;
        private readonly TMP_Text numberText;
        private readonly Text legacyNumberText;
        private readonly GameObject selectImage;

        public BuildItemView(GameObject root, BuildRecipeSO recipe, int index)
        {
            Root = root;
            this.recipe = recipe;
            this.index = index;

            Transform iconTransform = FindDeepChild(root.transform, "BuildItemIcon");
            if (iconTransform != null)
            {
                icon = iconTransform.GetComponent<Image>();
            }

            Transform nameTransform = FindDeepChild(root.transform, "BuildItemName");
            if (nameTransform != null)
            {
                nameText = nameTransform.GetComponent<TMP_Text>();
                legacyNameText = nameTransform.GetComponent<Text>();
            }

            Transform numberTransform = FindDeepChild(root.transform, "BuildItemNumbel");
            if (numberTransform != null)
            {
                numberText = numberTransform.GetComponent<TMP_Text>();
                legacyNumberText = numberTransform.GetComponent<Text>();
            }

            Transform selectTransform = FindDeepChild(root.transform, "SelectImage");
            selectImage = selectTransform != null ? selectTransform.gameObject : null;

            SetText(nameText, legacyNameText, recipe != null ? recipe.displayName : string.Empty);
            SetText(numberText, legacyNumberText, (index + 1).ToString());

            if (icon != null)
            {
                icon.sprite = recipe != null ? recipe.icon : null;
                icon.enabled = icon.sprite != null;
            }
        }

        public void BindClick(System.Action<int> onClick)
        {
            EventTrigger trigger = Root.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = Root.AddComponent<EventTrigger>();
            }

            trigger.triggers.RemoveAll(entry => entry != null && entry.eventID == EventTriggerType.PointerClick);
            EventTrigger.Entry clickEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            clickEntry.callback.AddListener(_ => onClick?.Invoke(index));
            trigger.triggers.Add(clickEntry);
        }

        public void Refresh(BuildRecipeSO selectedRecipe)
        {
            bool selected = selectedRecipe != null && recipe == selectedRecipe;
            if (selectImage != null)
            {
                selectImage.SetActive(selected);
            }
        }

        private static void SetText(TMP_Text tmpText, Text legacyText, string value)
        {
            if (tmpText != null)
            {
                tmpText.text = value;
            }

            if (legacyText != null)
            {
                legacyText.text = value;
            }
        }
    }
}
