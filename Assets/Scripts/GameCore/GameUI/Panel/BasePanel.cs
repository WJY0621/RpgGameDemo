using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
    private float alphaSpeed = 10;
    public bool IsShow => isShow;
    public bool IsFullyShown => canvasGroup != null && canvasGroup.alpha >= 0.99f;
    public bool IsFullyHidden => canvasGroup == null || canvasGroup.alpha <= 0.01f;
    private bool isShow;
    //隐藏之后的回调函数
    private UnityAction hideCallBack;

    protected virtual void Awake()
    {
        EnsureCanvasGroup();
    }

    private void EnsureCanvasGroup()
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
        EnsureCanvasGroup();
        isShow = true;
        canvasGroup.alpha = 0;
    }

    public virtual void Hide(UnityAction callBack = null)
    {
        EnsureCanvasGroup();
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

    public async UniTask WaitUntilFullyShownAsync()
    {
        EnsureCanvasGroup();
        while (isShow && !IsFullyShown)
        {
            await UniTask.Yield();
        }
    }

    protected virtual void Update()
    {
        EnsureCanvasGroup();
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
