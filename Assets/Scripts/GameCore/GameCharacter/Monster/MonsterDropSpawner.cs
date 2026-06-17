using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class MonsterDropSpawner
{
    private const string DropWhitePrefab = "Drop_White";
    private const string DropGreenPrefab = "Drop_Green";
    private const string DropBluePrefab = "Drop_Blue";
    private const string DropPurplePrefab = "Drop_Purple";
    private const string DropOrangePrefab = "Drop_Orange";
    private const string DropGoldPrefab = "Drop_Gold";
    private const float MinDropScatterRadius = 0.35f;
    private const float MaxDropScatterRadius = 1.25f;
    private const float MinDropSpacing = 0.28f;

    private struct PendingDrop
    {
        public bool isGold;
        public int itemId;
        public int amount;
        public ItemQuality quality;
    }

    public static async UniTask SpawnDropsAsync(MonsterRuntime runtime, Vector3 origin)
    {
        if (runtime == null || GameMgr.Package == null)
        {
            return;
        }

        await GameMgr.Package.Init();

        List<PendingDrop> pendingDrops = BuildPendingDrops(runtime);
        if (pendingDrops.Count == 0)
        {
            return;
        }

        List<Vector3> usedOffsets = new List<Vector3>();

        for (int i = 0; i < pendingDrops.Count; i++)
        {
            PendingDrop drop = pendingDrops[i];
            string prefabName = ResolvePrefabName(drop);
            GameObject prefab = await GameMgr.AssetLoader.LoadPrefab(prefabName);

            if (prefab == null)
            {
                await GrantDropDirectly(drop);
                continue;
            }

            Vector3 spawnPosition = origin + Vector3.up * 0.8f;
            GameObject instance = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);

            ItemPickUp pickup = instance.GetComponent<ItemPickUp>();
            if (pickup == null)
            {
                pickup = instance.AddComponent<ItemPickUp>();
            }

            if (drop.isGold)
            {
                pickup.SetupGold(drop.amount);
            }
            else
            {
                pickup.SetupItem(drop.itemId, drop.amount);
            }

            RandomizeSpawnMotion(pickup);
            Vector3 horizontalOffset = CreateRandomDropOffset(usedOffsets);
            usedOffsets.Add(horizontalOffset);
            pickup.PlaySpawnAnimation(horizontalOffset);
        }
    }

    private static void RandomizeSpawnMotion(ItemPickUp pickup)
    {
        if (pickup == null)
        {
            return;
        }

        pickup.spawnDuration = Random.Range(0.24f, 0.46f);
        pickup.spawnHeight = Random.Range(0.55f, 1.35f);
        pickup.spawnTumbleSpeedRange = new Vector2(Random.Range(280f, 460f), Random.Range(620f, 980f));
    }

    private static Vector3 CreateRandomDropOffset(List<Vector3> usedOffsets)
    {
        Vector3 bestOffset = Vector3.zero;
        float bestDistance = -1f;

        for (int attempt = 0; attempt < 12; attempt++)
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

    private static List<PendingDrop> BuildPendingDrops(MonsterRuntime runtime)
    {
        List<PendingDrop> result = new List<PendingDrop>();

        int goldAmount = runtime.RollGoldDrop();
        if (goldAmount > 0)
        {
            result.Add(new PendingDrop
            {
                isGold = true,
                amount = goldAmount,
                quality = ItemQuality.Common
            });
        }

        List<MonsterDropItemRuntime> itemDrops = runtime.RollItemDrops();
        for (int i = 0; i < itemDrops.Count; i++)
        {
            MonsterDropItemRuntime drop = itemDrops[i];
            if (drop == null || drop.itemID <= 0 || drop.amount <= 0)
            {
                continue;
            }

            Item itemConfig = GameMgr.Package.GetItemConfig(drop.itemID);
            result.Add(new PendingDrop
            {
                isGold = false,
                itemId = drop.itemID,
                amount = drop.amount,
                quality = itemConfig != null ? itemConfig.quality : ItemQuality.Common
            });
        }

        return result;
    }

    private static string ResolvePrefabName(PendingDrop drop)
    {
        if (drop.isGold)
        {
            return DropGoldPrefab;
        }

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

    private static async UniTask GrantDropDirectly(PendingDrop drop)
    {
        if (drop.isGold)
        {
            GameMgr.Package.AddGold(drop.amount);
        }
        else
        {
            await GameMgr.Package.AddItem(drop.itemId, drop.amount);
        }
    }
}
