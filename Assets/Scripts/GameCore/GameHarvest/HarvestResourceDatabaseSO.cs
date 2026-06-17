using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HarvestResourceDatabase", menuName = "Data/GameHarvest/Harvest Resource Database")]
public class HarvestResourceDatabaseSO : ScriptableObject
{
    public List<HarvestResourceData> resources = new List<HarvestResourceData>();

    public HarvestResourceData GetResource(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        return resources.Find(data => data != null && data.resourceId == resourceId);
    }

    public HarvestResourceData GetResource(HarvestResourceType resourceType)
    {
        return resources.Find(data => data != null && data.resourceType == resourceType);
    }

    [ContextMenu("Fill Default Harvest Resources")]
    public void FillDefaultHarvestResources()
    {
        resources = new List<HarvestResourceData>
        {
            CreateDefaultResource(
                "tree_common",
                "Common Tree",
                HarvestResourceType.Tree,
                "Tree",
                ToolType.Axe,
                30,
                3001,
                1,
                3),
            CreateDefaultResource(
                "ore_iron",
                "Iron Ore",
                HarvestResourceType.IronOre,
                "Ore",
                ToolType.Pickaxe,
                45,
                3003,
                1,
                2),
            CreateDefaultResource(
                "ore_silver",
                "Silver Ore",
                HarvestResourceType.SilverOre,
                "Ore",
                ToolType.Pickaxe,
                60,
                3004,
                1,
                2),
            CreateDefaultResource(
                "ore_gold",
                "Gold Ore",
                HarvestResourceType.GoldOre,
                "Ore",
                ToolType.Pickaxe,
                75,
                3005,
                1,
                2)
        };
    }

    private void OnValidate()
    {
        if (resources == null)
        {
            return;
        }

        for (int i = 0; i < resources.Count; i++)
        {
            resources[i]?.ApplyDefaults();
        }
    }

    private static HarvestResourceData CreateDefaultResource(
        string resourceId,
        string displayName,
        HarvestResourceType resourceType,
        string targetLayerName,
        ToolType requiredTool,
        int maxHealth,
        int dropItemId,
        int minDropAmount,
        int maxDropAmount)
    {
        HarvestResourceData data = new HarvestResourceData
        {
            resourceId = resourceId,
            displayName = displayName,
            resourceType = resourceType,
            targetLayerName = targetLayerName,
            requiredTool = requiredTool,
            maxHealth = maxHealth,
            dropStageCount = 3,
            drops = new List<HarvestDropRange>
            {
                new HarvestDropRange
                {
                    itemId = dropItemId,
                    minAmount = minDropAmount,
                    maxAmount = maxDropAmount
                }
            }
        };

        data.ApplyDefaults();
        return data;
    }
}
