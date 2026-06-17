using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BuildManager
{
    private const string BuildLayerName = "Build";
    private const string BuildSoundGroup = "Game";
    private const string BuildPlaceOrDemolishSound = "WoodHit2";

    public static BuildManager Instance => GameMgr.Build;

    [Header("Recipes")]
    private BuildRecipeListSO recipeList;
    private string recipeListAddress = "BuildRecipeList";
    private List<BuildRecipeSO> recipes = new List<BuildRecipeSO>();
    private bool createRuntimeDefaultRecipes = true;

    [Header("Placement")]
    private Camera placementCamera;
    private Transform placedRoot;
    private LayerMask groundLayer = ~0;
    private LayerMask blockingLayer = ~0;
    private int buildLayer = -1;
    private float maxPlacementDistance = 10f;
    private float rotateStep = 45f;
    private Material validPreviewMaterial;
    private Material invalidPreviewMaterial;

    [Header("Input")]
    private KeyCode toggleBuildMenuKey = KeyCode.T;
    private KeyCode cancelKey = KeyCode.Escape;
    private KeyCode rotateKey = KeyCode.R;
    private KeyCode heightUpKey = KeyCode.E;
    private KeyCode heightDownKey = KeyCode.Q;
    private KeyCode demolishModeKey = KeyCode.Y;
    private KeyCode placeKey = KeyCode.Mouse0;

    private readonly BuildPreview preview = new BuildPreview();
    private readonly List<RendererMaterialState> demolishPreviewStates = new List<RendererMaterialState>();
    private BuildRecipeSO activeRecipe;
    private BuildPlacementResult lastPlacement;
    private BuildableObject highlightedDemolishTarget;
    private Material demolishPreviewMaterial;
    private float yaw;
    private float heightOffset;
    private bool buildMenuOpen;
    private bool demolishMode;
    private bool hiddenPlayerMainPanelForBuild;
    private PlayerMainPanel hiddenPlayerMainPanelInstance;

    public IReadOnlyList<BuildRecipeSO> Recipes => ActiveRecipes;
    public BuildRecipeSO ActiveRecipe => activeRecipe;
    public bool IsBuildMenuOpen => buildMenuOpen;
    public bool IsDemolishMode => demolishMode;

    public event Action OnBuildStateChanged;
    public event Action OnBuildRecipeChanged;

    public BuildManager()
    {
        ConfigureLayerMasks();

        if (placedRoot == null)
        {
            GameObject root = new GameObject("PlacedBuildObjects");
            placedRoot = root.transform;
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        LoadRecipeListAsync().Forget();
    }

    private void ConfigureLayerMasks()
    {
        buildLayer = LayerMask.NameToLayer(BuildLayerName);
        if (buildLayer < 0)
        {
            Debug.LogWarning($"[BuildManager] Layer '{BuildLayerName}' was not found. Build objects will still be checked as blockers.");
            return;
        }

        blockingLayer &= ~(1 << buildLayer);
    }

    public void Dispose()
    {
        ClearDemolishPreview();
        preview.Dispose();

        if (demolishPreviewMaterial != null)
        {
            UnityEngine.Object.Destroy(demolishPreviewMaterial);
            demolishPreviewMaterial = null;
        }
    }

    private async UniTaskVoid LoadRecipeListAsync()
    {
        if (recipeList == null && GameMgr.AssetLoader != null && !string.IsNullOrWhiteSpace(recipeListAddress))
        {
            recipeList = await GameMgr.AssetLoader.LoadAsset<BuildRecipeListSO>(recipeListAddress);
            ApplyRecipeDefaults(recipeList != null ? recipeList.recipes : recipes);
            OnBuildStateChanged?.Invoke();
        }

        if (recipeList == null && recipes.Count == 0 && createRuntimeDefaultRecipes)
        {
            CreateDefaultRecipes();
            OnBuildStateChanged?.Invoke();
        }
    }

    public void Tick()
    {
        if (Input.GetKeyDown(toggleBuildMenuKey))
        {
            ToggleBuildMenu();
        }

        if (Input.GetKeyDown(demolishModeKey))
        {
            SetDemolishMode(true);
        }

        if (!buildMenuOpen && !demolishMode)
        {
            return;
        }

        HandleRecipeHotkeys();

        if (Input.GetKeyDown(cancelKey))
        {
            SetBuildMenuOpen(false);
            return;
        }

        if (demolishMode)
        {
            UpdateDemolishPreview();
            HandleDemolishInput();
            return;
        }

        if (activeRecipe == null)
        {
            return;
        }

        if (activeRecipe.allowRotation && Input.GetKeyDown(rotateKey))
        {
            yaw += rotateStep;
        }

        if (activeRecipe.allowHeightAdjust)
        {
            if (Input.GetKeyDown(heightUpKey))
            {
                heightOffset = Mathf.Clamp(heightOffset + activeRecipe.heightAdjustStep, activeRecipe.minHeightOffset, activeRecipe.maxHeightOffset);
            }
            else if (Input.GetKeyDown(heightDownKey))
            {
                heightOffset = Mathf.Clamp(heightOffset - activeRecipe.heightAdjustStep, activeRecipe.minHeightOffset, activeRecipe.maxHeightOffset);
            }
        }

        UpdatePreview();

        if (Input.GetKeyDown(placeKey))
        {
            TryPlaceActiveRecipe();
        }
    }

    public void StartBuilding(BuildRecipeSO recipe)
    {
        if (recipe == null)
        {
            return;
        }

        recipe.ApplyBuildTypeDefaults();
        activeRecipe = recipe;
        buildMenuOpen = true;
        demolishMode = false;
        ClearDemolishPreview();
        yaw = 0f;
        heightOffset = 0f;
        BuildPlacementValidator.ResetSnapLock();
        OpenBuildPanel();
        EnterPlayerBuildPose();
        preview.Create(activeRecipe, validPreviewMaterial, invalidPreviewMaterial);
        ShowMessage($"Selected build: {activeRecipe.displayName}");
        NotifyBuildStateChanged();
        OnBuildRecipeChanged?.Invoke();
    }

    public void CancelBuild()
    {
        activeRecipe = null;
        heightOffset = 0f;
        BuildPlacementValidator.ResetSnapLock();
        preview.Dispose();
        OnBuildRecipeChanged?.Invoke();
    }

    public void ToggleBuildMenu()
    {
        SetBuildMenuOpen(!buildMenuOpen || demolishMode);
    }

    public void SetBuildMenuOpen(bool open)
    {
        buildMenuOpen = open;
        demolishMode = false;
        ClearDemolishPreview();
        if (!buildMenuOpen)
        {
            CancelBuild();
            CloseBuildPanel();
        }
        else
        {
            OpenBuildPanel();
            EnterPlayerBuildPose();
            ShowMessage("Build mode: press 1-9 to select, R rotate, left click place, Y demolish.");
        }

        NotifyBuildStateChanged();
    }

    public void SetDemolishMode(bool open)
    {
        demolishMode = open;
        buildMenuOpen = open;
        ClearDemolishPreview();
        CancelBuild();

        if (open)
        {
            OpenDemolishPanel();
            EnterPlayerBuildPose();
            ShowMessage("Demolish mode enabled.");
        }
        else
        {
            CloseBuildPanel();
            ShowMessage("Demolish mode disabled.");
        }

        NotifyBuildStateChanged();
    }

    public void SelectRecipeByIndex(int index)
    {
        IReadOnlyList<BuildRecipeSO> activeRecipes = ActiveRecipes;
        if (index < 0 || index >= activeRecipes.Count)
        {
            return;
        }

        StartBuilding(activeRecipes[index]);
    }

    public int GetRecipeIndex(BuildRecipeSO recipe)
    {
        IReadOnlyList<BuildRecipeSO> activeRecipes = ActiveRecipes;
        for (int i = 0; i < activeRecipes.Count; i++)
        {
            if (activeRecipes[i] == recipe)
            {
                return i;
            }
        }

        return -1;
    }

    public bool CanBuild(BuildRecipeSO recipe)
    {
        return CanPayCost(recipe);
    }

    public int GetBuildableCount(BuildRecipeSO recipe)
    {
        if (recipe == null || recipe.costs == null || recipe.costs.Count == 0)
        {
            return int.MaxValue;
        }

        if (GameMgr.Package == null)
        {
            return 0;
        }

        int count = int.MaxValue;
        for (int i = 0; i < recipe.costs.Count; i++)
        {
            BuildCost cost = recipe.costs[i];
            if (cost == null || cost.itemId <= 0 || cost.count <= 0)
            {
                continue;
            }

            int ownedCount = GameMgr.Package.GetAvailableItemCount(cost.itemId);
            count = Mathf.Min(count, ownedCount / cost.count);
        }

        return count == int.MaxValue ? int.MaxValue : Mathf.Max(0, count);
    }

    private void HandleRecipeHotkeys()
    {
        IReadOnlyList<BuildRecipeSO> activeRecipes = ActiveRecipes;
        for (int i = 0; i < activeRecipes.Count && i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;
            if (Input.GetKeyDown(key))
            {
                StartBuilding(activeRecipes[i]);
                return;
            }
        }
    }

    private void UpdatePreview()
    {
        if (!preview.HasPreview)
        {
            preview.Create(activeRecipe, validPreviewMaterial, invalidPreviewMaterial);
        }

        Ray ray = GetPlacementRay();
        lastPlacement = BuildPlacementValidator.Validate(
            activeRecipe,
            ray,
            maxPlacementDistance,
            groundLayer,
            blockingLayer,
            yaw,
            activeRecipe.allowHeightAdjust ? heightOffset : 0f,
            preview.Transform,
            preview.Colliders,
            preview.Anchors);

        preview.SetVisible(lastPlacement.showPreview);
        if (lastPlacement.showPreview)
        {
            preview.SetPose(lastPlacement.position, lastPlacement.rotation);
            preview.SetValid(lastPlacement.isValid && CanPayCost(activeRecipe));
        }
    }

    private void TryPlaceActiveRecipe()
    {
        if (!lastPlacement.isValid)
        {
            ShowMessage(string.IsNullOrWhiteSpace(lastPlacement.reason) ? "Cannot build here." : lastPlacement.reason);
            return;
        }

        if (!TryPayCost(activeRecipe))
        {
            ShowMessage("Not enough materials.");
            return;
        }

        GameObject placedObject = CreatePlacedObject(activeRecipe);
        placedObject.transform.SetParent(placedRoot, true);
        placedObject.transform.SetPositionAndRotation(lastPlacement.position, lastPlacement.rotation);

        BuildableObject buildable = placedObject.GetComponent<BuildableObject>();
        if (buildable == null)
        {
            buildable = placedObject.AddComponent<BuildableObject>();
        }

        buildable.Initialize(activeRecipe);
        buildable.EnsureRuntimeComponents();
        PlayPlayerBuildAction();
        PlayBuildSound(placedObject.transform.position);
        ShowMessage($"Built {activeRecipe.displayName}.");
        NotifyBuildStateChanged();
    }

    private GameObject CreatePlacedObject(BuildRecipeSO recipe)
    {
        if (recipe != null && recipe.buildPrefab != null)
        {
            return UnityEngine.Object.Instantiate(recipe.buildPrefab);
        }

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.transform.localScale = recipe != null ? recipe.visualScale : Vector3.one;
        return fallback;
    }

    private void HandleDemolishInput()
    {
        if (!Input.GetKeyDown(placeKey))
        {
            return;
        }

        BuildableObject buildable = highlightedDemolishTarget != null ? highlightedDemolishTarget : FindDemolishTarget();
        if (buildable == null)
        {
            ShowMessage("No build object selected.");
            return;
        }

        ClearDemolishPreview();
        Vector3 demolishPosition = buildable.transform.position;
        Refund(buildable);
        BedInteraction bedInteraction = buildable.GetComponentInChildren<BedInteraction>();
        bedInteraction?.NotifyDemolished();
        UnityEngine.Object.Destroy(buildable.gameObject);
        PlayBuildSound(demolishPosition);
        ShowMessage($"Demolished {buildable.DisplayName}.");
        NotifyBuildStateChanged();
    }

    private void UpdateDemolishPreview()
    {
        SetDemolishPreviewTarget(FindDemolishTarget());
    }

    private BuildableObject FindDemolishTarget()
    {
        Ray ray = GetPlacementRay();
        if (!Physics.Raycast(ray, out RaycastHit hit, maxPlacementDistance, GetDemolishLayerMask(), QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<BuildableObject>();
    }

    private void SetDemolishPreviewTarget(BuildableObject target)
    {
        if (highlightedDemolishTarget == target)
        {
            return;
        }

        ClearDemolishPreview();
        highlightedDemolishTarget = target;

        if (highlightedDemolishTarget == null)
        {
            return;
        }

        Material material = GetDemolishPreviewMaterial();
        Renderer[] targetRenderers = highlightedDemolishTarget.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Material[] originalMaterials = targetRenderer.sharedMaterials;
            demolishPreviewStates.Add(new RendererMaterialState(targetRenderer, originalMaterials));

            Material[] previewMaterials = new Material[originalMaterials.Length];
            for (int j = 0; j < previewMaterials.Length; j++)
            {
                previewMaterials[j] = material;
            }

            targetRenderer.sharedMaterials = previewMaterials;
        }
    }

    private void ClearDemolishPreview()
    {
        for (int i = 0; i < demolishPreviewStates.Count; i++)
        {
            RendererMaterialState state = demolishPreviewStates[i];
            if (state.Renderer != null)
            {
                state.Renderer.sharedMaterials = state.Materials;
            }
        }

        demolishPreviewStates.Clear();
        highlightedDemolishTarget = null;
    }

    private Material GetDemolishPreviewMaterial()
    {
        if (demolishPreviewMaterial == null)
        {
            demolishPreviewMaterial = BuildPreview.CreatePreviewMaterial(new Color(1f, 0.08f, 0.04f, 0.38f));
        }

        return demolishPreviewMaterial;
    }

    private LayerMask GetDemolishLayerMask()
    {
        if (buildLayer < 0)
        {
            return blockingLayer;
        }

        return blockingLayer | (1 << buildLayer);
    }

    private Ray GetPlacementRay()
    {
        Camera cam = placementCamera != null ? placementCamera : Camera.main;
        if (cam == null)
        {
            Vector3 fallbackOrigin = GameMgr.Instance != null && GameMgr.Instance.Player != null
                ? GameMgr.Instance.Player.transform.position + Vector3.up
                : Vector3.up;
            Vector3 fallbackDirection = GameMgr.Instance != null && GameMgr.Instance.Player != null
                ? GameMgr.Instance.Player.transform.forward
                : Vector3.forward;
            return new Ray(fallbackOrigin, fallbackDirection);
        }

        Vector3 mousePosition = Input.mousePosition;
        if (mousePosition == Vector3.zero)
        {
            mousePosition = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        return cam.ScreenPointToRay(mousePosition);
    }

    private bool CanPayCost(BuildRecipeSO recipe)
    {
        recipe?.ApplyBuildTypeDefaults();
        if (recipe == null || recipe.costs == null || recipe.costs.Count == 0)
        {
            return true;
        }

        if (GameMgr.Package == null)
        {
            return false;
        }

        for (int i = 0; i < recipe.costs.Count; i++)
        {
            BuildCost cost = recipe.costs[i];
            if (cost == null || cost.itemId <= 0 || cost.count <= 0)
            {
                continue;
            }

            if (!GameMgr.Package.HasAvailableItemCount(cost.itemId, cost.count))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryPayCost(BuildRecipeSO recipe)
    {
        if (!CanPayCost(recipe))
        {
            return false;
        }

        if (recipe == null || recipe.costs == null || recipe.costs.Count == 0 || GameMgr.Package == null)
        {
            return true;
        }

        for (int i = 0; i < recipe.costs.Count; i++)
        {
            BuildCost cost = recipe.costs[i];
            if (cost == null || cost.itemId <= 0 || cost.count <= 0)
            {
                continue;
            }

            if (!GameMgr.Package.RemoveAvailableItems(cost.itemId, cost.count))
            {
                return false;
            }
        }

        return true;
    }

    private void Refund(BuildableObject buildable)
    {
        BuildRecipeSO recipe = buildable != null ? buildable.Recipe : null;
        if (recipe == null || recipe.costs == null || GameMgr.Package == null)
        {
            return;
        }

        for (int i = 0; i < recipe.costs.Count; i++)
        {
            BuildCost cost = recipe.costs[i];
            if (cost == null || cost.itemId <= 0 || cost.count <= 0)
            {
                continue;
            }

            int refundCount = Mathf.FloorToInt(cost.count * buildable.RefundPercent);
            if (refundCount > 0)
            {
                GameMgr.Package.AddItem(cost.itemId, refundCount).Forget();
            }
        }
    }

    private void CreateDefaultRecipes()
    {
        recipes.Add(CreateRuntimeRecipe("foundation", "Foundation", BuildType.Foundation, new Vector3(2f, 0.4f, 2f)));
        recipes.Add(CreateRuntimeRecipe("wood_wall", "Wood Wall", BuildType.Wall, new Vector3(2f, 2f, 0.2f)));
        recipes.Add(CreateRuntimeRecipe("board", "Board", BuildType.Board, new Vector3(2f, 0.2f, 2f)));
        recipes.Add(CreateRuntimeRecipe("equipment_station", "Equipment Crafting Table", BuildType.EquipmentStation, new Vector3(1.8f, 1.2f, 1.2f)));
        recipes.Add(CreateRuntimeRecipe("consumable_station", "Consumable Crafting Table", BuildType.ConsumableStation, new Vector3(1.8f, 1.2f, 1.2f)));
        recipes.Add(CreateRuntimeRecipe("bed", "Bed", BuildType.Bed, new Vector3(1.2f, 0.6f, 2f)));
    }

    private static BuildRecipeSO CreateRuntimeRecipe(
        string id,
        string displayName,
        BuildType buildType,
        Vector3 size)
    {
        BuildRecipeSO recipe = new BuildRecipeSO();
        recipe.recipeId = id;
        recipe.displayName = displayName;
        recipe.buildType = buildType;
        recipe.placementSize = size;
        recipe.ApplyBuildTypeDefaults();
        return recipe;
    }

    private static void ApplyRecipeDefaults(IReadOnlyList<BuildRecipeSO> targetRecipes)
    {
        if (targetRecipes == null)
        {
            return;
        }

        for (int i = 0; i < targetRecipes.Count; i++)
        {
            targetRecipes[i]?.ApplyBuildTypeDefaults();
        }
    }

    private static void ShowMessage(string message)
    {
        Debug.Log($"[BuildManager] {message}");
        if (GameMgr.Message != null)
        {
            GameMgr.Message.RegisterMessage(message);
        }
        else
        {
            Debug.Log(message);
        }
    }

    private void OpenBuildPanel()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        HidePlayerMainPanelForBuild();

        GameMgr.UI.HidePanel<UnBuildPanel>();
        GameMgr.UI.ShowPanel<BuildPanel>().Forget();
    }

    private void OpenDemolishPanel()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        HidePlayerMainPanelForBuild();

        GameMgr.UI.HidePanel<BuildPanel>();
        GameMgr.UI.ShowPanel<UnBuildPanel>().Forget();
    }

    private void CloseBuildPanel()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        GameMgr.UI.HidePanel<BuildPanel>();
        GameMgr.UI.HidePanel<UnBuildPanel>();

        RestorePlayerMainPanelAfterBuild();
    }

    private void HidePlayerMainPanelForBuild()
    {
        if (hiddenPlayerMainPanelForBuild)
        {
            return;
        }

        PlayerMainPanel panel = GameMgr.UI != null ? GameMgr.UI.GetPanelWithoutLoad<PlayerMainPanel>() : null;
        if (panel == null)
        {
            panel = UnityEngine.Object.FindFirstObjectByType<PlayerMainPanel>();
        }

        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            return;
        }

        hiddenPlayerMainPanelForBuild = true;
        hiddenPlayerMainPanelInstance = panel;

        panel.gameObject.SetActive(false);
    }

    private void RestorePlayerMainPanelAfterBuild()
    {
        if (!hiddenPlayerMainPanelForBuild)
        {
            return;
        }

        hiddenPlayerMainPanelForBuild = false;

        if (hiddenPlayerMainPanelInstance != null)
        {
            hiddenPlayerMainPanelInstance.gameObject.SetActive(true);
            hiddenPlayerMainPanelInstance.Show();
            hiddenPlayerMainPanelInstance = null;
            return;
        }

        GameMgr.UI?.ShowPanel<PlayerMainPanel>().Forget();
    }

    private static void EnterPlayerBuildPose()
    {
        PlayerWeaponModeController weaponModeController = GetPlayerWeaponModeController();
        weaponModeController?.EnterUnarmedMode();
    }

    private static void PlayPlayerBuildAction()
    {
        PlayerWeaponModeController weaponModeController = GetPlayerWeaponModeController();
        weaponModeController?.PlayBuildAction();
    }

    private static void PlayBuildSound(Vector3 position)
    {
        GameMgr.Audio?.PlayAt(BuildSoundGroup, BuildPlaceOrDemolishSound, position);
    }

    private static PlayerWeaponModeController GetPlayerWeaponModeController()
    {
        return GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.GetComponent<PlayerWeaponModeController>()
            : null;
    }

    private void NotifyBuildStateChanged()
    {
        OnBuildStateChanged?.Invoke();
    }

    private readonly struct RendererMaterialState
    {
        public RendererMaterialState(Renderer renderer, Material[] materials)
        {
            Renderer = renderer;
            Materials = materials;
        }

        public Renderer Renderer { get; }
        public Material[] Materials { get; }
    }

    private IReadOnlyList<BuildRecipeSO> ActiveRecipes
    {
        get
        {
            if (recipeList != null && recipeList.recipes != null)
            {
                return recipeList.recipes;
            }

            return recipes;
        }
    }
}
