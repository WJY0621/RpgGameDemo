using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameSound/GameSoundGroupData", fileName = "SoundGroup_")]
public class GameSoundGroupDataSO : ScriptableObject
{
    public string GroupName;
    public List<GameSound> GameSounds = new List<GameSound>();

    private Dictionary<string, GameSoundRuntimeEntry> gameSounds;

    private void OnValidate()
    {
        for (int i = 0; i < GameSounds.Count; i++)
        {
            GameSounds[i]?.EnsureDefaultPlaybackValues();
        }
    }

    public void Init()
    {
        gameSounds = new Dictionary<string, GameSoundRuntimeEntry>();
        for (int i = 0; i < GameSounds.Count; i++)
        {
            GameSound sound = GameSounds[i];
            if (sound == null || string.IsNullOrWhiteSpace(sound.soundName))
            {
                continue;
            }

            sound.EnsureDefaultPlaybackValues();
            if (!gameSounds.TryGetValue(sound.soundName, out GameSoundRuntimeEntry entry))
            {
                entry = new GameSoundRuntimeEntry(sound.soundName);
                gameSounds.Add(sound.soundName, entry);
            }

            entry.Add(sound);
        }
    }

    public AudioClip Get(string soundName)
    {
        GameSound sound = GetSound(soundName);
        return sound != null ? sound.GetClip() : null;
    }

    public GameSound GetSound(string soundName)
    {
        if (gameSounds == null)
        {
            Init();
        }

        if (string.IsNullOrWhiteSpace(soundName) || gameSounds == null)
        {
            return null;
        }

        if (gameSounds.TryGetValue(soundName, out GameSoundRuntimeEntry entry))
        {
            return entry.GetNext();
        }

        return null;
    }

    public bool ContainsSound(string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName))
        {
            return false;
        }

        if (gameSounds != null && gameSounds.ContainsKey(soundName))
        {
            return true;
        }

        for (int i = 0; i < GameSounds.Count; i++)
        {
            GameSound sound = GameSounds[i];
            if (sound != null && sound.soundName == soundName)
            {
                return true;
            }
        }

        return false;
    }
}

[System.Serializable]
public class GameSound
{
    public string soundName;
    public AudioClip audioClip;
    public List<AudioClip> variants = new List<AudioClip>();
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.5f)] public float pitchRandomRange;
    [Min(0f)] public float cooldown;
    public bool spatial;

    public AudioClip GetClip()
    {
        if (variants != null && variants.Count > 0)
        {
            int startIndex = variants.Count == 1 ? 0 : Random.Range(0, variants.Count);
            for (int i = 0; i < variants.Count; i++)
            {
                AudioClip clip = variants[(startIndex + i) % variants.Count];
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return audioClip;
    }

    public float GetPitch()
    {
        float random = pitchRandomRange > 0f ? Random.Range(-pitchRandomRange, pitchRandomRange) : 0f;
        return Mathf.Clamp(pitch + random, 0.1f, 3f);
    }

    public void EnsureDefaultPlaybackValues()
    {
        if (volume <= 0f)
        {
            volume = 1f;
        }

        if (pitch <= 0f)
        {
            pitch = 1f;
        }
    }
}

public sealed class GameSoundRuntimeEntry
{
    private readonly List<GameSound> sounds = new List<GameSound>();
    private int nextIndex;

    public string SoundName { get; }

    public GameSoundRuntimeEntry(string soundName)
    {
        SoundName = soundName;
    }

    public void Add(GameSound sound)
    {
        if (sound != null)
        {
            sounds.Add(sound);
        }
    }

    public GameSound GetNext()
    {
        if (sounds.Count == 0)
        {
            return null;
        }

        if (sounds.Count == 1)
        {
            return sounds[0];
        }

        GameSound sound = sounds[nextIndex % sounds.Count];
        nextIndex = (nextIndex + 1) % sounds.Count;
        return sound;
    }
}
