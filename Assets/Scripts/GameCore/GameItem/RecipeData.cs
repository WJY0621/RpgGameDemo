using System;
using System.Collections.Generic;

[Serializable]
public class RecipeData
{
    public int recipeId;
    public string recipeName;
    public int targetItemId;
    public int targetCount;
    public List<RecipeMaterialData> materials = new List<RecipeMaterialData>();
}

[Serializable]
public class RecipeMaterialData
{
    public int itemId;
    public int count;
}
