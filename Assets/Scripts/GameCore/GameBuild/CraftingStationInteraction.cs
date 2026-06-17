using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class CraftingStationInteraction : BuildInteractionBehaviour
{
    private BuildableObject buildableObject;

    protected override void Start()
    {
        base.Start();
        buildableObject = GetComponentInParent<BuildableObject>();
    }

    protected override void Interact()
    {
        OpenCraftingPanel().Forget();
    }

    protected override string GetInteractionPromptText()
    {
        return GetTargetItemType() == ItemType.Consumable ? "合成消耗品" : "合成装备";
    }

    private async UniTaskVoid OpenCraftingPanel()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        RecipePanel panel = await GameMgr.UI.ShowPanel<RecipePanel>();
        panel?.ShowOnlyItemType(GetTargetItemType());
    }

    private ItemType GetTargetItemType()
    {
        return buildableObject != null && buildableObject.InteractionType == BuildInteractionType.ConsumableCrafting
            ? ItemType.Consumable
            : ItemType.Weapon;
    }
}
