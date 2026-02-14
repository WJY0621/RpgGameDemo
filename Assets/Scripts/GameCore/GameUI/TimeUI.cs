using System;
using UnityEngine;
using UnityEngine.UI;

public class TimeUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Slider timeSlider;
    
    private void Start()
    {
        // 1. 初始化 Slider
        if (timeSlider != null)
        {
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 24f;
            timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        // 2. 注册时间变化事件
        if (GameMgr.Time != null)
        {
            GameMgr.Time.OnTimeChanged.AddListener(OnTimeChanged);
            // 初始化显示
            UpdateDisplay();
        }
    }
    
    private void OnTimeChanged(TimeSpan time)
    {
        UpdateDisplay();
    }
    
    private void UpdateDisplay()
    {
        if (timeSlider != null && GameMgr.Time != null)
        {
            // 使用 SetValueWithoutNotify 防止死循环调用 SetTime
            timeSlider.SetValueWithoutNotify(GameMgr.Time.CurrentTime);
        }
    }
    
    private void OnTimeSliderChanged(float value)
    {
        // 玩家拖拽滑块 -> 修改游戏时间
        if (GameMgr.Time != null)
        {
            GameMgr.Time.SetTime(value);
        }
    }
    
    private void OnDestroy()
    {
        if (GameMgr.Time != null)
        {
            GameMgr.Time.OnTimeChanged.RemoveListener(OnTimeChanged);
        }
    }
}
