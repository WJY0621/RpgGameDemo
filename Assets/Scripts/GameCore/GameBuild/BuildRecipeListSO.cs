using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildRecipeList", menuName = "Data/GameBuild/Build Recipe List")]
public class BuildRecipeListSO : ScriptableObject
{
    public List<BuildRecipeSO> recipes = new List<BuildRecipeSO>();

    private void OnValidate()
    {
        if (recipes == null)
        {
            return;
        }

        for (int i = 0; i < recipes.Count; i++)
        {
            recipes[i]?.ApplyBuildTypeDefaults();
        }
    }
}
