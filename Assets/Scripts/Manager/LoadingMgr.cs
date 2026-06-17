using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public enum LoadingStage
{
    None,
    ShowLoading,
    CheckRemoteCatalogs,
    ExitOldScene,
    PreloadAssets,
    LoadScene,
    ActivateScene,
    WaitSceneReady,
    EnterNewScene,
    Cleanup,
    HideLoading,
    Completed,
    Failed
}

public readonly struct LoadingProgress
{
    public readonly LoadingStage Stage;
    public readonly float Progress;
    public readonly string Message;

    public LoadingProgress(LoadingStage stage, float progress, string message)
    {
        Stage = stage;
        Progress = Mathf.Clamp01(progress);
        Message = message ?? string.Empty;
    }
}

public sealed class LoadingSceneRequest
{
    public string SceneName;
    public bool ShowLoadingPanel = true;
    public bool ActivateOnLoaded = true;
    public bool CleanupUnusedAssets = true;
    public bool CheckRemoteCatalogUpdates;
    public bool RecoverToPreviousSceneOnFailure = true;
    public string FailureFallbackSceneName;
    public int RetryCount = 1;
    public float SceneControllerReadyTimeout = 10f;
    public float FailureMessageDuration = 1.5f;
    public readonly List<string> PreloadAssetKeys = new List<string>();
    public readonly List<string> PreloadLabels = new List<string>();
    public Action<LoadingProgress> ProgressChanged;

    public LoadingSceneRequest(string sceneName)
    {
        SceneName = sceneName;
    }
}

public sealed class LoadingResult
{
    public bool Success;
    public bool Cancelled;
    public string SceneName;
    public string ErrorMessage;
    public Exception Exception;
}

public sealed class LoadingMgr
{
    private readonly SceneMgr sceneMgr;
    private LoadingSceneRequest preparedRequest;
    private SceneInstance preparedScene;
    private string preparedPreviousSceneName;
    private bool hasPreparedScene;
    private LoadingPanel activeLoadingPanel;

    public LoadingMgr(SceneMgr sceneMgr)
    {
        this.sceneMgr = sceneMgr;
    }

    public UniTask<LoadingResult> LoadSceneAsync(string sceneName)
    {
        return LoadSceneAsync(new LoadingSceneRequest(sceneName));
    }

    public async UniTask<LoadingResult> LoadSceneAsync(LoadingSceneRequest request)
    {
        if (request == null)
        {
            return Fail(string.Empty, "Loading request is null.", null);
        }

        request.ActivateOnLoaded = true;

        LoadingResult result = await PrepareSceneAsync(request);
        if (!result.Success)
        {
            return result;
        }

        return await ActivatePreparedSceneAsync();
    }

