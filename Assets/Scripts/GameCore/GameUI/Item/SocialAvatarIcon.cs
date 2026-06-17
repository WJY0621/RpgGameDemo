using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static class SocialAvatarIcon
{
    public const string DefaultPlayerIconName = "PlayerIcon1";

    private static Sprite cachedDefaultAvatar;

    public static void ApplyDefault(Image target)
    {
        ApplyDefaultAsync(target).Forget();
    }

    private static async UniTaskVoid ApplyDefaultAsync(Image target)
    {
        if (target == null)
        {
            return;
        }

        Sprite sprite = await LoadDefaultAvatar();
        if (target == null)
        {
            return;
        }

        if (sprite != null)
        {
            target.sprite = sprite;
            target.enabled = true;
            return;
        }

        target.enabled = target.sprite != null;
    }

    private static async UniTask<Sprite> LoadDefaultAvatar()
    {
        if (cachedDefaultAvatar != null)
        {
            return cachedDefaultAvatar;
        }

        if (GameMgr.IconAtlas != null)
        {
            cachedDefaultAvatar = await GameMgr.IconAtlas.GetRoleIcon(DefaultPlayerIconName);
            if (cachedDefaultAvatar != null)
            {
                return cachedDefaultAvatar;
            }
        }

        cachedDefaultAvatar = Resources.Load<Sprite>(DefaultPlayerIconName);
        return cachedDefaultAvatar;
    }
}
