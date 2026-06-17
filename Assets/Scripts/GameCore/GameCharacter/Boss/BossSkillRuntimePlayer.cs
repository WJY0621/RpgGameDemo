using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossSkillRuntimePlayer : MonoBehaviour
{
    private const int MaxHitResults = 32;
    private const string DefaultTargetLayerName = "Player";

    [Header("Runtime")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool driveTimelineByUpdate;

    [Header("Debug")]
    [SerializeField] private bool drawRuntimeGizmos = true;
    [SerializeField] private Color hitboxGizmoColor = new Color(1f, 0.16f, 0.08f, 0.85f);
    [SerializeField] private Color projectileGizmoColor = new Color(1f, 0.62f, 0.12f, 0.85f);
    [SerializeField] private Color movementGizmoColor = new Color(0.30f, 0.86f, 1f, 0.85f);

    private readonly Collider[] hitResults = new Collider[MaxHitResults];
    private readonly RaycastHit[] contactCastResults = new RaycastHit[MaxHitResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly HashSet<NetworkPlayer> hitNetworkPlayers = new HashSet<NetworkPlayer>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly List<Collider> authoredSolidColliders = new List<Collider>();
    private readonly List<bool> authoredSolidColliderInitialStates = new List<bool>();

    private BossController controller;
    private Animator animator;
    private BossBodyContactCapsuleSet bodyContactCapsules;
    private BossSkillSO skill;
    private bool[] animationTriggered;
    private bool[] audioTriggered;
    private bool[] vfxBegan;
    private bool[] vfxEnded;
    private VFXHandle[] activeVFXHandles;
    private GameObject[] activeVFXObjects;
    private bool[] barrageTriggered;
    private bool[] movementTriggered;
    private bool[] movementWarningBegan;
    private bool[] movementWarningEnded;
    private VFXHandle[] movementWarningHandles;
    private GameObject[] movementWarningObjects;
    private Vector3[] movementLockedDestinations;
    private bool[] hasMovementLockedDestination;
    private bool[] hitboxWindowBegan;
    private bool[] hitboxApplied;
    private bool[] hitboxWindowEnded;
    private Dictionary<IDamageable, float>[] contactDamageHitTimes;
    private Vector3[] previousContactStarts;
    private Vector3[] previousContactEnds;
    private bool[] hasPreviousContactPose;
    private bool playing;
    private bool finished;
    private float playTime;
    private float animatorSpeedBeforeSkill = 1f;
    private bool hasStoredAnimatorSpeed;
    private Vector3 skillStartTargetPosition;
    private bool hasSkillStartTargetPosition;
    private bool bossCollisionStateCached;
    private bool contactCollisionModeActive;
    private bool movementCollisionSuppressed;

    public bool IsPlaying => playing;
    public bool IsFinished => finished;
    public float PlayTime => playTime;
    public BossSkillSO CurrentSkill => skill;

    private void Awake()
    {
        controller = GetComponent<BossController>();
        animator = GetComponentInChildren<Animator>(true);
        bodyContactCapsules = GetComponentInChildren<BossBodyContactCapsuleSet>(true);
        EnsureDefaultTargetLayers();
    }

    private void Update()
    {
        if (driveTimelineByUpdate)
        {
            Tick(Time.deltaTime);
        }
    }

    public void Bind(BossController bossController)
    {
        controller = bossController;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (bodyContactCapsules == null)
        {
            bodyContactCapsules = GetComponentInChildren<BossBodyContactCapsuleSet>(true);
        }
    }

    public void Play(BossSkillSO nextSkill)
    {
        Stop();

        skill = nextSkill;
        if (skill == null)
        {
            finished = true;
            return;
        }

        playing = true;
        finished = false;
        playTime = 0f;
        StoreAnimatorSpeed();
        animationTriggered = new bool[skill.animationEvents.Count];
        audioTriggered = new bool[skill.audioEvents.Count];
        vfxBegan = new bool[skill.vfxEvents.Count];
        vfxEnded = new bool[skill.vfxEvents.Count];
        activeVFXHandles = new VFXHandle[skill.vfxEvents.Count];
        activeVFXObjects = new GameObject[skill.vfxEvents.Count];
        barrageTriggered = new bool[skill.barrageEvents.Count];
        movementTriggered = new bool[skill.movementEvents.Count];
        movementWarningBegan = new bool[skill.movementEvents.Count];
        movementWarningEnded = new bool[skill.movementEvents.Count];
        movementWarningHandles = new VFXHandle[skill.movementEvents.Count];
        movementWarningObjects = new GameObject[skill.movementEvents.Count];
        movementLockedDestinations = new Vector3[skill.movementEvents.Count];
        hasMovementLockedDestination = new bool[skill.movementEvents.Count];
        hitboxWindowBegan = new bool[skill.hitboxEvents.Count];
        hitboxApplied = new bool[skill.hitboxEvents.Count];
        hitboxWindowEnded = new bool[skill.hitboxEvents.Count];
        contactDamageHitTimes = new Dictionary<IDamageable, float>[skill.contactDamageEvents.Count];
        for (int i = 0; i < contactDamageHitTimes.Length; i++)
        {
            contactDamageHitTimes[i] = new Dictionary<IDamageable, float>();
        }

        if (bodyContactCapsules == null)
        {
            bodyContactCapsules = GetComponentInChildren<BossBodyContactCapsuleSet>(true);
        }

        CacheBossCollisionState();
        RestoreBossCollisionMode();
        EnsureContactPoseCache();
        hitTargets.Clear();
        hitNetworkPlayers.Clear();
        CaptureSkillStartTargetPosition();

        PlayInitialAnimation();
    }

    public void Tick(float deltaTime)
    {
        if (!playing || skill == null)
        {
            return;
        }

        float previousTime = playTime;
        playTime += Mathf.Max(0f, deltaTime);

        UpdateAnimationEvents(previousTime, playTime);
        UpdateAudioEvents(previousTime, playTime);
        UpdateMovementEvents(previousTime, playTime);
        UpdateVFXEvents(previousTime, playTime);
        UpdateBarrageEvents(previousTime, playTime);
        UpdateHitboxWindows(previousTime, playTime);
        UpdateHitboxDamage(previousTime, playTime);
        UpdateContactDamage(playTime);

        if (playTime >= GetPlaybackDuration())
        {
            Finish();
        }
    }

    public void Stop()
    {
        playing = false;
        finished = false;
        playTime = 0f;
        skill = null;
        contactDamageHitTimes = null;
        movementTriggered = null;
        movementWarningBegan = null;
        movementWarningEnded = null;
        movementLockedDestinations = null;
        hasMovementLockedDestination = null;
        hasSkillStartTargetPosition = false;
        movementCollisionSuppressed = false;
        contactCollisionModeActive = false;
        RestoreBossCollisionMode();
        StopMovementWarnings();
        StopActiveVFX();
        ClearContactPoseCache();
        hitTargets.Clear();
        hitNetworkPlayers.Clear();
        CleanupSpawnedObjects();
        RestoreAnimatorSpeed();
    }

    public void TriggerAudioEventByAnimation(int eventIndex)
    {
        if (!playing || skill == null || eventIndex < 0 || eventIndex >= skill.audioEvents.Count)
        {
            return;
        }

        PlayAudio(skill.audioEvents[eventIndex]);
        if (audioTriggered != null && eventIndex < audioTriggered.Length)
        {
            audioTriggered[eventIndex] = true;
        }
    }

    public void TriggerBarrageEventByAnimation(int eventIndex)
    {
        if (!playing || skill == null || eventIndex < 0 || eventIndex >= skill.barrageEvents.Count)
        {
            return;
        }

        SpawnBarrage(skill.barrageEvents[eventIndex]);
        if (barrageTriggered != null && eventIndex < barrageTriggered.Length)
        {
            barrageTriggered[eventIndex] = true;
        }
    }

    public void BeginHitboxEventByAnimation(int eventIndex)
    {
        if (!playing || skill == null || eventIndex < 0 || eventIndex >= skill.hitboxEvents.Count)
        {
            return;
        }

        if (hitboxWindowBegan != null && eventIndex < hitboxWindowBegan.Length)
        {
            hitboxWindowBegan[eventIndex] = true;
        }

        if (hitboxWindowEnded != null && eventIndex < hitboxWindowEnded.Length)
        {
            hitboxWindowEnded[eventIndex] = false;
        }
    }

    public void ApplyHitboxEventByAnimation(int eventIndex)
    {
        if (!playing || skill == null || eventIndex < 0 || eventIndex >= skill.hitboxEvents.Count)
        {
            return;
        }

        ApplyHitboxDamage(skill.hitboxEvents[eventIndex]);
        if (hitboxApplied != null && eventIndex < hitboxApplied.Length)
        {
            hitboxApplied[eventIndex] = true;
        }
    }

    public void EndHitboxEventByAnimation(int eventIndex)
    {
        if (!playing || skill == null || eventIndex < 0 || eventIndex >= skill.hitboxEvents.Count)
        {
            return;
        }

        if (hitboxWindowEnded != null && eventIndex < hitboxWindowEnded.Length)
        {
            hitboxWindowEnded[eventIndex] = true;
        }
    }

    private void PlayInitialAnimation()
    {
        BossAnimationClipEvent firstClip = GetFirstAnimationEvent();
        if (firstClip != null && firstClip.startTime <= 0.0001f)
        {
            PlayAnimationEvent(firstClip);
            if (animationTriggered.Length > 0)
            {
                int firstIndex = skill.animationEvents.IndexOf(firstClip);
                if (firstIndex >= 0)
                {
                    animationTriggered[firstIndex] = true;
                }
            }
            return;
        }

        if (firstClip == null && controller != null && !string.IsNullOrWhiteSpace(skill.animationStateName))
        {
            SetAnimatorSpeed(1f);
            controller.PlayBaseAnimation(skill.animationStateName);
        }
    }

    private BossAnimationClipEvent GetFirstAnimationEvent()
    {
        BossAnimationClipEvent result = null;
        float bestTime = float.MaxValue;
        for (int i = 0; i < skill.animationEvents.Count; i++)
        {
            BossAnimationClipEvent animationEvent = skill.animationEvents[i];
            if (animationEvent == null)
            {
                continue;
            }

            if (animationEvent.startTime < bestTime)
            {
                bestTime = animationEvent.startTime;
                result = animationEvent;
            }
        }

        return result;
    }

    private void UpdateAnimationEvents(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.animationEvents.Count; i++)
        {
            BossAnimationClipEvent animationEvent = skill.animationEvents[i];
            if (animationEvent == null || animationTriggered[i])
            {
                continue;
            }

            if (!DidCross(previousTime, currentTime, animationEvent.startTime))
            {
                continue;
            }

            PlayAnimationEvent(animationEvent);
            animationTriggered[i] = true;
        }
    }

    private void PlayAnimationEvent(BossAnimationClipEvent animationEvent)
    {
        if (animationEvent == null)
        {
            return;
        }

        string stateName = !string.IsNullOrWhiteSpace(animationEvent.animatorStateName)
            ? animationEvent.animatorStateName
            : animationEvent.animationClip != null
                ? animationEvent.animationClip.name
                : string.Empty;

        if (controller != null && !string.IsNullOrWhiteSpace(stateName))
        {
            SetAnimatorSpeed(animationEvent.playbackSpeed);
            controller.PlayBaseAnimation(stateName);
        }
        else if (animator != null && animationEvent.animationClip != null)
        {
            SetAnimatorSpeed(animationEvent.playbackSpeed);
            animator.Play(animationEvent.animationClip.name, 0, 0f);
        }
    }

    private void StoreAnimatorSpeed()
    {
        if (animator == null)
        {
            return;
        }

        animatorSpeedBeforeSkill = animator.speed;
        hasStoredAnimatorSpeed = true;
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.speed = Mathf.Max(0.01f, speed);
        }
    }

    private void RestoreAnimatorSpeed()
    {
        if (animator != null && hasStoredAnimatorSpeed)
        {
            animator.speed = animatorSpeedBeforeSkill;
        }

        hasStoredAnimatorSpeed = false;
    }

    private void UpdateAudioEvents(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.audioEvents.Count; i++)
        {
            BossAudioClipEvent audioEvent = skill.audioEvents[i];
            if (audioEvent == null || audioTriggered[i] || !DidCross(previousTime, currentTime, audioEvent.triggerTime))
            {
                continue;
            }

            PlayAudio(audioEvent);
            audioTriggered[i] = true;
        }
    }

    private void UpdateMovementEvents(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.movementEvents.Count; i++)
        {
            BossMovementEvent movementEvent = skill.movementEvents[i];
            if (movementEvent == null)
            {
                continue;
            }

            if (!movementTriggered[i] && DidCross(previousTime, currentTime, movementEvent.triggerTime))
            {
                ApplyMovementEvent(i, movementEvent);
                movementTriggered[i] = true;

                bool shouldShowWarning = ShouldShowMovementWarning(movementEvent);
                if (shouldShowWarning && !movementWarningBegan[i])
                {
                    BeginMovementWarning(i, movementEvent);
                    movementWarningBegan[i] = true;
                }
            }

            if (ShouldShowMovementWarning(movementEvent) &&
                movementWarningBegan[i] &&
                !movementWarningEnded[i] &&
                DidCross(previousTime, currentTime, GetMovementWarningEndTime(movementEvent)))
            {
                EndMovementWarning(i);
                movementWarningEnded[i] = true;
            }
        }
    }

    private void CaptureSkillStartTargetPosition()
    {
        hasSkillStartTargetPosition = controller != null && controller.Target != null;
        skillStartTargetPosition = hasSkillStartTargetPosition ? ResolveTargetFootPosition(controller.Target) : transform.position;
    }

    private void ApplyMovementEvent(int index, BossMovementEvent movementEvent)
    {
        if (movementEvent == null)
        {
            return;
        }

        if (movementEvent.disableCollisionUntilContactDamage)
        {
            SuppressBossCollisionForMovement();
        }

        Vector3 destination = GetOrLockMovementDestination(index, movementEvent);
        Quaternion rotation = transform.rotation;
        if (movementEvent.faceTargetAfterMove && TryGetMovementLookTarget(movementEvent, out Vector3 lookTarget))
        {
            Vector3 lookDirection = lookTarget - destination;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }

        if (controller != null)
        {
            controller.TeleportTo(destination, rotation);
        }
        else
        {
            transform.SetPositionAndRotation(destination, rotation);
        }

        ClearContactPoseCache();
    }

    private void BeginMovementWarning(int index, BossMovementEvent movementEvent)
    {
        Vector3 destination = GetOrLockMovementDestination(index, movementEvent);
        Vector3 position = destination + movementEvent.warningOffset;
        Quaternion rotation = Quaternion.Euler(movementEvent.warningRotation);

        if (!string.IsNullOrWhiteSpace(movementEvent.warningVFXKey) && GameMgr.VFX != null)
        {
            movementWarningHandles[index] = GameMgr.VFX.PlayLooping(movementEvent.warningVFXKey, position, rotation);
        }

        if (movementEvent.warningPrefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(movementEvent.warningPrefab, position, rotation);
        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, movementEvent.warningScale);
        movementWarningObjects[index] = instance;
        spawnedObjects.Add(instance);
    }

    private void EndMovementWarning(int index)
    {
        if (movementWarningHandles != null && index >= 0 && index < movementWarningHandles.Length)
        {
            movementWarningHandles[index]?.Stop();
            movementWarningHandles[index] = null;
        }

        if (movementWarningObjects != null && index >= 0 && index < movementWarningObjects.Length && movementWarningObjects[index] != null)
        {
            GameObject instance = movementWarningObjects[index];
            movementWarningObjects[index] = null;
            spawnedObjects.Remove(instance);
            Destroy(instance);
        }
    }

    private void StopMovementWarnings()
    {
        if (movementWarningHandles != null)
        {
            for (int i = 0; i < movementWarningHandles.Length; i++)
            {
                movementWarningHandles[i]?.Stop();
                movementWarningHandles[i] = null;
            }
        }

        if (movementWarningObjects != null)
        {
            for (int i = 0; i < movementWarningObjects.Length; i++)
            {
                if (movementWarningObjects[i] != null)
                {
                    spawnedObjects.Remove(movementWarningObjects[i]);
                    Destroy(movementWarningObjects[i]);
                    movementWarningObjects[i] = null;
                }
            }
        }
    }

    private Vector3 GetOrLockMovementDestination(int index, BossMovementEvent movementEvent)
    {
        if (hasMovementLockedDestination != null && index >= 0 && index < hasMovementLockedDestination.Length && hasMovementLockedDestination[index])
        {
            return movementLockedDestinations[index];
        }

        Vector3 destination = ResolveMovementDestination(movementEvent);
        if (movementLockedDestinations != null && hasMovementLockedDestination != null && index >= 0 && index < movementLockedDestinations.Length)
        {
            movementLockedDestinations[index] = destination;
            hasMovementLockedDestination[index] = true;
        }

        return destination;
    }

    private Vector3 ResolveMovementDestination(BossMovementEvent movementEvent)
    {
        Vector3 destination = movementEvent.targetSource switch
        {
            BossMovementTargetSource.CurrentTargetPosition when controller != null && controller.Target != null => ResolveTargetFootPosition(controller.Target),
            BossMovementTargetSource.SkillStartTargetPosition when hasSkillStartTargetPosition => skillStartTargetPosition,
            BossMovementTargetSource.BossCurrentPosition => transform.position,
            _ => transform.position
        };

        destination += new Vector3(movementEvent.targetOffset.x, 0f, movementEvent.targetOffset.z);
        if (movementEvent.keepCurrentY)
        {
            destination.y = transform.position.y + movementEvent.targetOffset.y;
        }
        else if (movementEvent.projectToGround)
        {
            destination = ProjectMovementDestinationToGround(destination, movementEvent);
        }
        else
        {
            destination.y += movementEvent.targetOffset.y;
        }

        return destination;
    }

    private Vector3 ProjectMovementDestinationToGround(Vector3 destination, BossMovementEvent movementEvent)
    {
        Vector3 origin = destination + Vector3.up * movementEvent.groundRayStartHeight;
        float distance = movementEvent.groundRayStartHeight + movementEvent.groundRayDistance;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, movementEvent.groundLayers, QueryTriggerInteraction.Ignore);
        RaycastHit bestHit = default;
        float bestDistance = float.MaxValue;
        Transform target = controller != null ? controller.Target : null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || IsSelfCollider(hitCollider) || IsTargetCollider(hitCollider, target))
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestHit = hits[i];
            }
        }

        if (bestDistance < float.MaxValue)
        {
            destination.y = bestHit.point.y + movementEvent.targetOffset.y;
        }

        return destination;
    }

    private static bool ShouldShowMovementWarning(BossMovementEvent movementEvent)
    {
        return movementEvent != null &&
               (movementEvent.showTargetWarning ||
                movementEvent.warningPrefab != null ||
                !string.IsNullOrWhiteSpace(movementEvent.warningVFXKey));
    }

    private static Vector3 ResolveTargetFootPosition(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        CharacterController characterController = target.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            Vector3 center = characterController.transform.TransformPoint(characterController.center);
            float bottomY = center.y - characterController.height * 0.5f;
            return new Vector3(characterController.transform.position.x, bottomY, characterController.transform.position.z);
        }

        Collider collider = target.GetComponentInParent<Collider>();
        if (collider == null)
        {
            collider = target.GetComponentInChildren<Collider>();
        }

        if (collider != null)
        {
            return new Vector3(collider.transform.position.x, collider.bounds.min.y, collider.transform.position.z);
        }

        return target.position;
    }

    private static bool IsTargetCollider(Collider hit, Transform target)
    {
        if (hit == null || target == null)
        {
            return false;
        }

        return hit.transform == target ||
               hit.transform.IsChildOf(target) ||
               target.IsChildOf(hit.transform);
    }

    private static float GetMovementWarningEndTime(BossMovementEvent movementEvent)
    {
        return movementEvent.triggerTime + Mathf.Max(0f, movementEvent.warningDuration);
    }

    private bool TryGetMovementLookTarget(BossMovementEvent movementEvent, out Vector3 lookTarget)
    {
        if (movementEvent.targetSource == BossMovementTargetSource.SkillStartTargetPosition && hasSkillStartTargetPosition)
        {
            lookTarget = skillStartTargetPosition;
            return true;
        }

        if (controller != null && controller.Target != null)
        {
            lookTarget = controller.Target.position;
            return true;
        }

        lookTarget = default;
        return false;
    }

    private void PlayAudio(BossAudioClipEvent audioEvent)
    {
        if (audioEvent == null)
        {
            return;
        }

        Vector3 position = transform.position;
        if (GameMgr.Audio != null)
        {
            if (!string.IsNullOrWhiteSpace(audioEvent.soundName))
            {
                string group = string.IsNullOrWhiteSpace(audioEvent.soundGroup) ? "Game" : audioEvent.soundGroup;
                if (GameMgr.Audio.TryResolveSoundGroup(group, audioEvent.soundName, out string resolvedGroup))
                {
                    GameMgr.Audio.PlayAt(resolvedGroup, audioEvent.soundName, position, Mathf.Clamp01(audioEvent.volume));
                    return;
                }
            }

            if (audioEvent.audioClip != null)
            {
                GameMgr.Audio.PlayClipAt(audioEvent.audioClip, position, Mathf.Clamp01(audioEvent.volume), Mathf.Max(0.1f, audioEvent.pitch));
                return;
            }

            if (!string.IsNullOrWhiteSpace(audioEvent.soundName))
            {
                string group = string.IsNullOrWhiteSpace(audioEvent.soundGroup) ? "Game" : audioEvent.soundGroup;
                GameMgr.Audio.PlayAt(group, audioEvent.soundName, position, Mathf.Clamp01(audioEvent.volume));
            }
        }
        else if (audioEvent.audioClip != null)
        {
            AudioSource.PlayClipAtPoint(audioEvent.audioClip, position, Mathf.Clamp01(audioEvent.volume));
        }
    }

    private void UpdateVFXEvents(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.vfxEvents.Count; i++)
        {
            BossVFXEvent vfxEvent = skill.vfxEvents[i];
            if (vfxEvent == null)
            {
                continue;
            }

            if (!vfxBegan[i] && DidCross(previousTime, currentTime, vfxEvent.startTime))
            {
                BeginVFX(vfxEvent, i);
                vfxBegan[i] = true;
            }

            if (!vfxEnded[i] && DidCross(previousTime, currentTime, vfxEvent.endTime))
            {
                EndVFX(i);
                vfxEnded[i] = true;
            }
        }
    }

    private void BeginVFX(BossVFXEvent vfxEvent, int index)
    {
        if (vfxEvent == null)
        {
            return;
        }

        Vector3 position = transform.TransformPoint(vfxEvent.offset);
        Quaternion rotation = transform.rotation * Quaternion.Euler(vfxEvent.rotation);
        if (!string.IsNullOrWhiteSpace(vfxEvent.vfxKey) && GameMgr.VFX != null)
        {
            VFXHandle handle;
            if (vfxEvent.followBoss)
            {
                handle = GameMgr.VFX.PlayLooping(vfxEvent.vfxKey, transform, vfxEvent.offset, Quaternion.Euler(vfxEvent.rotation));
            }
            else
            {
                handle = GameMgr.VFX.PlayLooping(vfxEvent.vfxKey, position, rotation);
            }

            if (activeVFXHandles != null && index >= 0 && index < activeVFXHandles.Length)
            {
                activeVFXHandles[index] = handle;
            }

            return;
        }

        if (vfxEvent.previewPrefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(vfxEvent.previewPrefab, position, rotation);
        if (vfxEvent.followBoss)
        {
            instance.transform.SetParent(transform, true);
        }

        if (activeVFXObjects != null && index >= 0 && index < activeVFXObjects.Length)
        {
            activeVFXObjects[index] = instance;
        }

        spawnedObjects.Add(instance);
    }

    private void EndVFX(int index)
    {
        if (activeVFXHandles != null && index >= 0 && index < activeVFXHandles.Length)
        {
            activeVFXHandles[index]?.Stop();
            activeVFXHandles[index] = null;
        }

        if (activeVFXObjects != null && index >= 0 && index < activeVFXObjects.Length && activeVFXObjects[index] != null)
        {
            GameObject instance = activeVFXObjects[index];
            spawnedObjects.Remove(instance);
            Destroy(instance);
            activeVFXObjects[index] = null;
        }
    }

    private void StopActiveVFX()
    {
        if (activeVFXHandles != null)
        {
            for (int i = 0; i < activeVFXHandles.Length; i++)
            {
                activeVFXHandles[i]?.Stop();
                activeVFXHandles[i] = null;
            }
        }

        if (activeVFXObjects != null)
        {
            for (int i = 0; i < activeVFXObjects.Length; i++)
            {
                if (activeVFXObjects[i] != null)
                {
                    spawnedObjects.Remove(activeVFXObjects[i]);
                    Destroy(activeVFXObjects[i]);
                    activeVFXObjects[i] = null;
                }
            }
        }
    }

    private void UpdateBarrageEvents(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.barrageEvents.Count; i++)
        {
            BossBarrageEvent barrageEvent = skill.barrageEvents[i];
            if (barrageEvent == null || barrageTriggered[i] || !DidCross(previousTime, currentTime, barrageEvent.triggerTime))
            {
                continue;
            }

            SpawnBarrage(barrageEvent);
            barrageTriggered[i] = true;
        }
    }

    private void SpawnBarrage(BossBarrageEvent barrageEvent)
    {
        if (barrageEvent == null)
        {
            return;
        }

        GetBarrageBasePose(barrageEvent, out Vector3 position, out Quaternion rotation);
        int projectileCount = Mathf.Max(1, barrageEvent.projectileCount);
        float spread = projectileCount <= 1 ? 0f : barrageEvent.spreadAngle;
        float step = projectileCount <= 1 ? 0f : spread / (projectileCount - 1);
        float startAngle = -spread * 0.5f;

        for (int i = 0; i < projectileCount; i++)
        {
            Quaternion shotRotation = rotation * Quaternion.Euler(0f, startAngle + step * i, 0f);
            shotRotation = ApplyRandomAimCone(shotRotation, barrageEvent.aimRandomAngle);
            SpawnProjectile(barrageEvent, position, shotRotation);
        }
    }

    private static Quaternion ApplyRandomAimCone(Quaternion rotation, float maxAngle)
    {
        if (maxAngle <= 0f)
        {
            return rotation;
        }

        Vector2 offset = Random.insideUnitCircle * maxAngle;
        return rotation * Quaternion.Euler(-offset.y, offset.x, 0f);
    }

    private static Quaternion CreateLookRotation(Vector3 forward, Quaternion fallback)
    {
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return fallback;
        }

        Vector3 normalizedForward = forward.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(normalizedForward, up);
    }

    private void SpawnProjectile(BossBarrageEvent barrageEvent, Vector3 position, Quaternion rotation)
    {
        BossProjectileConfig config = BuildProjectileConfig(barrageEvent);
        string projectileName = GetProjectileName(barrageEvent);
        NetworkBoss.SendBossProjectileVisual(config, projectileName, position, rotation);

        BossProjectileRuntimeController projectile = BossProjectilePool.Get(projectileName);
        projectile.Initialize(config, position, rotation, GetResolvedTargetLayers(), GetEventDamage(barrageEvent.damageMultiplier), gameObject);
        projectile.SetDebugGizmos(drawRuntimeGizmos, projectileGizmoColor);

        if (barrageEvent.projectilePrefab == null)
        {
            return;
        }

        GameObject visual = BossProjectilePool.GetVisual(barrageEvent.projectilePrefab);
        if (visual == null)
        {
            return;
        }

        visual.transform.SetParent(projectile.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        SetProjectileVisualStabilized(visual, barrageEvent.stabilizeVisualRotation);

        projectile.RegisterExternalVisual(visual);
    }

    private static void SetProjectileVisualStabilized(GameObject visual, bool stabilized)
    {
        if (visual == null)
        {
            return;
        }

        BossProjectileVisualRotationStabilizer stabilizer = visual.GetComponent<BossProjectileVisualRotationStabilizer>();
        if (!stabilized)
        {
            if (stabilizer != null)
            {
                stabilizer.enabled = false;
            }

            return;
        }

        if (stabilizer == null)
        {
            stabilizer = visual.AddComponent<BossProjectileVisualRotationStabilizer>();
        }

        stabilizer.Capture();
        stabilizer.enabled = true;
    }

    private BossProjectileConfig BuildProjectileConfig(BossBarrageEvent barrageEvent)
    {
        return new BossProjectileConfig
        {
            projectileVFXKey = GetProjectileName(barrageEvent),
            hitVFXKey = barrageEvent.hitVFXKey,
            hitVFXRandomEulerRange = barrageEvent.hitVFXRandomEulerRange,
            motionMode = WeaponProjectileMotionMode.CodeDriven,
            speed = Mathf.Max(0f, barrageEvent.speed),
            lifeTime = Mathf.Max(0.01f, barrageEvent.lifeTime),
            visualForwardDistance = 0f,
            visualForwardCurve = null,
            hitShape = WeaponProjectileHitShape.Sphere,
            hitRadius = Mathf.Max(0.01f, barrageEvent.hitRadius),
            hitHeight = 1f,
            hitCenterOffset = barrageEvent.hitCenterOffset
        };
    }

    private void GetBarrageBasePose(BossBarrageEvent barrageEvent, out Vector3 position, out Quaternion rotation)
    {
        position = transform.TransformPoint(barrageEvent.spawnOffset);

        Vector3 forward = transform.forward;
        if (barrageEvent.aimSource == BossProjectileAimSource.TargetPosition && controller != null && controller.Target != null)
        {
            Vector3 targetPosition = controller.Target.position + Vector3.up * Mathf.Max(0f, barrageEvent.targetAimHeightOffset);
            forward = targetPosition - position;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        rotation = CreateLookRotation(forward, transform.rotation) * Quaternion.Euler(barrageEvent.spawnRotation);
    }

    private void UpdateHitboxWindows(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.hitboxEvents.Count; i++)
        {
            BossHitboxEvent hitbox = skill.hitboxEvents[i];
            if (hitbox == null)
            {
                continue;
            }

            if (!hitboxWindowBegan[i] && DidCross(previousTime, currentTime, hitbox.beginWindowTime))
            {
                hitboxWindowBegan[i] = true;
                hitboxWindowEnded[i] = false;
                hitTargets.Clear();
            }

            if (!hitboxWindowEnded[i] && DidCross(previousTime, currentTime, hitbox.endWindowTime))
            {
                hitboxWindowEnded[i] = true;
            }
        }
    }

    private void UpdateHitboxDamage(float previousTime, float currentTime)
    {
        for (int i = 0; i < skill.hitboxEvents.Count; i++)
        {
            BossHitboxEvent hitbox = skill.hitboxEvents[i];
            if (hitbox == null || hitboxApplied[i] || !DidCross(previousTime, currentTime, hitbox.applyDamageTime))
            {
                continue;
            }

            ApplyHitboxDamage(hitbox);
            hitboxApplied[i] = true;
        }
    }

    private void ApplyHitboxDamage(BossHitboxEvent hitbox)
    {
        if (hitbox == null)
        {
            return;
        }

        Vector3 center = transform.TransformPoint(hitbox.offset);
        float radius = Mathf.Max(0.01f, hitbox.radius);
        int hitCount = hitbox.shape == BossHitboxShape.Cylinder
            ? OverlapCylinder(center, hitbox)
            : Physics.OverlapSphereNonAlloc(center, radius, hitResults, GetResolvedTargetLayers().value, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit == null || IsSelfCollider(hit))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitDirection = (hitPoint - transform.position).normalized;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = transform.forward;
            }

            int damage = GetEventDamage(hitbox.damageMultiplier);
            NetworkPlayer networkPlayer = hit.GetComponentInParent<NetworkPlayer>();
            if (networkPlayer != null && !hitNetworkPlayers.Contains(networkPlayer))
            {
                if (NetworkBoss.TryApplyBossDamageToNetworkPlayer(hit, damage, hitPoint, hitDirection, gameObject))
                {
                    hitNetworkPlayers.Add(networkPlayer);
                    PlayHitVfx(hitbox.hitVFXKey, hitbox.hitVFXRandomEulerRange, hitPoint, hitDirection);
                    continue;
                }
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable) || ShouldSkipDamageable(damageable))
            {
                continue;
            }

            hitTargets.Add(damageable);
            damageable.TakeDamage(new AttackDamageInfo(damage, hitPoint, hitDirection, gameObject));
            PlayHitVfx(hitbox.hitVFXKey, hitbox.hitVFXRandomEulerRange, hitPoint, hitDirection);
        }
    }

    private int OverlapCylinder(Vector3 center, BossHitboxEvent hitbox)
    {
        float radius = Mathf.Max(0.01f, hitbox.radius);
        float height = Mathf.Max(0.01f, hitbox.height);
        Vector3 halfHeight = GetHitboxRotation(hitbox) * Vector3.up * (height * 0.5f);
        return Physics.OverlapCapsuleNonAlloc(
            center - halfHeight,
            center + halfHeight,
            radius,
            hitResults,
            GetResolvedTargetLayers().value,
            QueryTriggerInteraction.Collide);
    }

    private void UpdateContactDamage(float currentTime)
    {
        if (bodyContactCapsules == null || skill == null)
        {
            ClearContactPoseCache();
            return;
        }

        EnsureContactPoseCache();
        bool anyActive = false;
        for (int i = 0; i < skill.contactDamageEvents.Count; i++)
        {
            BossContactDamageEvent contactEvent = skill.contactDamageEvents[i];
            if (contactEvent == null || currentTime < contactEvent.beginWindowTime || currentTime > contactEvent.endWindowTime)
            {
                continue;
            }

            anyActive = true;
            SetBossContactCollisionMode(true);
            ApplyContactDamage(contactEvent, i, currentTime);
        }

        if (anyActive)
        {
            CaptureCurrentContactPoses();
        }
        else
        {
            SetBossContactCollisionMode(false);
            ClearContactPoseCache();
        }
    }

    private void ApplyContactDamage(BossContactDamageEvent contactEvent, int eventIndex, float currentTime)
    {
        Dictionary<IDamageable, float> eventHitTimes = contactDamageHitTimes != null && eventIndex >= 0 && eventIndex < contactDamageHitTimes.Length
            ? contactDamageHitTimes[eventIndex]
            : null;

        for (int capsuleIndex = 0; capsuleIndex < bodyContactCapsules.Count; capsuleIndex++)
        {
            if (!bodyContactCapsules.ShouldUseCapsule(contactEvent, capsuleIndex) ||
                !bodyContactCapsules.TryGetCapsule(capsuleIndex, out Vector3 start, out Vector3 end, out float radius))
            {
                continue;
            }

            int hitCount = Physics.OverlapCapsuleNonAlloc(
                start,
                end,
                radius,
                hitResults,
                GetResolvedTargetLayers().value,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                ProcessContactHit(hitResults[i], contactEvent, eventHitTimes, currentTime, (start + end) * 0.5f, false, Vector3.zero);
            }

            if (hasPreviousContactPose != null && capsuleIndex < hasPreviousContactPose.Length && hasPreviousContactPose[capsuleIndex])
            {
                Vector3 previousStart = previousContactStarts[capsuleIndex];
                Vector3 previousEnd = previousContactEnds[capsuleIndex];
                Vector3 delta = ((start + end) - (previousStart + previousEnd)) * 0.5f;
                float distance = delta.magnitude;
                if (distance > 0.001f)
                {
                    int castCount = Physics.CapsuleCastNonAlloc(
                        previousStart,
                        previousEnd,
                        radius,
                        delta / distance,
                        contactCastResults,
                        distance,
                        GetResolvedTargetLayers().value,
                        QueryTriggerInteraction.Collide);

                    for (int i = 0; i < castCount; i++)
                    {
                        ProcessContactHit(contactCastResults[i].collider, contactEvent, eventHitTimes, currentTime, contactCastResults[i].point, true, contactCastResults[i].point);
                    }
                }
            }
        }
    }

    private void ProcessContactHit(Collider hit, BossContactDamageEvent contactEvent, Dictionary<IDamageable, float> eventHitTimes, float currentTime, Vector3 referencePoint, bool useReferenceAsHitPoint, Vector3 explicitHitPoint)
    {
        if (hit == null || IsSelfCollider(hit))
        {
            return;
        }

        IDamageable damageable = hit.GetComponentInParent<IDamageable>();
        if (damageable == null || ShouldSkipDamageable(damageable) || !CanApplyContactDamage(contactEvent, eventHitTimes, damageable, currentTime))
        {
            return;
        }

        if (eventHitTimes != null)
        {
            eventHitTimes[damageable] = currentTime;
        }

        Vector3 hitPoint = useReferenceAsHitPoint ? explicitHitPoint : hit.ClosestPoint(referencePoint);
        Vector3 hitDirection = (hitPoint - transform.position).normalized;
        if (hitDirection.sqrMagnitude <= 0.0001f)
        {
            hitDirection = transform.forward;
        }

        int damage = GetEventDamage(contactEvent.damageMultiplier);
        if (!NetworkBoss.TryApplyBossDamageToNetworkPlayer(hit, damage, hitPoint, hitDirection, gameObject))
        {
            damageable.TakeDamage(new AttackDamageInfo(damage, hitPoint, hitDirection, gameObject));
        }

        PlayHitVfx(contactEvent.hitVFXKey, contactEvent.hitVFXRandomEulerRange, hitPoint, hitDirection);
    }

    private void EnsureContactPoseCache()
    {
        int count = bodyContactCapsules != null ? bodyContactCapsules.Count : 0;
        if (count <= 0)
        {
            ClearContactPoseCache();
            return;
        }

        if (previousContactStarts != null && previousContactStarts.Length == count)
        {
            return;
        }

        previousContactStarts = new Vector3[count];
        previousContactEnds = new Vector3[count];
        hasPreviousContactPose = new bool[count];
    }

    private void CaptureCurrentContactPoses()
    {
        EnsureContactPoseCache();
        if (bodyContactCapsules == null || previousContactStarts == null)
        {
            return;
        }

        for (int i = 0; i < bodyContactCapsules.Count; i++)
        {
            if (bodyContactCapsules.TryGetCapsule(i, out Vector3 start, out Vector3 end, out _))
            {
                previousContactStarts[i] = start;
                previousContactEnds[i] = end;
                hasPreviousContactPose[i] = true;
            }
            else
            {
                hasPreviousContactPose[i] = false;
            }
        }
    }

    private void ClearContactPoseCache()
    {
        if (hasPreviousContactPose == null)
        {
            return;
        }

        for (int i = 0; i < hasPreviousContactPose.Length; i++)
        {
            hasPreviousContactPose[i] = false;
        }
    }

    private void CacheBossCollisionState()
    {
        if (bossCollisionStateCached)
        {
            return;
        }

        authoredSolidColliders.Clear();
        authoredSolidColliderInitialStates.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger || IsGeneratedContactSolidCollider(collider))
            {
                continue;
            }

            authoredSolidColliders.Add(collider);
            authoredSolidColliderInitialStates.Add(collider.enabled);
        }

        bossCollisionStateCached = true;
    }

    private void SuppressBossCollisionForMovement()
    {
        CacheBossCollisionState();
        movementCollisionSuppressed = true;
        contactCollisionModeActive = false;
        SetAuthoredSolidCollidersEnabled(false);
        bodyContactCapsules?.SetGeneratedSolidCollidersEnabled(false);
    }

    private void SetBossContactCollisionMode(bool active)
    {
        CacheBossCollisionState();
        if (active)
        {
            movementCollisionSuppressed = false;
            contactCollisionModeActive = true;
            SetAuthoredSolidCollidersEnabled(true);
            bodyContactCapsules?.SetGeneratedSolidCollidersEnabled(false);
            return;
        }

        if (contactCollisionModeActive)
        {
            contactCollisionModeActive = false;
        }

        if (movementCollisionSuppressed)
        {
            SetAuthoredSolidCollidersEnabled(false);
            bodyContactCapsules?.SetGeneratedSolidCollidersEnabled(false);
            return;
        }

        RestoreBossCollisionMode();
    }

    private void RestoreBossCollisionMode()
    {
        CacheBossCollisionState();
        movementCollisionSuppressed = false;
        contactCollisionModeActive = false;

        if (controller != null && controller.Health != null && controller.Health.IsDead)
        {
            SetAuthoredSolidCollidersEnabled(false);
            bodyContactCapsules?.SetGeneratedSolidCollidersEnabled(false);
            return;
        }

        for (int i = 0; i < authoredSolidColliders.Count; i++)
        {
            Collider collider = authoredSolidColliders[i];
            if (collider == null)
            {
                continue;
            }

            bool initialEnabled = i < authoredSolidColliderInitialStates.Count && authoredSolidColliderInitialStates[i];
            collider.enabled = initialEnabled;
        }

        bodyContactCapsules?.SetGeneratedSolidCollidersEnabled(false);
    }

    private void SetAuthoredSolidCollidersEnabled(bool enabled)
    {
        CacheBossCollisionState();
        for (int i = 0; i < authoredSolidColliders.Count; i++)
        {
            Collider collider = authoredSolidColliders[i];
            if (collider == null)
            {
                continue;
            }

            bool initialEnabled = i < authoredSolidColliderInitialStates.Count && authoredSolidColliderInitialStates[i];
            collider.enabled = enabled && initialEnabled;
        }
    }

    private bool IsGeneratedContactSolidCollider(Collider collider)
    {
        return bodyContactCapsules != null && bodyContactCapsules.IsGeneratedSolidCollider(collider);
    }

    private static bool CanApplyContactDamage(BossContactDamageEvent contactEvent, Dictionary<IDamageable, float> eventHitTimes, IDamageable damageable, float currentTime)
    {
        if (eventHitTimes == null || !eventHitTimes.TryGetValue(damageable, out float lastHitTime))
        {
            return true;
        }

        if (contactEvent.hitOncePerTarget)
        {
            return false;
        }

        return contactEvent.hitInterval <= 0f || currentTime - lastHitTime >= contactEvent.hitInterval;
    }

    private int GetEventDamage(float eventMultiplier)
    {
        int baseDamage = controller != null ? controller.CurrentAttack : 1;
        float skillMultiplier = skill != null ? Mathf.Max(0f, skill.damageMultiplier) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * skillMultiplier * Mathf.Max(0f, eventMultiplier)));
    }

    private void PlayHitVfx(string hitVFXKey, Vector3 randomEulerRange, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (string.IsNullOrWhiteSpace(hitVFXKey) || GameMgr.VFX == null)
        {
            return;
        }

        Quaternion rotation = CreateLookRotation(hitDirection, transform.rotation);
        rotation *= Quaternion.Euler(GetRandomEuler(randomEulerRange));
        GameMgr.VFX.Play(hitVFXKey, hitPoint, rotation);
    }

    private void Finish()
    {
        playing = false;
        finished = true;
        movementCollisionSuppressed = false;
        contactCollisionModeActive = false;
        RestoreBossCollisionMode();
        StopMovementWarnings();
        StopActiveVFX();
        CleanupSpawnedObjects();
        RestoreAnimatorSpeed();
    }

    private float GetPlaybackDuration()
    {
        if (skill == null)
        {
            return 0f;
        }

        return Mathf.Max(0.01f, skill.totalDuration);
    }

    private LayerMask GetResolvedTargetLayers()
    {
        if (targetLayers.value != 0)
        {
            return targetLayers;
        }

        int playerMask = LayerMask.GetMask(DefaultTargetLayerName);
        return playerMask != 0 ? playerMask : Physics.AllLayers;
    }

    private void EnsureDefaultTargetLayers()
    {
        if (targetLayers.value != 0)
        {
            return;
        }

        int playerMask = LayerMask.GetMask(DefaultTargetLayerName);
        if (playerMask != 0)
        {
            targetLayers = playerMask;
        }
    }

    private bool IsSelfCollider(Collider hit)
    {
        if (hit == null)
        {
            return false;
        }

        return hit.transform == transform ||
               hit.transform.IsChildOf(transform) ||
               transform.IsChildOf(hit.transform);
    }

    private static bool ShouldSkipDamageable(IDamageable damageable)
    {
        return damageable is BossHealth || damageable is MonsterHealth;
    }

    private static bool DidCross(float previousTime, float currentTime, float eventTime)
    {
        return eventTime >= previousTime && eventTime <= currentTime;
    }

    private static string GetProjectileName(BossBarrageEvent barrageEvent)
    {
        if (!string.IsNullOrWhiteSpace(barrageEvent.projectileVFXKey))
        {
            return barrageEvent.projectileVFXKey;
        }

        return barrageEvent.projectilePrefab != null ? barrageEvent.projectilePrefab.name : "BossProjectile";
    }

    private static Vector3 GetRandomEuler(Vector3 range)
    {
        return new Vector3(
            Random.Range(-Mathf.Abs(range.x), Mathf.Abs(range.x)),
            Random.Range(-Mathf.Abs(range.y), Mathf.Abs(range.y)),
            Random.Range(-Mathf.Abs(range.z), Mathf.Abs(range.z)));
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

    private void OnDrawGizmos()
    {
        if (!drawRuntimeGizmos || skill == null || !playing)
        {
            return;
        }

        Gizmos.color = hitboxGizmoColor;
        for (int i = 0; i < skill.hitboxEvents.Count; i++)
        {
            BossHitboxEvent hitbox = skill.hitboxEvents[i];
            if (hitbox == null || playTime < hitbox.beginWindowTime || playTime > hitbox.endWindowTime)
            {
                continue;
            }

            Vector3 center = transform.TransformPoint(hitbox.offset);
            if (hitbox.shape == BossHitboxShape.Cylinder)
            {
                DrawHitboxCylinderGizmo(center, hitbox);
            }
            else
            {
                Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, hitbox.radius));
            }
        }

        Gizmos.color = movementGizmoColor;
        for (int i = 0; i < skill.movementEvents.Count; i++)
        {
            BossMovementEvent movementEvent = skill.movementEvents[i];
            if (movementEvent == null || playTime < movementEvent.triggerTime)
            {
                continue;
            }

            Vector3 destination = ResolveMovementDestination(movementEvent);
            Gizmos.DrawWireSphere(destination, 0.5f);
            Gizmos.DrawLine(transform.position, destination);
        }

        if (bodyContactCapsules == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.8f);
        for (int i = 0; i < skill.contactDamageEvents.Count; i++)
        {
            BossContactDamageEvent contactEvent = skill.contactDamageEvents[i];
            if (contactEvent == null || playTime < contactEvent.beginWindowTime || playTime > contactEvent.endWindowTime)
            {
                continue;
            }

            for (int capsuleIndex = 0; capsuleIndex < bodyContactCapsules.Count; capsuleIndex++)
            {
                if (!bodyContactCapsules.ShouldUseCapsule(contactEvent, capsuleIndex) ||
                    !bodyContactCapsules.TryGetCapsule(capsuleIndex, out Vector3 start, out Vector3 end, out float radius))
                {
                    continue;
                }

                DrawContactCapsuleGizmo(start, end, radius);
            }
        }
    }

    private void DrawHitboxCylinderGizmo(Vector3 center, BossHitboxEvent hitbox)
    {
        float radius = Mathf.Max(0.01f, hitbox.radius);
        float height = Mathf.Max(0.01f, hitbox.height);
        Quaternion rotation = GetHitboxRotation(hitbox);
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 halfHeight = up * (height * 0.5f);
        Vector3 bottom = center - halfHeight;
        Vector3 top = center + halfHeight;

        DrawGizmoCircle(bottom, right, forward, radius);
        DrawGizmoCircle(top, right, forward, radius);
        Gizmos.DrawLine(bottom + right * radius, top + right * radius);
        Gizmos.DrawLine(bottom - right * radius, top - right * radius);
        Gizmos.DrawLine(bottom + forward * radius, top + forward * radius);
        Gizmos.DrawLine(bottom - forward * radius, top - forward * radius);
    }

    private static void DrawGizmoCircle(Vector3 center, Vector3 right, Vector3 forward, float radius)
    {
        const int Segments = 32;
        Vector3 previous = center + right * radius;
        for (int i = 1; i <= Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            Vector3 next = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private static void DrawContactCapsuleGizmo(Vector3 start, Vector3 end, float radius)
    {
        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);

        Vector3 axis = end - start;
        Vector3 up = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(up, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        Vector3 forward = Vector3.Cross(right, up).normalized;
        Gizmos.DrawLine(start + right * radius, end + right * radius);
        Gizmos.DrawLine(start - right * radius, end - right * radius);
        Gizmos.DrawLine(start + forward * radius, end + forward * radius);
        Gizmos.DrawLine(start - forward * radius, end - forward * radius);
    }

    private Quaternion GetHitboxRotation(BossHitboxEvent hitbox)
    {
        return transform.rotation * Quaternion.Euler(hitbox != null ? hitbox.rotation : Vector3.zero);
    }
}
