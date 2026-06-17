using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class HarvestDropSpawner
{
    private const string DropWhitePrefab = "Drop_White";
    private const string DropGreenPrefab = "Drop_Green";
    private const string DropBluePrefab = "Drop_Blue";
    private const string DropPurplePrefab = "Drop_Purple";
    private const string DropOrangePrefab = "Drop_Orange";
    private const float MinDropScatterRadius = 0.25f;
    private const float MaxDropScatterRadius = 0.95f;
    private const float MinDropSpacing = 0.24f;

    private struct PendingDrop
    {
        public int itemId;
        public int amount;
        public ItemQuality quality;
    }

    public static async UniTask SpawnDropsAsync(IReadOnlyList<HarvestDropResult> drops, Vector3 origin)
    {
        if (drops == null || drops.Count == 0 || GameMgr.Package == null)
        {
            return;
        }

        await GameMgr.Package.Init();

        List<PendingDrop> pendingDrops = BuildPendingDrops(drops);
        if (pendingDrops.Count == 0)
        {
            return;
        }

        List<Vector3> usedOffsets = new List<Vector3>();

        for (int i = 0; i < pendingDrops.Count; i++)
        {
            PendingDrop drop = pendingDrops[i];
            string prefabName = ResolvePrefabName(drop);
            GameObject prefab = GameMgr.AssetLoader != null ? await GameMgr.AssetLoader.LoadPrefab(prefabName) : null;

            if (prefab == null)
            {
                await GameMgr.Package.AddItem(drop.itemId, drop.amount);
                continue;
            }

            GameObject instance = Object.Instantiate(prefab, origin + Vector3.up * 0.65f, Quaternion.identity);
            ItemPickUp pickup = instance.GetComponent<ItemPickUp>();
            if (pickup == null)
            {
                pickup = instance.AddComponent<ItemPickUp>();
            }

            pickup.SetupItem(drop.itemId, drop.amount);
            RandomizeSpawnMotion(pickup);

            Vector3 horizontalOffset = CreateRandomDropOffset(usedOffsets);
            usedOffsets.Add(horizontalOffset);
            pickup.PlaySpawnAnimation(horizontalOffset);
        }
    }

    private static List<PendingDrop> BuildPendingDrops(IReadOnlyList<HarvestDropResult> drops)
    {
        List<PendingDrop> result = new List<PendingDrop>();
        for (int i = 0; i < drops.Count; i++)
        {
            HarvestDropResult drop = drops[i];
            if (drop == null || drop.itemId <= 0 || drop.amount <= 0)
            {
                continue;
            }

            Item itemConfig = GameMgr.Package.GetItemConfig(drop.itemId);
            result.Add(new PendingDrop
            {
                itemId = drop.itemId,
                amount = drop.amount,
                quality = itemConfig != null ? itemConfig.quality : ItemQuality.Common
            });
        }

        return result;
    }

    private static void RandomizeSpawnMotion(ItemPickUp pickup)
    {
        if (pickup == null)
        {
            return;
        }

        pickup.spawnDuration = Random.Range(0.22f, 0.4f);
        pickup.spawnHeight = Random.Range(0.45f, 1.05f);
        pickup.spawnTumbleSpeedRange = new Vector2(Random.Range(260f, 420f), Random.Range(560f, 880f));
    }

    private static Vector3 CreateRandomDropOffset(List<Vector3> usedOffsets)
    {
        Vector3 bestOffset = Vector3.zero;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(MinDropScatterRadius, MaxDropScatterRadius);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            float nearestDistance = GetNearestOffsetDistance(offset, usedOffsets);

            if (nearestDistance >= MinDropSpacing)
            {
                return offset;
            }

            if (nearestDistance > bestDistance)
            {
                bestDistance = nearestDistance;
                bestOffset = offset;
            }
        }

        if (bestOffset.sqrMagnitude > 0.0001f)
        {
            return bestOffset;
        }

        Vector2 fallback = Random.insideUnitCircle.normalized;
        if (fallback.sqrMagnitude <= 0.0001f)
        {
            fallback = Vector2.right;
        }

        return new Vector3(fallback.x, 0f, fallback.y) * MinDropScatterRadius;
    }

    private static float GetNearestOffsetDistance(Vector3 offset, List<Vector3> usedOffsets)
    {
        if (usedOffsets == null || usedOffsets.Count == 0)
        {
            return float.MaxValue;
        }

        float nearest = float.MaxValue;
        for (int i = 0; i < usedOffsets.Count; i++)
        {
            nearest = Mathf.Min(nearest, Vector3.Distance(offset, usedOffsets[i]));
        }

        return nearest;
    }

    private static string ResolvePrefabName(PendingDrop drop)
    {
        switch (drop.quality)
        {
            case ItemQuality.Advanced:
                return DropGreenPrefab;
            case ItemQuality.Rare:
                return DropBluePrefab;
            case ItemQuality.Epic:
                return DropPurplePrefab;
            case ItemQuality.Legendary:
                return DropOrangePrefab;
            case ItemQuality.Common:
            default:
                return DropWhitePrefab;
        }
    }
}
