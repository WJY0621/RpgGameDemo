using Cysharp.Threading.Tasks;
using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    private static NPCInteraction activePromptNPC;
    private static InteractUI sharedInteractUI;
    private static bool isLoadingInteractUI;

    [Header("NPC Data")]
    [SerializeField] private NPCDataComponent npcDataComponent;

    [Header("Interaction Settings")]
    [SerializeField] private float defaultCheckRadius = 3f;

    private Transform playerTransform;
    private bool wasPlayerInRange;
    private float lastPlayerDistance = float.MaxValue;

    private void Start()
    {
        if (npcDataComponent == null)
        {
            npcDataComponent = GetComponent<NPCDataComponent>();
        }

        TryCachePlayerTransform();
        EnsureInteractUILoaded().Forget();
    }

    private void OnDisable()
    {
        ReleasePromptOwnership();
    }

    private void OnDestroy()
    {
        ReleasePromptOwnership();
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

            sharedInteractUI?.HideInteraction();
        }
        finally
        {
            isLoadingInteractUI = false;
        }
    }

    private void Update()
    {
        if (npcDataComponent == null)
        {
            return;
        }

        TryCachePlayerTransform();

        if (npcDataComponent.isInteracting)
        {
            ReleasePromptOwnership();
            return;
        }

        CheckPlayerDistance();
        CheckInteractionInput();
    }

    private void CheckPlayerDistance()
    {
        if (playerTransform == null)
        {
            return;
        }

        lastPlayerDistance = Vector3.Distance(transform.position, playerTransform.position);
        bool isInRange = lastPlayerDistance <= GetInteractionDistance();

        if (isInRange != wasPlayerInRange)
        {
            wasPlayerInRange = isInRange;
            npcDataComponent.isPlayerInRange = isInRange;
        }

        if (!npcDataComponent.ShowPrompt)
        {
            ReleasePromptOwnership();
            return;
        }

        if (ItemPickUp.HasActivePrompt() || ChestInteraction.HasActivePrompt())
        {
            ReleasePromptOwnership();
            return;
        }

        if (isInRange)
        {
            TryShowPromptForNearestNPC();
        }
        else
        {
            ReleasePromptOwnership();
        }
    }

    private void CheckInteractionInput()
    {
        if (ItemPickUp.HasActivePrompt() || ChestInteraction.HasActivePrompt())
        {
            return;
        }

        if (activePromptNPC == this && npcDataComponent.isPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            HidePromptUI();
            npcDataComponent.OnInteraction();
        }
    }

    private void TryShowPromptForNearestNPC()
    {
        if (activePromptNPC == null || !activePromptNPC.CanKeepPromptOwnership() || lastPlayerDistance < activePromptNPC.lastPlayerDistance)
        {
            if (activePromptNPC != null && activePromptNPC != this)
            {
                activePromptNPC.HidePromptUI();
            }

            activePromptNPC = this;
        }

        if (activePromptNPC == this)
        {
            ShowPromptUI();
        }
    }

    private bool CanKeepPromptOwnership()
    {
        return npcDataComponent != null
            && !npcDataComponent.isInteracting
            && npcDataComponent.isPlayerInRange
            && lastPlayerDistance <= GetInteractionDistance()
            && !ItemPickUp.HasActivePrompt()
            && !ChestInteraction.HasActivePrompt();
    }

    private void ShowPromptUI()
    {
        if (sharedInteractUI != null)
        {
            sharedInteractUI.ShowInteraction(this);
        }
        else
        {
            EnsureInteractUILoaded().Forget();
        }
    }

    private void HidePromptUI()
    {
        if (sharedInteractUI != null && sharedInteractUI.IsShowingNPCInteraction)
        {
            sharedInteractUI.HideInteraction();
        }
    }

    private void ReleasePromptOwnership()
    {
        if (activePromptNPC == this)
        {
            HidePromptUI();
            activePromptNPC = null;
        }
    }

    private void TryCachePlayerTransform()
    {
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

    private float GetInteractionDistance()
    {
        return npcDataComponent != null && npcDataComponent.TriggerDistance > 0f
            ? npcDataComponent.TriggerDistance
            : defaultCheckRadius;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, defaultCheckRadius);
    }
}
