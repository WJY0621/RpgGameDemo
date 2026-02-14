using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 存档信息
/// </summary> 
[System.Serializable]
public class GameFile
{
    //名称
    public string fileName;
    //创建时间
    public string createTime;
    //最后场景
    public string lastScene;

    public List<PlayerSceneLocation> playerSceneLocations = new List<PlayerSceneLocation>();
    public PlayerData playerData;

    //获取玩家在场景中生成的位置
    public Vector3 GetPositionOnSceneLoaded(string sceneName)
    {
        for(int i = 0; i < playerSceneLocations.Count; i++)
        {
            if(playerSceneLocations[i].sceneName == sceneName)
            {
                return playerSceneLocations[i].position;
            }
        }
        return Vector3.zero;
    }
    public Quaternion GetRotationOnSceneLoaded(string sceneName)
    {
        for (int i = 0; i < playerSceneLocations.Count; i++)
        {
            if (playerSceneLocations[i].sceneName == sceneName)
            {
                return playerSceneLocations[i].rotation;
            }
        }
        return Quaternion.identity;
    }

    public void SetLocationOnSceneLoaded(string sceneName,Transform transform)
    {
        for (int i = 0; i < playerSceneLocations.Count; i++)
        {
            if (playerSceneLocations[i].sceneName == sceneName)
            {
                playerSceneLocations[i].position = transform.position;
                playerSceneLocations[i].rotation = transform.rotation;
                return;
            }
        }
        //不存在则添加
        PlayerSceneLocation newData = new PlayerSceneLocation();
        newData.sceneName = sceneName;
        newData.position = transform.position;
        newData.rotation = transform.rotation;
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


