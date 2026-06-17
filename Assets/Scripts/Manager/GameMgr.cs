using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GameMgr : MonoSingleton<GameMgr>
{
    private const string SceneChangedEventName = "SceneChanged";
    private const string StartSceneName = "GameStartScene";
    private const string GameSoundDataKey = "GameSoundDataSO";
    private const string PlayerInitialDataKey = "PlayerInitialData";

    [Header("Runtime")]
    public PlayerStateDriver Player;
    public PlayerData playerData;
    public PlayerInitialDataSO playerInitialData;

    [Header("Audio")]
    public AudioSource BGMAudioSource;
    public AudioSource UIAudioSource;
    public AudioSource GameEffectSource;

    [Header("Startup State")]
    public bool firstEnterGame;
    public bool completeGameInitialze;
    public bool sceneControllerInitiaFinished;

    private GameSoundDataSO gameSoundDataSO;
    public Dictionary<string, GameSoundGroupDataSO> gameSoundDataDic =
        new Dictionary<string, GameSoundGroupDataSO>();
#region 注册服务
    //音效
    private static AudioMgr mAudioMgr;
    public static AudioMgr Audio => mAudioMgr;
    //加载
    private static AssetLoader mAssetLoader;
    public static AssetLoader AssetLoader
    {
        get
        {
            if (mAssetLoader == null)
            {
                mAssetLoader = new AssetLoader();
            }

            return mAssetLoader;
        }
    }
    //相机
    private static CameraMgr mCameraMgr;
    public static CameraMgr cameraMgr => mCameraMgr;
    //输入
    private static InputMgr mInputMgr;
    public static InputMgr input => mInputMgr;
    //时间
    private static TimeScaleMgr mTimeScaleMgr = new TimeScaleMgr();
    public static TimeScaleMgr timeScaleMgr => mTimeScaleMgr;
    //事件
    private static EventMgr mEventMgr = new EventMgr();
    public static EventMgr Event => mEventMgr;
    //UI
    public static UIMgr mUIMgr;
    public static UIMgr UI => mUIMgr;

    private static CursorMgr mCursorMgr;
    public static CursorMgr Cursor => mCursorMgr;

    private static FileMgr mFileMgr;
    public static FileMgr File => mFileMgr;

    //场景
    private static SceneMgr mSceneMgr;
    public static SceneMgr Scene => mSceneMgr;
    //对话
    private static DialogueMgr mDialogue;
    public static DialogueMgr Dialogue => mDialogue;

    private static NPCMgr mNPC;
    public static NPCMgr NPC => mNPC;

    private static PackageMgr mPackage;
    public static PackageMgr Package => mPackage;

    private static ShopMgr mShop;
    public static ShopMgr Shop => mShop;

    private static EquipmentMgr mEquipmentMgr;
    public static EquipmentMgr Equipment => mEquipmentMgr;

    private static TaskManager mTaskMgr;
    public static TaskManager TaskMgr => mTaskMgr;

    private static MessageMgr mMessageMgr;
    public static MessageMgr Message => mMessageMgr;

    private static IconAtlasMgr mIconAtlasMgr;
    public static IconAtlasMgr IconAtlas => mIconAtlasMgr;

    private static CraftMgr mCraftMgr;
    public static CraftMgr Craft => mCraftMgr;

    private static BuildManager mBuildMgr;
    public static BuildManager Build => mBuildMgr;

    public static TimeMgr Time => TimeMgr.Instance;

    private static VFXMgr mVFXMgr;
    public static VFXMgr VFX => mVFXMgr;

    private static BuffMgr mBuffMgr;
    public static BuffMgr Buff => mBuffMgr;

    private static NetworkMgr mNetworkMgr;
    public static NetworkMgr Network => mNetworkMgr;

    private static AccountMgr mAccountMgr;
    public static AccountMgr Account => mAccountMgr;

    private static SocialMgr mSocialMgr;
    public static SocialMgr Social => mSocialMgr;

    private static ChatMgr mChatMgr;
    public static ChatMgr Chat => mChatMgr;

    private static RealtimeMgr mRealtimeMgr;
    public static RealtimeMgr Realtime => mRealtimeMgr;

    private static RedDotMgr mRedDotMgr;
    public static RedDotMgr RedDot => mRedDotMgr;

    private static LuaManager mLuaMgr;
    public static LuaManager Lua => mLuaMgr;
#endregion

    private NightMonsterSpawner nightMonsterSpawner;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
        {
            return;
        }

        InitializeCoreServices();
        InitializeGameplayServices();
        InitializeOnlineServices();

        // Lua 虚拟机：先初始化；入口脚本 main 在配置热更下载完最新 .lua 之后于 Start 里 require，
        // 保证当次启动就跑热更后的逻辑。
        InitializeRuntimeComponents();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (gameObject.GetComponent<NetworkDebugHud>() == null)
        {
            gameObject.AddComponent<NetworkDebugHud>();
        }

        if (gameObject.GetComponent<SocialDebugHud>() == null)
        {
            gameObject.AddComponent<SocialDebugHud>();
        }
#endif

        RegisterGlobalEvents();
    }
