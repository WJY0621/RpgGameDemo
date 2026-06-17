using UnityEngine;
using UnityEngine.Animations;

[DisallowMultipleComponent]
public sealed class PlayerWeaponAttachmentController : MonoBehaviour
{
    [SerializeField] private string swordObjectName = "Sword";
    [SerializeField] private string handSourceName = "Weapon_Target_Hand_R";
    [SerializeField] private string backSourceName = "WeaponTarget_Back";

    private ParentConstraint parentConstraint;

    public void AttachWeaponToHand()
    {
        SetSourceWeights(1f, 0f);
    }

    public void AttachWeaponToBack()
    {
        SetSourceWeights(0f, 1f);
    }

    private void SetSourceWeights(float handWeight, float backWeight)
    {
        ParentConstraint constraint = GetParentConstraint();
        if (constraint == null)
        {
            Debug.LogWarning("[PlayerWeaponAttachmentController] ParentConstraint on Sword was not found.", this);
            return;
        }

        for (int i = 0; i < constraint.sourceCount; i++)
        {
            ConstraintSource source = constraint.GetSource(i);
            if (source.sourceTransform == null)
            {
                continue;
            }

            if (source.sourceTransform.name == handSourceName)
            {
                source.weight = handWeight;
                constraint.SetSource(i, source);
            }
            else if (source.sourceTransform.name == backSourceName)
            {
                source.weight = backWeight;
                constraint.SetSource(i, source);
            }
        }
    }

    private ParentConstraint GetParentConstraint()
    {
        if (parentConstraint != null)
        {
            return parentConstraint;
        }

        Transform sword = FindChildRecursive(transform, swordObjectName);
        if (sword != null)
        {
            parentConstraint = sword.GetComponent<ParentConstraint>();
        }

        return parentConstraint;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
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
