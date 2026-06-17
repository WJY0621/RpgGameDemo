using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterAttackController : MonoBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private Vector3 attackOffset = new Vector3(0f, 1f, 1.2f);
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private int baseDamage = 5;

    [Header("Debug")]
    [SerializeField] private bool drawAttackGizmos = true;

    private readonly Collider[] overlapResults = new Collider[16];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private MonsterController controller;
    private bool attackWindowOpen;

    public void Bind(MonsterController monsterController)
    {
        controller = monsterController;
    }

    public void BeginAttackWindow()
    {
        attackWindowOpen = true;
        hitTargets.Clear();
    }

    public void EndAttackWindow()
    {
        attackWindowOpen = false;
        hitTargets.Clear();
    }

    public void ApplyAttackDamage()
    {
        if (!attackWindowOpen)
        {
            return;
        }

        Vector3 center = GetAttackCenter();
        int layerMask = targetLayers.value == 0 ? Physics.AllLayers : targetLayers.value;
        int hitCount = Physics.OverlapSphereNonAlloc(center, attackRadius, overlapResults, layerMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = ResolveDamageable(hit);
            if (damageable == null || hitTargets.Contains(damageable))
            {
                continue;
            }

            if (damageable is MonsterHealth)
            {
                continue;
            }

            hitTargets.Add(damageable);

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - transform.position).normalized;
            damageable.TakeDamage(new AttackDamageInfo(GetAttackDamage(), hitPoint, hitDirection, gameObject));
        }
    }

    private static IDamageable ResolveDamageable(Collider hit)
    {
        if (hit == null)
        {
            return null;
        }

        IDamageable damageable = hit.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            return damageable;
        }

        PlayerStateDriver player = hit.GetComponentInParent<PlayerStateDriver>();
        if (player != null)
        {
            return player.GetComponent<PlayerHealth>() ?? player.GetComponentInChildren<PlayerHealth>(true);
        }

        player = hit.GetComponentInChildren<PlayerStateDriver>(true);
        if (player != null)
        {
            return player.GetComponent<PlayerHealth>() ?? player.GetComponentInChildren<PlayerHealth>(true);
        }

        return hit.GetComponentInChildren<IDamageable>(true);
    }

    private int GetAttackDamage()
    {
        if (controller != null && controller.Runtime != null)
        {
            return Mathf.Max(1, controller.Runtime.GetATK());
        }

        return Mathf.Max(1, baseDamage);
    }

    private Vector3 GetAttackCenter()
    {
        if (attackPoint != null)
        {
            return attackPoint.position;
        }

        return transform.TransformPoint(attackOffset);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAttackGizmos)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetAttackCenter(), attackRadius);
    }
}
