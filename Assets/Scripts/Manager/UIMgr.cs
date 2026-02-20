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
            panelDic[panelName].gameObject.SetActive(true);
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

        // 如果面板不存在 就创建一个面板
        GameObject panelPrefab = await LoadPanel(panelName);
        if (panelPrefab == null)
        {
            Debug.LogError($"[UIMgr] Failed to load panel prefab: {panelName}");
            return null;
        }

        GameObject panelObj = GameObject.Instantiate(panelPrefab);
        
        // 确保 Canvas 引用有效
        if (canvasTrans == null) RefreshCanvas();
        
        panelObj.transform.SetParent(canvasTrans, false);
        
        //得到面板对象上的面板组件并返回
        T panel = panelObj.GetComponent<T>();
        if (panel == null)
        {
            Debug.LogError($"[UIMgr] Component {typeof(T).Name} not found on prefab {panelName}");
            return null;
        }

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
        //得到对应的面板后 调用其显示方法
        T panel = await GetPanel<T>();
        if (panel != null)
        {
            panel.Show();
        }
        return panel;
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
                GameObject.Destroy(panelDic[panelName]);
                panelDic.Remove(panelName);
            }
        }
    } 
    
}
