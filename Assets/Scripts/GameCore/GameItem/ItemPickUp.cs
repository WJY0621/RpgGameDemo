using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public enum DropPickupType
{
    Item,
    Gold
}

public class ItemPickUp : MonoBehaviour
{
    private static ItemPickUp activePromptPickup;
    private static InteractUI sharedInteractUI;
    private static bool isLoadingInteractUI;
    private static bool isCollectingNearbyDrops;

    private const float PickupRadius = 3f;
    private const float GroundRayStartHeight = 3f;
    private const float GroundRayDistance = 12f;
    private const float GroundOffset = 0.05f;
    private const string OreLayerName = "Ore";

    [Header("Item")]
    public DropPickupType pickupType = DropPickupType.Item;
    public int itemId;
    public int count = 1;
    public int goldAmount;

    [Header("Visual")]
    public bool autoRotate = false;
    public float rotateSpeed = 50f;
    public float spawnDuration = 0.3f;
    public float spawnHeight = 0.9f;
    public float pickupEnableDelay = 0.12f;
    public float flyToPlayerDuration = 0.22f;
    public bool tumbleOnSpawn = true;
    public Vector2 spawnTumbleSpeedRange = new Vector2(420f, 760f);
    public string essenceChildName = "Essence";

    private bool canPickUp = true;
    private bool isBeingCollected;
    private bool isPlayerInRange;
    private Transform playerTransform;
    private SphereCollider triggerCollider;
    private Transform essenceRoot;
    private Quaternion essenceUprightRotation = Quaternion.identity;
    private bool hasCachedEssenceRotation;
    private float lastPlayerDistance = float.MaxValue;

    public static bool HasActivePrompt()
    {
        return activePromptPickup != null && activePromptPickup.CanKeepPromptOwnership();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        CacheEssenceRoot();
        TryCachePlayerTransform();
    }

    private void Start()
    {
        EnsureInteractUILoaded().Forget();
    }

    private void Update()
    {
        TryCachePlayerTransform();

        if (autoRotate)
        {
            transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
        }

        if (!canPickUp || playerTransform == null || !isPlayerInRange)
        {
            if (!isPlayerInRange)
            {
                ReleasePromptOwnership();
            }
            return;
        }

        lastPlayerDistance = Vector3.Distance(transform.position, playerTransform.position);
        TryShowPromptForNearestPickup();

        if (activePromptPickup == this && Input.GetKeyDown(KeyCode.F))
        {
            CollectNearbyDropsAsync(playerTransform, PickupRadius).Forget();
        }
    }

    private void OnDisable()
    {
        ReleasePromptOwnership();
    }

    private void OnDestroy()
    {
        ReleasePromptOwnership();
    }

    public void SetupItem(int id, int amount)
    {
        pickupType = DropPickupType.Item;
        itemId = id;
        count = Mathf.Max(1, amount);
    }

    public void SetupGold(int amount)
    {
        pickupType = DropPickupType.Gold;
        goldAmount = Mathf.Max(1, amount);
    }

    public void PlaySpawnAnimation(Vector3 horizontalOffset)
    {
        StopAllCoroutines();
        StartCoroutine(SpawnAnimationRoutine(horizontalOffset));
    }

