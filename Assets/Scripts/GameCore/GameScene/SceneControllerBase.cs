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
        GameMgr.Scene.Register(sceneName, this);
        if (isGameScene)
        {
            //调整输入模式为玩家输入模式
            mainInfoPanel = await GameMgr.UI.ShowPanel<PlayerMainPanel>();
            GameObject player = await GameMgr.AssetLoader.LoadPrefab("Player");
            GameMgr.input.EnablePlayerActionMap();
            GameMgr.cameraMgr.UpdateCamera();
        }
        else
        {
            //调整输入模式为ui模式
            GameMgr.input.EnableUIActionMap();
        }
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
