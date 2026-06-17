using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CraftMgr
{
    public event Action OnRecipeStateChanged;

    public IReadOnlyList<RecipeData> AllRecipes => RecipeJsonDatabase.Recipes.Values.OrderBy(r => r.recipeId).ToList();

    public void Init()
    {
        RecipeJsonDatabase.EnsureLoaded();
    }

    public RecipeData GetRecipe(int recipeId)
    {
        return RecipeJsonDatabase.GetRecipe(recipeId);
    }

    public bool CanCraft(int recipeId)
    {
        RecipeData recipe = GetRecipe(recipeId);
        return CanCraft(recipe);
    }

    public bool CanCraft(int recipeId, int craftCount)
    {
        RecipeData recipe = GetRecipe(recipeId);
        return CanCraft(recipe, craftCount);
    }

    public bool CanCraft(RecipeData recipe)
    {
        return CanCraft(recipe, 1);
    }

    public bool CanCraft(RecipeData recipe, int craftCount)
    {
        if (recipe == null || recipe.targetItemId <= 0 || recipe.materials == null || recipe.materials.Count == 0)
        {
            return false;
        }

        if (craftCount <= 0)
        {
            return false;
        }

        if (GameMgr.Package == null)
        {
            return false;
        }

        for (int i = 0; i < recipe.materials.Count; i++)
        {
            RecipeMaterialData material = recipe.materials[i];
            if (material == null || material.itemId <= 0 || material.count <= 0)
            {
                return false;
            }

            if (!GameMgr.Package.HasAvailableItemCount(material.itemId, material.count * craftCount))
            {
                return false;
            }
        }

        return true;
    }

    public List<RecipeData> GetCraftableRecipes()
    {
        return AllRecipes.Where(recipe => CanCraft(recipe)).ToList();
    }

    public List<RecipeData> GetUncraftableRecipes()
    {
        return AllRecipes.Where(recipe => !CanCraft(recipe)).ToList();
    }

    public async UniTask<bool> Craft(int recipeId)
    {
        return await Craft(recipeId, 1);
    }

    public async UniTask<bool> Craft(int recipeId, int craftCount)
    {
        RecipeData recipe = GetRecipe(recipeId);
        if (!CanCraft(recipe, craftCount))
        {
            return false;
        }

        for (int i = 0; i < recipe.materials.Count; i++)
        {
            RecipeMaterialData material = recipe.materials[i];
            int consumeCount = material.count * craftCount;
            if (!GameMgr.Package.RemoveAvailableItems(material.itemId, consumeCount))
            {
                Debug.LogWarning($"[CraftMgr] Failed to consume material {material.itemId} x{consumeCount} for recipe {recipe.recipeId}.");
                return false;
            }
        }

        await GameMgr.Package.AddItem(recipe.targetItemId, Mathf.Max(1, recipe.targetCount) * craftCount);
        NotifyRecipeStateChanged();
        return true;
    }

    public int GetMaxCraftCount(RecipeData recipe)
    {
        if (recipe == null || recipe.materials == null || recipe.materials.Count == 0 || GameMgr.Package == null)
        {
            return 0;
        }

        int maxCraftCount = int.MaxValue;
        for (int i = 0; i < recipe.materials.Count; i++)
        {
            RecipeMaterialData material = recipe.materials[i];
            if (material == null || material.itemId <= 0 || material.count <= 0)
            {
                return 0;
            }

            int ownedCount = GameMgr.Package.GetAvailableItemCount(material.itemId);
            maxCraftCount = Mathf.Min(maxCraftCount, ownedCount / material.count);
        }

        return maxCraftCount == int.MaxValue ? 0 : Mathf.Max(0, maxCraftCount);
    }

    public string GetRecipeDisplayName(RecipeData recipe)
    {
        if (recipe == null)
        {
            return string.Empty;
        }

        Item targetItem = ItemJsonDatabase.GetItem(recipe.targetItemId);
        if (targetItem != null && !string.IsNullOrWhiteSpace(targetItem.name))
        {
            return targetItem.name;
        }

        return string.IsNullOrWhiteSpace(recipe.recipeName) ? $"Recipe {recipe.recipeId}" : recipe.recipeName;
    }

    public void NotifyRecipeStateChanged()
    {
        OnRecipeStateChanged?.Invoke();
    }
}
