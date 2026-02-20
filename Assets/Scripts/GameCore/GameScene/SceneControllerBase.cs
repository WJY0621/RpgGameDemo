using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public class SceneControllerBase : MonoBehaviour
{
    public string sceneName;
    private bool isGameScene;
    private PlayerMainPanel mainInfoPanel;
    private bool loadToAnotherScene;

    protected virtual async void Awake()
    {
        sceneName = this.GetType().GetCustomAttribute<SceneControllerAttribute>().sceneName;
        isGameScene = this.GetType().GetCustomAttribute<SceneControllerAttribute>().isGameScene;
        
        // GameMgr.Scene.Register(sceneName, this); // 移到最后，确保初始化完成后再注册
        if (isGameScene)
        {
            try
            {
                //调整输入模式为玩家输入模式
                mainInfoPanel = await GameMgr.UI.ShowPanel<PlayerMainPanel>();
                GameObject playerPrefab = await GameMgr.AssetLoader.LoadPrefab("Player");
                
                if (playerPrefab != null)
                {
                    GameObject player = Instantiate(playerPrefab);
                    player.name = "Player";
                }
                else
                {
                    Debug.LogError("[SceneControllerBase] Failed to load Player prefab!");
                }
                
                GameMgr.input.EnablePlayerActionMap();
                GameMgr.cameraMgr.UpdateCamera();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SceneControllerBase] Error during async init: {e}");
                // 即使出错也要尝试注册，否则 GameMgr 会一直等待
            }
        }
        else
        {
            //调整输入模式为ui模式
            GameMgr.input.EnableUIActionMap();
        }
        
        // 关键修复：所有异步初始化完成后再注册
        // 这样 GameMgr.OnSceneChanged 中的 WaitUntil 才会等到 Player 加载完毕后继续
        GameMgr.Scene.Register(sceneName, this);
    }

    protected virtual async void Update()
    {
        if(loadToAnotherScene) return;
        if (isGameScene)
        {
            if (!GameMgr.UI.IsShow<PlayerMainPanel>())
            {
                
            }
        }
    }

    public virtual void OnSceneEnter()
    {
        //当场景启用时做什么 子类中具体实现
        //GameMgr.File.SaveGameFile();
    }

    /// <summary>
    /// 当场景退出时调用
    /// </summary>
    public virtual void OnSceneExit()
    {
        //当离开场景后做什么
        //GameMgr.File.UpdateFileData();
    }

    private void OnDestroy()
    {
        GameMgr.Scene.UnRegister(sceneName);
    }
}
