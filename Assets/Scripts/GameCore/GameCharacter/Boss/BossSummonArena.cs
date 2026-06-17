using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisallowMultipleComponent]
public sealed class BossSummonArena : MonoBehaviour
{
    private static readonly List<BossSummonArena> arenas = new List<BossSummonArena>();

    [Header("Item")]
    [SerializeField] private int summonItemId;
    [SerializeField] private bool oneShot = true;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private bool lockBossMovement = true;

    [Header("Player")]
    [SerializeField] private Transform playerTeleportPoint;
    [SerializeField] private Transform playerLookAtPoint;

    [Header("Battle Intro")]
    [SerializeField] private Collider battleTriggerCollider;
    [SerializeField] private bool playIntroImmediately;
    [SerializeField] private PlayableDirector introTimeline;
    [SerializeField] private bool bindIntroBossAnimationTrack = true;
    [SerializeField, Min(0)] private int introBossAnimationTrackIndex;
    [SerializeField] private bool startBattleAfterTimeline = true;

    private BossController activeBoss;
    private bool prepared;
    private bool battleStarted;
    private bool introPlaying;

    public int SummonItemId => summonItemId;
    public string ArenaId => BuildHierarchyId(transform);

    public static BossSummonArena FindAvailableArena(int itemId)
    {
        for (int i = 0; i < arenas.Count; i++)
        {
            BossSummonArena arena = arenas[i];
            if (arena != null && arena.summonItemId == itemId && arena.CanBeginSummon())
            {
                return arena;
            }
        }

        return null;
    }

    public bool CanBeginSummon()
    {
        CleanupFinishedBoss();

        if (summonItemId <= 0 || (oneShot && battleStarted && HasLivingActiveBoss()))
        {
            return false;
        }

        return playerTeleportPoint != null &&
               bossSpawnPoint != null &&
               bossPrefab != null;
    }

    public bool BeginSummon()
    {
        if (!CanBeginSummon())
        {
            return false;
        }

        CleanupFinishedBoss();
        activeBoss = PrepareBoss();
        if (activeBoss == null)
        {
            return false;
        }

        TeleportPlayer();
        prepared = true;
        battleStarted = false;
        introPlaying = false;
        SetBattleTriggerEnabled(!playIntroImmediately);

        if (playIntroImmediately)
        {
            BeginIntro();
        }

        NetworkBoss.SendBossSummon(this, activeBoss);
        return true;
    }

    public static BossController PrepareNetworkSummonedBoss(
        string arenaId,
        Vector3 position,
        Quaternion rotation,
        int currentHP,
        int maxHP,
        bool isDead,
        bool isBattleActive)
    {
        BossSummonArena arena = FindArenaById(arenaId);
        if (arena == null)
        {
            return null;
        }

        return arena.PrepareNetworkSummonedBoss(position, rotation, currentHP, maxHP, isDead, isBattleActive);
    }

    public static BossController TryPrepareNetworkBossByBossId(
        string bossId,
        Vector3 position,
        Quaternion rotation,
        int currentHP,
        int maxHP,
        bool isDead,
        bool isBattleActive)
    {
        if (string.IsNullOrWhiteSpace(bossId))
        {
            return null;
        }

        for (int i = 0; i < arenas.Count; i++)
        {
            BossSummonArena arena = arenas[i];
            if (arena == null || !bossId.StartsWith(arena.ArenaId, System.StringComparison.Ordinal))
            {
                continue;
            }

            return arena.PrepareNetworkSummonedBoss(position, rotation, currentHP, maxHP, isDead, isBattleActive);
        }

        return null;
    }

    internal void NotifyTriggerEntered(Collider other)
    {
        if (introPlaying || battleStarted || !IsPlayerCollider(other))
        {
            return;
        }

        if (!prepared)
        {
            activeBoss = PrepareBoss();
            prepared = activeBoss != null;
        }

        if (!prepared)
        {
            return;
        }

        BeginIntro();
    }

    private void Awake()
    {
        ResetIntroTimeline();
        EnsureTriggerHook();
        SetBattleTriggerEnabled(false);
    }

    private void OnEnable()
    {
        if (!arenas.Contains(this))
        {
            arenas.Add(this);
        }
    }

    private void OnDisable()
    {
        UnbindActiveBoss();
        arenas.Remove(this);
        if (introTimeline != null)
        {
            introTimeline.stopped -= HandleIntroStopped;
        }
    }

    private void ResetIntroTimeline()
    {
        if (introTimeline == null)
        {
            return;
        }

        introTimeline.playOnAwake = false;
        introTimeline.stopped -= HandleIntroStopped;
        introTimeline.Stop();
        introTimeline.time = 0d;
        introTimeline.Evaluate();
    }

