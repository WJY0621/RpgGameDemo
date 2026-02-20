using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TimeScaleMgr
{
    //是否准备缩放
    private bool alreadyScale = false;

    /// <summary>
    /// 时间缩放  
    /// </summary>
    /// <param name="targetScale">缩放比例</param>
    /// <param name="duration">持续时间 如果duration的值为-1 则会一直缩放</param>
    /// <returns></returns>
    public async void TimeScale(float targetScale, float duration)
    {
        if (alreadyScale) return;
        if (duration == -1f)    // 恢复时间-1f则不恢复，一直处于缩放状态
        {
            Time.timeScale = targetScale;
            alreadyScale = true;
            return;
        }
        await UniTask.Create(async () => await ScaleTimeAsync(targetScale, duration));
    }

    private async UniTask ScaleTimeAsync(float timeScale, float duration)
    {
        Time.timeScale = timeScale;
        alreadyScale = true;
        await UniTask.Delay((int)(duration * 1000), ignoreTimeScale: true);     //等待恢复时间
        ResetTimeScale();       //等待完成，进行恢复
        alreadyScale = false;
    }

    /// <summary>
    /// 恢复游戏缩放时间
    /// </summary>
    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
        alreadyScale = false;
    }
}
