using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UICamera : MonoBehaviour
{
    public static UICamera Instance;
    public Camera uiCamera;

    void Awake()
    {
        // 单例模式，防止重复创建
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 确保UI摄像机也被保留
        if (uiCamera != null)
        {
            DontDestroyOnLoad(uiCamera.gameObject);
        }
    }
}
