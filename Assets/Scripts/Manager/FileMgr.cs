using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FileMgr
{
#if UNITY_EDITOR
    private string filePath = Application.dataPath + "/Save/";
#else
    private string filePath = Application.persistentDataPath + "/Save/";
#endif
    [SerializeField]
    public GameFileData gameFileData;
    public GameFile CurrentGameFile
    {
        get
        {
            if (gameFileData == null) return null;
            return gameFileData.gameFiles.Find(gf => gf.fileName == gameFileData.currentGameFileName);
        }
    }

    public void UpdateFileData()
    {
        if (CurrentGameFile == null)
        {
            Debug.LogError("CurrentGameFile is null! Cannot update save data.");
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        // 如果是在开始菜单或 Logo 界面，不要覆盖存档中的 lastScene
        if (activeScene != "GameStartScene" && activeScene != "LogoScene" && activeScene != "InitializeScene")
        {
            //更新最后场景
            CurrentGameFile.lastScene = activeScene;
            //更新场景位置
            if (GameMgr.Instance.Player != null)
            {
                CurrentGameFile.SetLocationOnSceneLoaded(CurrentGameFile.lastScene, GameMgr.Instance.Player.transform);
            }
        }
        
        //更新玩家数据
        if (GameMgr.Instance.playerData != null)
        {
            CurrentGameFile.playerData = GameMgr.Instance.playerData;
        }
    }

    public bool SaveGameFile()
    {
        if (gameFileData == null)
        {
            Debug.LogError("[FileMgr] gameFileData is null! Cannot save.");
            return false;
        }

        //先更新一次再存储
        UpdateFileData();

        string resultPath = filePath + "gameSaveData.sav";
        string jsonData = JsonUtility.ToJson(gameFileData, true);

        Debug.Log($"[FileMgr] SaveGameFile: filePath = {resultPath}");

        if (!File.Exists(resultPath))
        {
            Directory.CreateDirectory(filePath);
        }
        File.WriteAllText(resultPath, jsonData);
        GameMgr.Instance.firstEnterGame = false;

        Debug.Log($"[FileMgr] SaveGameFile successful! File exists: {File.Exists(resultPath)}");
        return true;
    }

    // 创建新存档
    public void CreateNewGame(string playerName = "Player")
    {
        Debug.Log($"[FileMgr] CreateNewGame called with playerName: '{playerName}'");

        if (gameFileData == null)
        {
            Debug.Log("[FileMgr] gameFileData is null, creating new instance...");
            gameFileData = new GameFileData();
        }
        if (gameFileData.gameFiles == null)
        {
            Debug.Log("[FileMgr] gameFileData.gameFiles is null, creating new list...");
            gameFileData.gameFiles = new List<GameFile>();
        }

        GameFile newSave = new GameFile();
        // 使用时间戳作为唯一文件名
        newSave.fileName = "Save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        newSave.playerName = playerName;
        newSave.createTime = System.DateTime.Now.ToString("yyyy-MM-dd");
        newSave.lastScene = "GameScene"; // 默认进入 GameScene

        Debug.Log($"[FileMgr] Creating save: fileName={newSave.fileName}, playerName={playerName}");

        // 初始化玩家数据
        if (GameMgr.Instance.playerInitialData != null)
        {
            Debug.Log("[FileMgr] Using playerInitialData to initialize player data");
            newSave.playerData = GameMgr.Instance.playerInitialData.GetPlayerInitialData();
        }
        else
        {
            Debug.LogWarning("[FileMgr] playerInitialData is null, using default PlayerData!");
            newSave.playerData = new PlayerData(); // 防止空引用
        }

        gameFileData.gameFiles.Add(newSave);
        gameFileData.currentGameFileName = newSave.fileName;

        GameMgr.Instance.firstEnterGame = true;

        Debug.Log("[FileMgr] Calling SaveGameFile...");
        // 立即保存到磁盘
        SaveGameFile();

        Debug.Log($"[FileMgr] Created new save: {newSave.fileName}, PlayerName: {playerName}");
    }

    public bool LoadGameFile()
    {
        string resultPath = filePath + "gameSaveData.sav";
        bool loadSuccess = false;

        if (File.Exists(resultPath))
        {
            try
            {
                string jsonData = File.ReadAllText(resultPath);
                gameFileData = JsonUtility.FromJson<GameFileData>(jsonData);
                
                // 校验数据有效性
                if (gameFileData != null && gameFileData.gameFiles != null && gameFileData.gameFiles.Count > 0)
                {
                    // 尝试获取当前存档
                    var current = gameFileData.gameFiles.Find(gf => gf.fileName == gameFileData.currentGameFileName);
                    
                    // 如果找不到当前指向的存档，或者没有指定当前存档，就默认选最后一个（最新的）
                    if (current == null)
                    {
                        gameFileData.currentGameFileName = gameFileData.gameFiles.Last().fileName;
                    }
                    loadSuccess = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Load Save Failed: {e.Message}");
            }
        }

        // 如果没有有效存档，或者加载失败，这里不自动创建新游戏，而是让 UI 层决定
        // 只是确保 gameFileData 不为空，防止报错
        if (!loadSuccess)
        {
            gameFileData = new GameFileData();
            gameFileData.gameFiles = new List<GameFile>();
            // 注意：这里不再自动 Add 一个 New Start，而是等待玩家点击“开始游戏”时调用 CreateNewGame
        }

        // 如果有当前存档，就应用数据
        if (CurrentGameFile != null)
        {
            GameMgr.Instance.playerData = CurrentGameFile.playerData;
            return true;
        }

        return false;
    }

    // 清空所有存档
    public void ClearAllSaves()
    {
        gameFileData = new GameFileData();
        gameFileData.gameFiles = new List<GameFile>();
        gameFileData.currentGameFileName = null;

        // 删除存档文件
        string resultPath = filePath + "gameSaveData.sav";
        if (File.Exists(resultPath))
        {
            File.Delete(resultPath);
        }

        Debug.Log("All saves cleared.");
    }
}