    private System.Collections.IEnumerator SpawnAnimationRoutine(Vector3 horizontalOffset)
    {
        EnsureTriggerCollider();
        CacheEssenceRoot();
        canPickUp = false;
        isBeingCollected = false;
        ReleasePromptOwnership();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
        SetEssenceVisible(false);

        Vector3 start = transform.position;
        Vector3 end = ResolveGroundedDropPosition(start + horizontalOffset);
        Quaternion startRotation = transform.rotation;
        Vector3 tumbleAxis = Random.onUnitSphere;
        if (Mathf.Abs(tumbleAxis.y) > 0.9f)
        {
            tumbleAxis = Vector3.Cross(tumbleAxis, Vector3.right).normalized;
        }

        float tumbleSpeed = Random.Range(spawnTumbleSpeedRange.x, spawnTumbleSpeedRange.y);
        if (Random.value < 0.5f)
        {
            tumbleSpeed = -tumbleSpeed;
        }

        float elapsed = 0f;

        while (elapsed < spawnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, spawnDuration));
            Vector3 horizontal = Vector3.Lerp(start, end, t);
            float arc = 4f * spawnHeight * t * (1f - t);
            transform.position = horizontal + Vector3.up * arc;
            if (tumbleOnSpawn)
            {
                transform.rotation = Quaternion.AngleAxis(tumbleSpeed * elapsed, tumbleAxis) * startRotation;
            }
            yield return null;
        }

        transform.position = end;
        SettleDropVisual();

        if (pickupEnableDelay > 0f)
        {
            yield return new WaitForSeconds(pickupEnableDelay);
        }

        canPickUp = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        isPlayerInRange = true;
        TryCachePlayerTransform(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        isPlayerInRange = false;
        ReleasePromptOwnership();
    }

    private async UniTaskVoid PickUp()
    {
        await PickUpInternal();
    }

    private async UniTask PickUpInternal()
    {
        if (!canPickUp || isBeingCollected)
        {
            return;
        }

        isBeingCollected = true;
        canPickUp = false;
        isPlayerInRange = false;
        ReleasePromptOwnership();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        await PlayFlyToPlayerAnimation();

        if (pickupType == DropPickupType.Gold)
        {
            GameMgr.Package.AddGold(goldAmount);
        }
        else
        {
            await GameMgr.Package.AddItem(itemId, count);
        }

        Destroy(gameObject);
    }

    public static async UniTaskVoid CollectNearbyDropsAsync(Transform player, float radius)
    {
        if (isCollectingNearbyDrops || player == null)
        {
            return;
        }

        isCollectingNearbyDrops = true;

        try
        {
            ItemPickUp[] pickups = Object.FindObjectsByType<ItemPickUp>(FindObjectsSortMode.None);
            List<UniTask> collectTasks = new List<UniTask>();

            for (int i = 0; i < pickups.Length; i++)
            {
                ItemPickUp pickup = pickups[i];
                if (pickup == null || !pickup.canPickUp || pickup.isBeingCollected)
                {
                    continue;
                }

                float distance = Vector3.Distance(player.position, pickup.transform.position);
                if (distance > radius)
                {
                    continue;
                }

                pickup.playerTransform = player;
                collectTasks.Add(pickup.PickUpInternal());
            }

            if (collectTasks.Count > 0)
            {
                await UniTask.WhenAll(collectTasks);
            }
        }
        finally
        {
            isCollectingNearbyDrops = false;
        }
    }

    private async UniTask PlayFlyToPlayerAnimation()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector3 start = transform.position;
        Vector3 target = playerTransform.position + Vector3.up * 1.1f;
        float elapsed = 0f;

        while (elapsed < flyToPlayerDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, flyToPlayerDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(start, target, eased);
            float scale = Mathf.Lerp(1f, 0.35f, eased);
            transform.localScale = Vector3.one * scale;
            await UniTask.Yield();
        }
    }

    private void TryShowPromptForNearestPickup()
    {
        if (activePromptPickup == null || !activePromptPickup.CanKeepPromptOwnership() || lastPlayerDistance < activePromptPickup.lastPlayerDistance)
        {
            if (activePromptPickup != null && activePromptPickup != this)
            {
                activePromptPickup.HidePromptUI();
            }

            activePromptPickup = this;
        }

        if (activePromptPickup == this)
        {
            ShowPromptUI();
        }
    }

    private bool CanKeepPromptOwnership()
    {
        return canPickUp && isPlayerInRange && playerTransform != null;
    }

    private void ShowPromptUI()
    {
        if (sharedInteractUI != null)
        {
            sharedInteractUI.ShowItemInteraction(this);
        }
        else
        {
            EnsureInteractUILoaded().Forget();
        }
    }

    private void HidePromptUI()
    {
        if (sharedInteractUI != null && sharedInteractUI.IsShowingItemInteraction)
        {
            sharedInteractUI.HideInteraction();
        }
    }

    private void ReleasePromptOwnership()
    {
        if (activePromptPickup == this)
        {
            HidePromptUI();
            activePromptPickup = null;
        }
    }

    private async UniTaskVoid EnsureInteractUILoaded()
    {
        if (sharedInteractUI != null || isLoadingInteractUI)
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

            if (activePromptPickup == null)
            {
                sharedInteractUI?.HideInteraction();
            }
        }
        finally
        {
            isLoadingInteractUI = false;
        }
    }

    private void TryCachePlayerTransform(Transform candidate = null)
    {
        if (candidate != null)
        {
            PlayerStateDriver driver = candidate.GetComponentInParent<PlayerStateDriver>();
            if (driver != null)
            {
                playerTransform = driver.transform;
                return;
            }
        }

        if (playerTransform != null || GameMgr.Instance == null)
        {
            return;
        }

        var player = GameMgr.Instance.Player;
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<PlayerStateDriver>() != null);
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<SphereCollider>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<SphereCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.radius = PickupRadius;
    }

    private void CacheEssenceRoot()
    {
        if (essenceRoot != null)
        {
            return;
        }

        essenceRoot = FindChildByName(transform, essenceChildName);
        if (essenceRoot != null && !hasCachedEssenceRotation)
        {
            essenceUprightRotation = essenceRoot.rotation;
            hasCachedEssenceRotation = true;
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void SetEssenceVisible(bool visible)
    {
        if (essenceRoot == null)
        {
            return;
        }

        essenceRoot.gameObject.SetActive(visible);
    }

    private void SettleDropVisual()
    {
        if (essenceRoot == null)
        {
            return;
        }

        SetEssenceVisible(true);
        essenceRoot.rotation = hasCachedEssenceRotation ? essenceUprightRotation : Quaternion.identity;
    }

    private Vector3 ResolveGroundedDropPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * GroundRayStartHeight;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, GroundRayDistance, GetDropGroundRaycastMask(), QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * GroundOffset;
        }

        return desiredPosition;
    }

    private static int GetDropGroundRaycastMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        int oreLayer = LayerMask.NameToLayer(OreLayerName);
        if (oreLayer >= 0)
        {
            mask &= ~(1 << oreLayer);
        }

        return mask;
    }
}