#region 初始化服务

    private void InitializeCoreServices()
    {
        mAudioMgr = new AudioMgr(BGMAudioSource, UIAudioSource, GameEffectSource);
        mAssetLoader = new AssetLoader();
        mCameraMgr = new CameraMgr();
        mInputMgr = new InputMgr();
        mTimeScaleMgr = new TimeScaleMgr();
        mEventMgr = new EventMgr();
        mUIMgr = new UIMgr();
        mCursorMgr = new CursorMgr();
        mCursorMgr.Init();
    }

    private void InitializeGameplayServices()
    {
        mTaskMgr = new TaskManager();
        mPackage = new PackageMgr();
        mShop = new ShopMgr();
        mEquipmentMgr = new EquipmentMgr();
        mEquipmentMgr.Init(mPackage);

        mFileMgr = new FileMgr();
        mFileMgr.LoadGameFile();

        mSceneMgr = new SceneMgr();
        mDialogue = new DialogueMgr();
        mNPC = new NPCMgr();
        mDialogue.Init(mTaskMgr);

        mMessageMgr = new MessageMgr();
        mIconAtlasMgr = new IconAtlasMgr();
        mCraftMgr = new CraftMgr();
        mCraftMgr.Init();
        mBuildMgr = new BuildManager();
        mVFXMgr = new VFXMgr();
        mBuffMgr = new BuffMgr();
        mBuffMgr.Init();
        mNetworkMgr = new NetworkMgr();
        mNetworkMgr.Init();
    }

    private void InitializeOnlineServices()
    {
        mAccountMgr = new AccountMgr(new HttpAccountService());
        mAccountMgr.OnProfileChanged += HandleAccountProfileChanged;
        mAccountMgr.Init();

        mRedDotMgr = new RedDotMgr();

        mSocialMgr = new SocialMgr(new HttpSocialService());
        mSocialMgr.Init();

        mChatMgr = new ChatMgr(new HttpChatService());
        mChatMgr.Init();

        mRealtimeMgr = new RealtimeMgr();
        mRealtimeMgr.OnChatMessageReceived += HandleRealtimeChatMessage;
        mRealtimeMgr.OnOnlineInviteReceived += HandleRealtimeOnlineInvite;
        mRealtimeMgr.OnOnlineInviteResultReceived += HandleRealtimeOnlineInviteResult;
        mRealtimeMgr.OnSocialRefreshReceived += HandleRealtimeSocialRefresh;
        mRealtimeMgr.OnSessionKickedReceived += HandleSessionKicked;
        mRealtimeMgr.Init();
    }

    private void InitializeRuntimeComponents()
    {
        mLuaMgr = new LuaManager();
        mLuaMgr.Init();
        if (gameObject.GetComponent<LuaRuntime>() == null)
        {
            gameObject.AddComponent<LuaRuntime>();
        }

        nightMonsterSpawner = gameObject.GetComponent<NightMonsterSpawner>();
        if (nightMonsterSpawner == null)
        {
            nightMonsterSpawner = gameObject.AddComponent<NightMonsterSpawner>();
        }
    }