    private BossController PrepareBoss()
    {
        Transform spawn = bossSpawnPoint != null ? bossSpawnPoint : transform;
        BossController boss = activeBoss;
        if (boss == null)
        {
            boss = InstantiateBossPrefab(bossPrefab, spawn);
            BindActiveBoss(boss);
        }

        if (boss == null)
        {
            Debug.LogWarning("[BossSummonArena] No valid BossController found. Assign a Boss prefab with BossController.");
            return null;
        }

        Transform bossRoot = GetBossPlacementRoot(boss, spawn);
        ActivateHierarchy(bossRoot);
        ActivateChildren(bossRoot);
        ResetBossPlacement(boss, bossRoot, spawn);
        boss.SetHomeTransform(boss.transform.position, boss.transform.rotation);
        boss.SetTarget(GetPlayerTransform());
        boss.ClearActiveSkill();
        boss.SetMovementLocked(lockBossMovement);
        boss.StopMovement();
        boss.ChangeState(BossStateType.Inactive);
        return boss;
    }

    private static BossController InstantiateBossPrefab(GameObject prefab, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = parent != null
            ? Instantiate(prefab, parent, false)
            : Instantiate(prefab);

        if (parent != null)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }

        BossController boss = instance.GetComponent<BossController>();
        if (boss == null)
        {
            boss = instance.GetComponentInChildren<BossController>(true);
        }

        if (boss == null)
        {
            Destroy(instance);
            return null;
        }

