using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAttackController : MonoBehaviour
{
    [Header("Fallback Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.5f;
    [SerializeField] private Vector3 attackOffset = new Vector3(0f, 1f, 1.5f);
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private int fallbackDamage = 10;

    [Header("Debug")]
    [SerializeField] private bool drawAttackGizmos = true;

    private readonly Collider[] overlapResults = new Collider[32];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly HashSet<NetworkPlayer> hitNetworkPlayers = new HashSet<NetworkPlayer>();
    private BossController controller;
    private bool attackWindowOpen;

    public void Bind(BossController bossController)
    {
        controller = bossController;
    }

    public void BeginAttackWindow()
    {
        attackWindowOpen = true;
        hitTargets.Clear();
        hitNetworkPlayers.Clear();
    }

    public void EndAttackWindow()
    {
        attackWindowOpen = false;
        hitTargets.Clear();
        hitNetworkPlayers.Clear();
    }

    public void ApplyAttackDamage()
    {
        if (!attackWindowOpen)
        {
            return;
        }

        BossSkillSO skill = controller != null ? controller.ActiveSkill : null;
        Vector3 center = GetAttackCenter(skill);
        float radius = GetAttackRadius(skill);
        int layerMask = targetLayers.value == 0 ? Physics.AllLayers : targetLayers.value;
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapResults, layerMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            if (hit == null)
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - transform.position).normalized;
            int damage = GetAttackDamage(skill);

            NetworkPlayer networkPlayer = hit.GetComponentInParent<NetworkPlayer>();
            if (networkPlayer != null && hitNetworkPlayers.Contains(networkPlayer))
            {
                continue;
            }

            if (NetworkBoss.TryApplyBossDamageToNetworkPlayer(hit, damage, hitPoint, hitDirection, gameObject))
            {
                if (networkPlayer != null)
                {
                    hitNetworkPlayers.Add(networkPlayer);
                }

                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable))
            {
                continue;
            }

            if (damageable is BossHealth)
            {
                continue;
            }

            hitTargets.Add(damageable);
            damageable.TakeDamage(new AttackDamageInfo(damage, hitPoint, hitDirection, gameObject));
        }
    }

    private int GetAttackDamage(BossSkillSO skill)
    {
        if (skill != null && controller != null)
        {
            return skill.GetDamage(controller);
        }

        return controller != null ? controller.CurrentAttack : Mathf.Max(1, fallbackDamage);
    }

    private float GetAttackRadius(BossSkillSO skill)
    {
        return skill != null ? Mathf.Max(0.05f, skill.hitRadius) : Mathf.Max(0.05f, attackRadius);
    }

    private Vector3 GetAttackCenter(BossSkillSO skill)
    {
        if (skill != null && controller != null)
        {
            return skill.GetHitCenter(controller);
        }

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

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackCenter(null), attackRadius);
    }
}
