using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BossSkill", menuName = "Data/GameCharacter/Boss/Boss Skill")]
public class BossSkillSO : ScriptableObject
{
    [Header("Identity")]
    public string skillID;
    public string displayName;
    public BossSkillReleaseType releaseType = BossSkillReleaseType.Melee;

    [Header("Decision")]
    public float cooldown = 3f;
    public float minUseRange = 0f;
    public float maxUseRange = 3f;
    [Min(1)] public int weight = 1;

    [Header("Timeline")]
    [Min(0.01f)] public float totalDuration = 1f;
    public List<BossSkillTrackData> tracks = new List<BossSkillTrackData>();

    [Header("Animation")]
    public string animationStateName = "Attack";
    [Range(0f, 1f)] public float finishedNormalizedTime = 0.95f;
    public float fallbackDuration = 2f;
    public bool stopMovementWhenCasting = true;
    public bool faceTargetDuringCast = true;
    public List<BossAnimationClipEvent> animationEvents = new List<BossAnimationClipEvent>();
    public List<BossAnimationKeyframeEvent> animationKeyframes = new List<BossAnimationKeyframeEvent>();

    [Header("Audio Clips")]
    public List<BossAudioClipEvent> audioEvents = new List<BossAudioClipEvent>();

    [Header("VFX Clips")]
    public List<BossVFXEvent> vfxEvents = new List<BossVFXEvent>();

    [Header("Barrage / Projectile Clips")]
    public List<BossBarrageEvent> barrageEvents = new List<BossBarrageEvent>();

    [Header("Hitbox Clips")]
    public List<BossHitboxEvent> hitboxEvents = new List<BossHitboxEvent>();

    [Header("Contact Damage Clips")]
    public List<BossContactDamageEvent> contactDamageEvents = new List<BossContactDamageEvent>();

    [Header("Movement Clips")]
    public List<BossMovementEvent> movementEvents = new List<BossMovementEvent>();

    [Header("Damage")]
    public float damageMultiplier = 1f;
    public float hitRadius = 1.2f;
    public Vector3 hitOffset = new Vector3(0f, 1f, 1.4f);
    public Transform hitPointOverride;

    public virtual bool CanUse(BossController boss, Transform target)
    {
        if (boss == null || target == null)
        {
            return false;
        }

        float distance = Vector3.Distance(boss.transform.position, target.position);
        return distance >= minUseRange && distance <= maxUseRange;
    }

    public virtual int GetDamage(BossController boss)
    {
        int baseDamage = boss != null ? boss.CurrentAttack : 1;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Max(0f, damageMultiplier)));
    }

    public virtual Vector3 GetHitCenter(BossController boss)
    {
        if (hitPointOverride != null)
        {
            return hitPointOverride.position;
        }

        return boss != null ? boss.transform.TransformPoint(hitOffset) : Vector3.zero;
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        totalDuration = Mathf.Max(0.01f, totalDuration);
        fallbackDuration = Mathf.Max(0.01f, fallbackDuration);
        maxUseRange = Mathf.Max(minUseRange, maxUseRange);
        weight = Mathf.Max(1, weight);

        for (int i = 0; i < tracks.Count; i++)
        {
            tracks[i]?.EnsureId();
        }

        for (int i = 0; i < animationEvents.Count; i++)
        {
            animationEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < animationKeyframes.Count; i++)
        {
            animationKeyframes[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < audioEvents.Count; i++)
        {
            audioEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < vfxEvents.Count; i++)
        {
            vfxEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < barrageEvents.Count; i++)
        {
            barrageEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < hitboxEvents.Count; i++)
        {
            hitboxEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < contactDamageEvents.Count; i++)
        {
            contactDamageEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < movementEvents.Count; i++)
        {
            movementEvents[i]?.Validate(totalDuration);
        }
    }

#if UNITY_EDITOR
    public void SyncDurationFromTimeline()
    {
        float duration = 0.01f;

        for (int i = 0; i < animationEvents.Count; i++)
        {
            BossAnimationClipEvent clip = animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            duration = Mathf.Max(duration, clip.startTime + Mathf.Max(0.01f, clip.Duration));
        }

        for (int i = 0; i < audioEvents.Count; i++)
        {
            BossAudioClipEvent clip = audioEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.triggerTime + Mathf.Max(0.01f, clip.Duration));
            }
        }

        for (int i = 0; i < vfxEvents.Count; i++)
        {
            BossVFXEvent clip = vfxEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.endTime);
            }
        }

        for (int i = 0; i < barrageEvents.Count; i++)
        {
            BossBarrageEvent clip = barrageEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.triggerTime);
            }
        }

        for (int i = 0; i < hitboxEvents.Count; i++)
        {
            BossHitboxEvent clip = hitboxEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.endWindowTime);
            }
        }

        for (int i = 0; i < contactDamageEvents.Count; i++)
        {
            BossContactDamageEvent clip = contactDamageEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.endWindowTime);
            }
        }

        for (int i = 0; i < movementEvents.Count; i++)
        {
            BossMovementEvent clip = movementEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.triggerTime);
            }
        }

        totalDuration = duration;
        fallbackDuration = Mathf.Max(fallbackDuration, totalDuration);
    }
