using System;
using System.Collections.Generic;
using UnityEngine;

public enum HarvestResourceType
{
    Tree,
    IronOre,
    SilverOre,
    GoldOre
}

[Serializable]
public class HarvestDropRange
{
    public int itemId;
    public int minAmount = 1;
    public int maxAmount = 1;

    public int RollAmount()
    {
        int min = Mathf.Max(0, minAmount);
        int max = Mathf.Max(min, maxAmount);
        return max <= min ? min : UnityEngine.Random.Range(min, max + 1);
    }
}

[Serializable]
public class HarvestDropResult
{
    public int itemId;
    public int amount;

    public HarvestDropResult(int itemId, int amount)
    {
        this.itemId = itemId;
        this.amount = amount;
    }
}

[Serializable]
public class HarvestResourceData
{
    [Header("Info")]
    public string resourceId = "resource_id";
    public string displayName = "Resource";
    public HarvestResourceType resourceType;
    public string targetLayerName = "Tree";
    public ToolType requiredTool = ToolType.Axe;

    [Header("Health")]
    public int maxHealth = 30;
    [Min(1)] public int dropStageCount = 3;

    [Header("Drops")]
    public List<HarvestDropRange> drops = new List<HarvestDropRange>();

    public void ApplyDefaults()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        dropStageCount = Mathf.Max(1, dropStageCount);

        switch (resourceType)
        {
            case HarvestResourceType.Tree:
                targetLayerName = string.IsNullOrWhiteSpace(targetLayerName) ? "Tree" : targetLayerName;
                requiredTool = ToolType.Axe;
                break;
            case HarvestResourceType.IronOre:
            case HarvestResourceType.SilverOre:
            case HarvestResourceType.GoldOre:
                targetLayerName = string.IsNullOrWhiteSpace(targetLayerName) ? "Ore" : targetLayerName;
                requiredTool = ToolType.Pickaxe;
                break;
        }
    }

    public int GetDropStageIndex(int currentHealth)
    {
        int safeMaxHealth = Mathf.Max(1, maxHealth);
        int safeStageCount = Mathf.Max(1, dropStageCount);
        int clampedHealth = Mathf.Clamp(currentHealth, 0, safeMaxHealth);
        int lostHealth = safeMaxHealth - clampedHealth;

        if (lostHealth <= 0)
        {
            return 0;
        }

        float stageSize = safeMaxHealth / (float)safeStageCount;
        return Mathf.Clamp(Mathf.FloorToInt(lostHealth / stageSize), 0, safeStageCount);
    }

    public int GetPassedDropStageCount(int previousHealth, int currentHealth)
    {
        return Mathf.Max(0, GetDropStageIndex(currentHealth) - GetDropStageIndex(previousHealth));
    }

    public List<HarvestDropResult> RollDropsForPassedStages(int previousHealth, int currentHealth)
    {
        int passedStageCount = GetPassedDropStageCount(previousHealth, currentHealth);
        List<HarvestDropResult> results = new List<HarvestDropResult>();

        if (passedStageCount <= 0 || drops == null)
        {
            return results;
        }

        for (int stage = 0; stage < passedStageCount; stage++)
        {
            for (int i = 0; i < drops.Count; i++)
            {
                HarvestDropRange drop = drops[i];
                if (drop == null || drop.itemId <= 0)
                {
                    continue;
                }

                int amount = drop.RollAmount();
                if (amount > 0)
                {
                    results.Add(new HarvestDropResult(drop.itemId, amount));
                }
            }
        }

        return results;
    }
}
