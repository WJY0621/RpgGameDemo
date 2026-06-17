using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildCategory
{
    Production,
    Structure,
    Living
}

public enum BuildInteractionType
{
    None,
    EquipmentCrafting,
    ConsumableCrafting,
    Bed,
    Door
}

public enum BuildPieceType
{
    Foundation,
    Wall,
    Board,
    CraftingStation,
    Bed
}

public enum BuildPlacementRule
{
    GroundOnly,
    FloorOnly,
    GroundOrFloor,
    SnapPointOnly
}

public enum BuildSnapPointType
{
    Foundation,
    Floor,
    Wall,
    Board,
    Furniture,
    FoundationEdge,
    WallJoint,
    WallTop,
    BoardEdge
}

public enum BuildType
{
    Foundation,
    Wall,
    Board,
    Bed,
    EquipmentStation,
    ConsumableStation,
    DoorWall
}

[Serializable]
public class BuildCost
{
    public int itemId;
    public int count = 1;
}

[Serializable]
public class BuildRecipeSO
{
    [Header("Info")]
    public string recipeId = "build_recipe";
    public string displayName = "Build Object";
    public Sprite icon;
    public BuildType buildType;

    [Header("Prefab")]
    public GameObject buildPrefab;
    public GameObject previewPrefab;

    [Header("Size")]
    public Vector3 placementSize = Vector3.one;
    public float groundEmbedDepth;

    [Header("Costs")]
    public List<BuildCost> costs = new List<BuildCost>();
    [HideInInspector] public float refundPercent = 1f;

    [HideInInspector] public BuildInteractionType interactionType;
    [HideInInspector] public BuildCategory category;
    [HideInInspector] public BuildPieceType pieceType;
    [HideInInspector] public BuildPlacementRule placementRule = BuildPlacementRule.GroundOnly;
    [HideInInspector] public Vector3 visualScale = Vector3.one;
    [HideInInspector] public float groundOffset;
    [HideInInspector] public bool requireGround = true;
    [HideInInspector] public bool snapToGrid;
    [HideInInspector] public float gridSize = 1f;
    [HideInInspector] public bool allowRotation = true;
    [HideInInspector] public bool allowHeightAdjust;
    [HideInInspector] public float heightAdjustStep = 0.25f;
    [HideInInspector] public float minHeightOffset;
    [HideInInspector] public float maxHeightOffset = 3f;
    [HideInInspector] public bool alignToGroundNormal;
    [HideInInspector] public bool preferSnapPoint;
    [HideInInspector] public BuildSnapPointType targetSnapPointType = BuildSnapPointType.Floor;
    [HideInInspector] public float snapSearchRadius = 0.75f;
    [HideInInspector] public bool useSnapPointRotation = true;
    [HideInInspector] public Vector3 snapOffset;

    public void ApplyBuildTypeDefaults()
    {
        visualScale = placementSize;
        refundPercent = 1f;
        gridSize = Mathf.Max(0.1f, gridSize);
        heightAdjustStep = Mathf.Max(0.01f, heightAdjustStep);
        groundEmbedDepth = Mathf.Max(0f, groundEmbedDepth);
        alignToGroundNormal = false;
        useSnapPointRotation = true;
        snapOffset = Vector3.zero;
        snapSearchRadius = Mathf.Max(0.1f, snapSearchRadius);

        switch (buildType)
        {
            case BuildType.Foundation:
                category = BuildCategory.Structure;
                interactionType = BuildInteractionType.None;
                pieceType = BuildPieceType.Foundation;
                placementRule = BuildPlacementRule.GroundOnly;
                requireGround = true;
                snapToGrid = true;
                allowRotation = true;
                allowHeightAdjust = true;
                groundEmbedDepth = groundEmbedDepth <= 0f ? 0.15f : groundEmbedDepth;
                minHeightOffset = -0.2f;
                maxHeightOffset = 2f;
                preferSnapPoint = true;
                targetSnapPointType = BuildSnapPointType.Foundation;
                useSnapPointRotation = false;
                break;
            case BuildType.Wall:
                category = BuildCategory.Structure;
                interactionType = BuildInteractionType.None;
                pieceType = BuildPieceType.Wall;
                placementRule = BuildPlacementRule.SnapPointOnly;
                requireGround = false;
                snapToGrid = false;
                allowRotation = false;
                allowHeightAdjust = false;
                preferSnapPoint = true;
                targetSnapPointType = BuildSnapPointType.FoundationEdge;
                break;
            case BuildType.DoorWall:
                category = BuildCategory.Structure;
                interactionType = BuildInteractionType.Door;
                pieceType = BuildPieceType.Wall;
                placementRule = BuildPlacementRule.SnapPointOnly;
                requireGround = false;
                snapToGrid = false;
                allowRotation = false;
                allowHeightAdjust = false;
                preferSnapPoint = true;
                targetSnapPointType = BuildSnapPointType.FoundationEdge;
                break;
            case BuildType.Board:
                category = BuildCategory.Structure;
                interactionType = BuildInteractionType.None;
                pieceType = BuildPieceType.Board;
                placementRule = BuildPlacementRule.SnapPointOnly;
                requireGround = false;
                snapToGrid = false;
                allowRotation = false;
                allowHeightAdjust = false;
                preferSnapPoint = true;
                targetSnapPointType = BuildSnapPointType.BoardEdge;
                break;
            case BuildType.Bed:
                category = BuildCategory.Living;
                interactionType = BuildInteractionType.Bed;
                pieceType = BuildPieceType.Bed;
                placementRule = BuildPlacementRule.FloorOnly;
                requireGround = true;
                snapToGrid = false;
                allowRotation = true;
                allowHeightAdjust = false;
                preferSnapPoint = false;
                targetSnapPointType = BuildSnapPointType.Furniture;
                break;
            case BuildType.EquipmentStation:
                category = BuildCategory.Production;
                interactionType = BuildInteractionType.EquipmentCrafting;
                pieceType = BuildPieceType.CraftingStation;
                placementRule = BuildPlacementRule.GroundOrFloor;
                requireGround = true;
                snapToGrid = false;
                allowRotation = true;
                allowHeightAdjust = false;
                preferSnapPoint = false;
                targetSnapPointType = BuildSnapPointType.Furniture;
                break;
            case BuildType.ConsumableStation:
                category = BuildCategory.Production;
                interactionType = BuildInteractionType.ConsumableCrafting;
                pieceType = BuildPieceType.CraftingStation;
                placementRule = BuildPlacementRule.GroundOrFloor;
                requireGround = true;
                snapToGrid = false;
                allowRotation = true;
                allowHeightAdjust = false;
                preferSnapPoint = false;
                targetSnapPointType = BuildSnapPointType.Furniture;
                break;
        }

        groundOffset = Mathf.Max(0f, placementSize.y * 0.5f - groundEmbedDepth);
    }
}
