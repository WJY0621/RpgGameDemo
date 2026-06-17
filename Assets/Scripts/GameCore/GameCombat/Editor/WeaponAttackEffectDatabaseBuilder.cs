using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WeaponAttackEffectDatabaseBuilder
{
    private const string DatabasePath = "Assets/GameData/WeaponData/WeaponAttackEffectDatabase.asset";
    private const string SearchFolder = "Assets/GameData/WeaponData";

    [MenuItem("Tools/Weapon Attack Effect/Rebuild Database")]
    public static void RebuildDatabase()
    {
        WeaponAttackEffectDatabaseSO database = AssetDatabase.LoadAssetAtPath<WeaponAttackEffectDatabaseSO>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<WeaponAttackEffectDatabaseSO>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        string[] guids = AssetDatabase.FindAssets("t:WeaponAttackEffectSO", new[] { SearchFolder });
        List<WeaponAttackEffectSO> effects = new List<WeaponAttackEffectSO>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            WeaponAttackEffectSO effect = AssetDatabase.LoadAssetAtPath<WeaponAttackEffectSO>(path);
            if (effect != null)
            {
                effects.Add(effect);
            }
        }

        database.SetEffects(effects);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = database;
        Debug.Log($"[WeaponAttackEffectDatabaseBuilder] Rebuilt database with {effects.Count} weapon attack effects.");
    }
}
