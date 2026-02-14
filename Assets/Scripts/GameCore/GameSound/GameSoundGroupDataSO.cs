using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameSound/GameSoundGroupData", fileName = "SoundGroup_")]
public class GameSoundGroupDataSO : ScriptableObject
{
    public string GroupName;
    public List<GameSound> GameSounds;

    private Dictionary<string, AudioClip> gameSounds;

    public void Init()
    {
        gameSounds = new Dictionary<string, AudioClip>();
        foreach (var sound in GameSounds)
        {
            gameSounds.Add(sound.soundName, sound.audioClip);
        }
    }

    public AudioClip Get(string soundName)
    {
        if (gameSounds.ContainsKey(soundName))
        {
            return gameSounds[soundName];
        }
        return null;
    }
}

[System.Serializable]
public class GameSound
{
    public string soundName;
    public AudioClip audioClip;
}
