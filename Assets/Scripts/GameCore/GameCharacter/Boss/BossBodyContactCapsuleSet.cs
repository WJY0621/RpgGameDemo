using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossBodyContactCapsuleSet : MonoBehaviour
{
    private const string SolidColliderRootName = "[BossSolidBodyColliders]";

    [SerializeField] private Color gizmoColor = new Color(1f, 0.35f, 0.05f, 0.7f);
    [SerializeField] private List<BossBodyContactCapsule> capsules = new List<BossBodyContactCapsule>();

    [Header("Generated Solid Collision (Editor Only)")]
    [SerializeField, Min(0f)] private float solidColliderRadiusInset = 0.05f;
    [SerializeField] private PhysicMaterial solidColliderMaterial;

    public int Count => capsules.Count;

    public bool TryGetCapsule(int index, out Vector3 start, out Vector3 end, out float radius)
    {
        start = default;
        end = default;
        radius = 0f;

        if (index < 0 || index >= capsules.Count)
        {
            return false;
        }

        BossBodyContactCapsule capsule = capsules[index];
        if (capsule == null || !capsule.enabled)
        {
            return false;
        }

        start = capsule.startPoint != null ? capsule.startPoint.position : transform.TransformPoint(capsule.localStart);
        end = capsule.endPoint != null ? capsule.endPoint.position : transform.TransformPoint(capsule.localEnd);
        radius = Mathf.Max(0.01f, capsule.radius);
        return true;
    }

    public bool ShouldUseCapsule(BossContactDamageEvent contactEvent, int index)
    {
        if (contactEvent == null || contactEvent.useAllBodyCapsules || contactEvent.capsuleIndices == null || contactEvent.capsuleIndices.Count == 0)
        {
            return true;
        }

        return contactEvent.capsuleIndices.Contains(index);
    }

    public bool IsGeneratedSolidCollider(Collider target)
    {
        if (target == null)
        {
            return false;
        }

        Transform solidRoot = transform.Find(SolidColliderRootName);
        return solidRoot != null && target.transform.IsChildOf(solidRoot);
    }

    public void SetGeneratedSolidCollidersEnabled(bool enabled)
    {
        Transform solidRoot = transform.Find(SolidColliderRootName);
        if (solidRoot == null)
        {
            return;
        }

        Collider[] generatedColliders = solidRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < generatedColliders.Length; i++)
        {
            generatedColliders[i].enabled = enabled;
        }
    }

    private void Awake()
    {
        SetGeneratedSolidCollidersEnabled(false);
    }

    [ContextMenu("Rebuild Solid Colliders")]
    public void RebuildSolidColliders()
    {
        Transform solidRoot = GetOrCreateSolidColliderRoot();
        if (solidRoot == null)
        {
            return;
        }

        ClearGeneratedColliders(solidRoot);

        for (int i = 0; i < capsules.Count; i++)
        {
            if (!TryGetCapsule(i, out Vector3 start, out Vector3 end, out float radius))
            {
                continue;
            }

            CreateSolidCapsuleCollider(solidRoot, i, start, end, radius);
        }
    }

    private Transform GetOrCreateSolidColliderRoot()
    {
        Transform existing = transform.Find(SolidColliderRootName);
        if (existing != null)
        {
            existing.gameObject.layer = gameObject.layer;
            return existing;
        }

        GameObject rootObject = new GameObject(SolidColliderRootName);
        rootObject.layer = gameObject.layer;
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        return rootObject.transform;
    }

    private static void ClearGeneratedColliders(Transform solidRoot)
    {
        for (int i = solidRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = solidRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void CreateSolidCapsuleCollider(Transform solidRoot, int index, Vector3 start, Vector3 end, float radius)
    {
        float solidRadius = Mathf.Max(0.01f, radius - solidColliderRadiusInset);
        Vector3 axis = end - start;
        float height = Mathf.Max(solidRadius * 2f, axis.magnitude + solidRadius * 2f);
        Vector3 center = (start + end) * 0.5f;
        Quaternion rotation = axis.sqrMagnitude > 0.0001f
            ? Quaternion.FromToRotation(Vector3.up, axis.normalized)
            : Quaternion.identity;

        GameObject colliderObject = new GameObject($"SolidCapsule_{index:00}");
        colliderObject.layer = solidRoot.gameObject.layer;
        colliderObject.transform.SetParent(solidRoot, true);
        colliderObject.transform.SetPositionAndRotation(center, rotation);

        CapsuleCollider capsuleCollider = colliderObject.AddComponent<CapsuleCollider>();
        capsuleCollider.isTrigger = false;
        capsuleCollider.direction = 1;
        capsuleCollider.center = Vector3.zero;
        capsuleCollider.radius = solidRadius;
        capsuleCollider.height = height;
        capsuleCollider.material = solidColliderMaterial;
    }

    private void OnValidate()
    {
        solidColliderRadiusInset = Mathf.Max(0f, solidColliderRadiusInset);
        for (int i = 0; i < capsules.Count; i++)
        {
            capsules[i]?.Validate();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        for (int i = 0; i < capsules.Count; i++)
        {
            if (!TryGetCapsule(i, out Vector3 start, out Vector3 end, out float radius))
            {
                continue;
            }

            DrawCapsuleGizmo(start, end, radius);
        }
    }

    private static void DrawCapsuleGizmo(Vector3 start, Vector3 end, float radius)
    {
        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);

        Vector3 axis = end - start;
        Vector3 side = Vector3.Cross(axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up, Vector3.up);
        if (side.sqrMagnitude <= 0.0001f)
        {
            side = Vector3.right;
        }

        side.Normalize();
        Vector3 otherSide = Vector3.Cross(axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward, side).normalized;
        Gizmos.DrawLine(start + side * radius, end + side * radius);
        Gizmos.DrawLine(start - side * radius, end - side * radius);
        Gizmos.DrawLine(start + otherSide * radius, end + otherSide * radius);
        Gizmos.DrawLine(start - otherSide * radius, end - otherSide * radius);
    }
}

[Serializable]
public sealed class BossBodyContactCapsule
{
    public string label = "Body Capsule";
    public bool enabled = true;
    public Transform startPoint;
    public Transform endPoint;
    public Vector3 localStart = new Vector3(0f, 1f, -0.5f);
    public Vector3 localEnd = new Vector3(0f, 1f, 0.5f);
    [Min(0.01f)] public float radius = 0.5f;

    public void Validate()
    {
        radius = Mathf.Max(0.01f, radius);
    }
}
