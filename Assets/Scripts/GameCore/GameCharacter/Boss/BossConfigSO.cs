using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossPhaseConfig
{
    public string phaseName = "Phase";
    [Range(0f, 1f)] public float enterAtHpPercent = 1f;

    [Header("Transition")]
    public string phaseEnterAnimation;
    public float phaseEnterMinDuration = 1f;
    public bool invincibleDuringTransition = true;

    [Header("Modifiers")]
    public float attackMultiplier = 1f;
    public float moveSpeedMultiplier = 1f;
    public float cooldownMultiplier = 1f;

    [Header("Skill Pool")]
    public List<BossSkillSO> skills = new List<BossSkillSO>();
}

[CreateAssetMenu(fileName = "BossConfig", menuName = "Data/GameCharacter/Boss/Boss Config")]
public class BossConfigSO : ScriptableObject
{
    [Header("Identity")]
    public int bossID;
    public string bossName = "Boss";

    [Header("Stats")]
    public int maxHP = 300;
    public int attack = 20;
    public int defense = 0;
    public float moveSpeed = 3.5f;

    [Header("AI")]
    public float detectRange = 18f;
    public float loseTargetRange = 30f;
    public float preferredCombatRange = 4f;
    public float skillDecisionInterval = 0.25f;
    public bool canMove = true;
    public bool autoStartOnDetect = false;
    public bool enterHurtStateOnDamage = false;

    [Header("Damage Reaction")]
    public bool enterHurtStateOnQuarterHpLoss = true;
    [Range(0.05f, 1f)] public float hurtHpLossStep = 0.25f;

    [Header("Animation")]
    public string idleStateName = "Idle";
    public string idleBattleStateName = "IdleBattle";
    public string runStateName = "Run";
    public string hurtStateName = "GetHit";
    public string deadStateName = "Die";
    [Range(0f, 1f)] public float hurtFinishedNormalizedTime = 0.9f;

    [Header("Skills")]
    public List<BossSkillSO> defaultSkills = new List<BossSkillSO>();

    [Header("Phases")]
    public List<BossPhaseConfig> phases = new List<BossPhaseConfig>();

    [Header("Death Reward Chest")]
    public bool dropChestOnDeath = true;
    public GameObject deathChestPrefab;
    public string deathChestPrefabAddress = "ChestNew";
    public Vector3 deathChestSpawnOffset = Vector3.zero;
    public float deathChestGroundProbeHeight = 3f;
    public float deathChestGroundProbeDistance = 12f;
}
