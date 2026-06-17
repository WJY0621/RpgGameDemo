using System.Collections.Generic;
using UnityEngine;

public class AudioMgr
{
    private const string BGMGroup = "BGM";
    private const string UIGroup = "UI";
    private const string DefaultEffectGroup = "Game";
    private const int DefaultPoolSize = 12;
    private const string MasterVolumeKey = "WorkDemo.Settings.Audio.MasterVolume";
    private const string BGMVolumeKey = "WorkDemo.Settings.Audio.BGMVolume";
    private const string SoundVolumeKey = "WorkDemo.Settings.Audio.SoundVolume";

    public AudioSource BGMSource;
    public AudioSource AmbientBGMSource;
    public AudioSource UIEffectSource;
    public AudioSource GameEffectSource;

    private readonly List<AudioSource> gameEffectPool = new List<AudioSource>();
    private readonly Dictionary<string, float> cooldownEndTimes = new Dictionary<string, float>();
    private readonly HashSet<string> warnedMissingAudioKeys = new HashSet<string>();
    private readonly Transform poolRoot;

    private GameSoundDataSO soundData;
    private string currentBGMName;
    private float bgmTargetVolume = 1f;
    private float bgmFadeDuration = 1f;
    private float bgmFadeTime;
    private float bgmFadeStartVolume;
    private bool bgmFading;
    private string currentAmbientBGMName;
    private float ambientBGMTargetVolume = 1f;
    private float ambientBGMFadeDuration = 1f;
    private float ambientBGMFadeTime;
    private float ambientBGMFadeStartVolume;
    private bool ambientBGMFading;
    private string pendingBGMName;
    private float pendingBGMVolume = 1f;
    private float pendingBGMFadeDuration = 1f;
    private string pendingAmbientBGMName;
    private float pendingAmbientBGMVolume = 1f;
    private float pendingAmbientBGMFadeDuration = 1f;
    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float soundVolume = 1f;

    public float MasterVolume => masterVolume;
    public float BGMVolume => bgmVolume;
    public float SoundVolume => soundVolume;
    public string CurrentBGMName => currentBGMName;
    public float CurrentBGMTargetVolume => bgmTargetVolume;

    public AudioMgr(AudioSource BGM, AudioSource UIEffect, AudioSource GameEffect)
    {
        BGMSource = BGM;
        UIEffectSource = UIEffect;
        GameEffectSource = GameEffect;
        LoadVolumeSettings();
        poolRoot = CreatePoolRoot();
        ConfigureSource(BGMSource, false);
        ConfigureSource(UIEffectSource, false);
        ConfigureSource(GameEffectSource, false);
        EnsurePool(DefaultPoolSize);
    }

    public void Init(GameSoundDataSO data)
    {
        soundData = data;
        soundData?.Init();

        if (!string.IsNullOrWhiteSpace(pendingBGMName))
        {
            string bgmName = pendingBGMName;
            float volume = pendingBGMVolume;
            float fadeDuration = pendingBGMFadeDuration;
            pendingBGMName = null;
            PlayBGM(bgmName, volume, fadeDuration);
        }

        if (!string.IsNullOrWhiteSpace(pendingAmbientBGMName))
        {
            string bgmName = pendingAmbientBGMName;
            float volume = pendingAmbientBGMVolume;
            float fadeDuration = pendingAmbientBGMFadeDuration;
            pendingAmbientBGMName = null;
            PlayAmbientBGM(bgmName, volume, fadeDuration);
        }
    }

    public void Tick(float deltaTime)
    {
        ProcessBGM(deltaTime);
        ProcessAmbientBGM(deltaTime);
    }

    public void PlayBGM(string name)
    {
        PlayBGM(name, 1f, 1f);
    }

