using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioMgr
{
    public AudioSource BGMSource;
    public AudioSource UIEffectSource;
    public AudioSource GameEffectSource;

    private bool beginBGM;

    public AudioMgr(AudioSource BGM, AudioSource UIEffect, AudioSource GameEffect)
    {
        BGMSource = BGM;
        UIEffectSource = UIEffect;
        GameEffectSource = GameEffect;
    }

    public void PlayBGM(string name)
    {
        if (BGMSource != null)
        {
            BGMSource.Stop();
            AudioClip BGMclip = GameMgr.Instance.gameSoundDataDic["BGM"].Get(name);
            if (BGMclip != null)
                BGMSource.clip = BGMclip;
            BGMSource.Play();
            BGMSource.volume = 0.5f;
            beginBGM = true;
        }
    }

    public void ProcessBGM()
    {
        if (beginBGM)
        {
            BGMSource.volume += Time.deltaTime;
            if (BGMSource.volume >= 1)
            {
                beginBGM = false;
            }
        }
        else
        {

        }
    }

    public void PlayUIEffect(string soundName, float volume = 1f)
    {
        if(UIEffectSource != null)
        {
            AudioClip clip = GameMgr.Instance.gameSoundDataDic["UI"].Get(soundName);
            if(clip is not null)
            {
                UIEffectSource.volume = volume;
                UIEffectSource.PlayOneShot(clip);
            }
        }
    }

    public void PlayEffect(AudioSource audioSource, string groupName, string soundName, float volume = 1f)
    {
        if(audioSource != null)
        {
            if (GameMgr.Instance.gameSoundDataDic.ContainsKey(groupName))
            {
                AudioClip clip = GameMgr.Instance.gameSoundDataDic[groupName].Get(soundName);
                if(clip is not null)
                {
                    audioSource.volume = volume;
                    audioSource.PlayOneShot(clip);
                }
            }
        }
    }
}
