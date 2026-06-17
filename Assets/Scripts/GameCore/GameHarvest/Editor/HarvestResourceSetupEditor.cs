using UnityEditor;
using UnityEngine;

public static class HarvestResourceSetupEditor
{
    [MenuItem("Tools/GameHarvest/Setup Selected As Tree")]
    private static void SetupSelectedAsTree()
    {
        SetupSelected("tree_common", HarvestResourceType.Tree, "Tree");
    }

    [MenuItem("Tools/GameHarvest/Setup Selected As Iron Ore")]
    private static void SetupSelectedAsIronOre()
    {
        SetupSelected("ore_iron", HarvestResourceType.IronOre, "Ore");
    }

    [MenuItem("Tools/GameHarvest/Setup Selected As Silver Ore")]
    private static void SetupSelectedAsSilverOre()
    {
        SetupSelected("ore_silver", HarvestResourceType.SilverOre, "Ore");
    }

    [MenuItem("Tools/GameHarvest/Setup Selected As Gold Ore")]
    private static void SetupSelectedAsGoldOre()
    {
        SetupSelected("ore_gold", HarvestResourceType.GoldOre, "Ore");
    }

    [MenuItem("Tools/GameHarvest/Apply Harvest Layers To Selected")]
    private static void ApplyHarvestLayersToSelected()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("[HarvestResourceSetupEditor] Select one or more resource roots first.");
            return;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            HarvestableResource resource = selected[i] != null ? selected[i].GetComponent<HarvestableResource>() : null;
            if (resource == null || resource.Data == null)
            {
                continue;
            }

            ApplyLayerToRootAndColliders(selected[i], resource.Data.targetLayerName);
        }
    }

    private static void SetupSelected(string resourceId, HarvestResourceType resourceType, string layerName)
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("[HarvestResourceSetupEditor] Select one or more resource roots first.");
            return;
        }

        HarvestResourceDatabaseSO database = FindFirstDatabase();
        for (int i = 0; i < selected.Length; i++)
        {
            GameObject root = selected[i];
            if (root == null)
            {
                continue;
            }

            HarvestableResource resource = root.GetComponent<HarvestableResource>();
            if (resource == null)
            {
                resource = Undo.AddComponent<HarvestableResource>(root);
            }

            SerializedObject serializedResource = new SerializedObject(resource);
            serializedResource.FindProperty("database").objectReferenceValue = database;
            serializedResource.FindProperty("resourceId").stringValue = resourceId;
            serializedResource.FindProperty("fallbackResourceType").enumValueIndex = (int)resourceType;
            serializedResource.ApplyModifiedProperties();

            ApplyLayerToRootAndColliders(root, layerName);
            EditorUtility.SetDirty(resource);
        }
    }

    private static void ApplyLayerToRootAndColliders(GameObject root, string layerName)
    {
        if (root == null || string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[HarvestResourceSetupEditor] Layer not found: {layerName}");
            return;
        }

        Undo.RecordObject(root, "Set Harvest Resource Layer");
        root.layer = layer;
        EditorUtility.SetDirty(root);

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            Undo.RecordObject(collider.gameObject, "Set Harvest Collider Layer");
            collider.gameObject.layer = layer;
            EditorUtility.SetDirty(collider.gameObject);
        }
    }

    private static HarvestResourceDatabaseSO FindFirstDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:HarvestResourceDatabaseSO");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<HarvestResourceDatabaseSO>(path);
    }
}
