using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class BedInteraction : BuildInteractionBehaviour
{
    private static readonly List<BedRespawnEntry> activeBeds = new List<BedRespawnEntry>();
    private static int nextRegistrationOrder;
    private static bool sleepTimeScaleActive;
    private static float sleepBaseTimeScale = -1f;
    private static BedInteraction currentSleepingBed;
    private static PlayerStateDriver sleepingPlayer;
    private static bool sleepingPlayerControlWasEnabled;

    [SerializeField] private bool healToFull = true;
    [SerializeField] private Transform sleepTarget;
    [SerializeField] private string sleepAnimationStateName = "Sleep";
    [SerializeField, Min(0f)] private float sleepAnimationBlendDuration = 0.1f;
    [SerializeField] private string wakeAnimationStateName = "Idle";
    [SerializeField, Min(0f)] private float wakeAnimationBlendDuration = 0.02f;
    [SerializeField, Min(0f)] private float sleepTimeScaleMultiplier = 10f;
    [SerializeField] private Vector3 sleepPositionOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 sleepRotationOffsetEuler = new Vector3(0f, 180f, 0f);
    [SerializeField, Min(0f)] private float sleepExitMoveInputThreshold = 0.1f;
    [SerializeField, Min(0f)] private float sleepReinteractionUnlockDistance = 1.25f;

    private bool registeredAsRespawnBed;
    private bool waitingForPlayerToLeaveSleepSpot;
    private int registrationOrder;

    protected override string GetInteractionPromptText()
    {
        return "睡觉";
    }

    protected override void Start()
    {
        base.Start();
        RegisterAsRespawnBed();
    }

    protected override void Update()
    {
        if (currentSleepingBed == this && HasSleepExitMoveInput())
        {
            EndSleep();
        }

        base.Update();
    }

    protected override bool CanInteract()
    {
        return currentSleepingBed == null && !IsWaitingForPlayerToLeaveSleepSpot() && base.CanInteract();
    }

    protected override void Interact()
    {
        PlayerStateDriver player = GetPlayer();
        if (player == null)
        {
            return;
        }

        RefreshRespawnPointFromLatestBed(true);
        TeleportPlayerToBed(player);
        BeginSleep(player);
        PlaySleepAnimation(player);
        AccelerateGameTimeForSleep();

        if (healToFull)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.SetCurrentHP(health.MaxHP);
            }
        }

        GameMgr.Message?.RegisterMessage("You are sleeping.");
    }

    protected override void OnDisable()
    {
        if (currentSleepingBed == this)
        {
            EndSleep();
        }

        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (currentSleepingBed == this)
        {
            EndSleep();
        }

        bool updateRespawnPoint = registeredAsRespawnBed && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        UnregisterAsRespawnBed(updateRespawnPoint);
        base.OnDestroy();
    }

    public void NotifyDemolished()
    {
        UnregisterAsRespawnBed(true);
    }

    private void RegisterAsRespawnBed()
    {
        if (registeredAsRespawnBed)
        {
            return;
        }

        registeredAsRespawnBed = true;
        registrationOrder = ++nextRegistrationOrder;
        RemoveInvalidBedEntries();
        activeBeds.Add(new BedRespawnEntry(this, registrationOrder, GetSceneName()));
        RefreshRespawnPointFromLatestBed(true);
    }

    private void UnregisterAsRespawnBed(bool updateRespawnPoint)
    {
        if (!registeredAsRespawnBed)
        {
            return;
        }

        registeredAsRespawnBed = false;
        for (int i = activeBeds.Count - 1; i >= 0; i--)
        {
            BedRespawnEntry entry = activeBeds[i];
            if (entry.bed == null || entry.bed == this)
            {
                activeBeds.RemoveAt(i);
            }
        }

        if (updateRespawnPoint)
        {
            RefreshRespawnPointFromLatestBed(true);
        }
    }

    private static void RefreshRespawnPointFromLatestBed(bool save)
    {
        RemoveInvalidBedEntries();

        GameFile file = GameMgr.File != null ? GameMgr.File.CurrentGameFile : null;
        if (file == null)
        {
            return;
        }

        BedRespawnEntry latest = GetLatestBedEntry();
        if (latest != null && latest.bed != null)
        {
            file.SetRespawnPoint(latest.sceneName, latest.bed.GetRespawnTransform());
        }
        else
        {
            file.ClearRespawnPoint();
        }

        if (save)
        {
            GameMgr.File.SaveGameFile();
        }
    }

    private static BedRespawnEntry GetLatestBedEntry()
    {
        BedRespawnEntry latest = null;
        for (int i = 0; i < activeBeds.Count; i++)
        {
            BedRespawnEntry entry = activeBeds[i];
            if (entry.bed == null)
            {
                continue;
            }

            if (latest == null || entry.order > latest.order)
            {
                latest = entry;
            }
        }

        return latest;
    }

    private static void RemoveInvalidBedEntries()
    {
        for (int i = activeBeds.Count - 1; i >= 0; i--)
        {
            if (activeBeds[i].bed == null)
            {
                activeBeds.RemoveAt(i);
            }
        }
    }

    private Transform GetRespawnTransform()
    {
        return sleepTarget != null ? sleepTarget : transform;
    }

    private string GetSceneName()
    {
        Scene objectScene = gameObject.scene;
        if (objectScene.IsValid() &&
            objectScene.isLoaded &&
            !string.IsNullOrWhiteSpace(objectScene.name) &&
            objectScene.name != "DontDestroyOnLoad")
        {
            return objectScene.name;
        }

        return SceneManager.GetActiveScene().name;
    }

    private static PlayerStateDriver GetPlayer()
    {
        return GameMgr.Instance != null ? GameMgr.Instance.Player : null;
    }

    private void TeleportPlayerToBed(PlayerStateDriver player)
    {
        if (player == null)
        {
            return;
        }

        Transform target = GetRespawnTransform();
        Vector3 sleepPosition = GetSleepPosition(target);
        Quaternion sleepRotation = GetSleepRotation(target);
        Transform playerTransform = player.transform;
        Transform rigRoot = playerTransform.parent != null ? playerTransform.parent : playerTransform;
        CharacterController characterController = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (rigRoot != playerTransform)
        {
            Vector3 delta = sleepPosition - playerTransform.position;
            rigRoot.position += delta;
            playerTransform.rotation = sleepRotation;
        }
        else
        {
            playerTransform.SetPositionAndRotation(sleepPosition, sleepRotation);
        }

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }
    }

    private void BeginSleep(PlayerStateDriver player)
    {
        if (currentSleepingBed != null && currentSleepingBed != this)
        {
            currentSleepingBed.EndSleep();
        }

        currentSleepingBed = this;
        waitingForPlayerToLeaveSleepSpot = true;
        sleepingPlayer = player;
        sleepingPlayerControlWasEnabled = player != null && player.IsLocalControlEnabled;
        if (sleepingPlayerControlWasEnabled)
        {
            player.SetLocalControlEnabled(false);
        }
    }

    private void EndSleep()
    {
        if (currentSleepingBed != this)
        {
            return;
        }

        PlayerStateDriver playerToWake = sleepingPlayer;
        if (sleepingPlayer != null && sleepingPlayerControlWasEnabled)
        {
            sleepingPlayer.SetLocalControlEnabled(true);
        }

        PlayWakeAnimation(playerToWake);
        currentSleepingBed = null;
        sleepingPlayer = null;
        sleepingPlayerControlWasEnabled = false;
        RestoreGameTimeAfterSleep();
    }

    private bool IsWaitingForPlayerToLeaveSleepSpot()
    {
        if (!waitingForPlayerToLeaveSleepSpot)
        {
            return false;
        }

        Transform playerTransform = PlayerTransform;
        if (playerTransform == null)
        {
            PlayerStateDriver player = GetPlayer();
            playerTransform = player != null ? player.transform : null;
        }

        if (playerTransform == null)
        {
            return true;
        }

        float unlockDistance = Mathf.Max(0f, sleepReinteractionUnlockDistance);
        if (Vector3.Distance(playerTransform.position, GetSleepPosition(GetRespawnTransform())) <= unlockDistance)
        {
            return true;
        }

        waitingForPlayerToLeaveSleepSpot = false;
        return false;
    }

    private bool HasSleepExitMoveInput()
    {
        if (GameMgr.input == null || GameMgr.input.Data == null)
        {
            return false;
        }

        Vector2 moveInput = GameMgr.input.Data.DirKeyAxis;
        return moveInput.sqrMagnitude > sleepExitMoveInputThreshold * sleepExitMoveInputThreshold;
    }

    private Vector3 GetSleepPosition(Transform target)
    {
        return target.position + target.TransformDirection(sleepPositionOffset);
    }

    private Quaternion GetSleepRotation(Transform target)
    {
        return target.rotation * Quaternion.Euler(sleepRotationOffsetEuler);
    }

    private void PlaySleepAnimation(PlayerStateDriver player)
    {
        if (string.IsNullOrWhiteSpace(sleepAnimationStateName))
        {
            return;
        }

        Animator animator = player != null ? player.GetComponentInChildren<Animator>(true) : null;
        if (!PlayerStateDriver.HasPlayableAnimator(animator))
        {
            return;
        }

        int stateHash = Animator.StringToHash(sleepAnimationStateName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"[BedInteraction] Player animator state not found: {sleepAnimationStateName}", this);
            return;
        }

        animator.CrossFade(sleepAnimationStateName, sleepAnimationBlendDuration, 0, 0f);
    }

    private void PlayWakeAnimation(PlayerStateDriver player)
    {
        Animator animator = player != null ? player.GetComponentInChildren<Animator>(true) : null;
        if (!PlayerStateDriver.HasPlayableAnimator(animator))
        {
            return;
        }

        string stateName = !string.IsNullOrWhiteSpace(wakeAnimationStateName)
            ? wakeAnimationStateName
            : player.ctx.idleAnimStateName;
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            Debug.LogWarning($"[BedInteraction] Player wake animator state not found: {stateName}", this);
            return;
        }

        animator.CrossFade(stateName, wakeAnimationBlendDuration, 0, 0f);
    }

    private void AccelerateGameTimeForSleep()
    {
        TimeMgr timeMgr = GameMgr.Time;
        if (timeMgr == null)
        {
            return;
        }

        if (!sleepTimeScaleActive)
        {
            sleepBaseTimeScale = Mathf.Max(0f, timeMgr.TimeScale);
            sleepTimeScaleActive = true;
        }

        float baseScale = sleepBaseTimeScale >= 0f ? sleepBaseTimeScale : timeMgr.TimeScale;
        timeMgr.SetTimeScale(baseScale * Mathf.Max(0f, sleepTimeScaleMultiplier));
    }

    private static void RestoreGameTimeAfterSleep()
    {
        if (!sleepTimeScaleActive)
        {
            return;
        }

        TimeMgr timeMgr = GameMgr.Time;
        if (timeMgr != null && sleepBaseTimeScale >= 0f)
        {
            timeMgr.SetTimeScale(sleepBaseTimeScale);
        }

        sleepBaseTimeScale = -1f;
        sleepTimeScaleActive = false;
    }

    private sealed class BedRespawnEntry
    {
        public readonly BedInteraction bed;
        public readonly int order;
        public readonly string sceneName;

        public BedRespawnEntry(BedInteraction bed, int order, string sceneName)
        {
            this.bed = bed;
            this.order = order;
            this.sceneName = sceneName;
        }
    }
}
