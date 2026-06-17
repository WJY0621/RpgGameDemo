using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class MultiplayerPrefabSetup
{
    private static readonly string[] PlayerPrefabPaths =
    {
        "Assets/Resources_moved/Prefabs/Player.prefab",
        "Assets/Resources/Prefabs/Player.prefab"
    };

    [MenuItem("Tools/WorkDemo Multiplayer/Setup Player Prefab")]
    public static void SetupPlayerPrefab()
    {
        string playerPrefabPath = ResolvePlayerPrefabPath();
        if (string.IsNullOrWhiteSpace(playerPrefabPath))
        {
            Debug.LogError("[MultiplayerPrefabSetup] Player prefab was not found.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
        try
        {
            bool changed = false;

            if (prefabRoot.GetComponent<NetworkObject>() == null)
            {
                prefabRoot.AddComponent<NetworkObject>();
                changed = true;
            }

            if (prefabRoot.GetComponent<NetworkPlayer>() == null)
            {
                prefabRoot.AddComponent<NetworkPlayer>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                Debug.Log($"[MultiplayerPrefabSetup] Updated {playerPrefabPath}");
            }
            else
            {
                Debug.Log($"[MultiplayerPrefabSetup] {playerPrefabPath} is already configured.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static string ResolvePlayerPrefabPath()
    {
        for (int i = 0; i < PlayerPrefabPaths.Length; i++)
        {
            string path = PlayerPrefabPaths[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return path;
            }
        }

        string[] guids = AssetDatabase.FindAssets("Player t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.EndsWith("/Player.prefab"))
            {
                return path;
            }
        }

        return null;
    }
}
