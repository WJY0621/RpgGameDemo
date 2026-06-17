using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneMgr
{
    private readonly Dictionary<string, SceneControllerBase> sceneControllerDic = new Dictionary<string, SceneControllerBase>();
    private readonly LoadingMgr loadingMgr;

    public SceneControllerBase currentSceneController;
    public bool IsSceneTransitioning { get; private set; }

    public SceneMgr()
    {
        loadingMgr = new LoadingMgr(this);
    }

    public void Register(string sceneName, SceneControllerBase sceneController)
    {
        if (string.IsNullOrEmpty(sceneName) || sceneController == null)
        {
            return;
        }

        sceneControllerDic[sceneName] = sceneController;
        currentSceneController = sceneController;
        GameMgr.Instance.sceneControllerInitiaFinished = true;
    }

    public void UnRegister(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (sceneControllerDic.ContainsKey(sceneName))
        {
            if (currentSceneController == sceneControllerDic[sceneName])
            {
                currentSceneController = null;
            }

            sceneControllerDic.Remove(sceneName);
        }
    }

    public void OnSceneEnter(string sceneName)
    {
        if (sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) && controller != null)
        {
            controller.OnSceneEnter();
            return;
        }

        string keys = string.Join(", ", sceneControllerDic.Keys);
        Debug.LogError($"[SceneMgr] No SceneController registered for: {sceneName}. Available keys: [{keys}]");
    }

    public async UniTask OnScenePreloadAsync(string sceneName)
    {
        if (sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) && controller != null)
        {
            await controller.OnScenePreloadAsync();
            return;
        }

        string keys = string.Join(", ", sceneControllerDic.Keys);
        Debug.LogError($"[SceneMgr] No SceneController registered for preload: {sceneName}. Available keys: [{keys}]");
    }

    public async UniTask OnSceneEnterAsync(string sceneName)
    {
        if (sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) && controller != null)
        {
            await controller.OnSceneEnterAsync();
            return;
        }

        string keys = string.Join(", ", sceneControllerDic.Keys);
        Debug.LogError($"[SceneMgr] No SceneController registered for enter: {sceneName}. Available keys: [{keys}]");
    }

    public void OnSceneExit(string sceneName)
    {
        if (sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) && controller != null)
        {
            controller.OnSceneExit();
        }
    }

    public async UniTask OnSceneExitAsync(string sceneName)
    {
        if (sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) && controller != null)
        {
            await controller.OnSceneExitAsync();
        }
    }

    public UniTask<LoadingResult> LoadSceneAsync(string sceneName)
    {
        return loadingMgr.LoadSceneAsync(sceneName);
    }

    public UniTask<LoadingResult> LoadSceneAsync(LoadingSceneRequest request)
    {
        return loadingMgr.LoadSceneAsync(request);
    }

    public UniTask<LoadingResult> PrepareSceneAsync(LoadingSceneRequest request)
    {
        if (request != null)
        {
            request.ActivateOnLoaded = false;
        }

        return loadingMgr.PrepareSceneAsync(request);
    }

    public UniTask<LoadingResult> ActivatePreparedSceneAsync()
    {
        return loadingMgr.ActivatePreparedSceneAsync();
    }

    public UniTask<LoadingResult> CancelPreparedSceneAsync()
    {
        return loadingMgr.CancelPreparedSceneAsync();
    }

    public bool HasSceneController(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
               sceneControllerDic.TryGetValue(sceneName, out SceneControllerBase controller) &&
               controller != null;
    }

    public bool TryGetSceneController(string sceneName, out SceneControllerBase controller)
    {
        if (!string.IsNullOrEmpty(sceneName) &&
            sceneControllerDic.TryGetValue(sceneName, out controller) &&
            controller != null)
        {
            return true;
        }

        controller = null;
        return false;
    }

    public bool TryBeginSceneTransition(string sceneName)
    {
        if (IsSceneTransitioning)
        {
            Debug.LogWarning($"[SceneMgr] Scene transition already in progress. Ignore request: {sceneName}");
            return false;
        }

        IsSceneTransitioning = true;
        return true;
    }

    public void CompleteSceneTransition()
    {
        IsSceneTransitioning = false;
    }
}
