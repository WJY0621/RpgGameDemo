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
            return gameFileData.gameFiles.Find(gf =>
                gf.fileName == gameFileData.currentGameFileName &&
                IsVisibleForCurrentAccount(gf));
        }
    }

    public List<GameFile> GetCurrentAccountGameFiles()
    {
        if (gameFileData == null || gameFileData.gameFiles == null)
        {
            return new List<GameFile>();
        }

        string currentPlayerId = GetCurrentAccountPlayerId();
        if (string.IsNullOrWhiteSpace(currentPlayerId))
        {
            return new List<GameFile>(gameFileData.gameFiles);
        }

        return gameFileData.gameFiles
            .Where(gf => gf != null && string.Equals(gf.ownerPlayerId, currentPlayerId, System.StringComparison.Ordinal))
            .ToList();
    }

    public void EnsureCurrentGameFileForCurrentAccount()
    {
        if (gameFileData == null)
        {
            gameFileData = new GameFileData();
        }

        if (gameFileData.gameFiles == null)
        {
            gameFileData.gameFiles = new List<GameFile>();
        }

        List<GameFile> accountFiles = GetCurrentAccountGameFiles();
        GameFile current = accountFiles.Find(gf => gf.fileName == gameFileData.currentGameFileName);
        if (current != null)
        {
            return;
        }

        gameFileData.currentGameFileName = accountFiles.Count > 0
            ? accountFiles[accountFiles.Count - 1].fileName
            : null;
    }

    public void UpdateFileData()
    {
        if (CurrentGameFile == null)
        {
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        // 濡傛灉鏄湪寮€濮嬭彍鍗曟垨 Logo 鐣岄潰锛屼笉瑕佽鐩栧瓨妗ｄ腑鐨?lastScene
        if (activeScene != "GameStartScene" && activeScene != "LogoScene" && activeScene != "InitializeScene")
        {
            //鏇存柊鏈€鍚庡満鏅?
            CurrentGameFile.lastScene = activeScene;
            //鏇存柊鍦烘櫙浣嶇疆
            if (GameMgr.Instance.Player != null)
            {
                CurrentGameFile.SetLocationOnSceneLoaded(CurrentGameFile.lastScene, GameMgr.Instance.Player.transform);
            }
        }
        
        //鏇存柊鐜╁鏁版嵁
        if (GameMgr.Instance.playerData != null)
        {
            CurrentGameFile.playerData = GameMgr.Instance.playerData.CreateSaveSnapshot();
        }

        if (GameMgr.Package != null)
        {
            CurrentGameFile.gold = GameMgr.Package.GetGold();
            CurrentGameFile.inventoryData = GameMgr.Package.BuildSaveData();
        }

        if (GameMgr.TaskMgr != null)
        {
            CurrentGameFile.taskSystemData = GameMgr.TaskMgr.BuildSaveData();
        }

        if (GameMgr.Time != null)
        {
            CurrentGameFile.SetGameTime(GameMgr.Time);
        }
    }

    public bool SaveGameFile()
    {
        if (gameFileData == null)
        {
            Debug.LogError("[FileMgr] gameFileData is null! Cannot save.");
            return false;
        }

        //鍏堟洿鏂颁竴娆″啀瀛樺偍
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

    // 鍒涘缓鏂板瓨妗?
    public void CreateNewGame(string playerName = "Player", string roleModelName = null)
    {
        if (gameFileData == null)
        {
            gameFileData = new GameFileData();
        }
        if (gameFileData.gameFiles == null)
        {
            gameFileData.gameFiles = new List<GameFile>();
        }

        // 鐢熸垚鍞竴鐨勬枃浠跺悕
        string fileName = "Save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // 妫€鏌ユ槸鍚﹀凡瀛樺湪鐩稿悓鏂囦欢鍚嶇殑瀛樻。
        if (gameFileData.gameFiles.Any(gf => gf.fileName == fileName))
        {
            // 濡傛灉宸插瓨鍦紝娣诲姞姣鏁颁互纭繚鍞竴鎬?
            fileName = "Save_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }

        GameFile newSave = new GameFile();
        newSave.fileName = fileName;
        newSave.ownerPlayerId = GetCurrentAccountPlayerId();
        newSave.playerName = playerName;
        newSave.createTime = System.DateTime.Now.ToString("yyyy-MM-dd");
        newSave.lastScene = "GameScene"; // 榛樿杩涘叆 GameScene
        newSave.roleModelName = roleModelName; // 淇濆瓨瑙掕壊妯″瀷鍚嶇О
        newSave.gold = GameMgr.Instance.playerInitialData != null
            ? Mathf.Max(0, GameMgr.Instance.playerInitialData.GC)
            : 0;
        newSave.inventoryData = new InventorySaveData();
        newSave.inventoryData.gold = newSave.gold;
        ApplyInitialInventoryItems(newSave.inventoryData, GameMgr.Instance.playerInitialData);

        // 璁剧疆鐜╁鍒濆浣嶇疆
        if (GameMgr.Instance.playerInitialData != null)
        {
            PlayerSceneLocation initialLocation = new PlayerSceneLocation();
            initialLocation.sceneName = "GameScene";
            initialLocation.position = GameMgr.Instance.playerInitialData.initialPosition;
            initialLocation.rotation = GameMgr.Instance.playerInitialData.initialRotation;
            newSave.playerSceneLocations.Add(initialLocation);
        }

        // 鍒濆鍖栫帺瀹舵暟鎹?
        if (GameMgr.Instance.playerInitialData != null)
        {
            newSave.playerData = GameMgr.Instance.playerInitialData.GetPlayerInitialData();
        }
        else
        {
            newSave.playerData = new PlayerData(); // 闃叉绌哄紩鐢?
        }

        newSave.taskSystemData = new TaskSystemSaveData();
        newSave.SetGameTime(TimeMgr.DefaultStartDay, TimeMgr.DefaultStartTimeInHours);

        gameFileData.gameFiles.Add(newSave);
        gameFileData.currentGameFileName = newSave.fileName;
        if (GameMgr.Package != null)
        {
            GameMgr.Package.LoadFromSaveData(newSave.inventoryData);
        }

        if (GameMgr.Instance != null)
        {
            GameMgr.Instance.playerData = newSave.playerData;
        }

        if (GameMgr.TaskMgr != null)
        {
            GameMgr.TaskMgr.LoadFromSaveData(newSave.taskSystemData);
        }

        if (GameMgr.Time != null)
        {
            GameMgr.Time.ResetToDefaultStartTime();
        }

        GameMgr.Instance.firstEnterGame = true;

        // 绔嬪嵆淇濆瓨鍒扮鐩?
        SaveGameFile();
    }

    public void ApplyCurrentGameFileToRuntime()
    {
        if (CurrentGameFile == null)
        {
            return;
        }

        GameMgr.Instance.playerData = CurrentGameFile.playerData;
        if (CurrentGameFile.taskSystemData == null)
        {
            CurrentGameFile.taskSystemData = new TaskSystemSaveData();
        }

        if (CurrentGameFile.inventoryData == null)
        {
            CurrentGameFile.inventoryData = new InventorySaveData();
        }

        CurrentGameFile.inventoryData.gold = Mathf.Max(CurrentGameFile.inventoryData.gold, CurrentGameFile.gold);

        if (GameMgr.TaskMgr != null)
        {
            GameMgr.TaskMgr.LoadFromSaveData(CurrentGameFile.taskSystemData);
        }

        if (GameMgr.Package != null)
        {
            GameMgr.Package.LoadFromSaveData(CurrentGameFile.inventoryData);
        }

        if (GameMgr.Time != null && CurrentGameFile.TryGetGameTime(out int day, out float timeInHours))
        {
            GameMgr.Time.SetDateTime(day, timeInHours);
        }
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
                
                // 鏍￠獙鏁版嵁鏈夋晥鎬?
                if (gameFileData != null && gameFileData.gameFiles != null && gameFileData.gameFiles.Count > 0)
                {
                    // 灏濊瘯鑾峰彇褰撳墠瀛樻。
                    var current = gameFileData.gameFiles.Find(gf => gf.fileName == gameFileData.currentGameFileName);
                    
                    // 濡傛灉鎵句笉鍒板綋鍓嶆寚鍚戠殑瀛樻。锛屾垨鑰呮病鏈夋寚瀹氬綋鍓嶅瓨妗ｏ紝灏遍粯璁ら€夋渶鍚庝竴涓紙鏈€鏂扮殑锛?
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

        // 濡傛灉娌℃湁鏈夋晥瀛樻。锛屾垨鑰呭姞杞藉け璐ワ紝杩欓噷涓嶈嚜鍔ㄥ垱寤烘柊娓告垙锛岃€屾槸璁?UI 灞傚喅瀹?
        // 鍙槸纭繚 gameFileData 涓嶄负绌猴紝闃叉鎶ラ敊
        if (!loadSuccess)
        {
            gameFileData = new GameFileData();
            gameFileData.gameFiles = new List<GameFile>();
            // 娉ㄦ剰锛氳繖閲屼笉鍐嶈嚜鍔?Add 涓€涓?New Start锛岃€屾槸绛夊緟鐜╁鐐瑰嚮鈥滃紑濮嬫父鎴忊€濇椂璋冪敤 CreateNewGame
        }

        // 濡傛灉鏈夊綋鍓嶅瓨妗ｏ紝灏卞簲鐢ㄦ暟鎹?
        if (CurrentGameFile != null)
        {
            ApplyCurrentGameFileToRuntime();
            return true;
        }

        return false;
    }

    // 娓呯┖鎵€鏈夊瓨妗?
    public void ClearAllSaves()
    {
        gameFileData = new GameFileData();
        gameFileData.gameFiles = new List<GameFile>();
        gameFileData.currentGameFileName = null;

        // 鍒犻櫎瀛樻。鏂囦欢
        string resultPath = filePath + "gameSaveData.sav";
        if (File.Exists(resultPath))
        {
            File.Delete(resultPath);
        }

        Debug.Log("All saves cleared.");
    }

    private void ApplyInitialInventoryItems(InventorySaveData inventoryData, PlayerInitialDataSO initialData)
    {
        if (inventoryData == null || initialData == null || initialData.initialInventoryItems == null)
        {
            return;
        }

        if (inventoryData.items == null)
        {
            inventoryData.items = new List<InventoryItem>();
        }

        ItemJsonDatabase.EnsureLoaded();
        Dictionary<ItemType, HashSet<int>> occupiedSlotsByType = new Dictionary<ItemType, HashSet<int>>();

        for (int i = 0; i < initialData.initialInventoryItems.Length; i++)
        {
            PlayerInitialInventoryItem initialItem = initialData.initialInventoryItems[i];
            if (initialItem == null || initialItem.itemId <= 0)
            {
                continue;
            }

            Item itemConfig = ItemJsonDatabase.GetItem(initialItem.itemId);
            if (itemConfig == null)
            {
                Debug.LogWarning($"[FileMgr] Initial inventory item not found: {initialItem.itemId}");
                continue;
            }

            int count = Mathf.Max(1, initialItem.count);
            if (itemConfig.itemType == ItemType.Weapon)
            {
                for (int weaponIndex = 0; weaponIndex < count; weaponIndex++)
                {
                    AddInitialInventoryItem(inventoryData, itemConfig, 1, occupiedSlotsByType);
                }

                continue;
            }

            InventoryItem existingItem = inventoryData.items.Find(item =>
                item != null &&
                item.itemId == itemConfig.id &&
                item.location == InventoryItemLocation.Inventory);

            if (existingItem != null)
            {
                existingItem.count += count;
                existingItem.isNew = true;
                continue;
            }

            AddInitialInventoryItem(inventoryData, itemConfig, count, occupiedSlotsByType);
        }
    }

    private void AddInitialInventoryItem(
        InventorySaveData inventoryData,
        Item itemConfig,
        int count,
        Dictionary<ItemType, HashSet<int>> occupiedSlotsByType)
    {
        inventoryData.items.Add(new InventoryItem
        {
            uid = System.Guid.NewGuid().ToString(),
            itemId = itemConfig.id,
            count = Mathf.Max(1, count),
            isNew = true,
            slotIndex = GetFirstInitialInventorySlot(inventoryData, itemConfig.itemType, occupiedSlotsByType),
            location = InventoryItemLocation.Inventory,
            locationKey = string.Empty
        });
    }

    private int GetFirstInitialInventorySlot(
        InventorySaveData inventoryData,
        ItemType itemType,
        Dictionary<ItemType, HashSet<int>> occupiedSlotsByType)
    {
        if (!occupiedSlotsByType.TryGetValue(itemType, out HashSet<int> occupiedSlots))
        {
            occupiedSlots = new HashSet<int>();
            occupiedSlotsByType[itemType] = occupiedSlots;

            for (int i = 0; i < inventoryData.items.Count; i++)
            {
                InventoryItem item = inventoryData.items[i];
                if (item == null || item.location != InventoryItemLocation.Inventory)
                {
                    continue;
                }

                Item existingConfig = ItemJsonDatabase.GetItem(item.itemId);
                if (existingConfig != null && existingConfig.itemType == itemType)
                {
                    occupiedSlots.Add(item.slotIndex);
                }
            }
        }

        int slotIndex = 0;
        while (occupiedSlots.Contains(slotIndex))
        {
            slotIndex++;
        }

        occupiedSlots.Add(slotIndex);
        return slotIndex;
    }

    private bool IsVisibleForCurrentAccount(GameFile gameFile)
    {
        if (gameFile == null)
        {
            return false;
        }

        string currentPlayerId = GetCurrentAccountPlayerId();
        return string.IsNullOrWhiteSpace(currentPlayerId) ||
            string.Equals(gameFile.ownerPlayerId, currentPlayerId, System.StringComparison.Ordinal);
    }

    private string GetCurrentAccountPlayerId()
    {
        return GameMgr.Account != null && GameMgr.Account.CurrentProfile != null
            ? GameMgr.Account.CurrentProfile.playerId
            : string.Empty;
    }
}

