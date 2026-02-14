using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public abstract class BasePanel : MonoBehaviour
{
    //存储面板上的CanvasGroup 用于渐隐渐现
    private CanvasGroup canvasGroup;
    //显隐速度
    private float alphaSpeed = 4;
    public bool IsShow => isShow;
    private bool isShow;
    //隐藏之后的回调函数
    private UnityAction hideCallBack;

    protected virtual void Awake()
    {
        canvasGroup = this.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
        }
    }

    public abstract void Init();

    public virtual void Show()
    {
        isShow = true;
        canvasGroup.alpha = 0;
    }

    public virtual void Hide(UnityAction callBack = null)
    {
        isShow = false;
        if (canvasGroup)
        {
            canvasGroup.alpha = 1;
        }
        hideCallBack = callBack;
    }
    public void SetAlphaSpeed(float speed)
    {
        alphaSpeed = speed;
    }

    protected virtual void Update()
    {
        if (isShow && canvasGroup.alpha != 1)
        {
            canvasGroup.alpha += alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha >= 1)
            {
                canvasGroup.alpha = 1;
            }
        }
        else if (!isShow && canvasGroup.alpha != 0)
        {
            canvasGroup.alpha -= alphaSpeed * Time.unscaledDeltaTime;
            if (canvasGroup.alpha <= 0)
            {
                canvasGroup.alpha = 0;
                hideCallBack?.Invoke();
            }
        }
    }

}
