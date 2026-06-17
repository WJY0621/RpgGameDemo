using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameFile
{
    public string fileName;
    public string ownerPlayerId;
    public string playerName;
    public string createTime;
    public string lastScene;
    public string roleModelName;
    public int gold;
    public bool hasSavedGameTime;
    public float savedTimeInHours;
    public int savedDay = 1;
    public InventorySaveData inventoryData = new InventorySaveData();

    public List<PlayerSceneLocation> playerSceneLocations = new List<PlayerSceneLocation>();
    public PlayerData playerData;
    public TaskSystemSaveData taskSystemData = new TaskSystemSaveData();

    public void SetGameTime(TimeMgr timeMgr)
    {
        if (timeMgr == null)
        {
            return;
        }

        SetGameTime(timeMgr.CurrentDay, timeMgr.CurrentTime);
    }

    public void SetGameTime(int day, float timeInHours)
    {
        hasSavedGameTime = true;
        savedTimeInHours = Mathf.Clamp(timeInHours, 0f, 24f);
        savedDay = Mathf.Max(1, day);
    }

    public bool TryGetGameTime(out int day, out float timeInHours)
    {
        if (hasSavedGameTime)
        {
            day = Mathf.Max(1, savedDay);
            timeInHours = Mathf.Clamp(savedTimeInHours, 0f, 24f);
            return true;
        }

        day = 1;
        timeInHours = 0f;
        return false;
    }

    // 重生点：仅由床设置。注意不能复用 playerSceneLocations，
    // 因为每次 SaveGameFile -> UpdateFileData 都会用玩家当前位置覆盖那份场景位置。
    // 未建造床时 hasRespawnPoint 为 false，死亡复活时回退到初始点。
    public bool hasRespawnPoint;
    public string respawnScene;
    public Vector3 respawnPosition;
    public Quaternion respawnRotation = Quaternion.identity;

    /// <summary>
    /// 设置重生点（床调用）。
    /// </summary>
    public void SetRespawnPoint(string sceneName, Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return;
        }

        hasRespawnPoint = true;
        respawnScene = sceneName;
        respawnPosition = targetTransform.position;
        respawnRotation = targetTransform.rotation;
    }

    public void ClearRespawnPoint()
    {
        hasRespawnPoint = false;
        respawnScene = string.Empty;
        respawnPosition = Vector3.zero;
        respawnRotation = Quaternion.identity;
    }

    /// <summary>
    /// 尝试获取指定场景的重生点。只有当玩家建造过床且场景匹配时才返回 true。
    /// </summary>
    public bool TryGetRespawnPoint(string sceneName, out Vector3 position, out Quaternion rotation)
    {
        if (hasRespawnPoint && respawnScene == sceneName)
        {
            position = respawnPosition;
            rotation = respawnRotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    public bool TryGetSceneLocation(string sceneName, out Vector3 position, out Quaternion rotation)
    {
        if (playerSceneLocations != null)
        {
            for (int i = 0; i < playerSceneLocations.Count; i++)
            {
                if (playerSceneLocations[i].sceneName == sceneName)
                {
                    position = playerSceneLocations[i].position;
                    rotation = playerSceneLocations[i].rotation;
                    return true;
                }
            }
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    public Vector3 GetPositionOnSceneLoaded(string sceneName)
    {
        return TryGetSceneLocation(sceneName, out Vector3 position, out _) ? position : Vector3.zero;
    }

    public Quaternion GetRotationOnSceneLoaded(string sceneName)
    {
        return TryGetSceneLocation(sceneName, out _, out Quaternion rotation) ? rotation : Quaternion.identity;
    }

    public void SetLocationOnSceneLoaded(string sceneName, Transform targetTransform)
    {
        if (playerSceneLocations != null)
        {
            for (int i = 0; i < playerSceneLocations.Count; i++)
            {
                if (playerSceneLocations[i].sceneName == sceneName)
                {
                    playerSceneLocations[i].position = targetTransform.position;
                    playerSceneLocations[i].rotation = targetTransform.rotation;
                    return;
                }
            }
        }

        PlayerSceneLocation newData = new PlayerSceneLocation
        {
            sceneName = sceneName,
            position = targetTransform.position,
            rotation = targetTransform.rotation
        };

        playerSceneLocations.Add(newData);
    }
}

[System.Serializable]
public class PlayerSceneLocation
{
    public string sceneName;
    public Vector3 position;
    public Quaternion rotation;
}
