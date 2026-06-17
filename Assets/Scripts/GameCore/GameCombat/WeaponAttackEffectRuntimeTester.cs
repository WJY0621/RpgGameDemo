using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class WeaponAttackEffectRuntimeTester : MonoBehaviour
{
    private const int MaxHitResults = 32;

    [Header("Input")]
    [SerializeField] private KeyCode playKey = KeyCode.Space;
    [SerializeField] private bool restartWhenPressed = true;

    [Header("Weapon Effect")]
    [SerializeField] private WeaponAttackEffectSO weaponEffect;
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private Transform effectRoot;
    [SerializeField] private AudioSource audioSource;

    [Header("Damage Test")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(1)] private int baseDamage = 10;
    [SerializeField] private bool drawRuntimeGizmos = true;

    private readonly Collider[] hitResults = new Collider[MaxHitResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private PlayableGraph animationGraph;
    private AnimationMixerPlayable animationMixer;
    private AnimationClipPlayable playableA;
    private AnimationClipPlayable playableB;
    private AnimationClip activeClipA;
    private AnimationClip activeClipB;
    private bool animationGraphValid;

    private bool[] vfxTriggered;
    private bool[] audioTriggered;
    private bool[] hitboxApplied;
    private bool playing;
    private float playTime;

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>();
        }

        if (effectRoot == null)
        {
            effectRoot = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(playKey))
        {
            if (!playing || restartWhenPressed)
            {
                Play();
            }
        }

        if (!playing || weaponEffect == null)
        {
            return;
        }

        playTime += Time.deltaTime;
        UpdateAnimation();
        UpdateAudioEvents();
        UpdateVfxEvents();
        UpdateHitboxEvents();

        if (playTime >= GetPlaybackDuration())
        {
            Stop();
        }
    }

    public void Play()
    {
        if (weaponEffect == null)
        {
            Debug.LogWarning("[WeaponAttackEffectRuntimeTester] WeaponAttackEffectSO is not assigned.", this);
            return;
        }

        Stop();
        playing = true;
        playTime = 0f;
        vfxTriggered = new bool[weaponEffect.vfxEvents.Count];
        audioTriggered = new bool[weaponEffect.audioEvents.Count];
        hitboxApplied = new bool[weaponEffect.hitboxEvents.Count];
        hitTargets.Clear();

        EnsureAnimationGraph();
        UpdateAnimation();
    }

    public void Stop()
    {
        playing = false;
        playTime = 0f;
        hitTargets.Clear();
        DestroyAnimationGraph();
        CleanupSpawnedObjects();
    }

    private void OnDisable()
    {
        Stop();
    }

    private float GetPlaybackDuration()
    {
        float duration = weaponEffect != null ? Mathf.Max(0.01f, weaponEffect.totalDuration) : 0.01f;

        for (int i = 0; i < weaponEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = weaponEffect.animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
            duration = Mathf.Max(duration, clip.startTime + clipDuration);
        }

        for (int i = 0; i < weaponEffect.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = weaponEffect.vfxEvents[i];
            if (vfx == null)
            {
                continue;
            }

            float clipDuration = vfx.clipType == WeaponVFXClipType.ProjectileTrigger
                ? Mathf.Max(0.01f, vfx.projectileLifeTime)
                : Mathf.Max(0.01f, vfx.duration);
            duration = Mathf.Max(duration, vfx.triggerTime + clipDuration);
        }

        return duration;
    }

    private void UpdateAnimation()
    {
        if (targetAnimator == null || weaponEffect == null)
        {
            return;
        }

        FindActiveAnimationClips(out WeaponAnimationEvent first, out WeaponAnimationEvent second);
        if (first == null || first.animationClip == null)
        {
            SetAnimationInputs(null, null, 0f, 0f, 0f);
            return;
        }

        float localA = Mathf.Clamp(playTime - first.startTime, 0f, first.animationClip.length);
        if (second == null || second.animationClip == null)
        {
            SetAnimationInputs(first.animationClip, null, localA, 0f, 0f);
            return;
        }

        float localB = Mathf.Clamp(playTime - second.startTime, 0f, second.animationClip.length);
        float overlapStart = Mathf.Max(first.startTime, second.startTime);
        float overlapEnd = Mathf.Min(first.startTime + first.animationClip.length, second.startTime + second.animationClip.length);
        float blend = overlapEnd > overlapStart ? Mathf.InverseLerp(overlapStart, overlapEnd, playTime) : 1f;
        SetAnimationInputs(first.animationClip, second.animationClip, localA, localB, blend);
    }

    private void FindActiveAnimationClips(out WeaponAnimationEvent first, out WeaponAnimationEvent second)
    {
        first = null;
        second = null;

        for (int i = 0; i < weaponEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = weaponEffect.animationEvents[i];
            if (clip == null || clip.animationClip == null)
            {
                continue;
            }

            float start = clip.startTime;
            float end = start + clip.animationClip.length;
            if (playTime < start || playTime > end)
            {
                continue;
            }

            if (first == null || clip.startTime < first.startTime)
            {
                second = first;
                first = clip;
            }
            else if (second == null || clip.startTime < second.startTime)
            {
                second = clip;
            }
        }
    }

    private void EnsureAnimationGraph()
    {
        if (animationGraphValid || targetAnimator == null)
        {
            return;
        }

        animationGraph = PlayableGraph.Create("WeaponAttackEffectRuntimeTester");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        animationMixer = AnimationMixerPlayable.Create(animationGraph, 2);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "Animation", targetAnimator);
        output.SetSourcePlayable(animationMixer);
        animationGraph.Play();
        animationGraphValid = true;
    }

    private void SetAnimationInputs(AnimationClip clipA, AnimationClip clipB, float localA, float localB, float blendToB)
    {
        EnsureAnimationGraph();
        if (!animationGraphValid)
        {
            return;
        }

        SetPlayableInput(0, clipA, ref activeClipA, ref playableA);
        SetPlayableInput(1, clipB, ref activeClipB, ref playableB);

        if (playableA.IsValid())
        {
            playableA.SetTime(localA);
        }

        if (playableB.IsValid())
        {
            playableB.SetTime(localB);
        }

        animationMixer.SetInputWeight(0, clipA != null ? 1f - Mathf.Clamp01(blendToB) : 0f);
        animationMixer.SetInputWeight(1, clipB != null ? Mathf.Clamp01(blendToB) : 0f);
        animationGraph.Evaluate(0f);
    }

    private void SetPlayableInput(int inputIndex, AnimationClip clip, ref AnimationClip activeClip, ref AnimationClipPlayable playable)
    {
        if (activeClip == clip)
        {
            return;
        }

        if (playable.IsValid())
        {
            animationMixer.DisconnectInput(inputIndex);
            playable.Destroy();
        }

        activeClip = clip;
        if (clip == null)
        {
            playable = default;
            return;
        }

        playable = AnimationClipPlayable.Create(animationGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetSpeed(0d);
        animationGraph.Connect(playable, 0, animationMixer, inputIndex);
    }

    private void DestroyAnimationGraph()
    {
        activeClipA = null;
        activeClipB = null;
        playableA = default;
        playableB = default;

        if (animationGraphValid && animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }

        animationGraphValid = false;
    }

    private void UpdateAudioEvents()
    {
        for (int i = 0; i < weaponEffect.audioEvents.Count; i++)
        {
            WeaponAudioEvent audioEvent = weaponEffect.audioEvents[i];
            if (audioEvent == null || audioTriggered[i] || playTime < audioEvent.triggerTime)
            {
                continue;
            }

            PlayAudioEvent(audioEvent);
            audioTriggered[i] = true;
        }
    }

    private void PlayAudioEvent(WeaponAudioEvent audioEvent)
    {
        if (audioEvent == null)
        {
            return;
        }

        if (GameMgr.Audio != null && !string.IsNullOrWhiteSpace(audioEvent.soundName))
        {
            string requestedGroup = string.IsNullOrWhiteSpace(audioEvent.soundGroup) ? "Game" : audioEvent.soundGroup;
            if (GameMgr.Audio.TryResolveSoundGroup(requestedGroup, audioEvent.soundName, out string resolvedGroup))
            {
                GameMgr.Audio.PlayAt(
                    resolvedGroup,
                    audioEvent.soundName,
                    transform.position,
                    Mathf.Clamp01(audioEvent.volume));
                return;
            }
        }

        if (GameMgr.Audio != null && audioEvent.audioClip != null)
        {
            GameMgr.Audio.PlayClipAt(
                audioEvent.audioClip,
                transform.position,
                Mathf.Clamp01(audioEvent.volume),
                Mathf.Max(0.1f, audioEvent.pitch));
            return;
        }

        if (audioSource != null && audioEvent.audioClip != null)
        {
            audioSource.pitch = Mathf.Max(0.01f, audioEvent.pitch);
            audioSource.PlayOneShot(audioEvent.audioClip, Mathf.Clamp01(audioEvent.volume));
            return;
        }

        if (GameMgr.Audio == null || string.IsNullOrWhiteSpace(audioEvent.soundName))
        {
            return;
        }

        GameMgr.Audio.PlayAt(
            string.IsNullOrWhiteSpace(audioEvent.soundGroup) ? "Game" : audioEvent.soundGroup,
            audioEvent.soundName,
            transform.position,
            Mathf.Clamp01(audioEvent.volume));
    }

    private void UpdateVfxEvents()
    {
        for (int i = 0; i < weaponEffect.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = weaponEffect.vfxEvents[i];
            if (vfx == null || vfxTriggered[i] || playTime < vfx.triggerTime)
            {
                continue;
            }

            if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger)
            {
                SpawnProjectile(vfx);
            }
            else
            {
                SpawnSelfMotionVfx(vfx);
            }

            vfxTriggered[i] = true;
        }
    }

    private void SpawnSelfMotionVfx(WeaponVFXEvent vfx)
    {
        if (vfx.vfxPrefab == null)
        {
            return;
        }

        GetSpawnPose(vfx.spawnOffset, vfx.spawnRotation, vfx.attachToWeapon, out Vector3 position, out Quaternion rotation);
        GameObject instance = Instantiate(vfx.vfxPrefab, position, rotation, vfx.attachToWeapon ? effectRoot : null);
        spawnedObjects.Add(instance);
        Destroy(instance, Mathf.Max(0.01f, vfx.duration));
    }

    private void SpawnProjectile(WeaponVFXEvent vfx)
    {
        GetSpawnPose(vfx.spawnOffset, vfx.spawnRotation, vfx.attachToWeapon, out Vector3 position, out Quaternion rotation);

        WeaponProjectileConfigEntry config = BuildProjectileConfig(vfx);
        WeaponProjectileRuntimeController projectile = WeaponProjectilePool.Get(GetVfxName(vfx));
        projectile.Initialize(config, position, rotation, targetLayers, baseDamage, gameObject);
        projectile.SetDebugGizmos(drawRuntimeGizmos, new Color(1f, 0.22f, 0.15f, 0.82f));

        if (vfx.vfxPrefab == null)
        {
            return;
        }

        if (vfx.projectileMotionMode == WeaponProjectileMotionMode.CodeDriven)
        {
            GameObject visual = WeaponProjectilePool.GetVisual(vfx.vfxPrefab);
            visual.transform.SetParent(projectile.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            projectile.RegisterExternalVisual(visual);
        }
        else
        {
            GameObject visual = WeaponProjectilePool.GetVisual(vfx.vfxPrefab);
            visual.transform.SetPositionAndRotation(position, rotation);
            projectile.RegisterExternalVisual(visual);
        }
    }

    private WeaponProjectileConfigEntry BuildProjectileConfig(WeaponVFXEvent vfx)
    {
        return new WeaponProjectileConfigEntry
        {
            weaponModelName = weaponEffect != null ? weaponEffect.weaponName : "RuntimeTest",
            projectileVFXKey = GetVfxName(vfx),
            hitVFXKey = vfx.hitVFXKey,
            hitVFXRandomEulerRange = vfx.hitVFXRandomEulerRange,
            damageMultiplier = Mathf.Max(0f, vfx.damageMultiplier),
            motionMode = vfx.projectileMotionMode,
            speed = GetProjectileTravelSign(vfx) * Mathf.Max(0f, vfx.projectileSpeed),
            lifeTime = Mathf.Max(0.01f, vfx.projectileLifeTime),
            visualForwardDistance = GetProjectileTravelSign(vfx) * Mathf.Max(0f, vfx.visualForwardDistance),
            visualForwardCurve = vfx.visualForwardCurve,
            hitShape = vfx.projectileHitShape,
            hitRadius = Mathf.Max(0.01f, vfx.projectileHitRadius),
            hitHeight = Mathf.Max(0.01f, vfx.projectileHitHeight),
            hitCenterOffset = vfx.projectileHitCenterOffset,
            pierceCount = vfx.pierceMonsters ? Mathf.Max(1, vfx.pierceCount) : 0,
            spawnOffset = vfx.spawnOffset
        };
    }

    private static string GetVfxName(WeaponVFXEvent vfx)
    {
        if (!string.IsNullOrWhiteSpace(vfx.projectileVFXKey))
        {
            return vfx.projectileVFXKey;
        }

        if (!string.IsNullOrWhiteSpace(vfx.vfxKey))
        {
            return vfx.vfxKey;
        }

        return vfx.vfxPrefab != null ? vfx.vfxPrefab.name : "VFX";
    }

    private static float GetProjectileTravelSign(WeaponVFXEvent vfx)
    {
        return vfx.reverseProjectileTravel ? -1f : 1f;
    }

    private void UpdateHitboxEvents()
    {
        for (int i = 0; i < weaponEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = weaponEffect.hitboxEvents[i];
            if (hitbox == null || hitboxApplied[i] || playTime < hitbox.applyDamageTime)
            {
                continue;
            }

            if (playTime <= hitbox.endWindowTime)
            {
                ApplyHitboxDamage(hitbox);
            }

            hitboxApplied[i] = true;
        }
    }

    private void ApplyHitboxDamage(WeaponHitboxEvent hitbox)
    {
        Vector3 center = transform.TransformPoint(hitbox.offset);
        float radius = Mathf.Max(0.01f, hitbox.radius);
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, hitResults, targetLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable))
            {
                continue;
            }

            hitTargets.Add(damageable);
            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - transform.position).normalized;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = transform.forward;
            }

            damageable.TakeDamage(new AttackDamageInfo(baseDamage, hitPoint, hitDirection, gameObject));
            if (!IsHarvestableDamageable(damageable))
            {
                PlayHitboxHitVfx(hitbox, hitPoint, hitDirection);
            }
        }
    }

    private void PlayHitboxHitVfx(WeaponHitboxEvent hitbox, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (hitbox == null || string.IsNullOrWhiteSpace(hitbox.hitVFXKey) || GameMgr.VFX == null)
        {
            return;
        }

        Quaternion hitRotation = hitDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(hitDirection.normalized, Vector3.up)
            : transform.rotation;
        hitRotation *= Quaternion.Euler(GetRandomHitVfxEuler(hitbox.hitVFXRandomEulerRange));
        GameMgr.VFX.Play(hitbox.hitVFXKey, hitPoint, hitRotation);
    }

    private static bool IsHarvestableDamageable(IDamageable damageable)
    {
        return damageable is HarvestableResource;
    }

    private static Vector3 GetRandomHitVfxEuler(Vector3 range)
    {
        return new Vector3(
            UnityEngine.Random.Range(-Mathf.Abs(range.x), Mathf.Abs(range.x)),
            UnityEngine.Random.Range(-Mathf.Abs(range.y), Mathf.Abs(range.y)),
            UnityEngine.Random.Range(-Mathf.Abs(range.z), Mathf.Abs(range.z)));
    }

    private void GetSpawnPose(Vector3 offset, Vector3 rotationEuler, bool attachToOwner, out Vector3 position, out Quaternion rotation)
    {
        Transform root = effectRoot != null ? effectRoot : transform;
        if (attachToOwner)
        {
            position = root.TransformPoint(offset);
            rotation = root.rotation * Quaternion.Euler(rotationEuler);
        }
        else
        {
            position = offset;
            rotation = Quaternion.Euler(rotationEuler);
        }
    }

    private void CleanupSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRuntimeGizmos || weaponEffect == null || !Application.isPlaying || !playing)
        {
            return;
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < weaponEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = weaponEffect.hitboxEvents[i];
            if (hitbox == null)
            {
                continue;
            }

            if (playTime < hitbox.beginWindowTime || playTime > hitbox.endWindowTime)
            {
                continue;
            }

            Gizmos.DrawWireSphere(transform.TransformPoint(hitbox.offset), Mathf.Max(0.01f, hitbox.radius));
        }
    }
}
