using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家死亡/复活控制器。订阅 <see cref="PlayerHealth.OnDied"/>，
/// 在玩家血量归零时冻结控制、播放 Dead 动画、关闭所有面板并显示 <see cref="PlayerDeadPanel"/>。
/// 复活时把玩家传送到重生点（床 -> 初始点）、回满血并恢复控制与主面板 HUD。
/// 该组件由 <see cref="PlayerStateDriver"/> 在本地游玩玩家上自动挂载。
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathController : MonoBehaviour
{
    [Header("动画状态名")]
    [SerializeField] private string deadAnimStateName = "Dead";
    [SerializeField] private string reviveAnimStateName = "Idle";
    [SerializeField, Min(0f)] private float deadAnimBlendDuration = 0.1f;

    private PlayerHealth health;
    private PlayerStateDriver driver;
    private bool isDeathHandled;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        driver = GetComponent<PlayerStateDriver>();
    }

    private void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
        }

        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied(PlayerHealth source)
    {
        if (isDeathHandled)
        {
            return;
        }

        isDeathHandled = true;
        HandleDeathAsync().Forget();
    }

    private async UniTaskVoid HandleDeathAsync()
    {
        // 关闭本地控制：PlayerStateDriver.Update 会直接 return，
        // 同时停掉移动、状态机推进和动画参数刷新，Dead 动画才能稳定播放。
        if (driver != null)
        {
            driver.SetLocalControlEnabled(false);
        }

        PlayAnimatorState(deadAnimStateName);

        // 释放鼠标并切到 UI 输入，便于点击复活按钮
        GameMgr.Cursor?.SetCursorState(true);
        GameMgr.input?.EnableUIActionMap();

        // 关闭其它所有面板，只保留死亡面板
        PlayerDeadPanel panel = await GameMgr.UI.ShowPanel<PlayerDeadPanel>();
        GameMgr.UI.DestroyAllPanelsExcept<PlayerDeadPanel>();

        if (panel != null)
        {
            panel.Bind(this);
        }
    }

    /// <summary>
    /// 由 <see cref="PlayerDeadPanel"/> 的 ContinueButton 调用，执行复活。
    /// </summary>
    public async UniTask ReviveAsync()
    {
        if (!isDeathHandled)
        {
            return;
        }

        TeleportToRespawnPoint();

        // 回满血（同时把 isDead 置回 false 并刷新 HUD 数据）
        if (health != null)
        {
            health.SetCurrentHP(health.MaxHP);
        }

        PlayAnimatorState(reviveAnimStateName);

        if (driver != null)
        {
            driver.SetLocalControlEnabled(true);
        }

        isDeathHandled = false;

        // 恢复游戏输入并锁定鼠标
        GameMgr.Cursor?.SetCursorState(false);
        GameMgr.input?.EnablePlayerActionMap();

        // 关闭死亡面板，恢复主面板 HUD
        GameMgr.UI.HidePanel<PlayerDeadPanel>();
        await GameMgr.UI.ShowPanel<PlayerMainPanel>();
    }

    private void TeleportToRespawnPoint()
    {
        if (!TryResolveRespawnPoint(out Vector3 position, out Quaternion rotation))
        {
            return;
        }

        Transform playerTransform = driver != null ? driver.transform : transform;
        Transform rigRoot = playerTransform.parent != null ? playerTransform.parent : playerTransform;

        // 移动前关闭 CharacterController，避免它把 transform 拉回，
        // 与 GameSceneController.ApplySavedTransformAsync 保持一致。
        CharacterController cc = GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null)
        {
            cc.enabled = false;
        }

        if (rigRoot != playerTransform)
        {
            Vector3 delta = position - playerTransform.position;
            rigRoot.position += delta;
            playerTransform.rotation = rotation;
        }
        else
        {
            playerTransform.SetPositionAndRotation(position, rotation);
        }

        if (cc != null && ccWasEnabled)
        {
            cc.enabled = true;
        }
    }

    /// <summary>
    /// 解析重生点：建造过床则用床的位置，否则回退到初始点。
    /// </summary>
    private bool TryResolveRespawnPoint(out Vector3 position, out Quaternion rotation)
    {
        GameFile file = GameMgr.File != null ? GameMgr.File.CurrentGameFile : null;
        if (file != null)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (file.TryGetRespawnPoint(sceneName, out position, out rotation))
            {
                return true;
            }
        }

        PlayerInitialDataSO initial = GameMgr.Instance != null ? GameMgr.Instance.playerInitialData : null;
        if (initial != null)
        {
            position = initial.initialPosition;
            rotation = initial.initialRotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    private void PlayAnimatorState(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        Animator animator = ResolveAnimator();
        if (!PlayerStateDriver.HasPlayableAnimator(animator))
        {
            return;
        }

        animator.speed = 1f;
        if (!animator.HasState(0, Animator.StringToHash(stateName)))
        {
            Debug.LogWarning($"[PlayerDeathController] Animator state not found: {stateName}", this);
            return;
        }

        animator.CrossFade(stateName, deadAnimBlendDuration, 0, 0f);
    }

    private Animator ResolveAnimator()
    {
        if (driver != null && driver.ctx != null && driver.ctx.anim != null)
        {
            return driver.ctx.anim;
        }

        return GetComponentInChildren<Animator>(true);
    }
}
