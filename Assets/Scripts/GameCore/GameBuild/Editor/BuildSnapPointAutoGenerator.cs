using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildSnapPointAutoGenerator
{
    private const string AutoRootName = "AutoSnapPoints";
    private const string AutoPrefix = "AutoSnap_";

    [MenuItem("Tools/Game Build/Snap Points/Auto Infer For Selected")]
    private static void AutoInferForSelected()
    {
        GenerateForSelected(null);
    }

    [MenuItem("Tools/Game Build/Snap Points/Generate Foundation For Selected")]
    private static void GenerateFoundationForSelected()
    {
        GenerateForSelected(BuildType.Foundation);
    }

    [MenuItem("Tools/Game Build/Snap Points/Generate Wall For Selected")]
    private static void GenerateWallForSelected()
    {
        GenerateForSelected(BuildType.Wall);
    }

    [MenuItem("Tools/Game Build/Snap Points/Generate Board For Selected")]
    private static void GenerateBoardForSelected()
    {
        GenerateForSelected(BuildType.Board);
    }

    private static void GenerateForSelected(BuildType? forcedType)
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[BuildSnapPointAutoGenerator] Select one or more prefab assets or scene objects first.");
            return;
        }

        int changedCount = 0;
        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            GameObject selected = Selection.gameObjects[i];
            if (selected == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrWhiteSpace(assetPath) && PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    if (GenerateOnRoot(prefabRoot, forcedType))
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                        changedCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(selected, "Generate Build Snap Points");
            if (GenerateOnRoot(selected, forcedType))
            {
                EditorUtility.SetDirty(selected);
                changedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BuildSnapPointAutoGenerator] Generated snap points for {changedCount} object(s).");
    }

    private static bool GenerateOnRoot(GameObject root, BuildType? forcedType)
    {
        if (root == null)
        {
            return false;
        }

        BuildType buildType = forcedType ?? InferBuildType(root);
        if (!TryCalculateLocalBounds(root.transform, out Bounds bounds))
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
        }

        RemoveExistingSnapPoints(root.transform);
        Transform autoRoot = CreateAutoSnapRoot(root.transform);

        switch (buildType)
        {
            case BuildType.Foundation:
                GenerateFoundation(autoRoot, bounds);
                return true;
            case BuildType.Wall:
                GenerateWall(autoRoot, bounds);
                return true;
            case BuildType.Board:
                GenerateBoard(autoRoot, bounds);
                return true;
            default:
                Debug.LogWarning($"[BuildSnapPointAutoGenerator] {root.name}: cannot auto-generate snap points for {buildType}.");
                return false;
        }
    }

    private static Transform CreateAutoSnapRoot(Transform root)
    {
        GameObject autoRootObject = new GameObject(AutoRootName);
        autoRootObject.transform.SetParent(root, false);
        autoRootObject.transform.localPosition = Vector3.zero;
        autoRootObject.transform.localRotation = Quaternion.identity;
        autoRootObject.transform.localScale = Vector3.one;
        return autoRootObject.transform;
    }

    private static BuildType InferBuildType(GameObject root)
    {
        BuildableObject buildable = root.GetComponent<BuildableObject>();
        if (buildable != null)
        {
            switch (buildable.PieceType)
            {
                case BuildPieceType.Foundation:
                    return BuildType.Foundation;
                case BuildPieceType.Wall:
                    return BuildType.Wall;
                case BuildPieceType.Board:
                    return BuildType.Board;
            }
        }

        string name = root.name.ToLowerInvariant();
        if (name.Contains("wall"))
        {
            return BuildType.Wall;
        }

        if (name.Contains("board") || name.Contains("floor"))
        {
            return BuildType.Board;
        }

        return BuildType.Foundation;
    }

    private static void GenerateFoundation(Transform root, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
        float topY = bounds.max.y;
        float pivotY = 0f;

        CreateSnapPoint(root, "Socket_Foundation_East", BuildSnapPointRole.Socket, BuildSnapPointType.Foundation, new Vector3(size.x, pivotY, 0f), Vector3.right, 1, 0f, false);
        CreateSnapPoint(root, "Socket_Foundation_West", BuildSnapPointRole.Socket, BuildSnapPointType.Foundation, new Vector3(-size.x, pivotY, 0f), Vector3.left, 1, 0f, false);
        CreateSnapPoint(root, "Socket_Foundation_North", BuildSnapPointRole.Socket, BuildSnapPointType.Foundation, new Vector3(0f, pivotY, size.z), Vector3.forward, 1, 0f, false);
        CreateSnapPoint(root, "Socket_Foundation_South", BuildSnapPointRole.Socket, BuildSnapPointType.Foundation, new Vector3(0f, pivotY, -size.z), Vector3.back, 1, 0f, false);
        CreateSnapPoint(root, "Anchor_Foundation_Center", BuildSnapPointRole.Anchor, BuildSnapPointType.Foundation, new Vector3(0f, pivotY, 0f), Vector3.forward, 0, 0f, false);

        CreateSnapPoint(root, "Socket_Wall_East", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(bounds.max.x, topY, center.z), Vector3.right, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_West", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(bounds.min.x, topY, center.z), Vector3.left, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_North", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(center.x, topY, bounds.max.z), Vector3.forward, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_South", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(center.x, topY, bounds.min.z), Vector3.back, 2, 0f, true);

        // Furniture stays free-placeable on valid floor colliders, so no automatic Floor socket is generated here.
    }

    private static void GenerateWall(Transform root, Bounds bounds)
    {
        float pivotY = 0f;
        float topY = bounds.max.y;

        CreateSnapPoint(root, "Socket_WallJoint_Left", BuildSnapPointRole.Socket, BuildSnapPointType.WallJoint, new Vector3(bounds.min.x, pivotY, 0f), Vector3.forward, 3, 1.25f, true);
        CreateSnapPoint(root, "Socket_WallJoint_Right", BuildSnapPointRole.Socket, BuildSnapPointType.WallJoint, new Vector3(bounds.max.x, pivotY, 0f), Vector3.forward, 3, 1.25f, true);
        CreateSnapPoint(root, "Socket_WallTop_Center", BuildSnapPointRole.Socket, BuildSnapPointType.WallTop, new Vector3(0f, topY, 0f), Vector3.forward, 2, 0f, true);

        CreateSnapPoint(root, "Anchor_Wall_BottomCenter", BuildSnapPointRole.Anchor, BuildSnapPointType.FoundationEdge, new Vector3(0f, pivotY, 0f), Vector3.forward, 0, 0f, true);
        CreateSnapPoint(root, "Anchor_WallJoint_Left", BuildSnapPointRole.Anchor, BuildSnapPointType.WallJoint, new Vector3(bounds.min.x, pivotY, 0f), Vector3.forward, 0, 0f, true);
        CreateSnapPoint(root, "Anchor_WallJoint_Right", BuildSnapPointRole.Anchor, BuildSnapPointType.WallJoint, new Vector3(bounds.max.x, pivotY, 0f), Vector3.forward, 0, 0f, true);
    }

    private static void GenerateBoard(Transform root, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
        float pivotY = 0f;
        float topY = bounds.max.y;

        CreateSnapPoint(root, "Socket_Board_East", BuildSnapPointRole.Socket, BuildSnapPointType.BoardEdge, new Vector3(size.x, pivotY, 0f), Vector3.right, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Board_West", BuildSnapPointRole.Socket, BuildSnapPointType.BoardEdge, new Vector3(-size.x, pivotY, 0f), Vector3.left, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Board_North", BuildSnapPointRole.Socket, BuildSnapPointType.BoardEdge, new Vector3(0f, pivotY, size.z), Vector3.forward, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Board_South", BuildSnapPointRole.Socket, BuildSnapPointType.BoardEdge, new Vector3(0f, pivotY, -size.z), Vector3.back, 2, 0f, true);
        CreateSnapPoint(root, "Anchor_Board_Center", BuildSnapPointRole.Anchor, BuildSnapPointType.BoardEdge, new Vector3(0f, pivotY, 0f), Vector3.forward, 0, 0f, true);

        CreateSnapPoint(root, "Socket_Wall_East", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(bounds.max.x, topY, center.z), Vector3.right, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_West", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(bounds.min.x, topY, center.z), Vector3.left, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_North", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(center.x, topY, bounds.max.z), Vector3.forward, 2, 0f, true);
        CreateSnapPoint(root, "Socket_Wall_South", BuildSnapPointRole.Socket, BuildSnapPointType.FoundationEdge, new Vector3(center.x, topY, bounds.min.z), Vector3.back, 2, 0f, true);

        CreateSnapPoint(root, "Anchor_WallTop_BackEdge", BuildSnapPointRole.Anchor, BuildSnapPointType.WallTop, new Vector3(0f, pivotY, bounds.min.z), Vector3.forward, 0, 0f, true);
        CreateSnapPoint(root, "Anchor_WallTop_FrontEdge", BuildSnapPointRole.Anchor, BuildSnapPointType.WallTop, new Vector3(0f, pivotY, bounds.max.z), Vector3.back, 0, 0f, true);
        CreateSnapPoint(root, "Anchor_WallTop_LeftEdge", BuildSnapPointRole.Anchor, BuildSnapPointType.WallTop, new Vector3(bounds.min.x, pivotY, 0f), Vector3.right, 0, 0f, true);
        CreateSnapPoint(root, "Anchor_WallTop_RightEdge", BuildSnapPointRole.Anchor, BuildSnapPointType.WallTop, new Vector3(bounds.max.x, pivotY, 0f), Vector3.left, 0, 0f, true);
    }

    private static BuildSnapPoint CreateSnapPoint(
        Transform root,
        string name,
        BuildSnapPointRole role,
        BuildSnapPointType type,
        Vector3 localPosition,
        Vector3 localForward,
        int priority,
        float catchRadiusOverride,
        bool usePointRotation)
    {
        GameObject pointObject = new GameObject(AutoPrefix + name);
        pointObject.transform.SetParent(root, false);
        pointObject.transform.localPosition = localPosition;
        pointObject.transform.localRotation = GetLocalRotation(localForward);
        pointObject.transform.localScale = Vector3.one;

        BuildSnapPoint point = pointObject.AddComponent<BuildSnapPoint>();
        SerializedObject serializedObject = new SerializedObject(point);
        serializedObject.FindProperty("snapPointType").enumValueIndex = (int)type;
        serializedObject.FindProperty("role").enumValueIndex = (int)role;
        serializedObject.FindProperty("priority").intValue = priority;
        serializedObject.FindProperty("catchRadiusOverride").floatValue = catchRadiusOverride;
        serializedObject.FindProperty("usePointRotation").boolValue = usePointRotation;
        serializedObject.FindProperty("placementOffset").vector3Value = Vector3.zero;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return point;
    }

    private static Quaternion GetLocalRotation(Vector3 localForward)
    {
        localForward.y = 0f;
        if (localForward.sqrMagnitude < 0.0001f)
        {
            localForward = Vector3.forward;
        }

        return Quaternion.LookRotation(localForward.normalized, Vector3.up);
    }

    private static void RemoveExistingSnapPoints(Transform root)
    {
        List<GameObject> targets = new List<GameObject>();
        List<BuildSnapPoint> rootComponents = new List<BuildSnapPoint>();
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name == AutoRootName)
            {
                targets.Add(child.gameObject);
            }
        }

        BuildSnapPoint[] points = root.GetComponentsInChildren<BuildSnapPoint>(true);
        for (int i = 0; i < points.Length; i++)
        {
            BuildSnapPoint point = points[i];
            if (point == null || IsUnderAutoRoot(point.transform, root))
            {
                continue;
            }

            if (point.transform == root)
            {
                rootComponents.Add(point);
            }
            else
            {
                targets.Add(point.gameObject);
            }
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Object.DestroyImmediate(targets[i]);
        }

        for (int i = 0; i < rootComponents.Count; i++)
        {
            Object.DestroyImmediate(rootComponents[i]);
        }
    }

    private static bool IsUnderAutoRoot(Transform target, Transform root)
    {
        Transform current = target != null ? target.parent : null;
        while (current != null && current != root)
        {
            if (current.name == AutoRootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool TryCalculateLocalBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponentInParent<BuildSnapPoint>() != null)
            {
                continue;
            }

            EncapsulateWorldBounds(root, renderer.bounds, ref bounds, ref hasBounds);
        }

        if (!hasBounds)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.GetComponentInParent<BuildSnapPoint>() != null)
                {
                    continue;
                }

                EncapsulateWorldBounds(root, collider.bounds, ref bounds, ref hasBounds);
            }
        }

        return hasBounds;
    }

    private static void EncapsulateWorldBounds(Transform root, Bounds worldBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 worldPoint = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 localPoint = root.InverseTransformPoint(worldPoint);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }
        }
    }
}