    public async UniTask<LoadingResult> PrepareSceneAsync(LoadingSceneRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SceneName))
        {
            return Fail(request?.SceneName, "Target scene name is empty.", null);
        }

        if (!sceneMgr.TryBeginSceneTransition(request.SceneName))
        {
            return Fail(request.SceneName, $"Scene transition already in progress: {request.SceneName}", null);
        }

        if (request.ShowLoadingPanel)
        {
            await ShowLoadingPanelAsync();
        }

        int attempts = Mathf.Max(1, request.RetryCount + 1);
        LoadingResult lastResult = null;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                Report(request, LoadingStage.ShowLoading, 0.02f, "Show loading.");

                if (request.CheckRemoteCatalogUpdates)
                {
                    await CheckRemoteCatalogUpdatesAsync(request);
                }

                await PreloadAssetsAsync(request);
                SceneInstance sceneInstance = await LoadAddressableSceneAsync(request);

                preparedRequest = request;
                preparedScene = sceneInstance;
                preparedPreviousSceneName = SceneManager.GetActiveScene().name;
                hasPreparedScene = true;

                if (!request.ActivateOnLoaded)
                {
                    Report(request, LoadingStage.Completed, 0.9f, "Scene prepared.");
                }

                return new LoadingResult { Success = true, SceneName = request.SceneName };
            }
            catch (Exception e)
            {
                lastResult = Fail(request.SceneName, $"Failed to prepare scene. Attempt {attempt}/{attempts}.", e);
                Debug.LogError($"[LoadingMgr] {lastResult.ErrorMessage}\n{e}");

                if (attempt < attempts)
                {
                    await UniTask.DelayFrame(1);
                    continue;
                }

                sceneMgr.CompleteSceneTransition();
                Report(request, LoadingStage.Failed, 1f, "Loading failed.");
                await ShowFailureAsync(request, lastResult);
                await HideLoadingPanelAsync(request);
            }
        }

        return lastResult ?? Fail(request.SceneName, "Failed to prepare scene.", null);
    }

    public async UniTask<LoadingResult> ActivatePreparedSceneAsync()
    {
        if (!hasPreparedScene || preparedRequest == null)
        {
            return Fail(string.Empty, "No prepared scene to activate.", null);
        }

        LoadingSceneRequest request = preparedRequest;

        try
        {
            await ShowBlackScreenAsync(request);

            Report(request, LoadingStage.ExitOldScene, 0.62f, "Exit old scene.");
            if (request.ShowLoadingPanel)
            {
                GameMgr.UI?.DestroyAllPanelsExcept<LoadingPanel>();
            }

            await sceneMgr.OnSceneExitAsync(preparedPreviousSceneName);
            await DestroyCurrentPlayerAsync();

            GameMgr.Instance.sceneControllerInitiaFinished = false;

            Report(request, LoadingStage.ActivateScene, 0.75f, "Activate scene.");
            AsyncOperation activateOperation = preparedScene.ActivateAsync();
            await activateOperation;

            await UniTask.Yield();
            await UniTask.Yield();

            string newSceneName = preparedScene.Scene.name;
            Report(request, LoadingStage.WaitSceneReady, 0.88f, "Wait scene controller.");
            GameMgr.Event.Broadcast("SceneChanged", new GameEventParameter<string>(newSceneName));
            bool sceneControllerReady = await WaitForSceneControllerReadyAsync(newSceneName, request.SceneControllerReadyTimeout);
            if (!sceneControllerReady)
            {
                throw new TimeoutException($"SceneController not registered for scene: {newSceneName}");
            }

            await sceneMgr.OnScenePreloadAsync(newSceneName);

            Report(request, LoadingStage.EnterNewScene, 0.94f, "Enter scene.");
            await sceneMgr.OnSceneEnterAsync(newSceneName);

            if (request.CleanupUnusedAssets)
            {
                Report(request, LoadingStage.Cleanup, 0.98f, "Cleanup unused assets.");
                await CleanupSceneMemoryAsync();
            }

            Report(request, LoadingStage.HideLoading, 0.995f, "Hide loading.");
            await HideLoadingPanelAsync(request);
            Report(request, LoadingStage.Completed, 1f, "Scene loaded.");

            return new LoadingResult { Success = true, SceneName = newSceneName };
        }
        catch (Exception e)
        {
            Report(request, LoadingStage.Failed, 1f, "Scene activation failed.");
            Debug.LogError($"[LoadingMgr] Scene activation failed: {request.SceneName}\n{e}");
            LoadingResult failureResult = Fail(request.SceneName, "Scene activation failed.", e);
            await ShowFailureAsync(request, failureResult);
            await TryUnloadPreparedSceneAsync();
            await TryRecoverToFallbackSceneAsync(request);
            await HideLoadingPanelAsync(request);
            return failureResult;
        }
        finally
        {
            preparedRequest = null;
            hasPreparedScene = false;
            sceneMgr.CompleteSceneTransition();
        }
    }

    public async UniTask<LoadingResult> CancelPreparedSceneAsync()
    {
        if (!hasPreparedScene || preparedRequest == null)
        {
            return new LoadingResult
            {
                Success = true,
                Cancelled = true,
                SceneName = string.Empty,
                ErrorMessage = "No prepared scene to cancel."
            };
        }

        LoadingSceneRequest request = preparedRequest;
        string sceneName = request.SceneName;

        await TryUnloadPreparedSceneAsync();
        preparedRequest = null;
        hasPreparedScene = false;
        sceneMgr.CompleteSceneTransition();
        await HideLoadingPanelAsync(request);

        return new LoadingResult
        {
            Success = true,
            Cancelled = true,
            SceneName = sceneName
        };
    }

    private async UniTask CheckRemoteCatalogUpdatesAsync(LoadingSceneRequest request)
    {
        Report(request, LoadingStage.CheckRemoteCatalogs, 0.08f, "Check remote catalogs.");

        AsyncOperationHandle<List<string>> checkHandle = Addressables.CheckForCatalogUpdates(false);
        await checkHandle.ToUniTask();

        List<string> catalogs = checkHandle.Status == AsyncOperationStatus.Succeeded ? checkHandle.Result : null;
        Addressables.Release(checkHandle);

        if (catalogs == null || catalogs.Count == 0)
        {
            return;
        }

        AsyncOperationHandle<List<IResourceLocator>> updateHandle =
            Addressables.UpdateCatalogs(catalogs, false);
        await updateHandle.ToUniTask();
        Addressables.Release(updateHandle);
    }

    private async UniTask PreloadAssetsAsync(LoadingSceneRequest request)
    {
        Report(request, LoadingStage.PreloadAssets, 0.12f, "Preload assets.");

        int totalCount = request.PreloadAssetKeys.Count + request.PreloadLabels.Count;
        if (totalCount == 0)
        {
            return;
        }

        int completedCount = 0;

        foreach (string assetKey in request.PreloadAssetKeys)
        {
            if (!string.IsNullOrWhiteSpace(assetKey))
            {
                await GameMgr.AssetLoader.LoadAsset<UnityEngine.Object>(assetKey);
            }

            completedCount++;
            ReportPreloadProgress(request, completedCount, totalCount);
        }

        foreach (string label in request.PreloadLabels)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(label, false);
                await handle.ToUniTask();
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogWarning($"[LoadingMgr] Failed to download dependencies for label: {label}");
                }

                Addressables.Release(handle);
            }

            completedCount++;
            ReportPreloadProgress(request, completedCount, totalCount);
        }
    }

    private async UniTask<SceneInstance> LoadAddressableSceneAsync(LoadingSceneRequest request)
    {
        Report(request, LoadingStage.LoadScene, 0.35f, "Load scene.");

        AsyncOperationHandle<SceneInstance> handle =
            Addressables.LoadSceneAsync(request.SceneName, LoadSceneMode.Single, false);

        while (!handle.IsDone)
        {
            float sceneProgress = Mathf.Lerp(0.35f, 0.6f, handle.PercentComplete);
            Report(request, LoadingStage.LoadScene, sceneProgress, "Load scene.");
            await UniTask.Yield();
        }

        await handle.ToUniTask();

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            throw new InvalidOperationException($"Addressables failed to load scene: {request.SceneName}");
        }

        Report(request, LoadingStage.LoadScene, 0.6f, "Scene loaded.");
        return handle.Result;
    }

    private async UniTask DestroyCurrentPlayerAsync()
    {
        if (GameMgr.Instance.Player == null)
        {
            return;
        }

        Transform playerRoot = GameMgr.Instance.Player.transform.parent != null
            ? GameMgr.Instance.Player.transform.parent
            : GameMgr.Instance.Player.transform;

        UnityEngine.Object.Destroy(playerRoot.gameObject);
        GameMgr.Instance.Player = null;
        await UniTask.Yield();
    }

    private async UniTask<bool> WaitForSceneControllerReadyAsync(string sceneName, float timeout)
    {
        float timer = 0f;
        float safeTimeout = Mathf.Max(1f, timeout);

        while (!sceneMgr.HasSceneController(sceneName))
        {
            await UniTask.Yield();
            timer += Time.unscaledDeltaTime;

            if (timer > safeTimeout)
            {
                Debug.LogError($"[LoadingMgr] Wait scene controller timeout. Scene: {sceneName}. Elapsed: {timer}s");
                return false;
            }
        }

        return true;
    }

    private async UniTask CleanupSceneMemoryAsync()
    {
        await UniTask.DelayFrame(2);
        AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
        await unloadOperation;
    }

    private async UniTask ShowLoadingPanelAsync()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        activeLoadingPanel = await GameMgr.UI.ShowPanel<LoadingPanel>();
        if (activeLoadingPanel != null)
        {
            activeLoadingPanel.SetProgress(0f);
            await activeLoadingPanel.WaitUntilFullyShownAsync();
        }
    }

    private async UniTask ShowBlackScreenAsync(LoadingSceneRequest request)
    {
        if (request.ShowLoadingPanel || GameMgr.UI == null)
        {
            return;
        }

        GameMgr.UI.ShowBlackScreenImmediate();
        await UniTask.Yield();
    }

    private async UniTask HideLoadingPanelAsync(LoadingSceneRequest request)
    {
        if (!request.ShowLoadingPanel && GameMgr.UI != null)
        {
            await GameMgr.UI.FadeFromBlackAsync();
            activeLoadingPanel = null;
            return;
        }

        if (!request.ShowLoadingPanel || activeLoadingPanel == null || GameMgr.UI == null)
        {
            activeLoadingPanel = null;
            return;
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        GameMgr.UI.HidePanel<LoadingPanel>(() =>
        {
            completionSource.TrySetResult();
        });

        await completionSource.Task;
        activeLoadingPanel = null;
    }

    private void ReportPreloadProgress(LoadingSceneRequest request, int completedCount, int totalCount)
    {
        float progress = totalCount <= 0
            ? 0.32f
            : Mathf.Lerp(0.12f, 0.32f, completedCount / (float)totalCount);
        Report(request, LoadingStage.PreloadAssets, progress, "Preload assets.");
    }

    private void Report(LoadingSceneRequest request, LoadingStage stage, float progress, string message)
    {
        LoadingProgress loadingProgress = new LoadingProgress(stage, progress, message);
        activeLoadingPanel?.ApplyProgress(loadingProgress);
        request.ProgressChanged?.Invoke(loadingProgress);
    }

    private async UniTask ShowFailureAsync(LoadingSceneRequest request, LoadingResult result)
    {
        if (activeLoadingPanel != null)
        {
            activeLoadingPanel.ShowFailure(result.ErrorMessage);
        }

        float duration = request != null ? Mathf.Max(0f, request.FailureMessageDuration) : 0f;
        if (duration > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), ignoreTimeScale: true);
        }
    }

    private async UniTask TryUnloadPreparedSceneAsync()
    {
        if (!hasPreparedScene)
        {
            return;
        }

        try
        {
            AsyncOperationHandle<SceneInstance> unloadHandle = Addressables.UnloadSceneAsync(preparedScene, true);
            await unloadHandle.ToUniTask();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LoadingMgr] Failed to unload prepared scene: {preparedRequest?.SceneName}\n{e}");
        }
    }

    private async UniTask TryRecoverToFallbackSceneAsync(LoadingSceneRequest request)
    {
        string fallbackSceneName = request?.FailureFallbackSceneName;
        if (string.IsNullOrWhiteSpace(fallbackSceneName) && request != null && request.RecoverToPreviousSceneOnFailure)
        {
            fallbackSceneName = preparedPreviousSceneName;
        }

        if (string.IsNullOrWhiteSpace(fallbackSceneName))
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.name == fallbackSceneName)
        {
            return;
        }

        try
        {
            GameMgr.Instance.sceneControllerInitiaFinished = false;
            AsyncOperationHandle<SceneInstance> recoverHandle =
                Addressables.LoadSceneAsync(fallbackSceneName, LoadSceneMode.Single, true);
            await recoverHandle.ToUniTask();

            if (recoverHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[LoadingMgr] Failed to recover to fallback scene: {fallbackSceneName}");
                return;
            }

            string recoveredSceneName = recoverHandle.Result.Scene.name;
            GameMgr.Event.Broadcast("SceneChanged", new GameEventParameter<string>(recoveredSceneName));
            bool ready = await WaitForSceneControllerReadyAsync(
                recoveredSceneName,
                request != null ? request.SceneControllerReadyTimeout : 10f);

            if (ready)
            {
                await sceneMgr.OnSceneEnterAsync(recoveredSceneName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LoadingMgr] Recovery scene load failed: {fallbackSceneName}\n{e}");
        }
    }

    private LoadingResult Fail(string sceneName, string message, Exception exception)
    {
        return new LoadingResult
        {
            Success = false,
            SceneName = sceneName,
            ErrorMessage = message,
            Exception = exception
        };
    }
}
