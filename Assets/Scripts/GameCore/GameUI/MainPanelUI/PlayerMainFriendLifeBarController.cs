using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainFriendLifeBarController
{
    private readonly Transform panelRoot;

    private Transform friendLifeBarRoot;
    private Slider lifeBarSlider;
    private Image lifeFillImage;
    private Image friendIcon;
    private TMP_Text tmpFriendNameText;
    private Text legacyFriendNameText;
    private NetworkPlayer boundRemotePlayer;

    public PlayerMainFriendLifeBarController(Transform panelRoot)
    {
        this.panelRoot = panelRoot;
    }

    public void Init()
    {
        friendLifeBarRoot = panelRoot.Find("FriendLifebar");
        if (friendLifeBarRoot == null)
        {
            return;
        }

        lifeBarSlider = friendLifeBarRoot.GetComponent<Slider>();
        if (lifeBarSlider == null)
        {
            lifeBarSlider = friendLifeBarRoot.GetComponentInChildren<Slider>(true);
        }

        if (lifeBarSlider != null)
        {
            lifeBarSlider.minValue = 0f;
            lifeBarSlider.maxValue = 1f;
            lifeBarSlider.interactable = false;
            lifeBarSlider.transition = Selectable.Transition.None;
            lifeBarSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        Transform fillTransform = friendLifeBarRoot.Find("Image");
        if (fillTransform != null)
        {
            lifeFillImage = fillTransform.GetComponent<Image>();
        }

        Transform iconTransform = friendLifeBarRoot.Find("FriendIcon");
        if (iconTransform != null)
        {
            friendIcon = iconTransform.GetComponent<Image>();
            SocialAvatarIcon.ApplyDefault(friendIcon);
        }

        Transform nameTransform = friendLifeBarRoot.Find("FriendNameText");
        if (nameTransform != null)
        {
            tmpFriendNameText = nameTransform.GetComponent<TMP_Text>();
            legacyFriendNameText = nameTransform.GetComponent<Text>();
        }

        SetVisible(false);
    }

    public void Tick()
    {
        if (friendLifeBarRoot == null)
        {
            return;
        }

        NetworkPlayer remotePlayer = FindRemotePlayer();
        if (remotePlayer == null)
        {
            boundRemotePlayer = null;
            SetVisible(false);
            return;
        }

        if (boundRemotePlayer != remotePlayer)
        {
            boundRemotePlayer = remotePlayer;
            SocialAvatarIcon.ApplyDefault(friendIcon);
        }

        SetVisible(true);
        Refresh(remotePlayer);
    }

    public void Dispose()
    {
        boundRemotePlayer = null;
    }

    private void Refresh(NetworkPlayer remotePlayer)
    {
        string displayName = remotePlayer.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "好友";
        }

        SetText(displayName);

        float normalizedHP = Mathf.Clamp01(remotePlayer.NormalizedHP);
        if (lifeBarSlider != null)
        {
            lifeBarSlider.SetValueWithoutNotify(normalizedHP);
        }

        if (lifeFillImage != null)
        {
            lifeFillImage.fillAmount = normalizedHP;
        }
    }

    private NetworkPlayer FindRemotePlayer()
    {
        if (GameMgr.Network == null || !GameMgr.Network.IsSessionActive)
        {
            return null;
        }

        NetworkPlayer[] players = Object.FindObjectsOfType<NetworkPlayer>();
        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayer player = players[i];
            if (player != null && player.isActiveAndEnabled && player.IsSpawned && !player.IsLocalOwner)
            {
                return player;
            }
        }

        return null;
    }

    private void SetVisible(bool visible)
    {
        if (friendLifeBarRoot != null && friendLifeBarRoot.gameObject.activeSelf != visible)
        {
            friendLifeBarRoot.gameObject.SetActive(visible);
        }
    }

    private void SetText(string value)
    {
        if (tmpFriendNameText != null)
        {
            tmpFriendNameText.text = value;
        }

        if (legacyFriendNameText != null)
        {
            legacyFriendNameText.text = value;
        }
    }
}
