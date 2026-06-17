using System;
using ARPGFX;
using Cysharp.Threading.Tasks;
using UnityEngine;

[Serializable]
public class ChestRewardItem
{
    public int itemId;
    public int count = 1;
}

[DisallowMultipleComponent]
public class ChestInteraction : MonoBehaviour
{
    private const string DefaultOpenSoundGroup = "Game";
    private const string DefaultOpenSoundName = "OpenChest";

    private static ChestInteraction activePromptChest;
    private static InteractUI sharedInteractUI;
    private static bool isLoadingInteractUI;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private bool oneShot = true;

    [Header("Open Effect")]
    [SerializeField] private GameObject openEffectPrefab;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private float effectLifetime = 2f;
    [SerializeField] private Animation chestOpenAnimation;
    [SerializeField] private string chestOpenAnimationName = "ChestOpen";

    [Header("Open Sound")]
    [SerializeField] private string openSoundGroup = DefaultOpenSoundGroup;
    [SerializeField] private string openSoundName = DefaultOpenSoundName;
    [SerializeField, Range(0f, 1f)] private float openSoundVolume = 1f;

    [Header("Rewards")]
    [SerializeField, Min(0)] private int rewardGold;
    [SerializeField] private ChestRewardItem[] rewards = Array.Empty<ChestRewardItem>();
    [SerializeField] private float rewardDelay;

    [Header("Disappear")]
    [SerializeField] private bool destroyAfterOpen = true;
    [SerializeField, Min(0f)] private float destroyDelayAfterOpen = 3f;

    private Transform playerTransform;
    private ARPGFXLoopScript loopScript;
    private ARPGFXCycler cycler;
    private bool opened;
    private bool opening;
    private float lastPlayerDistance = float.MaxValue;

    public static bool HasActivePrompt()
    {
        return activePromptChest != null && activePromptChest.CanKeepPromptOwnership();
    }

    private void Awake()
    {
        loopScript = GetComponent<ARPGFXLoopScript>();
        cycler = GetComponent<ARPGFXCycler>();
        DisableLoopingEffects();
    }

    private void Start()
    {
        TryCachePlayerTransform();
        EnsureInteractUILoaded().Forget();
    }

    private void Update()
    {
        TryCachePlayerTransform();
        UpdatePrompt();
        CheckInteractionInput();
    }

    private void OnDisable()
    {
        ReleasePromptOwnership();
    }

    private void OnDestroy()
    {
        ReleasePromptOwnership();
    }

    private void DisableLoopingEffects()
    {
        if (loopScript != null)
        {
            loopScript.enabled = false;
        }

        if (cycler != null)
        {
            cycler.enabled = false;
        }
    }

    private void UpdatePrompt()
    {
        if (!CanUseChest())
        {
            ReleasePromptOwnership();
            return;
        }

        lastPlayerDistance = Vector3.Distance(transform.position, playerTransform.position);
        if (lastPlayerDistance > interactionDistance)
        {
            ReleasePromptOwnership();
            return;
        }

        if (ItemPickUp.HasActivePrompt())
        {
            ReleasePromptOwnership();
            return;
        }

        TryShowPromptForNearestChest();
    }

    private void CheckInteractionInput()
    {
        if (activePromptChest == this && CanKeepPromptOwnership() && Input.GetKeyDown(interactKey))
        {
            OpenAsync().Forget();
        }
    }

    private async UniTaskVoid OpenAsync()
    {
        if (!CanUseChest())
        {
            return;
        }

        opening = true;
        opened = true;
        ReleasePromptOwnership();
        PlayOpenSound();
        PlayOpenAnimation();
        PlayOpenEffect();

        if (rewardDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(rewardDelay));
        }

