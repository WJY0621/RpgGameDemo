using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameSound/GameSoundData", fileName = "GameSoundData")]
public class GameSoundDataSO : ScriptableObject
{
    public List<GameSoundGroupDataSO> gameSoundGroups = new List<GameSoundGroupDataSO>();
    public List<SceneBGMSetting> sceneBGMs = new List<SceneBGMSetting>();

    private Dictionary<string, SceneBGMSetting> sceneBGMMap;

    public void Init()
    {
        sceneBGMMap = new Dictionary<string, SceneBGMSetting>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sceneBGMs.Count; i++)
        {
            SceneBGMSetting setting = sceneBGMs[i];
            if (setting == null || string.IsNullOrWhiteSpace(setting.sceneName) || string.IsNullOrWhiteSpace(setting.bgmName))
            {
                continue;
            }

            sceneBGMMap[setting.sceneName.Trim()] = setting;
        }
    }

    public bool TryGetSceneBGM(string sceneName, out SceneBGMSetting setting)
    {
        if (sceneBGMMap == null)
        {
            Init();
        }

        setting = null;
        return !string.IsNullOrWhiteSpace(sceneName) &&
               sceneBGMMap != null &&
               sceneBGMMap.TryGetValue(sceneName.Trim(), out setting);
    }
}

[Serializable]
public class SceneBGMSetting
{
    public string sceneName;
    public string bgmName;
    [Range(0f, 1f)] public float volume = 1f;
    [Min(0f)] public float fadeDuration = 1f;
}
