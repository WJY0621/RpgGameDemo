using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class GameStartPanel : BasePanel
{
    private Transform UIBeginButton;
    private Transform UIContinueButton;
    private Transform UISettingsButton;
    private Transform UIEndButton;

    void Start()
    {
        Init();
    }
    public override void Init()
    {
        InitUIName();
        InitClick();
    }

    private void InitUIName()
    {
        UIBeginButton = transform.Find("BeginButton");
        UIContinueButton = transform.Find("ContinueButton");
        UISettingsButton = transform.Find("SettingButton");
        UIEndButton = transform.Find("EndButton");
    }

    private void InitClick()
    {
        UIBeginButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnBeginButtonClick);
        UIContinueButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnContinueButtonClick);
        UISettingsButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnSettingsButtonClick);
        UIEndButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnEndButtonClick);
    }

    private void OnEndButtonClick()
    {
        throw new NotImplementedException();
    }

    private void OnSettingsButtonClick()
    {
        throw new NotImplementedException();
    }

    private void OnContinueButtonClick()
    {
        // 继续游戏逻辑：尝试加载最近的存档
        bool hasSave = GameMgr.File.LoadGameFile();
        if (hasSave && GameMgr.File.CurrentGameFile != null)
        {
            this.Hide();
            Debug.Log($"继续游戏，加载存档: {GameMgr.File.CurrentGameFile.fileName}");
            
            // 读取存档记录的场景，如果为空则默认进 GameScene
            string targetScene = GameMgr.File.CurrentGameFile.lastScene;
            if (string.IsNullOrEmpty(targetScene)) targetScene = "GameScene";
            
            GameMgr.Scene.LoadSceneAsync(targetScene).Forget();
        }
        else
        {
            Debug.Log("没有可用的存档，无法继续游戏");
            // 这里可以弹一个 TipPanel 提示用户
        }
    }

    private void OnBeginButtonClick()
    {
        // 开始新游戏逻辑：创建新存档
        this.Hide();
        Debug.Log("开始新游戏，创建新存档");
        
        GameMgr.File.CreateNewGame();
        
        // 新游戏默认进入 GameScene
        GameMgr.Scene.LoadSceneAsync("GameScene").Forget();
    }
}
