using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class BuildInteractionBehaviour : MonoBehaviour
{
    private static BuildInteractionBehaviour activePromptBuildInteraction;
    private static InteractUI sharedInteractUI;
    private static bool isLoadingInteractUI;

    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    protected Transform PlayerTransform { get; private set; }
    private float lastPlayerDistance = float.MaxValue;

    public static bool HasActivePrompt()
    {
        return activePromptBuildInteraction != null && activePromptBuildInteraction.CanKeepPromptOwnership();
    }

    protected virtual void Start()
    {
        TryCachePlayerTransform();
        EnsureInteractUILoaded().Forget();
    }

    protected virtual void Update()
    {
        TryCachePlayerTransform();
        UpdatePrompt();

        if (activePromptBuildInteraction == this && CanKeepPromptOwnership() && Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    protected virtual bool CanInteract()
    {
        return true;
    }

    protected virtual string GetInteractionPromptText()
    {
        return "交互";
    }

    protected abstract void Interact();

    protected virtual void OnDisable()
    {
        ReleasePromptOwnership();
    }

    protected virtual void OnDestroy()
    {
        ReleasePromptOwnership();
    }

    private void UpdatePrompt()
    {
        if (!CanInteract() || PlayerTransform == null)
        {
            ReleasePromptOwnership();
            return;
        }

        lastPlayerDistance = Vector3.Distance(transform.position, PlayerTransform.position);
        if (lastPlayerDistance > interactionDistance)
        {
            ReleasePromptOwnership();
            return;
        }

        if (ItemPickUp.HasActivePrompt() || ChestInteraction.HasActivePrompt())
        {
            ReleasePromptOwnership();
            return;
        }

        TryShowPromptForNearestBuildInteraction();
    }

    private void TryShowPromptForNearestBuildInteraction()
    {
        if (activePromptBuildInteraction == null ||
            !activePromptBuildInteraction.CanKeepPromptOwnership() ||
            lastPlayerDistance < activePromptBuildInteraction.lastPlayerDistance)
        {
            if (activePromptBuildInteraction != null && activePromptBuildInteraction != this)
            {
                activePromptBuildInteraction.HidePromptUI();
            }

            activePromptBuildInteraction = this;
        }

        if (activePromptBuildInteraction == this)
        {
            ShowPromptUI();
        }
    }

    private bool CanKeepPromptOwnership()
    {
        return CanInteract() &&
               PlayerTransform != null &&
               Vector3.Distance(transform.position, PlayerTransform.position) <= interactionDistance &&
               !ItemPickUp.HasActivePrompt() &&
               !ChestInteraction.HasActivePrompt();
    }

    private void ShowPromptUI()
    {
        if (sharedInteractUI != null)
        {
            sharedInteractUI.ShowBuildInteraction(this, GetInteractionPromptText());
        }
        else
        {
            EnsureInteractUILoaded().Forget();
        }
    }

    private void HidePromptUI()
    {
        if (sharedInteractUI != null && sharedInteractUI.IsShowingBuildInteraction)
        {
            sharedInteractUI.HideInteraction();
        }
    }

    private void ReleasePromptOwnership()
    {
        if (activePromptBuildInteraction == this)
        {
            HidePromptUI();
            activePromptBuildInteraction = null;
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

            if (activePromptBuildInteraction == null)
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
        if (PlayerTransform != null || GameMgr.Instance == null)
        {
            return;
        }

        PlayerStateDriver player = GameMgr.Instance.Player;
        if (player != null)
        {
            PlayerTransform = player.transform;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
