using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

public class IconAtlasMgr
{
    public const string RoleAtlasKey = "RoleAtlas";
    public const string ConsumAtlasKey = "ConsumAtlas";
    public const string MaterialAtlasKey = "MaterialAtlas";
    public const string EquipAtlasKey = "EquipAtlas";
    public const string AccessoryAtlasKey = "AccessoryAtlas";
    public const string PackageUIAtlasKey = "PackageUIAtlas";
    public const string BuffAtlasKey = "BuffAtlas";

    private readonly Dictionary<string, Sprite> directSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, SpriteAtlas> atlasCache = new Dictionary<string, SpriteAtlas>();
    private readonly Dictionary<string, Sprite> atlasSpriteCache = new Dictionary<string, Sprite>();

    // 负缓存：记住"图集里没有的 sprite"和"没有 direct sprite 的名字"，避免每次都重新发起异步查询导致卡死。
    private readonly HashSet<string> missingAtlasSprites = new HashSet<string>();
    private readonly HashSet<string> noDirectSprite = new HashSet<string>();
    private readonly HashSet<string> warnedMissingSprites = new HashSet<string>();

    public async UniTask<Sprite> GetRoleIcon(string iconName)
    {
        Sprite directSprite = await GetDirectSprite(iconName);
        if (directSprite != null)
        {
            return directSprite;
        }

        return await GetSpriteFromAtlas(RoleAtlasKey, iconName);
    }

    public async UniTask<Sprite> GetBuffIcon(string iconName)
    {
        return await GetSpriteFromAtlas(BuffAtlasKey, iconName);
    }

    public async UniTask<Sprite> GetItemIcon(Item item)
    {
        if (item == null)
        {
            return null;
        }

        string atlasKey = item is AccessoryItem ? AccessoryAtlasKey : GetItemAtlasKey(item.itemType);
        return await GetSpriteFromAtlas(atlasKey, item.iconName);
    }

    public async UniTask<Sprite> GetItemIcon(ItemType itemType, string iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
        {
            return null;
        }

        return await GetSpriteFromAtlas(GetItemAtlasKey(itemType), iconName);
    }

    public async UniTask<Sprite> GetPackageUISprite(string spriteName)
    {
        return await GetSpriteFromAtlas(PackageUIAtlasKey, spriteName);
    }

    private async UniTask<Sprite> GetSpriteFromAtlas(string atlasKey, string spriteName)
    {
        if (string.IsNullOrWhiteSpace(atlasKey) || string.IsNullOrWhiteSpace(spriteName))
        {
            return null;
        }

        string cacheKey = atlasKey + "/" + spriteName;
        if (atlasSpriteCache.TryGetValue(cacheKey, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        // 负缓存：之前已确认图集里没有这个 sprite，直接返回 null，不再走异步加载。
        if (missingAtlasSprites.Contains(cacheKey))
        {
            return null;
        }

        SpriteAtlas atlas = await GetAtlas(atlasKey);
        if (atlas == null)
        {
            WarnMissingOnce($"atlas:{atlasKey}", $"[IconAtlasMgr] Failed to load atlas: {atlasKey}");
            return null;
        }

        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
        {
            missingAtlasSprites.Add(cacheKey);
            WarnMissingOnce(cacheKey, $"[IconAtlasMgr] Sprite '{spriteName}' not found in atlas '{atlasKey}'.");
            return null;
        }

        atlasSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private void WarnMissingOnce(string key, string message)
    {
        if (warnedMissingSprites.Add(key))
        {
            Debug.LogWarning(message);
        }
    }

    private async UniTask<Sprite> GetDirectSprite(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return null;
        }

        if (directSpriteCache.TryGetValue(spriteName, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        // 负缓存：之前已确认没有这个名字的 direct sprite，直接返回，避免每次都跑异步 Addressables 查询。
        if (noDirectSprite.Contains(spriteName))
        {
            return null;
        }

        if (!await HasAddressableSprite(spriteName))
        {
            noDirectSprite.Add(spriteName);
            return null;
        }

        Sprite sprite = await GameMgr.AssetLoader.LoadAsset<Sprite>(spriteName);
        if (sprite != null)
        {
            directSpriteCache[spriteName] = sprite;
        }
        else
        {
            noDirectSprite.Add(spriteName);
        }

        return sprite;
    }

    private async UniTask<SpriteAtlas> GetAtlas(string atlasKey)
    {
        if (atlasCache.TryGetValue(atlasKey, out SpriteAtlas cachedAtlas))
        {
            return cachedAtlas;
        }

        SpriteAtlas atlas = await GameMgr.AssetLoader.LoadAsset<SpriteAtlas>(atlasKey);
        if (atlas != null)
        {
            atlasCache[atlasKey] = atlas;
        }

        return atlas;
    }

    private static async UniTask<bool> HasAddressableSprite(string spriteName)
    {
        AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> handle =
            Addressables.LoadResourceLocationsAsync(spriteName, typeof(Sprite));

        await handle.ToUniTask();
        bool hasLocation = handle.Status == AsyncOperationStatus.Succeeded &&
            handle.Result != null &&
            handle.Result.Count > 0;

        Addressables.Release(handle);
        return hasLocation;
    }

    private string GetItemAtlasKey(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                return EquipAtlasKey;
            case ItemType.Consumable:
                return ConsumAtlasKey;
            case ItemType.Material:
                return MaterialAtlasKey;
            default:
                return string.Empty;
        }
    }
}
