using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWingModelController : MonoBehaviour
{
    private const string DefaultWingModelAddress = "WingModel";
    private const string WeaponRootName = "Weapon";
    private const string BackMountName = "Back";

    [SerializeField] private string wingModelAddress = DefaultWingModelAddress;

    private PlayerModelManager modelManager;
    private PlayerStateDriver stateDriver;
    private GameObject currentWingObject;
    private bool wingEquipped;
    private int refreshVersion;

    private void Awake()
    {
        modelManager = GetComponent<PlayerModelManager>();
        stateDriver = GetComponent<PlayerStateDriver>();
    }

    private void OnEnable()
    {
        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
            modelManager.onModelSwitched += HandleModelSwitched;
        }
    }

    private void OnDisable()
    {
        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
        }
    }

    private void OnDestroy()
    {
        ClearWing();
    }

    public void SetWingEquipped(bool equipped)
    {
        if (wingEquipped == equipped)
        {
            if (equipped && currentWingObject == null)
            {
                RefreshWingAsync().Forget();
            }
            return;
        }

        wingEquipped = equipped;
        if (wingEquipped)
        {
            RefreshWingAsync().Forget();
        }
        else
        {
            ClearWing();
        }
    }

    private void HandleModelSwitched(Animator _)
    {
        if (wingEquipped)
        {
            RefreshWingAsync().Forget();
        }
    }

    private async UniTaskVoid RefreshWingAsync()
    {
        int version = ++refreshVersion;
        await UniTask.Yield();

        if (this == null || !isActiveAndEnabled || !wingEquipped || version != refreshVersion)
        {
            return;
        }

        Transform backMount = ResolveBackMount();
        if (backMount == null)
        {
            Debug.LogWarning("[PlayerWingModelController] Back mount not found under current player model.", this);
            return;
        }

        if (currentWingObject != null && currentWingObject.transform.parent == backMount)
        {
            BindWingAnimator();
            return;
        }

        GameObject prefab = GameMgr.AssetLoader != null
            ? await GameMgr.AssetLoader.LoadAsset<GameObject>(wingModelAddress)
            : null;

        if (this == null || !isActiveAndEnabled || !wingEquipped || version != refreshVersion)
        {
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerWingModelController] Wing model not found: {wingModelAddress}", this);
            return;
        }

        ClearWing();
        currentWingObject = Instantiate(prefab);
        currentWingObject.name = prefab.name;
        AttachWingToBack(currentWingObject.transform, backMount, prefab.transform);
        BindWingAnimator();
    }

    private static void AttachWingToBack(Transform wing, Transform backMount, Transform prefabTransform)
    {
        if (wing == null || backMount == null || prefabTransform == null)
        {
            return;
        }

        wing.SetParent(backMount, false);
        wing.localPosition = prefabTransform.localPosition;
        wing.localRotation = prefabTransform.localRotation;
        wing.localScale = prefabTransform.localScale;
    }

    private void BindWingAnimator()
    {
        if (stateDriver == null)
        {
            stateDriver = GetComponent<PlayerStateDriver>();
        }

        if (stateDriver == null || currentWingObject == null)
        {
            return;
        }

        stateDriver.wingAnimator = currentWingObject.GetComponentInChildren<Animator>(true);
    }

    private void ClearWing()
    {
        refreshVersion++;
        if (stateDriver != null && currentWingObject != null)
        {
            Animator wingAnimator = currentWingObject.GetComponentInChildren<Animator>(true);
            if (stateDriver.wingAnimator == wingAnimator)
            {
                stateDriver.wingAnimator = null;
            }
        }

        if (currentWingObject != null)
        {
            Destroy(currentWingObject);
            currentWingObject = null;
        }
    }

    private Transform ResolveBackMount()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return null;
        }

        Transform weaponRoot = FindChildRecursive(animator.transform, WeaponRootName);
        if (weaponRoot != null)
        {
            Transform back = FindChildRecursive(weaponRoot, BackMountName);
            if (back != null)
            {
                return back;
            }
        }

        return FindChildRecursive(animator.transform, BackMountName);
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
