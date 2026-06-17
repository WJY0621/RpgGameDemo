using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class RecipeJsonDatabase
{
    private const string JsonRootRelativePath = "GameData/RecipeData";
    private const string RecipeJsonFileName = "RecipeData.json";

    private static readonly Dictionary<int, RecipeData> recipeDataById = new Dictionary<int, RecipeData>();
    private static bool isLoaded;

    public static IReadOnlyDictionary<int, RecipeData> Recipes
    {
        get
        {
            EnsureLoaded();
            return recipeDataById;
        }
    }

    public static void Reload()
    {
        isLoaded = false;
        recipeDataById.Clear();
        EnsureLoaded();
    }

    public static RecipeData GetRecipe(int recipeId)
    {
        EnsureLoaded();
        recipeDataById.TryGetValue(recipeId, out RecipeData recipe);
        return recipe;
    }

    public static void EnsureLoaded()
    {
        if (isLoaded)
        {
            return;
        }

        isLoaded = true;
        recipeDataById.Clear();

        string json = LoadJsonText(RecipeJsonFileName);
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning($"[RecipeJsonDatabase] Recipe json not found: {RecipeJsonFileName}");
            return;
        }

        RecipeJsonCollection collection = null;
        try
        {
            collection = JsonUtility.FromJson<RecipeJsonCollection>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RecipeJsonDatabase] Failed to parse recipe json.\n{ex}");
            return;
        }

        if (collection?.recipes == null)
        {
            Debug.LogWarning("[RecipeJsonDatabase] Recipe json is empty or invalid.");
            return;
        }

        for (int i = 0; i < collection.recipes.Count; i++)
        {
            RecipeData recipe = collection.recipes[i];
            if (recipe == null || recipe.recipeId <= 0)
            {
                continue;
            }

            recipe.materials ??= new List<RecipeMaterialData>();
            recipeDataById[recipe.recipeId] = recipe;
        }
    }

    private static string LoadJsonText(string fileName)
    {
        string relativePath = Path.Combine(JsonRootRelativePath, fileName).Replace('/', Path.DirectorySeparatorChar);

        // 热更目录优先（下载下来的最新配置）。
        string hotUpdateRelative = $"{JsonRootRelativePath}/{fileName}";
        if (HotUpdatePaths.TryReadText(hotUpdateRelative, out string hotText) && !string.IsNullOrWhiteSpace(hotText))
        {
            return hotText;
        }

        string assetsPath = Path.Combine(Application.dataPath, relativePath);
        if (File.Exists(assetsPath))
        {
            return File.ReadAllText(assetsPath);
        }

        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", relativePath);
        if (File.Exists(projectPath))
        {
            return File.ReadAllText(projectPath);
        }

        // 打包后上面两条文件系统路径都无效，从 Resources 读取（文件位于 Assets/Resources/GameData/RecipeData/RecipeData.json）。
        string resourcesPath = Path.Combine(JsonRootRelativePath, Path.GetFileNameWithoutExtension(fileName)).Replace('\\', '/');
        TextAsset textAsset = Resources.Load<TextAsset>(resourcesPath);
        return textAsset != null ? textAsset.text : null;
    }

    [System.Serializable]
    private class RecipeJsonCollection
    {
        public List<RecipeData> recipes = new List<RecipeData>();
    }
}
