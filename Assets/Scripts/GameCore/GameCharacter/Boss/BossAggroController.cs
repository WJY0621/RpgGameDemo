using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAggroController : MonoBehaviour
{
    [Header("Threat")]
    [SerializeField, Min(0f)] private float initialThreat = 1f;
    [SerializeField, Min(0f)] private float damageThreatMultiplier = 1f;
    [SerializeField, Min(0f)] private float decayPerSecond = 1.5f;
    [SerializeField, Range(1f, 2f)] private float currentTargetStickiness = 1.15f;

    [Header("Target Selection")]
    [SerializeField, Min(0.05f)] private float selectionInterval = 0.25f;
    [SerializeField, Min(0f)] private float nearTargetBonusRange = 8f;
    [SerializeField, Min(0f)] private float nearTargetBonus = 5f;

    private readonly Dictionary<Transform, ThreatEntry> threatTable = new Dictionary<Transform, ThreatEntry>();
    private BossController controller;
    private float selectionTimer;
    private Transform cachedBestTarget;

    public Transform CurrentBestTarget => cachedBestTarget;
    public int ThreatTargetCount => threatTable.Count;

    private void Awake()
    {
        controller = GetComponent<BossController>();
    }

    public void Bind(BossController bossController)
    {
        controller = bossController != null ? bossController : GetComponent<BossController>();
    }

    public void Clear()
    {
        threatTable.Clear();
        cachedBestTarget = null;
        selectionTimer = 0f;
    }

    public void RegisterTarget(Transform target, float threat)
    {
        Transform playerTarget = ResolveThreatTarget(target);
        if (!IsValidTarget(playerTarget))
        {
            return;
        }

        AddThreatInternal(playerTarget, Mathf.Max(initialThreat, threat));
    }

    public bool AddDamageThreat(GameObject attacker, int actualDamage)
    {
        if (actualDamage <= 0)
        {
            return false;
        }

        Transform playerTarget = ResolveThreatTarget(attacker != null ? attacker.transform : null);
        if (!IsValidTarget(playerTarget))
        {
            return false;
        }

        AddThreatInternal(playerTarget, Mathf.Max(1f, actualDamage * damageThreatMultiplier));
        return true;
    }

    public Transform TickAndSelectTarget(float deltaTime, Transform currentTarget, float validRange)
    {
        TickThreatDecay(Mathf.Max(0f, deltaTime), validRange);

        selectionTimer -= deltaTime;
        if (selectionTimer > 0f && IsValidTarget(cachedBestTarget))
        {
            return cachedBestTarget;
        }

        selectionTimer = selectionInterval;
        cachedBestTarget = SelectBestTarget(currentTarget, validRange);
        return cachedBestTarget;
    }

    private void AddThreatInternal(Transform target, float amount)
    {
        if (target == null || amount <= 0f)
        {
            return;
        }

        if (!threatTable.TryGetValue(target, out ThreatEntry entry))
        {
            entry = new ThreatEntry();
            threatTable[target] = entry;
        }

        entry.threat = Mathf.Max(0f, entry.threat + amount);
        entry.lastThreatTime = Time.time;
        cachedBestTarget = SelectBestTarget(cachedBestTarget != null ? cachedBestTarget : target, GetValidRange());
    }

    private void TickThreatDecay(float deltaTime, float validRange)
    {
        if (threatTable.Count <= 0)
        {
            cachedBestTarget = null;
            return;
        }

        List<Transform> removeTargets = null;
        foreach (KeyValuePair<Transform, ThreatEntry> pair in threatTable)
        {
            Transform target = pair.Key;
            ThreatEntry entry = pair.Value;

            if (!IsValidTarget(target) || IsTargetOutOfRange(target, validRange))
            {
                removeTargets ??= new List<Transform>();
                removeTargets.Add(target);
                continue;
            }

            entry.threat = Mathf.Max(0f, entry.threat - decayPerSecond * deltaTime);
            if (entry.threat <= 0.0001f)
            {
                removeTargets ??= new List<Transform>();
                removeTargets.Add(target);
            }
        }

        if (removeTargets == null)
        {
            return;
        }

        for (int i = 0; i < removeTargets.Count; i++)
        {
            threatTable.Remove(removeTargets[i]);
        }
    }

    private Transform SelectBestTarget(Transform currentTarget, float validRange)
    {
        Transform bestTarget = null;
        float bestScore = float.MinValue;

        foreach (KeyValuePair<Transform, ThreatEntry> pair in threatTable)
        {
            Transform target = pair.Key;
            if (!IsValidTarget(target) || IsTargetOutOfRange(target, validRange))
            {
                continue;
            }

            float score = pair.Value.threat;
            if (target == currentTarget)
            {
                score *= currentTargetStickiness;
            }

            if (nearTargetBonusRange > 0f && Vector3.Distance(transform.position, target.position) <= nearTargetBonusRange)
            {
                score += nearTargetBonus;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private Transform ResolveThreatTarget(Transform source)
    {
        if (source == null)
        {
            return null;
        }

        NetworkPlayer networkPlayer = source.GetComponentInParent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            return networkPlayer.transform;
        }

        PlayerStateDriver playerDriver = source.GetComponentInParent<PlayerStateDriver>();
        if (playerDriver != null)
        {
            return playerDriver.transform;
        }

        PlayerHealth playerHealth = source.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        return source;
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            return false;
        }

        NetworkPlayer networkPlayer = target.GetComponent<NetworkPlayer>();
        if (networkPlayer != null && networkPlayer.CurrentHP <= 0)
        {
            return false;
        }

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        return playerHealth == null || !playerHealth.IsDead;
    }

    private bool IsTargetOutOfRange(Transform target, float validRange)
    {
        return validRange > 0f && Vector3.Distance(transform.position, target.position) > validRange;
    }

    private float GetValidRange()
    {
        return controller != null ? controller.LoseTargetRange : 0f;
    }

    private sealed class ThreatEntry
    {
        public float threat;
        public float lastThreatTime;
    }
}
