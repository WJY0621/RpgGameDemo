using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameMgr : MonoSingleton<GameMgr>
{
    public PlayerStateDriver Player;


    #region Manager实例
    #region 音效部分
    //音效数据
    private GameSoundDataSO gameSoundDataSO;
    public Dictionary<string, GameSoundGroupDataSO> gameSoundDataDic = new Dictionary<string, GameSoundGroupDataSO>();

    //游戏音效播放器
    public static AudioMgr Audio => mAudioMgr;
    private static AudioMgr mAudioMgr;
    [Header("Audio:需要配置")]
    public AudioSource BGMAudioSource;
    public AudioSource UIAudioSource;
    public AudioSource GameEffectSource;
    #endregion

    #region 资源加载
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
    #endregion

    #region 相机管理器
    public static CameraMgr cameraMgr => mCameraMgr;
    private static CameraMgr mCameraMgr;
    #endregion

    #region 输入管理器
    //输入管理
    public static InputMgr input => mInputMgr;
    private static InputMgr mInputMgr;
    #endregion

    #region 时间缩放管理器
    public static TimeScaleMgr timeScaleMgr => mTimeScaleMgr;
    private static TimeScaleMgr mTimeScaleMgr = new TimeScaleMgr();
    #endregion

    #region 事件管理器
    public static EventMgr Event => mEventMgr;
    private static EventMgr mEventMgr = new EventMgr();
    #endregion

    #region UI管理器
    public static UIMgr UI => mUIMgr;
    public static UIMgr mUIMgr;
    #endregion

    #region 鼠标管理器
    public static CursorMgr Cursor => mCursorMgr;
    private static CursorMgr mCursorMgr;
    #endregion

    #region 存档管理
    public bool firstEnterGame;

    public static FileMgr File => mFileMgr;
    private static FileMgr mFileMgr;
    #endregion

    #region 游戏场景管理
    //场景相关数据
    //游戏是否初始化完成
    public bool completeGameInitialze;
    //场景是否初始化完成
    public bool sceneControllerInitiaFinished;
    //是否准备激活场景
    public bool readyToActiveLoadedScene;
    //当前所在的场景名字
    private string previousSceneName;
    //正在加载的场景
    public SceneInstance LoadedScene
    {
        get
        {
            return loadedScene;
        }
        set
        {
            previousSceneName = loadedScene.Scene.name;
            if (string.IsNullOrEmpty(previousSceneName))
            {
                previousSceneName = SceneManager.GetActiveScene().name;
            }
            loadedScene = value;
        }
    }
    private SceneInstance loadedScene;
    //场景管理器
    public static SceneMgr Scene => mSceneMgr;
    private static SceneMgr mSceneMgr;
    #endregion

    #region 对话管理
    public static DialogueMgr Dialogue => mDialogue;
    private static DialogueMgr mDialogue;
    #endregion

    #region 背包管理
    public static PackageMgr Package => mPackage;
    private static PackageMgr mPackage;
    #endregion

    #region 时间管理
    public static TimeMgr Time => TimeMgr.Instance;
    #endregion

    #endregion

    #region 数据
    public PlayerData playerData;
    public PlayerInitialDataSO playerInitialData;
    #endregion

    //public GameObject playerObj;
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return; // 关键修复：如果是重复的单例，直接返回，避免覆盖静态变量和重复注册事件

        //初始化音效管理器
        mAudioMgr = new AudioMgr(BGMAudioSource, UIAudioSource, GameEffectSource);
        //初始化资源加载器
        mAssetLoader = new AssetLoader();
        //初始化相机管理器
        mCameraMgr = new CameraMgr();
        //初始化输入管理器
        mInputMgr = new InputMgr();
        //初始化时间缩放管理器
        mTimeScaleMgr = new TimeScaleMgr();
        //初始化事件管理器
        mEventMgr = new EventMgr();
        //初始化UI管理器
        mUIMgr = new UIMgr();
        //初始化鼠标管理器
        mCursorMgr = new CursorMgr();
        mCursorMgr.Init();
        //初始化存档管理
        mFileMgr = new FileMgr();
        // 尝试加载存档（如果有）
        mFileMgr.LoadGameFile();
        //初始化场景管理
        mSceneMgr = new SceneMgr();
        //对话管理器
        mDialogue = new DialogueMgr();
        //背包管理器
        mPackage = new PackageMgr();

        //注册事件
        Event.Register("SceneChanged", new GameEventOneParam<string>(new GameActionOneParam<string>(OnSceneChanged)));
    }

    private async void Start()
    {
        //加载音效数据
        gameSoundDataSO = await AssetLoader.LoadAsset<GameSoundDataSO>("GameSoundDataSO", (a) =>
        {
            Debug.Log("GameSoundDataSO 加载完成");
        });
        foreach (var group in gameSoundDataSO.gameSoundGroups)
        {
            group.Init();
            gameSoundDataDic.Add(group.GroupName, group);
        }
        //input.EnablePlayerActionMap();
        //加载玩家初始数据
        playerInitialData = await AssetLoader.LoadAsset<PlayerInitialDataSO>("PlayerInitialData");
        //加载游戏开始场景
        LogoPanel logoPanel = await UI.ShowPanel<LogoPanel>();
        await GameMgr.AssetLoader.loadScene("GameStartScene", null,
            scene =>
            {
                completeGameInitialze = true;
                logoPanel.ShowLogo();
            }
        );
        //playerObj.SetActive(true);
    }

    private void Update()
    {
        if (readyToActiveLoadedScene)
        {
            readyToActiveLoadedScene = false; // 立即重置，防止多次调用
            sceneControllerInitiaFinished = false; // 关键修复：在激活新场景前重置初始化完成标记，防止旧场景的标记残留导致误判
            
            // 调用场景管理器的退出方法
            if (!string.IsNullOrEmpty(previousSceneName))
            {
                Scene.OnSceneExit(previousSceneName);
            }
            else
            {
                // 如果 previousSceneName 为空，尝试获取当前场景
                Scene.OnSceneExit(SceneManager.GetActiveScene().name);
            }

            AsyncOperation handle = LoadedScene.ActivateAsync();
            handle.completed += (ao) =>
            {
                if (ao.isDone)
                {
                    // 场景激活完成后，更新 previousSceneName 为新场景，以便下次使用
                    // 但这里要注意，Event.Broadcast 之后，OnSceneChanged 会被调用，然后进入新场景的 OnSceneEnter
                    Event.Broadcast("SceneChanged", new GameEventParameter<string>(LoadedScene.Scene.name));
                }
            };
        }
        //Audio.ProcessBGM();
    }

    private async void OnSceneChanged(string newScene)
    {
        // 增加超时保护，防止无限等待
        float timeout = 30f; // 增加到 30 秒超时
        float timer = 0f;
        while (!sceneControllerInitiaFinished)
        {
            await UniTask.Yield();
            timer += UnityEngine.Time.deltaTime;
            if (timer > timeout)
            {
                Debug.LogError($"[GameMgr] Wait for sceneControllerInitiaFinished TIMEOUT! Scene: {newScene}. Elapsed: {timer}s");
                return;
            }
        }
        
        Scene.OnSceneEnter(newScene);
        
        sceneControllerInitiaFinished = false;
    }
}