#endif
}

public enum BossSkillReleaseType
{
    Melee,
    Ranged,
    Movement,
    Special
}

public enum BossSkillTrackType
{
    Animation = 0,
    Audio = 1,
    Barrage = 2,
    Hitbox = 3,
    ContactDamage = 4,
    Custom = 5,
    VFX = 6,
    Movement = 7
}

public enum BossProjectileAimSource
{
    BossForward,
    TargetPosition
}

public enum BossHitboxShape
{
    Sphere = 0,
    Cylinder = 1
}

public enum BossMovementTargetSource
{
    SkillStartTargetPosition,
    CurrentTargetPosition,
    BossCurrentPosition
}

[Serializable]
public class BossSkillTrackData
{
    public string id;
    public string displayName;
    public BossSkillTrackType type;
    public Color color = new Color(0.72f, 0.26f, 0.18f);
    public bool muted;
    public bool locked;
    public bool hiddenInPreview;

    public void EnsureId()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
        }
    }
}

[Serializable]
public class BossAnimationClipEvent
{
    public string trackId;
    public string label = "Boss Animation";
    public AnimationClip animationClip;
    [Min(0f)] public float startTime;
    [Min(0.01f)] public float duration = 0.5f;
    [Min(0.01f)] public float playbackSpeed = 1f;
    public string animatorStateName;
    public bool blendWithOverlaps = true;

    public float Duration => animationClip != null
        ? Mathf.Max(0.01f, animationClip.length / Mathf.Max(0.01f, playbackSpeed))
        : Mathf.Max(0.01f, duration);

    public void Validate(float totalDuration)
    {
        startTime = Mathf.Clamp(startTime, 0f, totalDuration);
        playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
        duration = Duration;
        if (string.IsNullOrWhiteSpace(label) && animationClip != null)
        {
            label = animationClip.name;
        }
    }
}

[Serializable]
public class BossAnimationKeyframeEvent
{
    public string trackId;
    public string label = "K";
    [Min(0f)] public float time;

    public void Validate(float totalDuration)
    {
        time = Mathf.Clamp(time, 0f, totalDuration);
    }
}

[Serializable]
public class BossVFXEvent
{
    public string trackId;
    public string label = "Boss VFX";
    [FormerlySerializedAs("triggerTime"), Min(0f)] public float startTime;
    [Min(0f)] public float endTime = 1f;
    public string vfxKey;
    public GameObject previewPrefab;
    public Vector3 offset;
    public Vector3 rotation;
    public bool followBoss;

    public float Duration => Mathf.Max(0.01f, endTime - startTime);

    public void Validate(float totalDuration)
    {
        startTime = Mathf.Clamp(startTime, 0f, totalDuration);
        endTime = Mathf.Clamp(Mathf.Max(endTime, startTime + 0.01f), startTime, totalDuration);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = previewPrefab != null ? previewPrefab.name : "Boss VFX";
        }

        if (string.IsNullOrWhiteSpace(vfxKey) && previewPrefab != null)
        {
            vfxKey = previewPrefab.name;
        }
    }
}

[Serializable]
public class BossAudioClipEvent
{
    public string trackId;
    public string label = "Boss Audio";
    [Min(0f)] public float triggerTime;
    [Min(0.01f)] public float duration = 0.2f;
    public string soundGroup = "Game";
    public string soundName;
    public AudioClip audioClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;

    public float Duration => audioClip != null ? Mathf.Max(0.01f, audioClip.length) : Mathf.Max(0.01f, duration);

    public void Validate(float totalDuration)
    {
        triggerTime = Mathf.Clamp(triggerTime, 0f, totalDuration);
        duration = Duration;
        if (string.IsNullOrWhiteSpace(soundGroup))
        {
            soundGroup = "Game";
        }

        if (string.IsNullOrWhiteSpace(soundName) && audioClip != null)
        {
            soundName = audioClip.name;
        }
    }
}

