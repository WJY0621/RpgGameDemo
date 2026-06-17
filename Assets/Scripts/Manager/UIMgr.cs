using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class UIMgr
{
    public int panelCount;
    private Dictionary<string, BasePanel> panelDic = new Dictionary<string, BasePanel>();
    // 防止重复加载的标志
    private HashSet<string> loadingPanels = new HashSet<string>();
    public MainCanvas mainCanvas;
    private Transform canvasTrans;

    public UIMgr()
    {
        RefreshCanvas();
    }

    private void RefreshCanvas()
    {
        if (mainCanvas == null)
        {
            mainCanvas = GameObject.FindFirstObjectByType<MainCanvas>();
        }
        if (mainCanvas != null)
        {
            canvasTrans = mainCanvas.panelsParent;
        }
    }

    /// <summary>
    /// 加载面板资源
    /// </summary>
    /// <param name="panelName">面板名称</param>
    /// <returns>返回面板对象</returns>
    public async UniTask<GameObject> LoadPanel(string panelName)
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(panelName);
        await handle.ToUniTask();
        return handle.Result;
    }

    //得到已经存在的UI面板
    public T GetPanelWithoutLoad<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName) && panelDic[panelName] != null)
        {
            return panelDic[panelName] as T;
        }
        return null;
    }

    /// <summary>
    /// 获取面板组件 没有则创建
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async UniTask<T> GetPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        // 检查字典中是否存在且未被销毁
        if (panelDic.ContainsKey(panelName) && panelDic[panelName] != null)
        {
            panelDic[panelName].gameObject.SetActive(true);
            return panelDic[panelName] as T;
        }

        // 清理可能残留的同名面板（防止重复创建）
        if (canvasTrans != null)
        {
            var existingPanels = canvasTrans.GetComponentsInChildren<T>(true);
            foreach (var p in existingPanels)
            {
                if (p != null && p.gameObject != null)
                {
                    GameObject.Destroy(p.gameObject);
                }
            }
        }

        // 如果面板不存在 就创建一个面板
        GameObject panelPrefab = await LoadPanel(panelName);
        if (panelPrefab == null)
        {
            Debug.LogError($"[UIMgr] Failed to load panel prefab: {panelName}");
            return null;
        }

        GameObject panelObj = GameObject.Instantiate(panelPrefab);

        // 确保 Canvas 引用有效
        if (canvasTrans == null)
        {
            RefreshCanvas();
        }

        if (canvasTrans == null)
        {
            Debug.LogError("[UIMgr] canvasTrans is still null after refresh!");
        }

        panelObj.transform.SetParent(canvasTrans, false);

        //得到面板对象上的面板组件并返回
        T panel = panelObj.GetComponent<T>();
        if (panel == null)
        {
            Debug.LogError($"[UIMgr] Component {typeof(T).Name} not found on prefab {panelName}");
            return null;
        }

        // 调用面板的 Init 方法
        panel.Init();

        panelDic[panelName] = panel;
        return panel;
    }

    //判断面板是否是激活状态
    public bool IsShow<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        {
            return panelDic[panelName].IsShow;
        }
        return false;
    }

    //显示面板
    public async UniTask<T> ShowPanel<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;

        // 防止重复加载
        if (loadingPanels.Contains(panelName))
        {
            return null;
        }

        loadingPanels.Add(panelName);

        try
        {
            //得到对应的面板后 调用其显示方法
            T panel = await GetPanel<T>();
            if (panel != null)
            {
                panel.Show();
                KeepLoadingPanelOnTop(panel);
            }
            return panel;
        }
        finally
        {
            loadingPanels.Remove(panelName);
        }
    }

    private void KeepLoadingPanelOnTop(BasePanel shownPanel)
    {
        if (shownPanel is LoadingPanel)
        {
            shownPanel.transform.SetAsLastSibling();
            return;
        }

        LoadingPanel loadingPanel = GetPanelWithoutLoad<LoadingPanel>();
        if (loadingPanel != null && loadingPanel.IsShow)
        {
            loadingPanel.transform.SetAsLastSibling();
        }
    }

    public async UniTask<TTo> SwitchPanelAsync<TFrom, TTo>()
        where TFrom : BasePanel
        where TTo : BasePanel
    {
        RefreshCanvas();
        if (mainCanvas != null)
        {
            await mainCanvas.FadeToBlackAsync();
        }

        TTo targetPanel = await ShowPanel<TTo>();
        if (targetPanel != null)
        {
            await targetPanel.WaitUntilFullyShownAsync();
        }

        HidePanelImmediate<TFrom>();

        if (mainCanvas != null)
        {
            await mainCanvas.FadeFromBlackAsync();
        }

        return targetPanel;
    }

    public async UniTask<T> ShowPanelWithBlackAsync<T>() where T : BasePanel
    {
        RefreshCanvas();
        if (mainCanvas != null)
        {
            await mainCanvas.FadeToBlackAsync();
        }

        T targetPanel = await ShowPanel<T>();
        if (targetPanel != null)
        {
            await targetPanel.WaitUntilFullyShownAsync();
        }

        if (mainCanvas != null)
        {
            await mainCanvas.FadeFromBlackAsync();
        }

        return targetPanel;
    }

    public async UniTask FadeToBlackAsync()
    {
        RefreshCanvas();
        if (mainCanvas != null)
        {
            await mainCanvas.FadeToBlackAsync();
        }
    }

    public async UniTask FadeFromBlackAsync()
    {
        RefreshCanvas();
        if (mainCanvas != null)
        {
            await mainCanvas.FadeFromBlackAsync();
        }
    }

    public void ShowBlackScreenImmediate()
    {
        RefreshCanvas();
        mainCanvas?.ShowBlackScreenImmediate();
    }

    public void HideBlackScreenImmediate()
    {
        RefreshCanvas();
        mainCanvas?.HideBlackScreenImmediate();
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    /// <param name="callBack">回调函数</param>
    /// <param name="isFade">是否淡入淡出</param>
    /// <typeparam name="T">面板类型</typeparam>
    public void HidePanel<T>(Action callBack = null, bool isFade = true) where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        {
            if (isFade)
            {
                panelDic[panelName].Hide(() =>
                {
                    panelDic[panelName].gameObject.SetActive(false);
                    callBack?.Invoke();
                });
            }
            else
            {
                GameObject.Destroy(panelDic[panelName].gameObject);
                panelDic.Remove(panelName);
            }
        }
    }

    public void HidePanelImmediate<T>() where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.TryGetValue(panelName, out BasePanel panel) && panel != null)
        {
            panel.Hide();
            panel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 销毁所有面板
    /// </summary>
    public void DestroyAllPanels()
    {
        // 先销毁字典中记录的所有面板
        foreach (var panel in panelDic.Values)
        {
            if (panel != null && panel.gameObject != null)
            {
                GameObject.Destroy(panel.gameObject);
            }
        }
        panelDic.Clear();

        // 额外检查 Canvas 下是否还有遗漏的面板（防止有多个同类型面板）
        if (canvasTrans != null)
        {
            var allPanels = canvasTrans.GetComponentsInChildren<BasePanel>(true);
            foreach (var panel in allPanels)
            {
                if (panel != null && panel.gameObject != null)
                {
                    GameObject.Destroy(panel.gameObject);
                }
            }
        }
    }

    public void DestroyAllPanelsExcept<T>() where T : BasePanel
    {
        string keepPanelName = typeof(T).Name;
        List<string> keysToRemove = new List<string>();

        foreach (var pair in panelDic)
        {
            if (pair.Key == keepPanelName)
            {
                continue;
            }

            if (pair.Value != null && pair.Value.gameObject != null)
            {
                GameObject.Destroy(pair.Value.gameObject);
            }

            keysToRemove.Add(pair.Key);
        }

        foreach (string key in keysToRemove)
        {
            panelDic.Remove(key);
        }

        if (canvasTrans != null)
        {
            var allPanels = canvasTrans.GetComponentsInChildren<BasePanel>(true);
            foreach (var panel in allPanels)
            {
                if (panel == null || panel is T)
                {
                    continue;
                }

                GameObject.Destroy(panel.gameObject);
            }
        }
    }
    
}
