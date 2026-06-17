using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameSettingPanel : BasePanel
{
    private const int DefaultStackItemCount = 867;

    // ===== 画面设置：全屏/窗口 + 分辨率 =====
    private const string FullScreenPrefKey = "WorkDemo.Settings.Video.FullScreen";
    private const string ResolutionPrefKey = "WorkDemo.Settings.Video.ResolutionIndex";

    // ToggleGroup option order: 0 = 1920x1080, 1 = 960x540, 2 = 1280x720.
    private static readonly Vector2Int[] ResolutionOptions =
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(960, 540),
        new Vector2Int(1280, 720)
    };

    private Transform UICloseButton;
    private Transform UISaveButton;
    private Transform UIGameEndButton;
    private Transform UIBackStartButton;
    private Transform UIGetItemButton;
    private Slider mainVoiceSlider;
    private Slider bgmSlider;
    private Slider soundSlider;
    private Toggle fullScreenToggle;
    private Toggle windowedToggle;
    private Toggle[] resolutionToggles;
    private bool syncingSliders;
    private bool syncingVideoUI;
    private bool isAddingItems;
    private bool isReturningToStart;
    private bool isQuitting;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }
    public override void Init()
    {
        InitUI();
        InitClick();
        RefreshVolumeSliders();
        RefreshVideoSettings();
        SetupLuaTimeButton();
    }

    /// <summary>
    /// Lua 热更扩展点：是否显示"设置时间"按钮、点了做什么，全由 lua 决定。
    /// 基线包里 lua 没定义这些函数 → 按钮隐藏（设置面板没有时间功能）；
    /// 热更下发新 lua 后 → 按钮显示并能打开时间面板。
    /// 前提：prefab 里放一个名为 TimeSettingButton 的按钮，默认 SetActive(false)。
    /// </summary>
    private void SetupLuaTimeButton()
    {
        Transform timeButton = FindDeepChild(transform, "TimeButton");
        if (timeButton == null)
        {
            return;
        }

        bool show = GameMgr.Lua != null && GameMgr.Lua.CallBool("ShouldShowTimeButton");
        timeButton.gameObject.SetActive(show);

        Button button = timeButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
                GameMgr.Lua?.DoString("if OnSettingTimeButton then OnSettingTimeButton() end"));
        }
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void InitUI()
    {
        UICloseButton = transform.Find("PanelBK/CloseButton");
        UIGetItemButton = transform.Find("PanelBK/FunctionContent/GetItemButton");
        UIGameEndButton = transform.Find("PanelBK/ButtonBK/GameEndButton");
        UIBackStartButton = transform.Find("PanelBK/ButtonBK/BackStartButton");
        UISaveButton = transform.Find("PanelBK/ButtonBK/SaveButton");
        if (UISaveButton == null)
        {
            UISaveButton = transform.Find("SaveButton");
        }

        mainVoiceSlider = transform.Find("PanelBK/VoiceContent/MainVoiceSlider")?.GetComponent<Slider>();
        bgmSlider = transform.Find("PanelBK/VoiceContent/BGMSlider")?.GetComponent<Slider>();
        soundSlider = transform.Find("PanelBK/VoiceContent/SoundSlider")?.GetComponent<Slider>();

        // 全屏/窗口的两个 Toggle 放在 ToggleGroup1 之下（同组单选，关闭 Allow Switch Off）。
        fullScreenToggle = transform.Find("PanelBK/PictureContent/ToggleGroup1/FullScreenToggle")?.GetComponent<Toggle>();
        windowedToggle = transform.Find("PanelBK/PictureContent/ToggleGroup1/WindowedToggle")?.GetComponent<Toggle>();

        // 三个分辨率 Toggle 在 ToggleGroup 之下
        Transform resolutionToggleGroup = transform.Find("PanelBK/PictureContent/ToggleGroup");
        resolutionToggles = new Toggle[ResolutionOptions.Length];
        resolutionToggles[0] = FindChildToggle(resolutionToggleGroup, "1920Toggle");
        resolutionToggles[1] = FindChildToggle(resolutionToggleGroup, "960Toggle");
        resolutionToggles[2] = FindChildToggle(resolutionToggleGroup, "1280Toggle");
    }

    private void InitClick()
    {
        BindButton(UICloseButton, OnCloseClick);
        BindButton(UISaveButton, OnSaveClick);
        BindButton(UIGetItemButton, OnGetItemClick);
        BindButton(UIGameEndButton, OnGameEndClick);
        BindButton(UIBackStartButton, OnBackStartClick);
        BindSlider(mainVoiceSlider, OnMainVoiceChanged);
        BindSlider(bgmSlider, OnBGMChanged);
        BindSlider(soundSlider, OnSoundChanged);
        BindToggle(fullScreenToggle, isOn => OnScreenModeToggleChanged(true, isOn));
        BindToggle(windowedToggle, isOn => OnScreenModeToggleChanged(false, isOn));
        for (int i = 0; i < resolutionToggles.Length; i++)
        {
            int resolutionIndex = i; // 捕获当前索引
            BindToggle(resolutionToggles[i], isOn => OnResolutionToggleChanged(resolutionIndex, isOn));
        }
    }

    private void BindButton(Transform buttonTransform, UnityAction onClick)
    {
        if (buttonTransform == null)
        {
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
    }

    private void BindSlider(Slider slider, UnityAction<float> onValueChanged)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(onValueChanged);
        slider.onValueChanged.AddListener(onValueChanged);
    }

    private void BindToggle(Toggle toggle, UnityAction<bool> onValueChanged)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(onValueChanged);
        toggle.onValueChanged.AddListener(onValueChanged);
    }

    private Toggle FindChildToggle(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform exactChild = parent.Find(childName);
        if (exactChild != null)
        {
            return exactChild.GetComponent<Toggle>();
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name.Trim() == childName)
            {
                return child.GetComponent<Toggle>();
            }
        }

        return null;
    }

    private void RefreshVolumeSliders()
    {
        if (GameMgr.Audio == null)
        {
            return;
        }

        syncingSliders = true;
        SetSliderValue(mainVoiceSlider, GameMgr.Audio.MasterVolume);
        SetSliderValue(bgmSlider, GameMgr.Audio.BGMVolume);
        SetSliderValue(soundSlider, GameMgr.Audio.SoundVolume);
        syncingSliders = false;
    }

    private void SetSliderValue(Slider slider, float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(value)));
    }

    private float GetSliderVolumeValue(Slider slider, float fallbackValue)
    {
        if (slider == null || Mathf.Approximately(slider.minValue, slider.maxValue))
        {
            return Mathf.Clamp01(fallbackValue);
        }

        return Mathf.Clamp01(Mathf.InverseLerp(slider.minValue, slider.maxValue, fallbackValue));
    }

    private void OnMainVoiceChanged(float value)
    {
        if (syncingSliders)
        {
            return;
        }

        GameMgr.Audio?.SetMasterVolume(GetSliderVolumeValue(mainVoiceSlider, value));
    }

    private void OnBGMChanged(float value)
    {
        if (syncingSliders)
        {
            return;
        }

        GameMgr.Audio?.SetBGMVolume(GetSliderVolumeValue(bgmSlider, value));
    }

    private void OnSoundChanged(float value)
    {
        if (syncingSliders)
        {
            return;
        }

        GameMgr.Audio?.SetSoundVolume(GetSliderVolumeValue(soundSlider, value));
    }

    // ==================== \u753b\u9762\u8bbe\u7f6e\uff1a\u5168\u5c4f/\u7a97\u53e3 + \u5206\u8fa8\u7387 ====================

    /// <summary>
    /// \u8bfb\u53d6\u5df2\u4fdd\u5b58\u7684\u753b\u9762\u8bbe\u7f6e\uff0c\u540c\u6b65\u5230 UI\uff0c\u5e76\u5e94\u7528\u5230\u5c4f\u5e55\u3002
    /// </summary>
    private void RefreshVideoSettings()
    {
        bool fullscreen = LoadSavedFullScreen();
        int resolutionIndex = LoadSavedResolutionIndex();

        // \u540c\u6b65 UI \u663e\u793a\uff08\u4e0d\u89e6\u53d1\u56de\u8c03\uff09
        syncingVideoUI = true;
        SetScreenModeToggles(fullscreen);
        SetResolutionToggles(resolutionIndex);
        syncingVideoUI = false;

        // \u5e94\u7528\u5230\u5c4f\u5e55\uff0c\u4fdd\u8bc1\u5b9e\u9645\u663e\u793a\u4e0e\u4fdd\u5b58\u503c\u4e00\u81f4
        ApplyVideoSettings(fullscreen, resolutionIndex);
    }

    /// <summary>
    /// \u5168\u5c4f/\u7a97\u53e3 Toggle \u6539\u53d8\u65f6\u89e6\u53d1\u3002\u4e24\u4e2a Toggle \u540c\u5c5e\u4e00\u4e2a ToggleGroup \u4fdd\u8bc1\u5355\u9009\uff0c
    /// \u53ea\u54cd\u5e94\u88ab\u9009\u4e2d\uff08isOn=true\uff09\u7684\u90a3\u4e2a\u3002
    /// </summary>
    private void OnScreenModeToggleChanged(bool fullscreen, bool isOn)
    {
        if (syncingVideoUI || !isOn)
        {
            return;
        }

        ApplyVideoSettings(fullscreen, GetSelectedResolutionIndex());

        PlayerPrefs.SetInt(FullScreenPrefKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// \u5206\u8fa8\u7387 Toggle \u6539\u53d8\u65f6\u89e6\u53d1\u3002ToggleGroup \u4fdd\u8bc1\u5355\u9009\uff0c\u53ea\u54cd\u5e94\u88ab\u9009\u4e2d\u7684\u90a3\u4e2a\u3002
    /// </summary>
    private void OnResolutionToggleChanged(int resolutionIndex, bool isOn)
    {
        if (syncingVideoUI || !isOn)
        {
            return;
        }

        syncingVideoUI = true;
        SetScreenModeToggles(false);
        syncingVideoUI = false;

        ApplyWindowedResolutionAsync(resolutionIndex).Forget();

        PlayerPrefs.SetInt(FullScreenPrefKey, 0);
        PlayerPrefs.SetInt(ResolutionPrefKey, resolutionIndex);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// \u540c\u65f6\u5e94\u7528\u5168\u5c4f\u6a21\u5f0f\u4e0e\u5206\u8fa8\u7387\u3002
    /// </summary>
    private void ApplyVideoSettings(bool fullscreen, int resolutionIndex)
    {
        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, ResolutionOptions.Length - 1);
        Vector2Int resolution = ResolutionOptions[resolutionIndex];
        FullScreenMode screenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(resolution.x, resolution.y, screenMode);
    }

    private async UniTaskVoid ApplyWindowedResolutionAsync(int resolutionIndex)
    {
        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, ResolutionOptions.Length - 1);
        Vector2Int resolution = ResolutionOptions[resolutionIndex];

        Screen.fullScreenMode = FullScreenMode.Windowed;
        Screen.fullScreen = false;
        Screen.SetResolution(resolution.x, resolution.y, false);
        Debug.Log($"[GameSettingPanel] Requested windowed resolution: {resolution.x}x{resolution.y}");

        await UniTask.Yield();

        if (Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed ||
            Screen.width != resolution.x || Screen.height != resolution.y)
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.fullScreen = false;
            Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
        }

        Debug.Log($"[GameSettingPanel] Current screen after resolution apply: {Screen.width}x{Screen.height}, fullscreen={Screen.fullScreen}, mode={Screen.fullScreenMode}");
    }

    // -------------------- \u8bfb\u53d6\u5df2\u4fdd\u5b58\u503c --------------------

    private bool LoadSavedFullScreen()
    {
        if (PlayerPrefs.HasKey(FullScreenPrefKey))
        {
            return PlayerPrefs.GetInt(FullScreenPrefKey) == 1;
        }

        return Screen.fullScreen;
    }

    private int LoadSavedResolutionIndex()
    {
        int fallbackIndex = FindClosestResolutionIndex();
        return Mathf.Clamp(PlayerPrefs.GetInt(ResolutionPrefKey, fallbackIndex), 0, ResolutionOptions.Length - 1);
    }

    /// <summary>
    /// \u627e\u5230\u4e0e\u5f53\u524d\u5c4f\u5e55\u5c3a\u5bf8\u6700\u63a5\u8fd1\u7684\u9884\u8bbe\u5206\u8fa8\u7387\uff0c\u4f5c\u4e3a\u6ca1\u6709\u5b58\u6863\u65f6\u7684\u9ed8\u8ba4\u503c\u3002
    /// </summary>
    private int FindClosestResolutionIndex()
    {
        int closestIndex = 0;
        int closestDistance = int.MaxValue;
        for (int i = 0; i < ResolutionOptions.Length; i++)
        {
            Vector2Int option = ResolutionOptions[i];
            int distance = Mathf.Abs(Screen.width - option.x) + Mathf.Abs(Screen.height - option.y);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    // -------------------- \u8bfb\u53d6 UI \u5f53\u524d\u9009\u62e9 --------------------

    private bool GetSelectedFullScreen()
    {
        if (fullScreenToggle != null && fullScreenToggle.isOn)
        {
            return true;
        }

        if (windowedToggle != null && windowedToggle.isOn)
        {
            return false;
        }

        return LoadSavedFullScreen();
    }

    private int GetSelectedResolutionIndex()
    {
        for (int i = 0; i < resolutionToggles.Length; i++)
        {
            if (resolutionToggles[i] != null && resolutionToggles[i].isOn)
            {
                return i;
            }
        }

        return LoadSavedResolutionIndex();
    }

    // -------------------- \u540c\u6b65 UI \u663e\u793a --------------------

    private void SetScreenModeToggles(bool fullscreen)
    {
        if (fullScreenToggle != null)
        {
            fullScreenToggle.SetIsOnWithoutNotify(fullscreen);
        }

        if (windowedToggle != null)
        {
            windowedToggle.SetIsOnWithoutNotify(!fullscreen);
        }
    }

    private void SetResolutionToggles(int resolutionIndex)
    {
        for (int i = 0; i < resolutionToggles.Length; i++)
        {
            if (resolutionToggles[i] != null)
            {
                resolutionToggles[i].SetIsOnWithoutNotify(i == resolutionIndex);
            }
        }
    }

    private void OnCloseClick()
    {
        GameMgr.UI.HidePanel<GameSettingPanel>();
    }
    private void OnSaveClick()
    {
        SaveGame();
    }

    private async void OnGetItemClick()
    {
        if (isAddingItems)
        {
            return;
        }

        if (GameMgr.Instance == null || GameMgr.Package == null)
        {
            Debug.LogWarning("[GameSettingPanel] Game manager is not ready. Cannot add all items.");
            GameMgr.Message?.RegisterMessage("\u80cc\u5305\u7cfb\u7edf\u672a\u51c6\u5907\u5b8c\u6210", priority: MessagePriority.High);
            return;
        }

        isAddingItems = true;
        try
        {
            int addedCount = await AddAllItemsAsync();
            GameMgr.Message?.RegisterMessage($"\u5df2\u83b7\u53d6\u5168\u90e8\u7269\u54c1 x{addedCount}", priority: MessagePriority.High);
        }
        finally
        {
            isAddingItems = false;
        }
    }

    private void OnGameEndClick()
    {
        QuitAfterSave();
    }

    private void QuitAfterSave()
    {
        if (isQuitting)
        {
            return;
        }

        isQuitting = true;
        if (!SaveGame())
        {
            isQuitting = false;
            return;
        }

        PlayerPrefs.Save();
        QuitApplication();
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private async void OnBackStartClick()
    {
        if (isReturningToStart)
        {
            return;
        }

        isReturningToStart = true;
        try
        {
            SaveGame();
            GameMgr.UI.HidePanel<GameSettingPanel>();

            LoadingResult result = await GameMgr.Scene.LoadSceneAsync("GameStartScene");
            if (!result.Success)
            {
                Debug.LogError($"[GameSettingPanel] Failed to return to GameStartScene: {result.ErrorMessage}");
                GameMgr.Message?.RegisterMessage("\u8fd4\u56de\u5f00\u59cb\u573a\u666f\u5931\u8d25", priority: MessagePriority.High);
                return;
            }

            await GameMgr.UI.ShowPanel<GameStartPanel>();
        }
        finally
        {
            isReturningToStart = false;
        }
    }

    private bool SaveGame()
    {
        if (GameMgr.File == null)
        {
            Debug.LogWarning("[GameSettingPanel] File manager is not ready. Cannot save game.");
            GameMgr.Message?.RegisterMessage("\u5b58\u6863\u7cfb\u7edf\u672a\u51c6\u5907\u5b8c\u6210", priority: MessagePriority.High);
            return false;
        }

        bool success = GameMgr.File.SaveGameFile();
        GameMgr.Message?.RegisterMessage(success ? "\u6e38\u620f\u5df2\u4fdd\u5b58" : "\u6e38\u620f\u4fdd\u5b58\u5931\u8d25",
            priority: success ? MessagePriority.Medium : MessagePriority.High);
        return success;
    }

    private async UniTask<int> AddAllItemsAsync()
    {
        if (GameMgr.Package == null)
        {
            return 0;
        }

        await GameMgr.Package.Init();
        GameMgr.Package.ReloadItemConfigs();

        List<Item> allItems = new List<Item>();
        foreach (Item item in ItemJsonDatabase.Items.Values)
        {
            if (item != null && item.id > 0)
            {
                allItems.Add(item);
            }
        }

        allItems.Sort((a, b) => a.id.CompareTo(b.id));

        for (int i = 0; i < allItems.Count; i++)
        {
            Item item = allItems[i];
            await GameMgr.Package.AddItem(item.id, GetAddCount(item));
        }

        if (allItems.Count == 0)
        {
            Debug.LogWarning("[GameSettingPanel] No item configs found in item database.");
        }

        return allItems.Count;
    }

    private static int GetAddCount(Item item)
    {
        if (item == null)
        {
            return 0;
        }

        if (item.itemType == ItemType.Weapon)
        {
            return 1;
        }

        return item.capacity > 0 ? item.capacity : DefaultStackItemCount;
    }

    private async void OnExitClick()
    {
        // 先检查 TipPanel 是否已经显示
        TipPanel existingTipPanel = GameMgr.UI.GetPanelWithoutLoad<TipPanel>();
        if (existingTipPanel != null)
        {
            return;
        }

        // 显示 TipPanel
        await GameMgr.UI.ShowPanel<TipPanel>();

        // 获取已显示的 TipPanel
        TipPanel tipPanel = GameMgr.UI.GetPanelWithoutLoad<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip("是否退出游戏？", () =>
        {
            QuitAfterSave();
        }, () =>
        {
            // 点击取消或背景，什么都不做
        });
    }
}
