using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAttackEffect_", menuName = "Data/GameCombat/Weapon Attack Effect")]
public class WeaponAttackEffectSO : ScriptableObject
{
    public string weaponId;
    public string weaponName;

    [Header("Timeline")]
    [Min(0.01f)] public float totalDuration = 1f;
    public List<WeaponAttackTrackData> tracks = new List<WeaponAttackTrackData>();

    [Header("Legacy Animation")]
    public AnimationClip animationClip;
    public string animatorStateName;

    [Header("Animation Clips")]
    public List<WeaponAnimationEvent> animationEvents = new List<WeaponAnimationEvent>();
    public List<WeaponAnimationKeyframeEvent> animationKeyframes = new List<WeaponAnimationKeyframeEvent>();

    [Header("VFX Clips")]
    public List<WeaponVFXEvent> vfxEvents = new List<WeaponVFXEvent>();

    [Header("Audio Clips")]
    public List<WeaponAudioEvent> audioEvents = new List<WeaponAudioEvent>();

    [Header("Hitbox Clips")]
    public List<WeaponHitboxEvent> hitboxEvents = new List<WeaponHitboxEvent>();

    private void OnValidate()
    {
        totalDuration = Mathf.Max(0.01f, totalDuration);

        for (int i = 0; i < tracks.Count; i++)
        {
            if (tracks[i] == null)
            {
                continue;
            }

            tracks[i].EnsureId();
        }

        for (int i = 0; i < animationEvents.Count; i++)
        {
            animationEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < animationKeyframes.Count; i++)
        {
            animationKeyframes[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < vfxEvents.Count; i++)
        {
            vfxEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < audioEvents.Count; i++)
        {
            audioEvents[i]?.Validate(totalDuration);
        }

        for (int i = 0; i < hitboxEvents.Count; i++)
        {
            hitboxEvents[i]?.Validate(totalDuration);
        }
    }

#if UNITY_EDITOR
    public void SyncDurationFromClips()
    {
        float duration = 0.01f;

        if (animationClip != null)
        {
            duration = Mathf.Max(duration, animationClip.length);
        }

        for (int i = 0; i < animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = animationEvents[i];
            if (clip == null || clip.animationClip == null)
            {
                if (clip != null)
                {
                    duration = Mathf.Max(duration, clip.startTime + Mathf.Max(0.01f, clip.duration));
                }
                continue;
            }

            duration = Mathf.Max(duration, clip.startTime + clip.animationClip.length);
        }

        for (int i = 0; i < audioEvents.Count; i++)
        {
            WeaponAudioEvent clip = audioEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.triggerTime + Mathf.Max(0.01f, clip.duration));
            }
        }

        for (int i = 0; i < vfxEvents.Count; i++)
        {
            WeaponVFXEvent clip = vfxEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, clip.triggerTime + Mathf.Max(0.01f, clip.duration));
            }
        }

        for (int i = 0; i < hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent clip = hitboxEvents[i];
            if (clip != null)
            {
                duration = Mathf.Max(duration, Mathf.Max(clip.beginWindowTime, clip.endWindowTime));
            }
        }

        totalDuration = duration;
    }

    public void SyncDurationFromClip()
    {
        SyncDurationFromClips();
    }
#endif
}

public enum WeaponAttackTrackType
{
    Animation,
    Audio,
    Effect,
    Hitbox,
    Custom
}

[Serializable]
public class WeaponAttackTrackData
{
    public string id;
    public string displayName;
    public WeaponAttackTrackType type;
    public Color color = new Color(0.30f, 0.58f, 0.88f);
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

public enum WeaponVFXClipType
{
    SelfMotionEffect,
    ProjectileTrigger
}

public enum WeaponProjectileAimSource
{
    OwnerForward,
    CameraForward
}

[Serializable]
public class WeaponAnimationEvent
{
    public string trackId;
    public string label = "Animation Clip";
    public AnimationClip animationClip;
    [Min(0f)] public float startTime;
    [Min(0.01f)] public float duration = 0.5f;
    public bool blendWithOverlaps = true;
    public string animatorStateName;

    public void Validate(float totalDuration)
    {
        startTime = Mathf.Clamp(startTime, 0f, totalDuration);
        if (animationClip != null)
        {
            duration = Mathf.Max(0.01f, animationClip.length);
        }
        else
        {
            duration = Mathf.Max(0.01f, duration);
        }
    }
}

[Serializable]
public class WeaponAnimationKeyframeEvent
{
    public string trackId;
    public string label = "Keyframe";
    [Min(0f)] public float time;

