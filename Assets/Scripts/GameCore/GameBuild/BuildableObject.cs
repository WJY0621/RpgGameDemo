using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildableObject : MonoBehaviour
{
    private static readonly List<BuildableObject> ActiveBuildables = new List<BuildableObject>();

    [SerializeField] private string recipeId;
    [SerializeField] private string displayName;
    [SerializeField] private BuildCategory category;
    [SerializeField] private BuildInteractionType interactionType;
    [SerializeField] private BuildPieceType pieceType;
    [SerializeField] private float refundPercent = 0.5f;

    public BuildRecipeSO Recipe { get; private set; }
    public string RecipeId => recipeId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public BuildInteractionType InteractionType => interactionType;
    public BuildPieceType PieceType => pieceType;
    public float RefundPercent => refundPercent;
    public static IReadOnlyList<BuildableObject> ActiveObjects => ActiveBuildables;

    private void OnEnable()
    {
        if (!ActiveBuildables.Contains(this))
        {
            ActiveBuildables.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveBuildables.Remove(this);
    }

    public void Initialize(BuildRecipeSO recipe)
    {
        Recipe = recipe;
        if (recipe == null)
        {
            return;
        }

        recipeId = recipe.recipeId;
        displayName = recipe.displayName;
        category = recipe.category;
        interactionType = recipe.interactionType;
        pieceType = recipe.pieceType;
        refundPercent = recipe.refundPercent;
        gameObject.name = string.IsNullOrWhiteSpace(recipe.displayName) ? recipe.recipeId : recipe.displayName;
    }

    public void EnsureRuntimeComponents()
    {
        if (GetComponentInChildren<Collider>() == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one;
        }

        if (interactionType == BuildInteractionType.EquipmentCrafting ||
            interactionType == BuildInteractionType.ConsumableCrafting)
        {
            if (GetComponent<CraftingStationInteraction>() == null)
            {
                gameObject.AddComponent<CraftingStationInteraction>();
            }
        }
        else if (interactionType == BuildInteractionType.Bed)
        {
            if (GetComponent<BedInteraction>() == null)
            {
                gameObject.AddComponent<BedInteraction>();
            }
        }
        else if (interactionType == BuildInteractionType.Door)
        {
            if (GetComponentInChildren<DoorInteraction>() == null)
            {
                gameObject.AddComponent<DoorInteraction>();
            }
        }
    }
}
