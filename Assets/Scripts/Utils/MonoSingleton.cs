using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                //去找挂载组建的对象
                instance = GameObject.FindObjectOfType<T>();
                //如果找不到
                if (instance == null)
                {
                    //用一个新的gameObject挂载
                    GameObject gameObject = new GameObject();
                    instance = gameObject.AddComponent<T>();
                    gameObject.name = typeof(T).Name;
                }
            }
            //返回出去
            return instance;
        }
    }

    protected virtual void Awake()
    {
        //当程序开始运行时 进行初始化
        if (instance == null)
        {
            //第一次进来
            instance = this as T;
            DontDestroyOnLoad(this.gameObject);
            return;
        }
        if (instance != this as T)
        {
            //如果已经存在 则直接销毁当前对象
            GameObject.Destroy(this.gameObject);
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (instance == this as T)
        {
            //在销毁时 进行释放
            instance = null;
        }
    }
}