    public void Validate(float totalDuration)
    {
        time = Mathf.Clamp(time, 0f, totalDuration);
    }
}

[Serializable]
public class WeaponVFXEvent
{
    public string trackId;
    public string label = "Effect Clip";
    public WeaponVFXClipType clipType = WeaponVFXClipType.SelfMotionEffect;
    [Min(0f)] public float triggerTime;
    [Min(0.01f)] public float duration = 0.5f;
    public string vfxKey;
    public GameObject vfxPrefab;
    public Vector3 spawnOffset;
    public Vector3 spawnRotation;
    public bool attachToWeapon = true;

    [Header("Projectile")]
    public string projectileVFXKey;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;
    [Min(0f)] public float damageMultiplier = 1f;
    public bool reverseProjectileTravel;
    public WeaponProjectileAimSource projectileAimSource = WeaponProjectileAimSource.OwnerForward;
    public WeaponProjectileMotionMode projectileMotionMode = WeaponProjectileMotionMode.CodeDriven;
    [Min(0f)] public float projectileSpeed = 12f;
    [Min(0.01f)] public float projectileLifeTime = 2f;
    [Min(0f)] public float visualForwardDistance = 6f;
    public AnimationCurve visualForwardCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public WeaponProjectileHitShape projectileHitShape = WeaponProjectileHitShape.Sphere;
    [Min(0.01f)] public float projectileHitRadius = 0.35f;
    [Min(0.01f)] public float projectileHitHeight = 1f;
    public Vector3 projectileHitCenterOffset;
    public bool pierceMonsters;
    [Min(0)] public int pierceCount;

    public void Validate(float totalDuration)
    {
        triggerTime = Mathf.Clamp(triggerTime, 0f, totalDuration);
        duration = clipType == WeaponVFXClipType.ProjectileTrigger ? 0.05f : Mathf.Max(0.01f, duration);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifeTime = Mathf.Max(0.01f, projectileLifeTime);
        visualForwardDistance = Mathf.Max(0f, visualForwardDistance);
        projectileHitRadius = Mathf.Max(0.01f, projectileHitRadius);
        projectileHitHeight = Mathf.Max(0.01f, projectileHitHeight);
        pierceCount = pierceMonsters ? Mathf.Max(1, pierceCount) : 0;
        if (clipType == WeaponVFXClipType.ProjectileTrigger && string.IsNullOrWhiteSpace(projectileVFXKey))
        {
            projectileVFXKey = vfxKey;
        }
    }
}

[Serializable]
public class WeaponAudioEvent
{
    public string trackId;
    public string label = "Audio Clip";
    [Min(0f)] public float triggerTime;
    [Min(0.01f)] public float duration = 0.2f;
    public string soundGroup = "Game";
    public string soundName;
    public AudioClip audioClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;

    public float Duration => Mathf.Max(0.01f, duration);

    public void Validate(float totalDuration)
    {
        triggerTime = Mathf.Clamp(triggerTime, 0f, totalDuration);
        duration = Mathf.Max(0.01f, duration);
        if (string.IsNullOrWhiteSpace(soundGroup))
        {
            soundGroup = "Game";
        }

        if (string.IsNullOrWhiteSpace(soundName) && audioClip != null)
        {
            soundName = audioClip.name;
        }

        if (audioClip != null && duration <= 0.011f)
        {
            duration = Mathf.Max(0.01f, audioClip.length);
        }
    }
}

[Serializable]
public class WeaponHitboxEvent
{
    public string trackId;
    public string label = "Hitbox Clip";
    [Min(0f)] public float beginWindowTime;
    [Min(0f)] public float applyDamageTime;
    [Min(0f)] public float endWindowTime = 0.5f;
    public Vector3 offset = new Vector3(0f, 1f, 1.2f);
    [Min(0.01f)] public float radius = 1.2f;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;

    public float WindowDuration => Mathf.Max(0f, endWindowTime - beginWindowTime);

    public void Validate(float totalDuration)
    {
        beginWindowTime = Mathf.Clamp(beginWindowTime, 0f, totalDuration);
        endWindowTime = Mathf.Clamp(endWindowTime, beginWindowTime, totalDuration);
        applyDamageTime = Mathf.Clamp(applyDamageTime, beginWindowTime, endWindowTime);
    }
}
