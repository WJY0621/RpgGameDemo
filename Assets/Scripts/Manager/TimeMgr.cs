using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TimeChangedEvent : UnityEvent<TimeSpan> { }
[System.Serializable]
public class DayChangedEvent : UnityEvent<int> { }

public class TimeMgr : MonoBehaviour
{
    [Header("时间设置")]
    [Tooltip("游戏开始时的时间（小时）")]
    [SerializeField] private float startTimeInHours = 6f;
    [Tooltip("时间流速：现实1秒 = 游戏N秒。60表示现实24分钟=游戏24小时")]
    [SerializeField] private float timeScale = 60f;

    [Header("日期设置")]
    [SerializeField] private int startDay = 1;

    [Header("光照设置")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Gradient sunColor;
    [SerializeField] private AnimationCurve sunIntensity = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("环境光更新频率（秒）")]
    [SerializeField] private float giUpdateInterval = 1f;

    // 运行时数据
    private float currentTime;
    private int currentDay;
    private float lastGITime;
    private bool isDaytimeCache;

    // 事件
    public TimeChangedEvent OnTimeChanged;
    public DayChangedEvent OnDayChanged;
    public UnityEvent OnDayStart;
    public UnityEvent OnNightStart;

    // 单例模式
    public static TimeMgr Instance { get; private set; }

    // 公共属性
    public float CurrentTime => currentTime;
    public int CurrentDay => currentDay;
    public bool IsDaytime => currentTime >= 6f && currentTime < 18f;
    public bool IsNighttime => !IsDaytime;
    public float TimeScale => timeScale;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        Initialize();
    }

    private void Initialize()
    {
        currentTime = startTimeInHours;
        currentDay = startDay;
        // 初始化缓存状态，反向设置以触发第一次事件（如果需要）或者同步状态
        // 这里直接同步，避免开局触发事件
        isDaytimeCache = IsDaytime;
        
        UpdateSun();
    }

    private void Update()
    {
        // 1. 更新时间
        float deltaTimeInHours = (Time.deltaTime * timeScale) / 3600f;
        currentTime += deltaTimeInHours;

        // 2. 处理日期变更
        if (currentTime >= 24f)
        {
            currentTime -= 24f;
            currentDay++;
            OnDayChanged?.Invoke(currentDay);
        }

        // 3. 触发时间变化事件
        OnTimeChanged?.Invoke(GetCurrentTimeSpan());

        // 4. 更新太阳位置和光照
        UpdateSun();

        // 5. 检查昼夜转换
        CheckDayNightTransition();

        // 6. 更新环境光 (GI)
        if (Time.time - lastGITime > giUpdateInterval)
        {
            lastGITime = Time.time;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void UpdateSun()
    {
        if (sunLight == null) return;

        // 规范化时间 0~1
        float timeNormalized = currentTime / 24f;

        // 计算太阳角度（0点-90度，6点0度，12点90度，18点180度）
        float sunAngleX = (timeNormalized * 360f) - 90f;
        sunLight.transform.rotation = Quaternion.Euler(sunAngleX, 170f, 0f);

        // 更新太阳颜色和强度
        // 注意：sunColor 和 sunIntensity 的 Time 轴应该也是 0~1
        sunLight.color = sunColor.Evaluate(timeNormalized);
        sunLight.intensity = sunIntensity.Evaluate(timeNormalized);

        // 优化：当强度极低时禁用阴影投射或组件（可选），这里保持开启以避免突兀消失
        // 只有当强度几乎为0且在地平线以下时才考虑禁用
        if (sunLight.intensity <= 0.01f && IsNighttime)
        {
             if (sunLight.enabled) sunLight.enabled = false;
        }
        else
        {
             if (!sunLight.enabled) sunLight.enabled = true;
        }
    }

    private void CheckDayNightTransition()
    {
        bool currentIsDay = IsDaytime;

        if (currentIsDay != isDaytimeCache)
        {
            if (currentIsDay)
            {
                OnDayStart?.Invoke();
            }
            else
            {
                OnNightStart?.Invoke();
            }
            isDaytimeCache = currentIsDay;
        }
    }

    // --- 公共控制方法 ---

    public void SetTime(float hours)
    {
        currentTime = Mathf.Clamp(hours, 0f, 24f);
        // 强制更新一次状态，防止瞬间切换导致事件漏发
        CheckDayNightTransition();
        UpdateSun();
        OnTimeChanged?.Invoke(GetCurrentTimeSpan());
    }

    public void SetTimeScale(float scale)
    {
        timeScale = Mathf.Max(0f, scale);
    }

    public void PauseTime()
    {
        timeScale = 0f;
    }

    public void ResumeTime()
    {
        timeScale = 60f; // 默认恢复为 60
    }

    public TimeSpan GetCurrentTimeSpan()
    {
        int hours = Mathf.FloorToInt(currentTime);
        int minutes = Mathf.FloorToInt((currentTime - hours) * 60f);
        return new TimeSpan(hours, minutes, 0);
    }

    public string GetFormattedTime()
    {
        TimeSpan time = GetCurrentTimeSpan();
        return $"{time.Hours:D2}:{time.Minutes:D2}";
    }

    public float GetNormalizedTime()
    {
        return currentTime / 24f;
    }
}