[Serializable]
public class BossBarrageEvent
{
    public string trackId;
    public string label = "Boss Barrage";
    [HideInInspector, Min(0f)] public float triggerTime;
    public string projectileVFXKey;
    public GameObject projectilePrefab;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;
    public BossProjectileAimSource aimSource = BossProjectileAimSource.TargetPosition;
    public float targetAimHeightOffset = 1f;
    [Range(0f, 90f)] public float aimRandomAngle;
    [Min(0f)] public float speed = 10f;
    [Min(0.01f)] public float lifeTime = 3f;
    public Vector3 spawnOffset = new Vector3(0f, 1.2f, 1f);
    public Vector3 spawnRotation;
    public bool stabilizeVisualRotation = true;
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0.01f)] public float hitRadius = 0.4f;
    public Vector3 hitCenterOffset;
    [Min(0)] public int projectileCount = 1;
    [Range(0f, 360f)] public float spreadAngle = 0f;

    public void Validate(float totalDuration)
    {
        triggerTime = Mathf.Clamp(triggerTime, 0f, totalDuration);
        targetAimHeightOffset = Mathf.Max(0f, targetAimHeightOffset);
        aimRandomAngle = Mathf.Clamp(aimRandomAngle, 0f, 90f);
        speed = Mathf.Max(0f, speed);
        lifeTime = Mathf.Max(0.01f, lifeTime);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        hitRadius = Mathf.Max(0.01f, hitRadius);
        projectileCount = Mathf.Max(1, projectileCount);
    }
}

[Serializable]
public class BossHitboxEvent
{
    public string trackId;
    public string label = "Boss Hitbox";
    [Min(0f)] public float beginWindowTime;
    [Min(0f)] public float applyDamageTime;
    [Min(0f)] public float endWindowTime = 0.5f;
    public BossHitboxShape shape = BossHitboxShape.Sphere;
    public Vector3 offset = new Vector3(0f, 1f, 1.5f);
    public Vector3 rotation;
    [Min(0.01f)] public float radius = 1.2f;
    [Min(0.01f)] public float height = 1.5f;
    [Min(0f)] public float damageMultiplier = 1f;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;

    public float WindowDuration => Mathf.Max(0f, endWindowTime - beginWindowTime);

    public void Validate(float totalDuration)
    {
        beginWindowTime = Mathf.Clamp(beginWindowTime, 0f, totalDuration);
        endWindowTime = Mathf.Clamp(endWindowTime, beginWindowTime, totalDuration);
        applyDamageTime = Mathf.Clamp(applyDamageTime, beginWindowTime, endWindowTime);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        radius = Mathf.Max(0.01f, radius);
        height = Mathf.Max(0.01f, height);
    }
}

[Serializable]
public class BossContactDamageEvent
{
    public string trackId;
    public string label = "Body Contact";
    [Min(0f)] public float beginWindowTime;
    [Min(0f)] public float endWindowTime = 0.8f;
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0f)] public float hitInterval;
    public bool hitOncePerTarget = true;
    public bool useAllBodyCapsules = true;
    public List<int> capsuleIndices = new List<int>();
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;

    public float WindowDuration => Mathf.Max(0f, endWindowTime - beginWindowTime);

    public void Validate(float totalDuration)
    {
        beginWindowTime = Mathf.Clamp(beginWindowTime, 0f, totalDuration);
        endWindowTime = Mathf.Clamp(endWindowTime, beginWindowTime, totalDuration);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        hitInterval = Mathf.Max(0f, hitInterval);
        for (int i = capsuleIndices.Count - 1; i >= 0; i--)
        {
            if (capsuleIndices[i] < 0)
            {
                capsuleIndices.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public class BossMovementEvent
{
    public string trackId;
    public string label = "Boss Movement";
    [HideInInspector, Min(0f)] public float triggerTime;
    public BossMovementTargetSource targetSource = BossMovementTargetSource.SkillStartTargetPosition;
    public Vector3 targetOffset;
    public bool projectToGround = true;
    public float groundRayStartHeight = 8f;
    public float groundRayDistance = 40f;
    public LayerMask groundLayers = Physics.DefaultRaycastLayers;
    public bool keepCurrentY;
    public bool faceTargetAfterMove = true;
    public bool disableCollisionUntilContactDamage = true;
    public bool showTargetWarning;
    [FormerlySerializedAs("warningLeadTime"), Min(0f)] public float warningDuration = 1f;
    public string warningVFXKey;
    public GameObject warningPrefab;
    public Vector3 warningOffset;
    public Vector3 warningRotation;
    public Vector3 warningScale = Vector3.one;

    public void Validate(float totalDuration)
    {
        triggerTime = Mathf.Clamp(triggerTime, 0f, totalDuration);
        groundRayStartHeight = Mathf.Max(0f, groundRayStartHeight);
        groundRayDistance = Mathf.Max(0.01f, groundRayDistance);
        warningDuration = Mathf.Max(0f, warningDuration);
        if (warningScale == Vector3.zero)
        {
            warningScale = Vector3.one;
        }
    }
}