#endregion
    
#region Others
    private void RegisterGlobalEvents()
    {
        Event.Register(SceneChangedEventName,
            new GameEventOneParam<string>(new GameActionOneParam<string>(OnSceneChanged)));
    }

    private void HandleAccountProfileChanged(AccountProfile profile)
    {
        mFileMgr?.EnsureCurrentGameFileForCurrentAccount();
        mSocialMgr?.SyncCurrentAccountProfileName();
        mChatMgr?.RefreshUnreadCount(false);
        mRealtimeMgr?.Reconnect();
    }

    private void HandleRealtimeChatMessage(ChatMessageData message)
    {
        mChatMgr?.HandleRealtimeMessage(message);
    }

    private void HandleRealtimeOnlineInvite(InviteData invite)
    {
        mSocialMgr?.ReceiveOnlineRequest(invite);
    }

    private void HandleRealtimeOnlineInviteResult(InviteResultData result)
    {
        mSocialMgr?.ReceiveOnlineInviteResult(result);
    }

    private void HandleRealtimeSocialRefresh()
    {
        mSocialMgr?.HandleRealtimeSocialRefresh();
    }

#endregion

#region 异步加载
    /// <summary>
    /// 配置热更新检查：下载有更新的 JSON 并重载各数据库。任何失败都吞掉，保证启动不被网络问题阻塞。
    /// </summary>
    private async Cysharp.Threading.Tasks.UniTask TryRunConfigHotUpdateAsync()
    {
        try
        {
            int updated = await ConfigHotUpdater.CheckAndApplyAsync();
            if (updated > 0)
            {
                // 背包维护着一份独立的物品配置快照(PackageMgr.itemDict)，只重载
                // ItemJsonDatabase 不会刷新它，会导致热更新增/改动的物品在 AddItem、
                // 合成、装备时按 id 找不到 config。这里通过背包统一重载(内部会先 Reload
                // ItemJsonDatabase 再重建快照)。
                if (mPackage != null)
                {
                    mPackage.ReloadItemConfigs();
                }
                else
                {
                    ItemJsonDatabase.Reload();
                }
                RecipeJsonDatabase.Reload();
                MonsterJsonDatabase.Reload();
                Debug.Log($"[GameMgr] 配置热更：已更新 {updated} 个文件并重载数据库（含背包配置快照）。");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameMgr] 配置热更检查失败（忽略，使用包内配置）：{ex.Message}");
        }
    }

    /// <summary>
    /// 资源热更新检查：更新 Addressables 远程目录。任何失败都吞掉，保证启动不被网络问题阻塞。
    /// </summary>
    private async Cysharp.Threading.Tasks.UniTask TryRunResourceHotUpdateAsync()
    {
        try
        {
            int updated = await ResourceHotUpdater.CheckAndUpdateCatalogsAsync();
            if (updated > 0)
            {
                Debug.Log($"[GameMgr] 资源热更：已更新 {updated} 个 Addressables 目录。");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameMgr] 资源热更检查失败（忽略，使用包内资源）：{ex.Message}");
        }
    }
