using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerModelManager : MonoBehaviour
{
    public static string CurrentRoleModelName { get; set; }

    [Header("Animator Controller")]
    public RuntimeAnimatorController playerAnimatorController;
    public RuntimeAnimatorController playerNormalAnimatorController;

    private Transform playerModelContainer;
    private GameObject currentModel;

    public System.Action<Animator> onModelSwitched;

    private void Start()
    {
        playerModelContainer = transform.Find("PlayerModel");

        if (playerModelContainer == null && gameObject.name == "PlayerModel")
        {
            playerModelContainer = transform;
        }

        if (playerModelContainer == null)
        {
            Debug.LogWarning("[PlayerModelManager] PlayerModel not found!");
            return;
        }

        if (!string.IsNullOrEmpty(CurrentRoleModelName))
        {
            _ = SwitchModel(CurrentRoleModelName);
        }
    }

    private async System.Threading.Tasks.Task SwitchModel(string modelName)
    {
        if (playerModelContainer == null || string.IsNullOrEmpty(modelName))
        {
            return;
        }

        foreach (Transform child in playerModelContainer)
        {
            Destroy(child.gameObject);
        }

        GameObject modelPrefab = await GameMgr.AssetLoader.LoadAsset<GameObject>(modelName);
        if (modelPrefab != null)
        {
            currentModel = Instantiate(modelPrefab, playerModelContainer);
            currentModel.transform.localPosition = Vector3.zero;
            currentModel.transform.localRotation = Quaternion.identity;

            AdjustLookAtHeight(modelName);

            Animator animator = currentModel.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = true;

                PlayerRootMotionRelay relay = currentModel.GetComponent<PlayerRootMotionRelay>();
                if (relay == null)
                {
                    relay = currentModel.AddComponent<PlayerRootMotionRelay>();
                }

                PlayerStateDriver driver = GetComponent<PlayerStateDriver>();
                if (driver != null)
                {
                    relay.Bind(driver);
                }

                PlayerAnimationEventRelay animationEventRelay = currentModel.GetComponent<PlayerAnimationEventRelay>();
                if (animationEventRelay == null)
                {
                    animationEventRelay = currentModel.AddComponent<PlayerAnimationEventRelay>();
                }

                if (currentModel.GetComponent<PlayerWeaponAttachmentController>() == null)
                {
                    currentModel.AddComponent<PlayerWeaponAttachmentController>();
                }

                PlayerAttackController attackController = GetComponent<PlayerAttackController>();
                if (attackController != null)
                {
                    animationEventRelay.Bind(attackController);
                }

                if (playerAnimatorController != null)
                {
                    animator.runtimeAnimatorController = playerAnimatorController;
                }

                onModelSwitched?.Invoke(animator);
            }
        }
    }

    private void AdjustLookAtHeight(string modelName)
    {
        Transform lookAt = transform.Find("LookAt");
        if (lookAt == null)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                lookAt = parent.Find("LookAt");
            }
        }

        if (lookAt == null)
        {
            Debug.LogWarning("[PlayerModelManager] LookAt not found!");
            return;
        }

        float targetY = 1.45f;
        if (modelName == "RoleModel_Man_01")
        {
            targetY = 1.65f;
        }

        Vector3 pos = lookAt.localPosition;
        pos.y = targetY;
        lookAt.localPosition = pos;
    }
}