    public void PlayBGM(string name, float volume, float fadeDuration)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            WarnAudioIssue("Invalid", BGMGroup, "Empty", "BGM request has no sound name.");
            return;
        }

        if (!IsSoundLibraryReady())
        {
            QueuePendingBGM(name, volume, fadeDuration);
            return;
        }

        AudioSource bgmSource = EnsureBGMSource();
        if (bgmSource == null)
        {
            return;
        }

        if (string.Equals(currentBGMName, name, System.StringComparison.Ordinal) && bgmSource.isPlaying)
        {
            FadeBGMTo(volume, fadeDuration);
            return;
        }

        AudioClip clip = GetClip(BGMGroup, name);
        if (clip == null)
        {
            WarnAudioIssue("BGM", BGMGroup, name, $"BGM not found: {name}");
            return;
        }

        currentBGMName = name;
        StopAmbientBGM(fadeDuration);
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.pitch = 1f;
        bgmSource.volume = fadeDuration > 0f ? 0f : GetEffectiveBGMVolume(volume);
        bgmSource.Play();
        FadeBGMTo(volume, fadeDuration);
    }

    public void PlaySceneBGM(string sceneName)
    {
        if (soundData == null || !soundData.TryGetSceneBGM(sceneName, out SceneBGMSetting setting))
        {
            return;
        }

        PlayBGM(setting.bgmName, setting.volume, setting.fadeDuration);
    }

    public void PlayAmbientBGM(string name)
    {
        PlayAmbientBGM(name, 1f, 1f);
    }

    public void PlayAmbientBGM(string name, float volume, float fadeDuration)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            WarnAudioIssue("Invalid", BGMGroup, "EmptyAmbient", "Ambient BGM request has no sound name.");
            return;
        }

        if (!IsSoundLibraryReady())
        {
            QueuePendingAmbientBGM(name, volume, fadeDuration);
            return;
        }

        AudioSource ambientSource = EnsureAmbientBGMSource();
        if (ambientSource == null)
        {
            return;
        }

        if (string.Equals(currentAmbientBGMName, name, System.StringComparison.Ordinal) && ambientSource.isPlaying)
        {
            FadeAmbientBGMTo(volume, fadeDuration);
            return;
        }

        AudioClip clip = GetClip(BGMGroup, name);
        if (clip == null)
        {
            WarnAudioIssue("BGM", BGMGroup, name, $"BGM not found: {name}");
            return;
        }

        currentAmbientBGMName = name;
        ambientSource.Stop();
        ambientSource.clip = clip;
        ambientSource.loop = true;
        ambientSource.pitch = 1f;
        ambientSource.volume = fadeDuration > 0f ? 0f : GetEffectiveBGMVolume(volume);
        ambientSource.Play();
        FadeAmbientBGMTo(volume, fadeDuration);
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        currentBGMName = string.Empty;
        FadeBGMTo(0f, fadeDuration);
        StopAmbientBGM(fadeDuration);
    }

    public void StopAmbientBGM(float fadeDuration = 0.5f)
    {
        currentAmbientBGMName = string.Empty;
        FadeAmbientBGMTo(0f, fadeDuration);
    }

    public void ProcessBGM()
    {
        ProcessBGM(Time.deltaTime);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
        ApplyCurrentVolumesToLiveSources();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
        ApplyCurrentVolumesToLiveSources();
    }

    public void SetSoundVolume(float volume)
    {
        soundVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
    }

    public void PlayUIEffect(string soundName, float volume = 1f)
    {
        PlayEffect(UIEffectSource, UIGroup, soundName, volume);
    }

    public void PlayEffect(string soundName, float volume = 1f)
    {
        PlayEffect(GameEffectSource, DefaultEffectGroup, soundName, volume);
    }

    public void PlayEffect(string groupName, string soundName, float volume = 1f)
    {
        PlayEffect(GameEffectSource, groupName, soundName, volume);
    }

    public void PlayPooledEffect(string groupName, string soundName, float volume = 1f)
    {
        PlayPooledEffectInternal(groupName, soundName, false, Vector3.zero, volume);
    }

    public void PlayPooledEffectAt(string groupName, string soundName, Vector3 position, float volume = 1f)
    {
        PlayPooledEffectInternal(groupName, soundName, true, position, volume);
    }

    public void PlayEffect(AudioSource audioSource, string groupName, string soundName, float volume = 1f)
    {
        GameSound sound = GetSound(groupName, soundName);
        if (sound == null)
        {
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            WarnAudioIssue("Clip", groupName, soundName, $"Sound has no AudioClip: {groupName}/{soundName}");
            return;
        }

        if (IsCoolingDown(groupName, soundName, sound.cooldown))
        {
            return;
        }

        AudioSource source = audioSource != null ? audioSource : GetAvailableGameEffectSource();
        PlayClip(source, clip, volume * sound.volume, sound.GetPitch(), false, Vector3.zero, sound.spatial);
    }

    private void PlayPooledEffectInternal(string groupName, string soundName, bool atPosition, Vector3 position, float volume)
    {
        GameSound sound = GetSound(groupName, soundName);
        if (sound == null)
        {
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            WarnAudioIssue("Clip", groupName, soundName, $"Sound has no AudioClip: {groupName}/{soundName}");
            return;
        }

        if (IsCoolingDown(groupName, soundName, sound.cooldown))
        {
            return;
        }

        PlayClip(GetAvailableGameEffectSource(), clip, volume * sound.volume, sound.GetPitch(), atPosition, position, sound.spatial);
    }

    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        PlayClip(GetAvailableGameEffectSource(), clip, volume, pitch, false, Vector3.zero, false);
    }

    public void PlayAt(string groupName, string soundName, Vector3 position, float volume = 1f)
    {
        GameSound sound = GetSound(groupName, soundName);
        if (sound == null)
        {
            return;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            WarnAudioIssue("Clip", groupName, soundName, $"Sound has no AudioClip: {groupName}/{soundName}");
            return;
        }

        if (IsCoolingDown(groupName, soundName, sound.cooldown))
        {
            return;
        }

        PlayClip(GetAvailableGameEffectSource(), clip, volume * sound.volume, sound.GetPitch(), true, position, true);
    }

    public void PlayClipAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        PlayClip(GetAvailableGameEffectSource(), clip, volume, pitch, true, position, true);
    }

    public bool PlayLoopingEffect(AudioSource source, string groupName, string soundName, float volume = 1f)
    {
        if (source == null)
        {
            WarnAudioIssue("Source", groupName, soundName, "No AudioSource is available for looping playback.");
            return false;
        }

        GameSound sound = GetSound(groupName, soundName);
        if (sound == null)
        {
            return false;
        }

        AudioClip clip = sound.GetClip();
        if (clip == null)
        {
            WarnAudioIssue("Clip", groupName, soundName, $"Sound has no AudioClip: {groupName}/{soundName}");
            return false;
        }

        ConfigureSource(source, sound.spatial);
        source.loop = true;
        source.volume = GetEffectiveSoundVolume(volume * sound.volume);
        source.pitch = Mathf.Clamp(sound.GetPitch(), 0.1f, 3f);

        if (source.clip != clip)
        {
            source.Stop();
            source.clip = clip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }

        return true;
    }

    public void StopLoopingEffect(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.loop = false;
        source.clip = null;
    }

    public bool TryResolveSoundGroup(string preferredGroupName, string soundName, out string resolvedGroupName)
    {
        resolvedGroupName = string.Empty;
        if (GameMgr.Instance == null ||
            GameMgr.Instance.gameSoundDataDic == null ||
            string.IsNullOrWhiteSpace(soundName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(preferredGroupName) &&
            GameMgr.Instance.gameSoundDataDic.TryGetValue(preferredGroupName, out GameSoundGroupDataSO preferredGroup) &&
            preferredGroup != null &&
            preferredGroup.ContainsSound(soundName))
        {
            resolvedGroupName = preferredGroupName;
            return true;
        }

        foreach (KeyValuePair<string, GameSoundGroupDataSO> pair in GameMgr.Instance.gameSoundDataDic)
        {
            GameSoundGroupDataSO group = pair.Value;
            if (group != null && group.ContainsSound(soundName))
            {
                resolvedGroupName = pair.Key;
                return true;
            }
        }

        return false;
    }

    private void ProcessBGM(float deltaTime)
    {
        if (!bgmFading || BGMSource == null)
        {
            return;
        }

        if (bgmFadeDuration <= 0f)
        {
            BGMSource.volume = GetEffectiveBGMVolume(bgmTargetVolume);
            bgmFading = false;
            if (BGMSource.volume <= 0f)
            {
                BGMSource.Stop();
            }

            return;
        }

        bgmFadeTime += Mathf.Max(0f, deltaTime);
        float t = Mathf.Clamp01(bgmFadeTime / bgmFadeDuration);
        BGMSource.volume = Mathf.Lerp(bgmFadeStartVolume, GetEffectiveBGMVolume(bgmTargetVolume), t);
        if (t >= 1f)
        {
            bgmFading = false;
            if (BGMSource.volume <= 0f)
            {
                BGMSource.Stop();
            }
        }
    }

    private void ProcessAmbientBGM(float deltaTime)
    {
        if (!ambientBGMFading || AmbientBGMSource == null)
        {
            return;
        }

        if (ambientBGMFadeDuration <= 0f)
        {
            AmbientBGMSource.volume = GetEffectiveBGMVolume(ambientBGMTargetVolume);
            ambientBGMFading = false;
            if (AmbientBGMSource.volume <= 0f)
            {
                AmbientBGMSource.Stop();
            }

            return;
        }

        ambientBGMFadeTime += Mathf.Max(0f, deltaTime);
        float t = Mathf.Clamp01(ambientBGMFadeTime / ambientBGMFadeDuration);
        AmbientBGMSource.volume = Mathf.Lerp(ambientBGMFadeStartVolume, GetEffectiveBGMVolume(ambientBGMTargetVolume), t);
        if (t >= 1f)
        {
            ambientBGMFading = false;
            if (AmbientBGMSource.volume <= 0f)
            {
                AmbientBGMSource.Stop();
            }
        }
    }

    private void FadeBGMTo(float volume, float fadeDuration)
    {
        if (BGMSource == null)
        {
            return;
        }

        bgmTargetVolume = Mathf.Clamp01(volume);
        bgmFadeDuration = Mathf.Max(0f, fadeDuration);
        bgmFadeTime = 0f;
        bgmFadeStartVolume = BGMSource.volume;
        bgmFading = true;
    }

    private void FadeAmbientBGMTo(float volume, float fadeDuration)
    {
        if (AmbientBGMSource == null)
        {
            return;
        }

        ambientBGMTargetVolume = Mathf.Clamp01(volume);
        ambientBGMFadeDuration = Mathf.Max(0f, fadeDuration);
        ambientBGMFadeTime = 0f;
        ambientBGMFadeStartVolume = AmbientBGMSource.volume;
        ambientBGMFading = true;
    }

    private void QueuePendingBGM(string name, float volume, float fadeDuration)
    {
        pendingBGMName = name;
        pendingBGMVolume = volume;
        pendingBGMFadeDuration = fadeDuration;
        WarnAudioIssue("Pending", BGMGroup, name, $"BGM requested before sound data was ready. It will play after audio initialization: {name}");
    }

    private void QueuePendingAmbientBGM(string name, float volume, float fadeDuration)
    {
        pendingAmbientBGMName = name;
        pendingAmbientBGMVolume = volume;
        pendingAmbientBGMFadeDuration = fadeDuration;
        WarnAudioIssue("Pending", BGMGroup, name, $"Ambient BGM requested before sound data was ready. It will play after audio initialization: {name}");
    }

    private bool IsSoundLibraryReady()
    {
        return GameMgr.Instance != null &&
               GameMgr.Instance.gameSoundDataDic != null &&
               GameMgr.Instance.gameSoundDataDic.Count > 0;
    }

    private AudioClip GetClip(string groupName, string soundName)
    {
        GameSound sound = GetSound(groupName, soundName);
        return sound != null ? sound.GetClip() : null;
    }

    private GameSound GetSound(string groupName, string soundName)
    {
        if (GameMgr.Instance == null ||
            GameMgr.Instance.gameSoundDataDic == null ||
            string.IsNullOrWhiteSpace(groupName) ||
            string.IsNullOrWhiteSpace(soundName))
        {
            WarnAudioIssue("Invalid", groupName, soundName, "Audio request is invalid or GameMgr is not ready.");
            return null;
        }

        if (!GameMgr.Instance.gameSoundDataDic.TryGetValue(groupName, out GameSoundGroupDataSO group) || group == null)
        {
            WarnAudioIssue("Group", groupName, soundName, $"Sound group not found: {groupName}");
            return null;
        }

        GameSound sound = group.GetSound(soundName);
        if (sound == null)
        {
            WarnAudioIssue("Sound", groupName, soundName, $"Sound not found: {groupName}/{soundName}");
        }

        return sound;
    }

    private bool IsCoolingDown(string groupName, string soundName, float cooldown)
    {
        if (cooldown <= 0f)
        {
            return false;
        }

        string key = groupName + "/" + soundName;
        if (cooldownEndTimes.TryGetValue(key, out float endTime) && Time.unscaledTime < endTime)
        {
            return true;
        }

        cooldownEndTimes[key] = Time.unscaledTime + cooldown;
        return false;
    }

    private void PlayClip(AudioSource source, AudioClip clip, float volume, float pitch, bool atPosition, Vector3 position, bool spatial)
    {
        if (source == null || clip == null)
        {
            if (clip == null)
            {
                WarnAudioIssue("Clip", "DirectClip", "Null", "Audio clip is null.");
            }

            if (source == null)
            {
                WarnAudioIssue("Source", "AudioSource", "Null", "No AudioSource is available for playback.");
            }

            return;
        }

        ConfigureSource(source, spatial);
        source.transform.position = atPosition ? position : (GameEffectSource != null ? GameEffectSource.transform.position : Vector3.zero);
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        source.PlayOneShot(clip, GetEffectiveSoundVolume(volume));
    }

    private void LoadVolumeSettings()
    {
        masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BGMVolumeKey, 1f));
        soundVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SoundVolumeKey, 1f));
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.SetFloat(BGMVolumeKey, bgmVolume);
        PlayerPrefs.SetFloat(SoundVolumeKey, soundVolume);
        PlayerPrefs.Save();
    }

    private void ApplyCurrentVolumesToLiveSources()
    {
        if (BGMSource != null && !bgmFading)
        {
            BGMSource.volume = GetEffectiveBGMVolume(bgmTargetVolume);
        }

        if (AmbientBGMSource != null && !ambientBGMFading)
        {
            AmbientBGMSource.volume = GetEffectiveBGMVolume(ambientBGMTargetVolume);
        }
    }

    private float GetEffectiveBGMVolume(float sourceVolume)
    {
        return Mathf.Clamp01(sourceVolume) * masterVolume * bgmVolume;
    }

    private float GetEffectiveSoundVolume(float sourceVolume)
    {
        return Mathf.Clamp01(sourceVolume) * masterVolume * soundVolume;
    }

    private void WarnAudioIssue(string issueType, string groupName, string soundName, string message)
    {
        string key = issueType + "/" + groupName + "/" + soundName;
        if (warnedMissingAudioKeys.Contains(key))
        {
            return;
        }

        warnedMissingAudioKeys.Add(key);
        Debug.LogWarning("[AudioMgr] " + message);
    }

    private AudioSource GetAvailableGameEffectSource()
    {
        EnsurePool(DefaultPoolSize);
        for (int i = 0; i < gameEffectPool.Count; i++)
        {
            AudioSource source = gameEffectPool[i];
            if (source != null && !source.isPlaying)
            {
                return source;
            }
        }

        return CreatePooledSource();
    }

    private AudioSource EnsureBGMSource()
    {
        if (BGMSource != null)
        {
            return BGMSource;
        }

        GameObject go = new GameObject("BGMSource_Runtime");
        go.transform.SetParent(poolRoot, false);
        BGMSource = go.AddComponent<AudioSource>();
        ConfigureSource(BGMSource, false);
        BGMSource.loop = true;
        return BGMSource;
    }

    private AudioSource EnsureAmbientBGMSource()
    {
        if (AmbientBGMSource != null)
        {
            return AmbientBGMSource;
        }

        GameObject go = new GameObject("AmbientBGMSource_Runtime");
        go.transform.SetParent(poolRoot, false);
        AmbientBGMSource = go.AddComponent<AudioSource>();
        ConfigureSource(AmbientBGMSource, false);
        AmbientBGMSource.loop = true;
        return AmbientBGMSource;
    }

    private void EnsurePool(int count)
    {
        while (gameEffectPool.Count < count)
        {
            CreatePooledSource();
        }
    }

    private AudioSource CreatePooledSource()
    {
        GameObject go = new GameObject("GameEffectSource_" + gameEffectPool.Count);
        go.transform.SetParent(poolRoot, false);
        AudioSource source = go.AddComponent<AudioSource>();
        ConfigureSource(source, false);
        gameEffectPool.Add(source);
        return source;
    }

    private static Transform CreatePoolRoot()
    {
        GameObject go = new GameObject("AudioMgr_RuntimePool");
        Object.DontDestroyOnLoad(go);
        return go.transform;
    }

    private static void ConfigureSource(AudioSource source, bool spatial)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }
}
