using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneMgr
{
    private Dictionary<string, SceneControllerBase> SceneControllerDic = new Dictionary<string, SceneControllerBase>();
    public SceneControllerBase currentSceneController;

    public void Register(string sceneName, SceneControllerBase sceneController)
    {
        SceneControllerDic[sceneName] = sceneController;
        GameMgr.Instance.sceneControllerInitiaFinished = true;
        currentSceneController = sceneController;
    }

    public void UnRegister(string sceneName)
    {
        SceneControllerDic[sceneName] = null;
    }

    /// <summary>
    /// 封装场景进入时调用方法
    /// </summary>
    /// <param name="sceneName"></param>
    public void OnSceneEnter(string sceneName)
    {
        if (SceneControllerDic.ContainsKey(sceneName))
        {
            if (SceneControllerDic[sceneName] != null)
            {
                SceneControllerDic[sceneName].OnSceneEnter();
            }
            else
            {
                Debug.LogError($"[SceneMgr] SceneController for {sceneName} is null!");
            }
        }
        else
        {
            string keys = string.Join(", ", SceneControllerDic.Keys);
            Debug.LogError($"[SceneMgr] No SceneController registered for: {sceneName}. Available keys: [{keys}]");
        }
    }

    /// <summary>
    /// 封装场景退出时调用方法
    /// </summary>
    /// <param name="sceneName"></param>
    public void OnSceneExit(string sceneName)
    {
        if (SceneControllerDic.ContainsKey(sceneName))
        {
            SceneControllerDic[sceneName]?.OnSceneExit();
        }
    }

    /// <summary>
    /// 统一的场景切换接口
    /// </summary>
    /// <param name="sceneName">目标场景名称</param>
    /// <returns></returns>
    public async Cysharp.Threading.Tasks.UniTask LoadSceneAsync(string sceneName)
    {
        // 1. (可选) 打开 Loading 界面
        // await GameMgr.UI.ShowPanel<LoadingPanel>();

        // 2. 调用 AssetLoader 异步加载场景
        // activateOnLoad 为 false，所以加载完不会立刻跳转
        await GameMgr.AssetLoader.loadScene(sceneName, 
            (progress) => 
            {
                // TODO: 更新 Loading 界面的进度条
                // Debug.Log($"Loading {sceneName}: {progress * 100}%");
            }, 
            (sceneInstance) => 
            {
                // 加载完成的回调
            }
        );

        // 3. 允许 GameMgr 在 Update 中激活场景
        // GameMgr.Update 会检测此变量，调用 OnSceneExit 并激活新场景
        GameMgr.Instance.readyToActiveLoadedScene = true;

        // 4. (可选) 等待场景切换事件完成后关闭 Loading 界面
        // 这部分逻辑也可以监听 EventMgr 的 "SceneChanged" 事件来处理
    }
}
