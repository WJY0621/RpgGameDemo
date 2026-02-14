using System.Collections;
using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class CameraMgr
{
    //存储虚拟相机
    private Dictionary<string, GameObject> cameraDic = new Dictionary<string, GameObject>();

    /// <summary>
    /// 更新相机列表
    /// </summary>
    public void UpdateCamera()
    {
        //先清除 再更新
        cameraDic.Clear();
        foreach (var item in Object.FindObjectsOfType<CinemachineVirtualCameraBase>(true))
        {
            cameraDic[item.gameObject.name] = item.gameObject;
        }
    }
    
    /// <summary>
    /// 获取相机
    /// </summary>
    public GameObject GetCamera(string cameraName)
    {
        if (cameraDic.Count == 0)
        {
            UpdateCamera();
        }
        
        return cameraDic.ContainsKey(cameraName) ? cameraDic[cameraName] : null;
    }
}