        await GrantRewards();
        opening = false;
        ScheduleDestroyAfterOpen();
    }

    private void PlayOpenEffect()
    {
        GameObject prefab = openEffectPrefab != null ? openEffectPrefab : loopScript != null ? loopScript.chosenEffect : null;
        if (prefab == null)
        {
            return;
        }

        Transform spawn = effectSpawnPoint != null ? effectSpawnPoint : transform;
        GameObject effect = Instantiate(prefab, spawn.position, spawn.rotation);
        if (effectLifetime > 0f)
        {
            Destroy(effect, effectLifetime);
        }
    }

    private void PlayOpenSound()
    {
        if (GameMgr.Audio == null || string.IsNullOrWhiteSpace(openSoundName))
        {
            return;
        }

        string group = string.IsNullOrWhiteSpace(openSoundGroup)
            ? DefaultOpenSoundGroup
            : openSoundGroup.Trim();

        GameMgr.Audio.PlayAt(group, openSoundName.Trim(), transform.position, openSoundVolume);
    }

    private void PlayOpenAnimation()
    {
        Animation animationComponent = chestOpenAnimation != null
            ? chestOpenAnimation
            : GetComponentInChildren<Animation>(true);

        if (animationComponent == null)
        {
            return;
        }

        string animationName = string.IsNullOrWhiteSpace(chestOpenAnimationName)
            ? "ChestOpen"
            : chestOpenAnimationName.Trim();

        if (animationComponent.GetClip(animationName) != null)
        {
            animationComponent.Stop();
            animationComponent.Play(animationName);
            return;
        }

        if (animationComponent.clip != null)
        {
            animationComponent.Stop();
            animationComponent.Play(animationComponent.clip.name);
        }
    }

    private async UniTask GrantRewards()
    {
        if (GameMgr.Package == null)
        {
            return;
        }

        if (rewardGold > 0)
        {
            GameMgr.Package.AddGold(rewardGold);
        }

        if (rewards == null)
        {
            return;
        }

        for (int i = 0; i < rewards.Length; i++)
        {
            ChestRewardItem reward = rewards[i];
            if (reward == null || reward.itemId <= 0 || reward.count <= 0)
            {
                continue;
            }

            await GameMgr.Package.AddItem(reward.itemId, reward.count);
        }
    }

    private void ScheduleDestroyAfterOpen()
    {
        if (!destroyAfterOpen)
        {
            return;
        }

        if (destroyDelayAfterOpen <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject, destroyDelayAfterOpen);
    }

    private void TryShowPromptForNearestChest()
    {
        if (activePromptChest == null || !activePromptChest.CanKeepPromptOwnership() || lastPlayerDistance < activePromptChest.lastPlayerDistance)
        {
            if (activePromptChest != null && activePromptChest != this)
            {
                activePromptChest.HidePromptUI();
            }

            activePromptChest = this;
        }

        if (activePromptChest == this)
        {
            ShowPromptUI();
        }
    }

    private bool CanUseChest()
    {
        return playerTransform != null && !opening && (!oneShot || !opened);
    }

    private bool CanKeepPromptOwnership()
    {
        return CanUseChest() &&
               Vector3.Distance(transform.position, playerTransform.position) <= interactionDistance &&
               !ItemPickUp.HasActivePrompt();
    }

    private void ShowPromptUI()
    {
        if (sharedInteractUI != null)
        {
            sharedInteractUI.ShowChestInteraction(this);
        }
        else
        {
            EnsureInteractUILoaded().Forget();
        }
    }

    private void HidePromptUI()
    {
        if (sharedInteractUI != null && sharedInteractUI.IsShowingChestInteraction)
        {
            sharedInteractUI.HideInteraction();
        }
    }

    private void ReleasePromptOwnership()
    {
        if (activePromptChest == this)
        {
            HidePromptUI();
            activePromptChest = null;
        }
    }

    private async UniTaskVoid EnsureInteractUILoaded()
    {
        if (sharedInteractUI != null || isLoadingInteractUI || GameMgr.UI == null)
        {
            return;
        }

        isLoadingInteractUI = true;

        try
        {
            sharedInteractUI = GameMgr.UI.GetPanelWithoutLoad<InteractUI>();
            if (sharedInteractUI == null)
            {
                sharedInteractUI = await GameMgr.UI.GetPanel<InteractUI>();
            }

            if (activePromptChest == null)
            {
                sharedInteractUI?.HideInteraction();
            }
        }
        finally
        {
            isLoadingInteractUI = false;
        }
    }

    private void TryCachePlayerTransform()
    {
        if (playerTransform != null || GameMgr.Instance == null)
        {
            return;
        }

        PlayerStateDriver player = GameMgr.Instance.Player;
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
