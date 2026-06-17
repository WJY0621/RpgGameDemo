using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameStartPanel : BasePanel
{
    private const string HoverSoundName = "UI_Hover3";
    private const string ClickSoundName = "UI_Hover2";
    private const string DefaultGameSceneName = "GameScene";

    private Transform UIBeginButton;
    private Transform UIContinueButton;
    private Transform UISettingsButton;
    private Transform UIEndButton;
    private Transform UILogOutButton;
    private Transform UIAccountText;
    private Transform UIMessagePosition;
    private bool isQuitting;

    private void Start()
    {
        Init();
    }

    public override void Init()
    {
        InitUIName();
        InitClick();
        RefreshAccountText();
    }

    private void OnEnable()
    {
        if (GameMgr.Account != null)
        {
            GameMgr.Account.OnProfileChanged -= HandleProfileChanged;
            GameMgr.Account.OnProfileChanged += HandleProfileChanged;
        }

        RefreshAccountText();
    }

    private void OnDestroy()
    {
        if (GameMgr.Account != null)
        {
            GameMgr.Account.OnProfileChanged -= HandleProfileChanged;
        }
    }

    private void InitUIName()
    {
        UIBeginButton = transform.Find("BeginButton");
        UIContinueButton = transform.Find("ContinueButton");
        UISettingsButton = transform.Find("SettingButton");
        UIEndButton = transform.Find("EndButton");
        UILogOutButton = transform.Find("BKImage/LogOutButton");
        if (UILogOutButton == null)
        {
            UILogOutButton = transform.Find("LogOutButton");
        }

        UIAccountText = transform.Find("BKImage/AccountText");
        if (UIAccountText == null)
        {
            UIAccountText = transform.Find("AccountText");
        }

        UIMessagePosition = transform.Find("MessagePosition");
    }

    private void InitClick()
    {
        BindButton(UIBeginButton, OnBeginButtonClick);
        BindButton(UIContinueButton, OnContinueButtonClick);
        BindButton(UISettingsButton, OnSettingsButtonClick);
        BindButton(UIEndButton, OnEndButtonClick);
        BindButton(UILogOutButton, OnLogOutButtonClick);
    }

    private void BindButton(Transform buttonTransform, UnityEngine.Events.UnityAction onClick)
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
        AddButtonSounds(buttonTransform);
    }

    private void AddButtonSounds(Transform buttonTransform)
    {
        AddHoverSound(buttonTransform);
        AddClickSound(buttonTransform);
    }

    private void AddHoverSound(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        UIHoverSound hoverSound = buttonTransform.GetComponent<UIHoverSound>();
        if (hoverSound == null)
        {
            hoverSound = buttonTransform.gameObject.AddComponent<UIHoverSound>();
        }

        hoverSound.Configure(HoverSoundName);
    }

    private void AddClickSound(Transform buttonTransform)
    {
        if (buttonTransform == null)
        {
            return;
        }

        UIClickSound clickSound = buttonTransform.GetComponent<UIClickSound>();
        if (clickSound == null)
        {
            clickSound = buttonTransform.gameObject.AddComponent<UIClickSound>();
        }

        clickSound.Configure(ClickSoundName);
    }

    private void OnEndButtonClick()
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
        if (!SaveBeforeQuit())
        {
            isQuitting = false;
            return;
        }

        QuitApplication();
    }

    private bool SaveBeforeQuit()
    {
        if (GameMgr.File == null || GameMgr.File.CurrentGameFile == null)
        {
            return true;
        }

        bool success = GameMgr.File.SaveGameFile();
        GameMgr.Message?.RegisterMessage(success ? "\u6e38\u620f\u5df2\u4fdd\u5b58" : "\u6e38\u620f\u4fdd\u5b58\u5931\u8d25",
            priority: success ? MessagePriority.Medium : MessagePriority.High);
        if (success)
        {
            PlayerPrefs.Save();
        }

        return success;
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private async void OnSettingsButtonClick()
    {
        await GameMgr.UI.ShowPanel<GameSettingPanel>();
    }

    private async void OnLogOutButtonClick()
    {
        if (GameMgr.Account == null || !GameMgr.Account.IsLoggedIn)
        {
            await GameMgr.UI.ShowPanel<LoginPanel>();
            return;
        }

        TipPanel tipPanel = await GameMgr.UI.ShowPanel<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip("确定退出此账号吗", () =>
        {
            GameMgr.Account.Logout();
            GameMgr.File?.EnsureCurrentGameFileForCurrentAccount();
            GameMgr.Social?.RefreshFriends();
            GameMgr.Social?.RefreshIncomingFriendRequests();
            RefreshAccountText();
            if (GameMgr.Message != null && UIMessagePosition != null)
            {
                GameMgr.Message.RegisterMessageAt("已退出当前账号", UIMessagePosition, priority: MessagePriority.Medium);
            }
            else
            {
                GameMgr.Message?.RegisterMessage("已退出当前账号", priority: MessagePriority.Medium);
            }
        });
    }

    private async void OnBeginButtonClick()
    {
        if (GameMgr.Account != null && !GameMgr.Account.IsLoggedIn)
        {
            await GameMgr.UI.ShowPanel<LoginPanel>();
            return;
        }

        await GameMgr.UI.SwitchPanelAsync<GameStartPanel, ChooseRolePanel>();
    }

    private async void OnContinueButtonClick()
    {
        if (GameMgr.Account != null && !GameMgr.Account.IsLoggedIn)
        {
            await GameMgr.UI.ShowPanel<LoginPanel>();
            return;
        }

        if (GameMgr.File == null)
        {
            GameMgr.Message?.RegisterMessage("存档系统未初始化", priority: MessagePriority.High);
            return;
        }

        GameMgr.File.EnsureCurrentGameFileForCurrentAccount();
        GameFile currentFile = GameMgr.File.CurrentGameFile;
        if (currentFile == null)
        {
            GameMgr.Message?.RegisterMessage("当前账号没有可继续的存档，请先开始游戏创建角色", priority: MessagePriority.Medium);
            await GameMgr.UI.SwitchPanelAsync<GameStartPanel, ChooseRolePanel>();
            return;
        }

        GameMgr.File.gameFileData.currentGameFileName = currentFile.fileName;
        GameMgr.File.ApplyCurrentGameFileToRuntime();
        SyncAccountDisplayName(currentFile);
        PlayerModelManager.CurrentRoleModelName = currentFile.roleModelName;

        string targetScene = ResolveContinueScene(currentFile);
        LoadingResult result = await GameMgr.Scene.LoadSceneAsync(targetScene);
        if (!result.Success)
        {
            Debug.LogError($"[GameStartPanel] Failed to continue game: {result.ErrorMessage}");
        }
    }

    private void HandleProfileChanged(AccountProfile _)
    {
        RefreshAccountText();
    }

    private void RefreshAccountText()
    {
        string accountName = "未登录";
        AccountProfile profile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        if (profile != null && profile.IsValid())
        {
            accountName = !string.IsNullOrWhiteSpace(profile.accountName)
                ? profile.accountName
                : profile.playerId;
        }

        SetText(UIAccountText, $"当前账号：{accountName}");
    }

    private static void SetText(Transform textTransform, string content)
    {
        if (textTransform == null)
        {
            return;
        }

        Text legacyText = textTransform.GetComponent<Text>();
        if (legacyText != null)
        {
            legacyText.text = content;
        }

        TextMeshProUGUI tmpText = textTransform.GetComponent<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.text = content;
        }
    }

    private static void SyncAccountDisplayName(GameFile gameFile)
    {
        if (gameFile == null || string.IsNullOrWhiteSpace(gameFile.playerName))
        {
            return;
        }

        GameMgr.Account?.SetDisplayName(gameFile.playerName);
    }

    private static string ResolveContinueScene(GameFile gameFile)
    {
        if (gameFile == null || string.IsNullOrWhiteSpace(gameFile.lastScene))
        {
            return DefaultGameSceneName;
        }

        string lastScene = gameFile.lastScene;
        if (lastScene == "GameStartScene" || lastScene == "LogoScene" || lastScene == "InitializeScene")
        {
            return DefaultGameSceneName;
        }

        return lastScene;
    }
}
