using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class PlayerWeaponAttackEffectController : MonoBehaviour
{
    private const int MaxHitResults = 32;
    private const string DatabaseAddress = "WeaponAttackEffectDatabase";
    private const string AttackLayerName = "UpperBody";
    private const string AttackLayerDefaultStateName = "Empty";
    private const string DefaultTargetLayerName = "Monster";
    private const string TreeTargetLayerName = "Tree";
    private const string OreTargetLayerName = "Ore";

    [Header("Config")]
    [SerializeField] private WeaponAttackEffectDatabaseSO database;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool restartWhenPressed = true;
    [SerializeField] private bool playAnimatorAttackLayer = true;
    [SerializeField] private bool driveEventsByAnimationEvents = true;
    [SerializeField] private List<WeaponAttackAnimatorStateBinding> animatorStateBindings = new List<WeaponAttackAnimatorStateBinding>
    {
        new WeaponAttackAnimatorStateBinding("HumanM@Attack1H02_R", "Atk", false),
        new WeaponAttackAnimatorStateBinding("HumanM@1HAttack03_R", "Atk2", true)
    };

    [Header("Debug")]
    [SerializeField] private bool drawRuntimeGizmos = true;
    [SerializeField] private bool showRuntimeDebugPanel;
    [SerializeField] private bool showAttackHitboxes = true;
    [SerializeField] private KeyCode debugPanelToggleKey = KeyCode.F7;

    private readonly Collider[] hitResults = new Collider[MaxHitResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly Dictionary<string, float> previousAnimatorClipTimes = new Dictionary<string, float>();

    private PlayerAttackController attackController;
    private PlayerStateDriver stateDriver;
    private PlayerWeaponModeController weaponModeController;
    private PlayerModelManager modelManager;
    private Animator targetAnimator;
    private WeaponAttackEffectSO currentEffect;
    private WeaponItem currentWeapon;

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
    private bool[] hitboxWindowBegan;
    private bool[] hitboxWindowEnded;
    private bool playing;
    private bool lastFireInput;
    private bool animationEventsInstalled;
    private int activeHitboxWindowCount;
    private float playTime;
    private string lastAnimatorAttackStateName = string.Empty;
    private Rect debugPanelRect = new Rect(16f, 16f, 330f, 238f);
    private bool remoteVisualOnly;

    public bool IsPlaying => playing;
    public WeaponAttackEffectSO CurrentEffect => currentEffect;
    public string CurrentEffectAddressKey => currentEffect != null ? currentEffect.name : string.Empty;
    public string CurrentEffectWeaponName => currentEffect != null ? currentEffect.weaponName : string.Empty;

    private void Awake()
    {
        attackController = GetComponent<PlayerAttackController>();
        stateDriver = GetComponent<PlayerStateDriver>();
        weaponModeController = GetComponent<PlayerWeaponModeController>();
        modelManager = GetComponent<PlayerModelManager>();
        targetAnimator = GetComponentInChildren<Animator>(true);
        EnsureDefaultTargetLayers();
    }

    private void OnEnable()
    {
        remoteVisualOnly = remoteVisualOnly || IsRemoteNetworkReplica();

        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
            modelManager.onModelSwitched += HandleModelSwitched;
        }

        if (!remoteVisualOnly && GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            GameMgr.Equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }

        LoadDatabaseAndBindAsync().Forget();
    }

    private bool IsRemoteNetworkReplica()
    {
        Unity.Netcode.NetworkObject networkObject = GetComponentInParent<Unity.Netcode.NetworkObject>();
        return networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner;
    }

    private void OnDisable()
    {
        if (modelManager != null)
        {
            modelManager.onModelSwitched -= HandleModelSwitched;
        }

        if (GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        Stop();
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugPanelToggleKey))
        {
            showRuntimeDebugPanel = !showRuntimeDebugPanel;
        }

        if (remoteVisualOnly)
        {
            UpdatePlayingEffectTimeline();
            return;
        }

        bool firePressed = GameMgr.input != null && GameMgr.input.Data.Fire;
        if (weaponModeController == null)
        {
            weaponModeController = GetComponent<PlayerWeaponModeController>();
        }

        if (!firePressed)
        {
            if (playing)
            {
                Stop();
            }

            lastFireInput = false;
            return;
        }

        if (firePressed && weaponModeController != null && !weaponModeController.CanUseWeaponAttack)
        {
            Stop();
            lastFireInput = firePressed;
            return;
        }

        bool shouldStartAttack = firePressed &&
                                 currentEffect != null &&
                                 (!playing || (!lastFireInput && restartWhenPressed));
        if (shouldStartAttack)
        {
            PlayCurrentEffect();
        }

        lastFireInput = firePressed;
        UpdatePlayingEffectTimeline();
    }

    private void UpdatePlayingEffectTimeline()
    {
        if (!playing || currentEffect == null)
        {
            return;
        }

        if (playAnimatorAttackLayer)
        {
            playTime += Time.deltaTime * GetAttackSpeedMultiplier();
        }
        else
        {
            playTime += Time.deltaTime * GetAttackSpeedMultiplier();
            UpdateAnimation();
        }

        if (animationEventsInstalled)
        {
            UpdateAnimatorClipTimeEvents();
        }
        else
        {
            UpdateVfxEvents();
            UpdateAudioEvents();
            UpdateHitboxWindows();
            UpdateHitboxEvents();
        }

        float loopDuration = playAnimatorAttackLayer ? GetAnimationCycleDuration() : GetPlaybackDuration();
        if (playTime >= loopDuration)
        {
            HandleLoopBoundary(loopDuration);
        }
    }

    public void PlayCurrentEffect()
    {
        if (weaponModeController == null)
        {
            weaponModeController = GetComponent<PlayerWeaponModeController>();
        }

        if (weaponModeController != null && !weaponModeController.CanUseWeaponAttack)
        {
            Stop();
            return;
        }

        if (currentEffect == null)
        {
            BindCurrentWeaponEffect();
        }

        if (currentEffect == null)
        {
            Debug.LogWarning("[PlayerWeaponAttackEffectController] No WeaponAttackEffectSO found for current weapon.", this);
            return;
        }

        StartEffectPlayback();
        NotifyNetworkAttackVisualStarted();
    }

    public void PlayRemoteAttackEffect(string effectAddressKey, string effectWeaponName, string weaponModelName)
    {
        SetRemoteVisualOnly(true);
        PlayRemoteAttackEffectAsync(effectAddressKey, effectWeaponName, weaponModelName).Forget();
    }

    public void SetRemoteVisualOnly(bool enabled)
    {
        remoteVisualOnly = enabled;
        if (remoteVisualOnly && GameMgr.Equipment != null)
        {
            GameMgr.Equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }

    private async UniTaskVoid PlayRemoteAttackEffectAsync(string effectAddressKey, string effectWeaponName, string weaponModelName)
    {
        if (database == null && GameMgr.AssetLoader != null)
        {
            database = await GameMgr.AssetLoader.LoadAsset<WeaponAttackEffectDatabaseSO>(DatabaseAddress);
        }

        currentWeapon = null;
        currentEffect = ResolveEffectByWeaponKey(effectWeaponName);
        if (currentEffect == null)
        {
            currentEffect = ResolveEffectByWeaponKey(effectAddressKey);
        }
        if (currentEffect == null)
        {
            currentEffect = ResolveEffectByWeaponKey(weaponModelName);
        }
        if (currentEffect == null && GameMgr.AssetLoader != null)
        {
            currentEffect = await LoadEffectByAddressAsync(effectAddressKey);
        }
        if (currentEffect == null && GameMgr.AssetLoader != null)
        {
            currentEffect = await LoadEffectByAddressAsync(effectWeaponName);
        }
        if (currentEffect == null && GameMgr.AssetLoader != null)
        {
            currentEffect = await LoadEffectByAddressAsync(weaponModelName);
        }

        if (this == null || !isActiveAndEnabled || !remoteVisualOnly || currentEffect == null)
        {
            return;
        }

        StartEffectPlayback();
    }

    private void StartEffectPlayback()
    {
        Stop(false);
        playing = true;
        SetContextAttackPlaying(true);
        ResetTimelineState();

        if (remoteVisualOnly)
        {
            if (playAnimatorAttackLayer && PlayAnimatorAttackState())
            {
                previousAnimatorClipTimes.Clear();
                animationEventsInstalled = false;
                return;
            }

            animationEventsInstalled = false;
            EnsureAnimationGraph();
            UpdateAnimation();
            return;
        }

        if (playAnimatorAttackLayer)
        {
            PlayAnimatorAttackState();
            previousAnimatorClipTimes.Clear();
            animationEventsInstalled = driveEventsByAnimationEvents;
        }
        else
        {
            animationEventsInstalled = false;
            EnsureAnimationGraph();
            UpdateAnimation();
        }
    }

    private void NotifyNetworkAttackVisualStarted()
    {
        NetworkPlayer networkPlayer = GetComponentInParent<NetworkPlayer>();
        if (networkPlayer != null)
        {
            networkPlayer.NotifyLocalWeaponAttackEffectStarted();
        }
    }

    public void Stop()
    {
        Stop(true);
    }

    private void Stop(bool resetAnimatorLayer)
    {
        if (playing && !remoteVisualOnly)
        {
            attackController?.EndAttackWindow();
        }

        playing = false;
        SetContextAttackPlaying(false);
        playTime = 0f;
        activeHitboxWindowCount = 0;
        hitTargets.Clear();
        DestroyAnimationGraph();
        if (resetAnimatorLayer)
        {
            ResetAnimatorAttackLayer();
        }

        CleanupSpawnedObjects();
    }

    private void RestartTimelineLoop()
    {
        if (!remoteVisualOnly)
        {
            attackController?.EndAttackWindow();
        }

        ResetTimelineState();
        playing = true;

        if (remoteVisualOnly)
        {
            if (playAnimatorAttackLayer && PlayAnimatorAttackState())
            {
                DestroyAnimationGraph();
                animationEventsInstalled = false;
            }
            else
            {
                DestroyAnimationGraph();
                animationEventsInstalled = false;
                EnsureAnimationGraph();
                UpdateAnimation();
            }

            return;
        }

        if (playAnimatorAttackLayer)
        {
            PlayAnimatorAttackState();
            previousAnimatorClipTimes.Clear();
        }
        else
        {
            DestroyAnimationGraph();
            EnsureAnimationGraph();
            UpdateAnimation();
        }
    }

    private void HandleLoopBoundary(float loopDuration)
    {
        if (remoteVisualOnly)
        {
            Stop(true);
            return;
        }

        if (playAnimatorAttackLayer && animationEventsInstalled && !ShouldRestartAnimatorStateEachLoop())
        {
            playTime = Mathf.Repeat(playTime, Mathf.Max(0.01f, loopDuration));
            NotifyNetworkAttackVisualStarted();
            return;
        }

        RestartTimelineLoop();
        NotifyNetworkAttackVisualStarted();
    }

    private void ResetTimelineState()
    {
        playTime = 0f;
        vfxTriggered = new bool[currentEffect.vfxEvents.Count];
        audioTriggered = new bool[currentEffect.audioEvents.Count];
        hitboxApplied = new bool[currentEffect.hitboxEvents.Count];
        hitboxWindowBegan = new bool[currentEffect.hitboxEvents.Count];
        hitboxWindowEnded = new bool[currentEffect.hitboxEvents.Count];
        hitTargets.Clear();
        activeHitboxWindowCount = 0;
    }

    public void TriggerVfxEventByAnimation(int eventIndex)
    {
        if (!playing || currentEffect == null || eventIndex < 0 || eventIndex >= currentEffect.vfxEvents.Count)
        {
            return;
        }

        WeaponVFXEvent vfx = currentEffect.vfxEvents[eventIndex];
        if (vfx == null)
        {
            return;
        }

        if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger)
        {
            SpawnProjectile(vfx);
        }
        else
        {
            SpawnSelfMotionVfx(vfx);
        }
    }

    public void TriggerAudioEventByAnimation(int eventIndex)
    {
        if (!playing || currentEffect == null || eventIndex < 0 || eventIndex >= currentEffect.audioEvents.Count)
        {
            return;
        }

        PlayAudioEvent(currentEffect.audioEvents[eventIndex]);
        if (audioTriggered != null && eventIndex < audioTriggered.Length)
        {
            audioTriggered[eventIndex] = true;
        }
    }

    public void BeginHitboxEventByAnimation(int eventIndex)
    {
        if (!playing || currentEffect == null || eventIndex < 0 || eventIndex >= currentEffect.hitboxEvents.Count)
        {
            return;
        }

        if (remoteVisualOnly)
        {
            if (hitboxWindowBegan != null && eventIndex < hitboxWindowBegan.Length)
            {
                hitboxWindowBegan[eventIndex] = true;
            }

            if (hitboxWindowEnded != null && eventIndex < hitboxWindowEnded.Length)
            {
                hitboxWindowEnded[eventIndex] = false;
            }

            return;
        }

        if (activeHitboxWindowCount <= 0)
        {
            hitTargets.Clear();
            attackController?.BeginAttackWindow();
        }

        activeHitboxWindowCount++;
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
        if (!playing || currentEffect == null || eventIndex < 0 || eventIndex >= currentEffect.hitboxEvents.Count)
        {
            return;
        }

        if (remoteVisualOnly)
        {
            if (hitboxApplied != null && eventIndex < hitboxApplied.Length)
            {
                hitboxApplied[eventIndex] = true;
            }

            return;
        }

        WeaponHitboxEvent hitbox = currentEffect.hitboxEvents[eventIndex];
        if (hitbox != null)
        {
            ApplyHitboxDamage(hitbox);
        }

        if (hitboxApplied != null && eventIndex < hitboxApplied.Length)
        {
            hitboxApplied[eventIndex] = true;
        }
    }

    public void EndHitboxEventByAnimation(int eventIndex)
    {
        if (!playing || currentEffect == null || eventIndex < 0 || eventIndex >= currentEffect.hitboxEvents.Count)
        {
            return;
        }

        if (remoteVisualOnly)
        {
            if (hitboxWindowEnded != null && eventIndex < hitboxWindowEnded.Length)
            {
                hitboxWindowEnded[eventIndex] = true;
            }

            return;
        }

        activeHitboxWindowCount = Mathf.Max(0, activeHitboxWindowCount - 1);
        if (activeHitboxWindowCount <= 0)
        {
            attackController?.EndAttackWindow();
        }

        if (hitboxWindowEnded != null && eventIndex < hitboxWindowEnded.Length)
        {
            hitboxWindowEnded[eventIndex] = true;
        }
    }

    private async UniTaskVoid LoadDatabaseAndBindAsync()
    {
        if (database == null && GameMgr.AssetLoader != null)
        {
            database = await GameMgr.AssetLoader.LoadAsset<WeaponAttackEffectDatabaseSO>(DatabaseAddress);
        }

        if (remoteVisualOnly)
        {
            return;
        }

        BindCurrentWeaponEffect();
    }

    private void HandleEquipmentChanged()
    {
        Stop();
        previousAnimatorClipTimes.Clear();
        BindCurrentWeaponEffect();
    }

    private void HandleModelSwitched(Animator animator)
    {
        targetAnimator = animator != null ? animator : GetComponentInChildren<Animator>(true);
        previousAnimatorClipTimes.Clear();
        if (playing && !playAnimatorAttackLayer)
        {
            DestroyAnimationGraph();
            EnsureAnimationGraph();
        }
    }

    private void BindCurrentWeaponEffect()
    {
        currentWeapon = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveWeapon() : null;
        currentEffect = database != null ? database.GetEffect(currentWeapon) : null;
        if (currentEffect == null && currentWeapon != null && currentWeapon.IsTool && database != null)
        {
            currentEffect = database.GetEffectByWeaponName("木剑");
        }

        if (currentEffect == null)
        {
            LoadCurrentWeaponEffectByAddressAsync().Forget();
        }
    }

    private WeaponAttackEffectSO ResolveEffectByWeaponKey(string weaponKey)
    {
        if (database == null || string.IsNullOrWhiteSpace(weaponKey))
        {
            return null;
        }

        return database.GetEffectByWeaponName(weaponKey) ?? database.GetEffectByWeaponId(weaponKey);
    }

    private async UniTaskVoid LoadCurrentWeaponEffectByAddressAsync()
    {
        if (currentWeapon == null || GameMgr.AssetLoader == null)
        {
            return;
        }

        string[] keys =
        {
            MakeEffectAddress(currentWeapon.name),
            MakeEffectAddress(currentWeapon.modelName),
            currentWeapon.id > 0 ? MakeEffectAddress(currentWeapon.id.ToString()) : string.Empty
        };

        for (int i = 0; i < keys.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(keys[i]))
            {
                continue;
            }

            WeaponAttackEffectSO loaded = await TryLoadEffectByAddressAsync(keys[i]);
            if (loaded != null)
            {
                currentEffect = loaded;
                return;
            }
        }
    }

    private async UniTask<WeaponAttackEffectSO> LoadEffectByAddressAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || GameMgr.AssetLoader == null)
        {
            return null;
        }

        string address = source.StartsWith("WeaponAttackEffect_", System.StringComparison.OrdinalIgnoreCase)
            ? source.Trim()
            : MakeEffectAddress(source);
        return await TryLoadEffectByAddressAsync(address);
    }

    private static async UniTask<WeaponAttackEffectSO> TryLoadEffectByAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || GameMgr.AssetLoader == null)
        {
            return null;
        }

        bool exists = await AddressableKeyExistsAsync(address);
        return exists
            ? await GameMgr.AssetLoader.LoadAsset<WeaponAttackEffectSO>(address)
            : null;
    }

    private static async UniTask<bool> AddressableKeyExistsAsync(string address)
    {
        AsyncOperationHandle<IList<IResourceLocation>> handle = Addressables.LoadResourceLocationsAsync(
            address,
            typeof(WeaponAttackEffectSO));

        try
        {
            await handle.ToUniTask();
            return handle.Status == AsyncOperationStatus.Succeeded &&
                   handle.Result != null &&
                   handle.Result.Count > 0;
        }
        finally
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }

    private static string MakeEffectAddress(string source)
    {
        return string.IsNullOrWhiteSpace(source) ? string.Empty : "WeaponAttackEffect_" + source.Trim();
    }

    private bool TryGetClipLocalTime(AnimationClip sourceClip, float timelineTime, out float localTime)
    {
        localTime = 0f;
        if (sourceClip == null || currentEffect == null)
        {
            return false;
        }

        for (int i = 0; i < currentEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent animationEvent = currentEffect.animationEvents[i];
            if (animationEvent == null || animationEvent.animationClip == null)
            {
                continue;
            }

            if (!string.Equals(animationEvent.animationClip.name, sourceClip.name, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            float clipDuration = Mathf.Max(0.01f, animationEvent.animationClip.length);
            float clipStart = animationEvent.startTime;
            float clipEnd = clipStart + clipDuration;
            if (timelineTime < clipStart - 0.0001f || timelineTime > clipEnd + 0.0001f)
            {
                continue;
            }

            localTime = Mathf.Clamp(timelineTime - clipStart, 0f, Mathf.Max(0f, sourceClip.length - 0.0001f));
            return true;
        }

        return false;
    }

    private void UpdateAnimatorClipTimeEvents()
    {
        if (currentEffect == null)
        {
            return;
        }

        if (!HasUsableTargetAnimator())
        {
            UpdateVfxEvents();
            UpdateAudioEvents();
            UpdateHitboxWindows();
            UpdateHitboxEvents();
            return;
        }

        int layerIndex = targetAnimator.GetLayerIndex(AttackLayerName);
        if (layerIndex < 0)
        {
            return;
        }

        bool processedAnyClip = false;
        ProcessAnimatorClipInfos(layerIndex, false, ref processedAnyClip);
        if (targetAnimator.IsInTransition(layerIndex))
        {
            ProcessAnimatorClipInfos(layerIndex, true, ref processedAnyClip);
        }

        if (!processedAnyClip)
        {
            UpdateVfxEvents();
            UpdateAudioEvents();
            UpdateHitboxWindows();
            UpdateHitboxEvents();
        }
    }

    private void ProcessAnimatorClipInfos(int layerIndex, bool nextState, ref bool processedAnyClip)
    {
        if (!HasUsableTargetAnimator())
        {
            return;
        }

        AnimatorStateInfo stateInfo = nextState
            ? targetAnimator.GetNextAnimatorStateInfo(layerIndex)
            : targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);
        AnimatorClipInfo[] clipInfos = nextState
            ? targetAnimator.GetNextAnimatorClipInfo(layerIndex)
            : targetAnimator.GetCurrentAnimatorClipInfo(layerIndex);

        for (int i = 0; i < clipInfos.Length; i++)
        {
            AnimationClip clip = clipInfos[i].clip;
            if (clip == null || clipInfos[i].weight <= 0.01f)
            {
                continue;
            }

            float clipLength = Mathf.Max(0.01f, clip.length);
            float normalizedTime = Mathf.Max(0f, stateInfo.normalizedTime);
            float currentLocalTime = Mathf.Repeat(normalizedTime, 1f) * clipLength;
            string key = clip.GetInstanceID().ToString();
            if (!previousAnimatorClipTimes.TryGetValue(key, out float previousLocalTime))
            {
                previousLocalTime = 0f;
            }

            TriggerCrossedAnimatorClipEvents(clip, previousLocalTime, currentLocalTime);
            previousAnimatorClipTimes[key] = currentLocalTime;
            processedAnyClip = true;
        }
    }

    private void TriggerCrossedAnimatorClipEvents(AnimationClip clip, float previousLocalTime, float currentLocalTime)
    {
        for (int i = 0; i < currentEffect.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = currentEffect.vfxEvents[i];
            if (vfx == null || !TryGetClipLocalTime(clip, vfx.triggerTime, out float localTime))
            {
                continue;
            }

            if (DidAnimatorClipTimeCross(previousLocalTime, currentLocalTime, localTime))
            {
                TriggerVfxEventByAnimation(i);
            }
        }

        for (int i = 0; i < currentEffect.audioEvents.Count; i++)
        {
            WeaponAudioEvent audioEvent = currentEffect.audioEvents[i];
            if (audioEvent == null || !TryGetClipLocalTime(clip, audioEvent.triggerTime, out float localTime))
            {
                continue;
            }

            if (DidAnimatorClipTimeCross(previousLocalTime, currentLocalTime, localTime))
            {
                TriggerAudioEventByAnimation(i);
            }
        }

        for (int i = 0; i < currentEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentEffect.hitboxEvents[i];
            if (hitbox == null)
            {
                continue;
            }

            if (TryGetClipLocalTime(clip, hitbox.beginWindowTime, out float beginTime) &&
                DidAnimatorClipTimeCross(previousLocalTime, currentLocalTime, beginTime))
            {
                BeginHitboxEventByAnimation(i);
            }

            if (TryGetClipLocalTime(clip, hitbox.applyDamageTime, out float applyTime) &&
                DidAnimatorClipTimeCross(previousLocalTime, currentLocalTime, applyTime))
            {
                ApplyHitboxEventByAnimation(i);
            }

            if (TryGetClipLocalTime(clip, hitbox.endWindowTime, out float endTime) &&
                DidAnimatorClipTimeCross(previousLocalTime, currentLocalTime, endTime))
            {
                EndHitboxEventByAnimation(i);
            }
        }
    }

    private static bool DidAnimatorClipTimeCross(float previousLocalTime, float currentLocalTime, float eventTime)
    {
        const float epsilon = 0.0001f;
        if (Mathf.Abs(currentLocalTime - previousLocalTime) <= epsilon)
        {
            return false;
        }

        if (currentLocalTime > previousLocalTime)
        {
            return eventTime > previousLocalTime + epsilon && eventTime <= currentLocalTime + epsilon;
        }

        return eventTime > previousLocalTime + epsilon || eventTime <= currentLocalTime + epsilon;
    }

    private bool PlayAnimatorAttackState()
    {
        if (!HasUsableTargetAnimator())
        {
            return false;
        }

        int layerIndex = targetAnimator.GetLayerIndex(AttackLayerName);
        if (layerIndex < 0)
        {
            return false;
        }

        string stateName = ResolveAnimatorAttackStateName();
        if (string.IsNullOrWhiteSpace(stateName))
        {
            stateName = "Atk";
        }

        if (!targetAnimator.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            return false;
        }

        targetAnimator.SetLayerWeight(layerIndex, 1f);
        targetAnimator.Play(stateName, layerIndex, 0f);
        targetAnimator.Update(0f);
        lastAnimatorAttackStateName = stateName;
        return true;
    }

    private void ResetAnimatorAttackLayer()
    {
        if (!playAnimatorAttackLayer || !HasUsableTargetAnimator())
        {
            return;
        }

        int layerIndex = targetAnimator.GetLayerIndex(AttackLayerName);
        if (layerIndex < 0)
        {
            return;
        }

        targetAnimator.Play(AttackLayerDefaultStateName, layerIndex, 0f);
        targetAnimator.SetLayerWeight(layerIndex, 0f);
        lastAnimatorAttackStateName = string.Empty;
    }

    private string ResolveAnimatorAttackStateName()
    {
        WeaponAttackAnimatorStateBinding binding = ResolveAnimatorAttackBinding();
        return binding != null ? binding.stateName : string.Empty;
    }

    private bool ShouldRestartAnimatorStateEachLoop()
    {
        WeaponAttackAnimatorStateBinding binding = ResolveAnimatorAttackBinding();
        if (binding == null)
        {
            return false;
        }

        return binding.restartStateEachCycle ||
               string.Equals(binding.stateName, "Atk2", System.StringComparison.OrdinalIgnoreCase);
    }

    private WeaponAttackAnimatorStateBinding ResolveAnimatorAttackBinding()
    {
        string marker = GetFirstAnimationMarkerName();
        for (int i = 0; i < animatorStateBindings.Count; i++)
        {
            WeaponAttackAnimatorStateBinding binding = animatorStateBindings[i];
            if (binding != null && binding.Matches(marker))
            {
                return binding;
            }
        }

        return null;
    }

    private string GetFirstAnimationMarkerName()
    {
        WeaponAnimationEvent first = null;
        for (int i = 0; i < currentEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentEffect.animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            if (first == null || clip.startTime < first.startTime)
            {
                first = clip;
            }
        }

        if (first == null)
        {
            return string.Empty;
        }

        if (first.animationClip != null)
        {
            return first.animationClip.name;
        }

        return first.label;
    }

    private float GetPlaybackDuration()
    {
        float duration = currentEffect != null ? Mathf.Max(0.01f, currentEffect.totalDuration) : 0.01f;

        for (int i = 0; i < currentEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentEffect.animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
            duration = Mathf.Max(duration, clip.startTime + clipDuration);
        }

        for (int i = 0; i < currentEffect.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = currentEffect.vfxEvents[i];
            if (vfx == null)
            {
                continue;
            }

            float clipDuration = vfx.clipType == WeaponVFXClipType.ProjectileTrigger
                ? Mathf.Max(0.01f, vfx.projectileLifeTime)
                : Mathf.Max(0.01f, vfx.duration);
            duration = Mathf.Max(duration, vfx.triggerTime + clipDuration);
        }

        for (int i = 0; i < currentEffect.audioEvents.Count; i++)
        {
            WeaponAudioEvent audioEvent = currentEffect.audioEvents[i];
            if (audioEvent == null)
            {
                continue;
            }

            duration = Mathf.Max(duration, audioEvent.triggerTime + audioEvent.Duration);
        }

        return duration;
    }

    private float GetAnimationCycleDuration()
    {
        if (currentEffect == null)
        {
            return 0.01f;
        }

        float duration = 0f;
        for (int i = 0; i < currentEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentEffect.animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
            duration = Mathf.Max(duration, clip.startTime + clipDuration);
        }

        return Mathf.Max(0.01f, duration > 0f ? duration : currentEffect.totalDuration);
    }

    private float GetAttackSpeedMultiplier()
    {
        PlayerData data = GameMgr.Instance != null ? GameMgr.Instance.playerData : null;
        return data != null ? Mathf.Clamp(data.GetAttackSpeedMultiplier(), 0.1f, 5f) : 1f;
    }

    private void SetContextAttackPlaying(bool value)
    {
        if (stateDriver == null)
        {
            stateDriver = GetComponent<PlayerStateDriver>();
        }

        if (stateDriver != null && stateDriver.ctx != null)
        {
            stateDriver.ctx.weaponAttackPlaying = value;
        }
    }

    private void UpdateAnimation()
    {
        if (targetAnimator == null || currentEffect == null)
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

        for (int i = 0; i < currentEffect.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentEffect.animationEvents[i];
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
        if (animationGraphValid || !HasUsableTargetAnimator())
        {
            return;
        }

        animationGraph = PlayableGraph.Create("PlayerWeaponAttackEffect");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        animationMixer = AnimationMixerPlayable.Create(animationGraph, 2);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "WeaponAttackEffect", targetAnimator);
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

    private void UpdateVfxEvents()
    {
        for (int i = 0; i < currentEffect.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = currentEffect.vfxEvents[i];
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

    private void UpdateAudioEvents()
    {
        for (int i = 0; i < currentEffect.audioEvents.Count; i++)
        {
            WeaponAudioEvent audioEvent = currentEffect.audioEvents[i];
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

        if (GameMgr.Audio != null)
        {
            if (!string.IsNullOrWhiteSpace(audioEvent.soundName))
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

            if (audioEvent.audioClip != null)
            {
                GameMgr.Audio.PlayClipAt(
                    audioEvent.audioClip,
                    transform.position,
                    Mathf.Clamp01(audioEvent.volume),
                    Mathf.Max(0.1f, audioEvent.pitch));
                return;
            }

            if (!string.IsNullOrWhiteSpace(audioEvent.soundName))
            {
                GameMgr.Audio.PlayAt(
                    string.IsNullOrWhiteSpace(audioEvent.soundGroup) ? "Game" : audioEvent.soundGroup,
                    audioEvent.soundName,
                    transform.position,
                    Mathf.Clamp01(audioEvent.volume));
            }
        }
    }

    private void SpawnSelfMotionVfx(WeaponVFXEvent vfx)
    {
        GetSpawnPose(vfx.spawnOffset, vfx.spawnRotation, vfx.attachToWeapon, out Vector3 position, out Quaternion rotation);
        if (vfx.vfxPrefab != null)
        {
            GameObject instance = Instantiate(vfx.vfxPrefab, position, rotation, vfx.attachToWeapon ? transform : null);
            spawnedObjects.Add(instance);
            Destroy(instance, Mathf.Max(0.01f, vfx.duration));
            return;
        }

        if (!string.IsNullOrWhiteSpace(vfx.vfxKey))
        {
            GameMgr.VFX?.Play(vfx.vfxKey, position, rotation);
        }
    }

    private void SpawnProjectile(WeaponVFXEvent vfx)
    {
        GetSpawnPose(vfx.spawnOffset, vfx.spawnRotation, vfx.attachToWeapon, out Vector3 position, out Quaternion rotation);
        if (!remoteVisualOnly && vfx.projectileAimSource == WeaponProjectileAimSource.CameraForward)
        {
            rotation = GetCameraAimRotation(vfx.spawnRotation, rotation);
        }

        WeaponProjectileConfigEntry config = BuildProjectileConfig(vfx);
        WeaponProjectileRuntimeController projectile = WeaponProjectilePool.Get(GetVfxName(vfx));
        if (remoteVisualOnly)
        {
            projectile.InitializeVisualOnly(config, position, rotation);
        }
        else
        {
            projectile.Initialize(config, position, rotation, GetResolvedTargetLayers(), GetBaseDamage(), gameObject);
        }

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
            weaponModelName = currentEffect != null ? currentEffect.weaponName : "PlayerWeapon",
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
            spawnOffset = vfx.spawnOffset,
            usePitchDirection = vfx.projectileAimSource == WeaponProjectileAimSource.CameraForward
        };
    }

    private void UpdateHitboxWindows()
    {
        if (remoteVisualOnly)
        {
            return;
        }

        for (int i = 0; i < currentEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentEffect.hitboxEvents[i];
            if (hitbox == null)
            {
                continue;
            }

            if (!hitboxWindowBegan[i] && playTime >= hitbox.beginWindowTime)
            {
                if (activeHitboxWindowCount <= 0)
                {
                    attackController?.BeginAttackWindow();
                }

                activeHitboxWindowCount++;
                hitboxWindowBegan[i] = true;
            }

            if (!hitboxWindowEnded[i] && playTime >= hitbox.endWindowTime)
            {
                activeHitboxWindowCount = Mathf.Max(0, activeHitboxWindowCount - 1);
                if (activeHitboxWindowCount <= 0)
                {
                    attackController?.EndAttackWindow();
                }

                hitboxWindowEnded[i] = true;
            }
        }
    }

    private void UpdateHitboxEvents()
    {
        if (remoteVisualOnly)
        {
            return;
        }

        for (int i = 0; i < currentEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentEffect.hitboxEvents[i];
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
        if (remoteVisualOnly)
        {
            return;
        }

        Vector3 center = transform.TransformPoint(hitbox.offset);
        float radius = Mathf.Max(0.01f, hitbox.radius);
        int layerMask = GetResolvedTargetLayers().value;
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, hitResults, layerMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit == null)
            {
                continue;
            }

            if (IsSelfCollider(hit))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || hitTargets.Contains(damageable))
            {
                continue;
            }

            if (damageable is PlayerHealth)
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

            damageable.TakeDamage(new AttackDamageInfo(GetBaseDamage(), hitPoint, hitDirection, gameObject));
            if (!IsHarvestableDamageable(damageable))
            {
                PlayHitboxHitVfx(hitbox, hitPoint, hitDirection);
            }
        }

        attackController?.ApplyAttackDamage();
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

    private bool IsSelfCollider(Collider hit)
    {
        return hit != null &&
               (hit.transform == transform ||
                hit.transform.IsChildOf(transform) ||
                transform.IsChildOf(hit.transform));
    }

    private int GetBaseDamage()
    {
        if (attackController != null)
        {
            return attackController.GetCurrentAttackDamage();
        }

        if (GameMgr.Instance != null && GameMgr.Instance.playerData != null)
        {
            return Mathf.Max(1, GameMgr.Instance.playerData.GetATK());
        }

        return 1;
    }

    private void EnsureDefaultTargetLayers()
    {
        if (targetLayers.value != 0)
        {
            return;
        }

        int monsterMask = LayerMask.GetMask(DefaultTargetLayerName);
        if (monsterMask != 0)
        {
            targetLayers = monsterMask;
        }
    }

    private LayerMask GetResolvedTargetLayers()
    {
        int mask = targetLayers.value;
        if (targetLayers.value != 0)
        {
            return currentWeapon != null && currentWeapon.IsTool ? mask | GetResourceTargetLayerMask() : mask;
        }

        int monsterMask = LayerMask.GetMask(DefaultTargetLayerName);
        mask = monsterMask != 0 ? monsterMask : Physics.AllLayers;
        return currentWeapon != null && currentWeapon.IsTool ? mask | GetResourceTargetLayerMask() : mask;
    }

    private static int GetResourceTargetLayerMask()
    {
        return LayerMask.GetMask(TreeTargetLayerName, OreTargetLayerName);
    }

    private void GetSpawnPose(Vector3 offset, Vector3 rotationEuler, bool attachToOwner, out Vector3 position, out Quaternion rotation)
    {
        if (attachToOwner)
        {
            position = transform.TransformPoint(offset);
            rotation = transform.rotation * Quaternion.Euler(rotationEuler);
        }
        else
        {
            position = offset;
            rotation = Quaternion.Euler(rotationEuler);
        }
    }

    private Quaternion GetCameraAimRotation(Vector3 rotationEuler, Quaternion fallbackRotation)
    {
        Transform lookAt = transform.Find("LookAt");
        Vector3 aimDirection = lookAt != null ? lookAt.forward : Vector3.zero;
        if (aimDirection.sqrMagnitude <= 0.0001f && Camera.main != null)
        {
            aimDirection = Camera.main.transform.forward;
        }

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return fallbackRotation;
        }

        return Quaternion.LookRotation(aimDirection.normalized, Vector3.up) * Quaternion.Euler(rotationEuler);
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

    private void OnGUI()
    {
        if (!Application.isPlaying || !showRuntimeDebugPanel)
        {
            return;
        }

        debugPanelRect = GUI.Window(GetInstanceID(), debugPanelRect, DrawRuntimeDebugWindow, "Attack Effect Debug");
    }

    private void DrawRuntimeDebugWindow(int windowId)
    {
        string effectName = currentEffect != null ? currentEffect.name : "None";
        string weaponName = currentWeapon != null ? currentWeapon.name : "None";
        float duration = currentEffect != null
            ? (playAnimatorAttackLayer ? GetAnimationCycleDuration() : GetPlaybackDuration())
            : 0f;

        GUILayout.Label("Weapon: " + weaponName);
        GUILayout.Label("Effect: " + effectName);
        GUILayout.Label("Playing: " + (playing ? "Yes" : "No") + "  Mode: " + (animationEventsInstalled ? "Animator" : "Timeline"));
        GUILayout.Label("State: " + (string.IsNullOrWhiteSpace(lastAnimatorAttackStateName) ? "None" : lastAnimatorAttackStateName));
        GUILayout.Label("Clips: " + GetCurrentAttackLayerClipNames());
        GUILayout.Label("Time: " + playTime.ToString("0.000") + " / " + duration.ToString("0.000"));
        GUILayout.Label("Events: VFX " + CountTrue(vfxTriggered) + "/" + GetCount(currentEffect != null ? currentEffect.vfxEvents : null)
            + "  Audio " + CountTrue(audioTriggered) + "/" + GetCount(currentEffect != null ? currentEffect.audioEvents : null)
            + "  Hit " + CountTrue(hitboxApplied) + "/" + GetCount(currentEffect != null ? currentEffect.hitboxEvents : null));
        GUILayout.Label("Hitbox Windows: " + activeHitboxWindowCount + "  Hits This Window: " + hitTargets.Count);

        showAttackHitboxes = GUILayout.Toggle(showAttackHitboxes, "Show attack hitboxes");
        drawRuntimeGizmos = GUILayout.Toggle(drawRuntimeGizmos, "Show projectile hitboxes");

        GUILayout.Label("Toggle Panel: " + debugPanelToggleKey);
        GUI.DragWindow();
    }

    private string GetCurrentAttackLayerClipNames()
    {
        if (!HasUsableTargetAnimator())
        {
            return "None";
        }

        int layerIndex = targetAnimator.GetLayerIndex(AttackLayerName);
        if (layerIndex < 0)
        {
            return "No UpperBody";
        }

        AnimatorClipInfo[] clips = targetAnimator.GetCurrentAnimatorClipInfo(layerIndex);
        if (clips == null || clips.Length == 0)
        {
            return "None";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i].clip;
            if (clip == null || clips[i].weight <= 0.01f)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(clip.name);
        }

        return builder.Length > 0 ? builder.ToString() : "None";
    }

    private static int CountTrue(bool[] values)
    {
        if (values == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                count++;
            }
        }

        return count;
    }

    private static int GetCount<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    private void OnDrawGizmos()
    {
        if (!showAttackHitboxes || currentEffect == null || !Application.isPlaying || !playing)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.08f, 0.04f, 0.88f);
        for (int i = 0; i < currentEffect.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentEffect.hitboxEvents[i];
            if (hitbox == null)
            {
                continue;
            }

            if (!IsHitboxActive(i, hitbox))
            {
                continue;
            }

            Gizmos.DrawWireSphere(transform.TransformPoint(hitbox.offset), Mathf.Max(0.01f, hitbox.radius));
        }
    }

    private bool IsHitboxActive(int index, WeaponHitboxEvent hitbox)
    {
        if (hitbox == null)
        {
            return false;
        }

        if (animationEventsInstalled && hitboxWindowBegan != null && hitboxWindowEnded != null)
        {
            return index < hitboxWindowBegan.Length &&
                   index < hitboxWindowEnded.Length &&
                   hitboxWindowBegan[index] &&
                   !hitboxWindowEnded[index];
        }

        return playTime >= hitbox.beginWindowTime && playTime <= hitbox.endWindowTime;
    }

    private bool HasUsableTargetAnimator()
    {
        return PlayerStateDriver.HasPlayableAnimator(targetAnimator);
    }
}

[System.Serializable]
public sealed class WeaponAttackAnimatorStateBinding
{
    public string firstAnimationName;
    public string stateName;
    public bool restartStateEachCycle;

    public WeaponAttackAnimatorStateBinding(string firstAnimationName, string stateName, bool restartStateEachCycle = false)
    {
        this.firstAnimationName = firstAnimationName;
        this.stateName = stateName;
        this.restartStateEachCycle = restartStateEachCycle;
    }

    public bool Matches(string markerName)
    {
        return !string.IsNullOrWhiteSpace(markerName) &&
               !string.IsNullOrWhiteSpace(firstAnimationName) &&
               string.Equals(markerName.Trim(), firstAnimationName.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