        return boss;
    }

    private static void ResetBossPlacement(BossController boss, Transform bossRoot, Transform spawn)
    {
        if (bossRoot == null || spawn == null)
        {
            return;
        }

        bossRoot.SetParent(spawn, false);
        bossRoot.localPosition = Vector3.zero;
        bossRoot.localRotation = Quaternion.identity;

        if (boss != null && boss.transform != bossRoot)
        {
            boss.transform.localPosition = Vector3.zero;
            boss.transform.localRotation = Quaternion.identity;
        }
    }

    private static Transform GetBossPlacementRoot(BossController boss, Transform spawn)
    {
        if (boss == null)
        {
            return null;
        }

        Transform root = boss.transform;
        while (root.parent != null && root.parent != spawn)
        {
            root = root.parent;
        }

        return root;
    }

    private static void ActivateHierarchy(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }
        }
    }

    private static void ActivateChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && !children[i].gameObject.activeSelf)
            {
                children[i].gameObject.SetActive(true);
            }
        }
    }

    private void TeleportPlayer()
    {
        Transform player = GetPlayerTransform();
        if (player == null || playerTeleportPoint == null)
        {
            return;
        }

        CharacterController characterController = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        player.SetPositionAndRotation(playerTeleportPoint.position, GetPlayerTeleportRotation(player));

        if (characterController != null)
        {
            characterController.enabled = controllerWasEnabled;
        }
    }

    private Quaternion GetPlayerTeleportRotation(Transform player)
    {
        if (playerLookAtPoint == null)
        {
            return playerTeleportPoint.rotation;
        }

        Vector3 direction = playerLookAtPoint.position - playerTeleportPoint.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return player != null ? player.rotation : playerTeleportPoint.rotation;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void BeginIntro()
    {
        if (activeBoss == null)
        {
            activeBoss = PrepareBoss();
        }

        if (activeBoss == null)
        {
            return;
        }

        SetBattleTriggerEnabled(false);
        introPlaying = true;

        if (introTimeline == null)
        {
            CompleteIntro();
            return;
        }

        introTimeline.stopped -= HandleIntroStopped;
        introTimeline.stopped += HandleIntroStopped;
        introTimeline.playOnAwake = false;
        BindIntroTimelineBossAnimation(activeBoss);
        introTimeline.time = 0d;
        introTimeline.Play();
    }

    private void BindIntroTimelineBossAnimation(BossController boss)
    {
        if (!bindIntroBossAnimationTrack || introTimeline == null || boss == null)
        {
            return;
        }

        TimelineAsset timeline = introTimeline.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            return;
        }

        Animator bossAnimator = boss.GetComponentInChildren<Animator>(true);
        if (bossAnimator == null)
        {
            Debug.LogWarning("[BossSummonArena] Boss intro Timeline cannot bind animation track because the active Boss has no Animator.");
            return;
        }

        int animationTrackIndex = 0;
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is not AnimationTrack)
            {
                continue;
            }

            if (animationTrackIndex == introBossAnimationTrackIndex)
            {
                introTimeline.SetGenericBinding(track, bossAnimator);
                introTimeline.RebindPlayableGraphOutputs();
                return;
            }

            animationTrackIndex++;
        }

        Debug.LogWarning("[BossSummonArena] Boss intro Timeline has no Animation Track at index " + introBossAnimationTrackIndex + ".");
    }

    private void HandleIntroStopped(PlayableDirector director)
    {
        if (director != introTimeline)
        {
            return;
        }

        introTimeline.stopped -= HandleIntroStopped;
        CompleteIntro();
    }

    private void CompleteIntro()
    {
        introPlaying = false;
        battleStarted = true;
        prepared = false;

        if (activeBoss == null)
        {
            return;
        }

        activeBoss.SetTarget(GetPlayerTransform());
        if (startBattleAfterTimeline)
        {
            activeBoss.StartBattle();
            NetworkBoss.SendBossSummon(this, activeBoss);
        }
    }

    private void BindActiveBoss(BossController boss)
    {
        if (boss == null)
        {
            return;
        }

        if (activeBoss != null && activeBoss != boss)
        {
            UnbindActiveBoss();
        }

        activeBoss = boss;
        if (activeBoss.Health != null)
        {
            activeBoss.Health.OnDied -= HandleActiveBossDied;
            activeBoss.Health.OnDied += HandleActiveBossDied;
        }
    }

    private void UnbindActiveBoss()
    {
        if (activeBoss != null && activeBoss.Health != null)
        {
            activeBoss.Health.OnDied -= HandleActiveBossDied;
        }
    }

    private void HandleActiveBossDied(BossHealth bossHealth)
    {
        prepared = false;
        battleStarted = false;
        introPlaying = false;
        SetBattleTriggerEnabled(false);
    }

    private bool HasLivingActiveBoss()
    {
        return activeBoss != null && activeBoss.Health != null && !activeBoss.Health.IsDead;
    }

    private void CleanupFinishedBoss()
    {
        if (activeBoss == null || activeBoss.Health == null || !activeBoss.Health.IsDead)
        {
            return;
        }

        BossController deadBoss = activeBoss;
        UnbindActiveBoss();
        activeBoss = null;
        prepared = false;
        battleStarted = false;
        introPlaying = false;

        Transform root = GetBossPlacementRoot(deadBoss, bossSpawnPoint);
        if (root != null)
        {
            Destroy(root.gameObject);
        }
        else
        {
            Destroy(deadBoss.gameObject);
        }
    }

    private void EnsureTriggerHook()
    {
        if (battleTriggerCollider == null)
        {
            battleTriggerCollider = GetComponent<Collider>();
        }

        if (battleTriggerCollider == null)
        {
            return;
        }

        battleTriggerCollider.isTrigger = true;
        BossSummonArenaTrigger trigger = battleTriggerCollider.GetComponent<BossSummonArenaTrigger>();
        if (trigger == null)
        {
            trigger = battleTriggerCollider.gameObject.AddComponent<BossSummonArenaTrigger>();
        }

        trigger.Bind(this);
    }

    private void SetBattleTriggerEnabled(bool enabled)
    {
        if (battleTriggerCollider != null)
        {
            battleTriggerCollider.enabled = enabled;
        }
    }

    private static Transform GetPlayerTransform()
    {
        return GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.transform
            : null;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        Transform player = GetPlayerTransform();
        return other != null && player != null &&
               (other.transform == player || other.transform.IsChildOf(player) || player.IsChildOf(other.transform));
    }

    private BossController PrepareNetworkSummonedBoss(
        Vector3 position,
        Quaternion rotation,
        int currentHP,
        int maxHP,
        bool isDead,
        bool isBattleActive)
    {
        CleanupFinishedBoss();
        activeBoss = PrepareBoss();
        if (activeBoss == null)
        {
            return null;
        }

        prepared = false;
        battleStarted = isBattleActive;
        introPlaying = false;
        SetBattleTriggerEnabled(false);

        activeBoss.TeleportTo(position, rotation);
        activeBoss.Health?.ApplyNetworkState(currentHP, maxHP, isDead);
        activeBoss.ApplyNetworkBattleState(isBattleActive, isDead);

        NetworkBoss networkBoss = activeBoss.GetComponent<NetworkBoss>();
        if (networkBoss == null)
        {
            networkBoss = activeBoss.gameObject.AddComponent<NetworkBoss>();
        }

        networkBoss.ApplyClientDisplayModeNow();
        return activeBoss;
    }

    private static BossSummonArena FindArenaById(string arenaId)
    {
        if (string.IsNullOrWhiteSpace(arenaId))
        {
            return null;
        }

        for (int i = 0; i < arenas.Count; i++)
        {
            BossSummonArena arena = arenas[i];
            if (arena != null && string.Equals(arena.ArenaId, arenaId, System.StringComparison.Ordinal))
            {
                return arena;
            }
        }

        return null;
    }

    private static string BuildHierarchyId(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}

public sealed class BossSummonArenaTrigger : MonoBehaviour
{
    private BossSummonArena owner;

    public void Bind(BossSummonArena arena)
    {
        owner = arena;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.NotifyTriggerEntered(other);
    }
}
