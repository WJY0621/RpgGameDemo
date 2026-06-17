using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class SceneControllerBase : MonoBehaviour
{
    public string sceneName;
    protected bool isGameScene;

    protected virtual void Awake()
    {
        SceneControllerAttribute attribute = GetType().GetCustomAttribute<SceneControllerAttribute>();
        if (attribute == null)
        {
            Debug.LogError($"[SceneControllerBase] Missing SceneControllerAttribute on {GetType().Name}");
            return;
        }

        sceneName = attribute.sceneName;
        isGameScene = attribute.isGameScene;
        GameMgr.Scene.Register(sceneName, this);
    }

    protected virtual void Update()
    {
    }

    public virtual void OnSceneEnter()
    {
    }

    public virtual void OnSceneExit()
    {
    }

    public virtual UniTask OnScenePreloadAsync()
    {
        return UniTask.CompletedTask;
    }

    public virtual UniTask OnSceneEnterAsync()
    {
        OnSceneEnter();
        return UniTask.CompletedTask;
    }

    public virtual UniTask OnSceneExitAsync()
    {
        OnSceneExit();
        return UniTask.CompletedTask;
    }

    protected virtual void OnDestroy()
    {
        if (GameMgr.Scene != null)
        {
            GameMgr.Scene.UnRegister(sceneName);
        }
    }
}
