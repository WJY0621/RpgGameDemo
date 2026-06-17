using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void DrawBottomBar()
    {
        Rect rect = new Rect(0f, position.height - BottomHeight, position.width, BottomHeight);
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f));
        DrawBorder(rect, new Color(0.08f, 0.08f, 0.08f));

        Rect row = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f);
        GUI.Label(new Rect(row.x, row.y + 2f, row.width - 380f, row.height), currentAsset != null ? GetAssetPreviewPath() : "请选择或新建一个武器攻击效果 SO。", miniDimStyle);

        GUI.enabled = currentAsset != null;
        if (GUI.Button(new Rect(row.xMax - 352f, row.y, 108f, row.height), "同步总时长"))
        {
            currentAsset.SyncDurationFromClips();
            playheadTime = Mathf.Clamp(playheadTime, 0f, currentAsset.totalDuration);
            EditorUtility.SetDirty(currentAsset);
        }

        if (GUI.Button(new Rect(row.xMax - 236f, row.y, 108f, row.height), "保存修改"))
        {
            SaveCurrentAssetChanges();
        }

        GUI.backgroundColor = new Color(0.24f, 0.58f, 0.32f);
        if (GUI.Button(new Rect(row.xMax - 108f, row.y, 108f, row.height), "保存 SO"))
        {
            SaveAsset();
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void ShowCreateTrackMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("新建轨道/动画轨道"), false, () => CreateTrack(WeaponAttackTrackType.Animation));
        menu.AddItem(new GUIContent("新建轨道/音效轨道"), false, () => CreateTrack(WeaponAttackTrackType.Audio));
        menu.AddItem(new GUIContent("新建轨道/特效轨道"), false, () => CreateTrack(WeaponAttackTrackType.Effect));
        menu.AddItem(new GUIContent("新建轨道/攻击盒轨道"), false, () => CreateTrack(WeaponAttackTrackType.Hitbox));
        menu.AddItem(new GUIContent("新建轨道/自定义轨道"), false, () => CreateTrack(WeaponAttackTrackType.Custom));
        menu.ShowAsContext();
    }

    private void ShowTrackContextMenu(int index, WeaponAttackTrackData track)
    {
        GenericMenu menu = new GenericMenu();
        if (track.type == WeaponAttackTrackType.Effect)
        {
            menu.AddItem(new GUIContent("添加片段/自带位移特效"), false, () => AddEffectClip(track, null, playheadTime));
            menu.AddItem(new GUIContent("添加片段/弹幕触发器"), false, () => AddProjectileEffectClip(track, playheadTime));
        }
        else
        {
            menu.AddItem(new GUIContent("添加片段"), false, () => AddEmptyClip(track));
        }
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent(track.hiddenInPreview ? "显示轨道预览" : "隐藏轨道预览"), false, () => ToggleTrackPreviewVisibility(track));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("新建轨道/动画轨道"), false, () => CreateTrack(WeaponAttackTrackType.Animation));
        menu.AddItem(new GUIContent("新建轨道/音效轨道"), false, () => CreateTrack(WeaponAttackTrackType.Audio));
        menu.AddItem(new GUIContent("新建轨道/特效轨道"), false, () => CreateTrack(WeaponAttackTrackType.Effect));
        menu.AddItem(new GUIContent("新建轨道/攻击盒轨道"), false, () => CreateTrack(WeaponAttackTrackType.Hitbox));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("删除轨道"), false, () => DeleteTrack(index));
        menu.ShowAsContext();
    }

    private void ToggleTrackPreviewVisibility(WeaponAttackTrackData track)
    {
        if (track == null)
        {
            return;
        }

        track.hiddenInPreview = !track.hiddenInPreview;
        CleanupPreviewVfxInstances();
        EditorUtility.SetDirty(currentAsset);
        SceneView.RepaintAll();
        Repaint();
    }

    private void ShowClipContextMenu(SelectedClip clip)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("跳到该片段"), false, () =>
        {
            playheadTime = GetClipStartTime(clip);
            Repaint();
        });
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("删除片段"), false, () => DeleteClip(clip));
        menu.ShowAsContext();
    }

    private void ShowAnimationKeyframeContextMenu(int index)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("跳到该关键帧"), false, () =>
        {
            if (IsValidIndex(currentAsset.animationKeyframes, index))
            {
                playheadTime = currentAsset.animationKeyframes[index].time;
                selectedAnimationKeyframeIndex = index;
                selectedClip = SelectedClip.None;
                Repaint();
            }
        });
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("删除关键帧"), false, () => DeleteAnimationKeyframe(index));
        menu.ShowAsContext();
    }

    private void CreateTrack(WeaponAttackTrackType type)
    {
        if (currentAsset == null)
        {
            CreateEmptyAssetInMemory();
        }

        WeaponAttackTrackData track = new WeaponAttackTrackData
        {
            id = Guid.NewGuid().ToString("N"),
            type = type,
            displayName = GetDefaultTrackName(type),
            color = GetDefaultTrackColor(type)
        };

        currentAsset.tracks.Add(track);
        selectedTrackIndex = currentAsset.tracks.Count - 1;
        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = -1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void DeleteTrack(int index)
    {
        if (index < 0 || index >= currentAsset.tracks.Count)
        {
            return;
        }

        string trackId = currentAsset.tracks[index].id;
        currentAsset.tracks.RemoveAt(index);
        currentAsset.animationEvents.RemoveAll(clip => clip != null && clip.trackId == trackId);
        currentAsset.animationKeyframes.RemoveAll(keyframe => keyframe != null && keyframe.trackId == trackId);
        currentAsset.audioEvents.RemoveAll(clip => clip != null && clip.trackId == trackId);
        currentAsset.vfxEvents.RemoveAll(clip => clip != null && clip.trackId == trackId);
        currentAsset.hitboxEvents.RemoveAll(clip => clip != null && clip.trackId == trackId);

        selectedTrackIndex = Mathf.Clamp(index - 1, -1, currentAsset.tracks.Count - 1);
        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = -1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void AddEmptyClip(WeaponAttackTrackData track)
    {
        switch (track.type)
        {
            case WeaponAttackTrackType.Animation:
                AddAnimationClip(track, null, playheadTime);
                break;
            case WeaponAttackTrackType.Audio:
                AddAudioClip(track, null, playheadTime);
                break;
            case WeaponAttackTrackType.Effect:
                AddEffectClip(track, null, playheadTime);
                break;
            case WeaponAttackTrackType.Hitbox:
                AddHitboxClip(track, playheadTime);
                break;
        }
    }

    private bool AddAnimationClip(WeaponAttackTrackData track, AnimationClip animationClip, float time)
    {
        float clipDuration = animationClip != null ? Mathf.Max(0.01f, animationClip.length) : 0.5f;
        if (WouldFullyOverlapClip(ClipKind.Animation, -1, track.id, time, clipDuration))
        {
            return false;
        }

        WeaponAnimationEvent clip = new WeaponAnimationEvent
        {
            trackId = track.id,
            label = animationClip != null ? animationClip.name : "Animation Clip",
            animationClip = animationClip,
            startTime = time,
            duration = clipDuration
        };

        currentAsset.animationEvents.Add(clip);
        selectedClip = new SelectedClip(ClipKind.Animation, currentAsset.animationEvents.Count - 1);
        SelectTrack(track.id);
        ExtendDurationIfNeeded(time + clip.duration);
        EditorUtility.SetDirty(currentAsset);
        return true;
    }

    private void AddAnimationKeyframe(WeaponAttackTrackData track)
    {
        if (track == null || currentAsset == null || track.type != WeaponAttackTrackType.Animation)
        {
            return;
        }

        currentAsset.animationKeyframes.Add(new WeaponAnimationKeyframeEvent
        {
            trackId = track.id,
            label = "Keyframe",
            time = Mathf.Max(0f, playheadTime)
        });

        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = currentAsset.animationKeyframes.Count - 1;
        ExtendDurationIfNeeded(playheadTime);
        SelectTrack(track.id);
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void KeyHitboxApplyDamageTime(WeaponAttackTrackData track)
    {
        if (track == null || currentAsset == null || track.type != WeaponAttackTrackType.Hitbox)
        {
            return;
        }

        int hitboxIndex = FindHitboxForApplyDamageKey(track);
        if (!IsValidIndex(currentAsset.hitboxEvents, hitboxIndex))
        {
            AddHitboxClip(track, playheadTime);
            hitboxIndex = currentAsset.hitboxEvents.Count - 1;
        }

        WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[hitboxIndex];
        float keyTime = Mathf.Max(0f, playheadTime);
        if (keyTime < hitbox.beginWindowTime)
        {
            hitbox.beginWindowTime = keyTime;
        }

        if (keyTime > hitbox.endWindowTime)
        {
            hitbox.endWindowTime = keyTime;
        }

        hitbox.applyDamageTime = Mathf.Clamp(keyTime, hitbox.beginWindowTime, hitbox.endWindowTime);
        selectedClip = new SelectedClip(ClipKind.Hitbox, hitboxIndex);
        selectedAnimationKeyframeIndex = -1;
        SelectTrack(track.id);
        ExtendDurationIfNeeded(hitbox.endWindowTime);
        EditorUtility.SetDirty(currentAsset);
        Repaint();
        SceneView.RepaintAll();
    }

    private int FindHitboxForApplyDamageKey(WeaponAttackTrackData track)
    {
        if (selectedClip.kind == ClipKind.Hitbox &&
            IsValidIndex(currentAsset.hitboxEvents, selectedClip.index) &&
            currentAsset.hitboxEvents[selectedClip.index].trackId == track.id)
        {
            return selectedClip.index;
        }

        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[i];
            if (hitbox == null || hitbox.trackId != track.id)
            {
                continue;
            }

            if (playheadTime >= hitbox.beginWindowTime && playheadTime <= hitbox.endWindowTime)
            {
                return i;
            }
        }

        float bestDistance = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[i];
            if (hitbox == null || hitbox.trackId != track.id)
            {
                continue;
            }

            float distance = Mathf.Min(Mathf.Abs(playheadTime - hitbox.beginWindowTime), Mathf.Abs(playheadTime - hitbox.endWindowTime));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void AddAudioClip(WeaponAttackTrackData track, AudioClip audioClip, float time)
    {
        WeaponAudioEvent clip = new WeaponAudioEvent
        {
            trackId = track.id,
            label = audioClip != null ? audioClip.name : "Audio Clip",
            soundGroup = "Game",
            soundName = audioClip != null ? audioClip.name : string.Empty,
            audioClip = audioClip,
            triggerTime = time,
            duration = Mathf.Max(0.2f, audioClip != null ? audioClip.length : 0.2f),
            volume = 1f,
            pitch = 1f
        };

        currentAsset.audioEvents.Add(clip);
        selectedClip = new SelectedClip(ClipKind.Audio, currentAsset.audioEvents.Count - 1);
        SelectTrack(track.id);
        ExtendDurationIfNeeded(time + clip.duration);
        EditorUtility.SetDirty(currentAsset);
    }

    private void AddEffectClip(WeaponAttackTrackData track, GameObject prefab, float time)
    {
        WeaponVFXEvent clip = new WeaponVFXEvent
        {
            trackId = track.id,
            label = prefab != null ? prefab.name : "Effect Clip",
            clipType = WeaponVFXClipType.SelfMotionEffect,
            vfxKey = prefab != null ? prefab.name : string.Empty,
            vfxPrefab = prefab,
            triggerTime = time,
            duration = 0.5f,
            attachToWeapon = true
        };

        currentAsset.vfxEvents.Add(clip);
        selectedClip = new SelectedClip(ClipKind.Effect, currentAsset.vfxEvents.Count - 1);
        SelectTrack(track.id);
        ExtendDurationIfNeeded(time + clip.duration);
        EditorUtility.SetDirty(currentAsset);
    }

    private void AddProjectileEffectClip(WeaponAttackTrackData track, float time)
    {
        WeaponVFXEvent clip = new WeaponVFXEvent
        {
            trackId = track.id,
            label = "Projectile",
            clipType = WeaponVFXClipType.ProjectileTrigger,
            triggerTime = time,
            duration = 0.05f,
            attachToWeapon = true,
            projectileMotionMode = WeaponProjectileMotionMode.CodeDriven,
            projectileSpeed = 12f,
            projectileLifeTime = 2f,
            projectileHitShape = WeaponProjectileHitShape.Sphere,
            projectileHitRadius = 0.35f,
            projectileHitHeight = 1f,
            damageMultiplier = 1f
        };

        currentAsset.vfxEvents.Add(clip);
        selectedClip = new SelectedClip(ClipKind.Effect, currentAsset.vfxEvents.Count - 1);
        SelectTrack(track.id);
        ExtendDurationIfNeeded(time);
        EditorUtility.SetDirty(currentAsset);
    }

    private void AddHitboxClip(WeaponAttackTrackData track, float time)
    {
        WeaponHitboxEvent clip = new WeaponHitboxEvent
        {
            trackId = track.id,
            label = "Hitbox Clip",
            beginWindowTime = time,
            applyDamageTime = time,
            endWindowTime = Mathf.Min(time + 0.25f, Mathf.Max(time + 0.25f, currentAsset.totalDuration)),
            offset = new Vector3(0f, 1f, 1.2f),
            radius = 1.2f
        };

        currentAsset.hitboxEvents.Add(clip);
        selectedClip = new SelectedClip(ClipKind.Hitbox, currentAsset.hitboxEvents.Count - 1);
        SelectTrack(track.id);
        ExtendDurationIfNeeded(clip.endWindowTime);
        EditorUtility.SetDirty(currentAsset);
    }

    private void ExtendDurationIfNeeded(float endTime)
    {
        if (endTime > currentAsset.totalDuration)
        {
            currentAsset.totalDuration = Mathf.Max(0.01f, endTime);
        }
    }

    private void SelectTrack(string trackId)
    {
        selectedTrackIndex = -1;
        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            if (currentAsset.tracks[i] != null && currentAsset.tracks[i].id == trackId)
            {
                selectedTrackIndex = i;
                return;
            }
        }
    }

    private int FindTrackIndexForClip(SelectedClip clip)
    {
        string trackId = GetClipTrackId(clip);
        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            if (currentAsset.tracks[i] != null && currentAsset.tracks[i].id == trackId)
            {
                return i;
            }
        }

        return -1;
    }

    private string GetClipTrackId(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                return IsValidIndex(currentAsset.animationEvents, clip.index) ? currentAsset.animationEvents[clip.index].trackId : string.Empty;
            case ClipKind.Audio:
                return IsValidIndex(currentAsset.audioEvents, clip.index) ? currentAsset.audioEvents[clip.index].trackId : string.Empty;
            case ClipKind.Effect:
                return IsValidIndex(currentAsset.vfxEvents, clip.index) ? currentAsset.vfxEvents[clip.index].trackId : string.Empty;
            case ClipKind.Hitbox:
                return IsValidIndex(currentAsset.hitboxEvents, clip.index) ? currentAsset.hitboxEvents[clip.index].trackId : string.Empty;
            default:
                return string.Empty;
        }
    }

    private void SetClipTrackId(SelectedClip clip, string trackId)
    {
        if (string.IsNullOrEmpty(trackId))
        {
            return;
        }

        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index))
                {
                    currentAsset.animationEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index))
                {
                    currentAsset.audioEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index))
                {
                    currentAsset.vfxEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index))
                {
                    currentAsset.hitboxEvents[clip.index].trackId = trackId;
                }
                break;
        }
    }

    private float GetClipStartTime(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                return IsValidIndex(currentAsset.animationEvents, clip.index) ? currentAsset.animationEvents[clip.index].startTime : 0f;
            case ClipKind.Audio:
                return IsValidIndex(currentAsset.audioEvents, clip.index) ? currentAsset.audioEvents[clip.index].triggerTime : 0f;
            case ClipKind.Effect:
                return IsValidIndex(currentAsset.vfxEvents, clip.index) ? currentAsset.vfxEvents[clip.index].triggerTime : 0f;
            case ClipKind.Hitbox:
                return IsValidIndex(currentAsset.hitboxEvents, clip.index) ? currentAsset.hitboxEvents[clip.index].beginWindowTime : 0f;
            default:
                return 0f;
        }
    }

    private void SetClipStartTime(SelectedClip clip, float time, float duration)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index))
                {
                    currentAsset.animationEvents[clip.index].startTime = time;
                }
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index))
                {
                    currentAsset.audioEvents[clip.index].triggerTime = time;
                }
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index))
                {
                    currentAsset.vfxEvents[clip.index].triggerTime = time;
                }
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index))
                {
                    WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[clip.index];
                    float windowLength = Mathf.Max(0f, hitbox.endWindowTime - hitbox.beginWindowTime);
                    float applyOffset = Mathf.Clamp(hitbox.applyDamageTime - hitbox.beginWindowTime, 0f, windowLength);
                    float clampedBegin = Mathf.Clamp(time, 0f, Mathf.Max(0f, duration - windowLength));
                    hitbox.beginWindowTime = clampedBegin;
                    hitbox.endWindowTime = clampedBegin + windowLength;
                    hitbox.applyDamageTime = clampedBegin + applyOffset;
                }
                break;
        }
    }

    private void SetClipDuration(SelectedClip clip, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index) && currentAsset.animationEvents[clip.index].animationClip == null)
                {
                    currentAsset.animationEvents[clip.index].duration = duration;
                }
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index))
                {
                    currentAsset.audioEvents[clip.index].duration = duration;
                }
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index) && currentAsset.vfxEvents[clip.index].clipType != WeaponVFXClipType.ProjectileTrigger)
                {
                    currentAsset.vfxEvents[clip.index].duration = duration;
                }
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index))
                {
                    WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[clip.index];
                    hitbox.endWindowTime = hitbox.beginWindowTime + duration;
                    hitbox.applyDamageTime = Mathf.Clamp(hitbox.applyDamageTime, hitbox.beginWindowTime, hitbox.endWindowTime);
                }
                break;
        }
    }

    private void SetClipTimeRange(SelectedClip clip, float startTime, float endTime)
    {
        float duration = Mathf.Max(0.01f, endTime - startTime);
        SetClipStartTime(clip, Mathf.Max(0f, startTime), Mathf.Max(currentAsset.totalDuration, endTime));
        SetClipDuration(clip, duration);
        ExtendDurationIfNeeded(endTime);
    }

    private SerializedProperty GetSelectedClipProperty()
    {
        if (serializedAsset == null)
        {
            return null;
        }

        SerializedProperty array = null;
        switch (selectedClip.kind)
        {
            case ClipKind.Animation:
                array = serializedAsset.FindProperty("animationEvents");
                break;
            case ClipKind.Audio:
                array = serializedAsset.FindProperty("audioEvents");
                break;
            case ClipKind.Effect:
                array = serializedAsset.FindProperty("vfxEvents");
                break;
            case ClipKind.Hitbox:
                array = serializedAsset.FindProperty("hitboxEvents");
                break;
        }

        if (array == null || selectedClip.index < 0 || selectedClip.index >= array.arraySize)
        {
            return null;
        }

        return array.GetArrayElementAtIndex(selectedClip.index);
    }

    private string GetSelectedClipTitle()
    {
        switch (selectedClip.kind)
        {
            case ClipKind.Animation:
                return "Animation Clip";
            case ClipKind.Audio:
                return "Audio Clip";
            case ClipKind.Effect:
                return "Effect Clip";
            case ClipKind.Hitbox:
                return "Hitbox Clip";
            default:
                return "Clip";
        }
    }

    private void DeleteClip(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index)) currentAsset.animationEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index)) currentAsset.audioEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index)) currentAsset.vfxEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index)) currentAsset.hitboxEvents.RemoveAt(clip.index);
                break;
        }

        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = -1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void DeleteAnimationKeyframe(int index)
    {
        if (!IsValidIndex(currentAsset.animationKeyframes, index))
        {
            return;
        }

        currentAsset.animationKeyframes.RemoveAt(index);
        selectedAnimationKeyframeIndex = -1;
        isDraggingAnimationKeyframe = false;
        dragAnimationKeyframeIndex = -1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private int GetTrackClipCount(WeaponAttackTrackData track)
    {
        if (track == null)
        {
            return 0;
        }

        int count = 0;
        switch (track.type)
        {
            case WeaponAttackTrackType.Animation:
                for (int i = 0; i < currentAsset.animationEvents.Count; i++) if (currentAsset.animationEvents[i]?.trackId == track.id) count++;
                break;
            case WeaponAttackTrackType.Audio:
                for (int i = 0; i < currentAsset.audioEvents.Count; i++) if (currentAsset.audioEvents[i]?.trackId == track.id) count++;
                break;
            case WeaponAttackTrackType.Effect:
                for (int i = 0; i < currentAsset.vfxEvents.Count; i++) if (currentAsset.vfxEvents[i]?.trackId == track.id) count++;
                break;
            case WeaponAttackTrackType.Hitbox:
                for (int i = 0; i < currentAsset.hitboxEvents.Count; i++) if (currentAsset.hitboxEvents[i]?.trackId == track.id) count++;
                break;
        }

        return count;
    }

}