#endregion
    private async void Start()
    {
        UI.ShowBlackScreenImmediate();

        await InitializeAudioAsync();
        await mVFXMgr.Init();

        // 配置热更新：best-effort 从服务器拉取最新配置（服务器不可用时静默跳过，不阻塞启动）。
        await TryRunConfigHotUpdateAsync();

        // 资源热更新：检查并更新 Addressables 远程目录（新图标/模型随后按需从服务器下载）。
        await TryRunResourceHotUpdateAsync();

        // Lua 入口：在配置热更下载完最新 .lua 之后再加载，确保跑的是热更后的逻辑。
        mLuaMgr?.Require("main");

        await InitializePlayerDataAsync();
        await PrepareStartSceneAsync();
    }

    private async UniTask InitializeAudioAsync()
    {
        gameSoundDataSO = await AssetLoader.LoadAsset<GameSoundDataSO>(GameSoundDataKey, _ =>
        {
            Debug.Log("GameSoundDataSO loaded");
        });

        foreach (GameSoundGroupDataSO group in gameSoundDataSO.gameSoundGroups)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.GroupName))
            {
                continue;
            }

            group.Init();
            gameSoundDataDic[group.GroupName] = group;
        }

        mAudioMgr?.Init(gameSoundDataSO);
    }

    private async UniTask InitializePlayerDataAsync()
    {
        playerInitialData = await AssetLoader.LoadAsset<PlayerInitialDataSO>(PlayerInitialDataKey);
        if (playerData == null && playerInitialData != null)
        {
            playerData = playerInitialData.GetPlayerInitialData();
        }

        if (playerData != null && playerInitialData != null)
        {
            playerData.EnsureInitialized(playerInitialData);
            playerData.ClearRuntimeBonuses();
        }

        mEquipmentMgr?.ApplyEquipmentStatsToPlayerData(playerData);
    }

    private async UniTask PrepareStartSceneAsync()
    {
        LogoPanel logoPanel = await UI.ShowPanel<LogoPanel>();
        LoadingSceneRequest startSceneRequest = new LoadingSceneRequest(StartSceneName)
        {
            ActivateOnLoaded = false,
            ShowLoadingPanel = false
        };

        LoadingResult prepareResult = await Scene.PrepareSceneAsync(startSceneRequest);
        if (!prepareResult.Success)
        {
            Debug.LogError($"[GameMgr] Failed to prepare start scene: {prepareResult.ErrorMessage}");
            return;
        }

        if (logoPanel == null)
        {
            Debug.LogError("[GameMgr] Failed to show LogoPanel.");
            UI.HideBlackScreenImmediate();
            return;
        }

        completeGameInitialze = true;
        logoPanel.ShowLogo();
        await logoPanel.WaitUntilFullyShownAsync();
        UI.HideBlackScreenImmediate();
    }

    private void Update()
    {
        mAudioMgr?.Tick(UnityEngine.Time.unscaledDeltaTime);
        mMessageMgr?.Tick(UnityEngine.Time.unscaledDeltaTime);
        mBuildMgr?.Tick();
    }

    private async void OnSceneChanged(string newScene)
    {
        await WaitForSceneControllerReady(newScene);
        mAudioMgr?.PlaySceneBGM(newScene);
    }

    private void HandleSessionKicked(string reason)
    {
        HandleSessionKickedAsync(reason).Forget();
    }

    private async UniTaskVoid HandleSessionKickedAsync(string reason)
    {
        Debug.LogWarning("[GameMgr] Account session kicked: " + reason);

        if (mNetworkMgr != null && mNetworkMgr.IsSessionActive)
        {
            await mNetworkMgr.DisconnectToSinglePlayerAsync();
        }

        mAccountMgr?.ClearLocalAccount();

        if (mUIMgr != null)
        {
            await mUIMgr.ShowPanel<LoginPanel>();
        }
    }

    private async UniTask WaitForSceneControllerReady(string newScene)
    {
        float timeout = 10f;
        float timer = 0f;

        while (Scene != null && !Scene.HasSceneController(newScene))
        {
            await UniTask.Yield();
            timer += UnityEngine.Time.unscaledDeltaTime;

            if (timer > timeout)
            {
                Debug.LogError($"[GameMgr] Wait for sceneControllerInitiaFinished TIMEOUT! Scene: {newScene}. Elapsed: {timer}s");
                return;
            }
        }
    }
}
