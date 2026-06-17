using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class InteractUI : BasePanel
{
    private const string NpcInteractText = "进行对话";
    private const string ItemInteractText = "拾取物品";

    private const string ChestInteractText = "打开宝箱";

    [Header("Interact UI")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;

    private NPCInteraction currentNPC;
    private ItemPickUp currentPickup;
    private ChestInteraction currentChest;
    private BuildInteractionBehaviour currentBuildInteraction;
    private bool isShowing;

    public bool IsShowingItemInteraction => currentPickup != null;
    public bool IsShowingNPCInteraction => currentNPC != null;
    public bool IsShowingChestInteraction => currentChest != null;
    public bool IsShowingBuildInteraction => currentBuildInteraction != null;

    public override void Init()
    {
        CacheReferences();
    }

    public override void Show()
    {
        CacheReferences();
        base.Show();
    }

    public override void Hide(UnityAction callBack = null)
    {
        base.Hide(callBack);
        isShowing = false;
        currentNPC = null;
        currentPickup = null;
        currentChest = null;
        currentBuildInteraction = null;
    }

    public void ShowInteraction(NPCInteraction npc)
    {
        if (npc == null)
        {
            return;
        }

        if (isShowing && currentNPC == npc && currentPickup == null)
        {
            SetInteractText(NpcInteractText);
            return;
        }

        currentNPC = npc;
        currentPickup = null;
        currentChest = null;
        currentBuildInteraction = null;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }

        SetInteractText(NpcInteractText);
        isShowing = true;
        Show();
    }

    public void ShowItemInteraction(ItemPickUp pickup)
    {
        if (pickup == null)
        {
            return;
        }

        if (isShowing && currentPickup == pickup)
        {
            SetInteractText(ItemInteractText);
            return;
        }

        currentPickup = pickup;
        currentNPC = null;
        currentChest = null;
        currentBuildInteraction = null;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }

        SetInteractText(ItemInteractText);
        isShowing = true;
        Show();
    }

    public void ShowChestInteraction(ChestInteraction chest)
    {
        if (chest == null)
        {
            return;
        }

        if (isShowing && currentChest == chest)
        {
            SetInteractText(ChestInteractText);
            return;
        }

        currentChest = chest;
        currentPickup = null;
        currentNPC = null;
        currentBuildInteraction = null;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }

        SetInteractText(ChestInteractText);
        isShowing = true;
        Show();
    }

    public void ShowBuildInteraction(BuildInteractionBehaviour buildInteraction, string promptText)
    {
        if (buildInteraction == null)
        {
            return;
        }

        if (isShowing && currentBuildInteraction == buildInteraction)
        {
            SetInteractText(promptText);
            return;
        }

        currentBuildInteraction = buildInteraction;
        currentChest = null;
        currentPickup = null;
        currentNPC = null;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(true);
        }

        SetInteractText(promptText);
        isShowing = true;
        Show();
    }

    public void HideInteraction()
    {
        if (!isShowing)
        {
            return;
        }

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        isShowing = false;
        currentNPC = null;
        currentPickup = null;
        currentChest = null;
        currentBuildInteraction = null;
        Hide();
    }

    private void SetInteractText(string content)
    {
        CacheReferences();
        if (interactText != null)
        {
            interactText.text = content;
        }
    }

    private void CacheReferences()
    {
        if (interactPrompt == null)
        {
            interactPrompt = gameObject;
        }

        if (interactText == null)
        {
            Transform textTransform = transform.Find("InteractText");
            if (textTransform == null)
            {
                textTransform = GetComponentInChildren<TextMeshProUGUI>(true)?.transform;
            }

            if (textTransform != null)
            {
                interactText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }
    }
}
