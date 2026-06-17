using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private float GetTimelineDuration()
    {
        float duration = currentAsset != null ? Mathf.Max(0.01f, currentAsset.totalDuration) : 1f;
        if (TryGetClipTimeRange(out _, out float clipEnd))
        {
            duration = Mathf.Max(duration, clipEnd);
        }

        return Mathf.Max(duration, playheadTime, 1f);
    }

    private bool TryGetClipTimeRange(out float minTime, out float maxTime)
    {
        minTime = float.MaxValue;
        maxTime = float.MinValue;

        if (currentAsset == null)
        {
            return false;
        }

        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentAsset.animationEvents[i];
            if (clip == null)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
            AddRange(clip.startTime, clip.startTime + clipDuration, ref minTime, ref maxTime);
        }

        for (int i = 0; i < currentAsset.audioEvents.Count; i++)
        {
            WeaponAudioEvent clip = currentAsset.audioEvents[i];
            if (clip == null)
            {
                continue;
            }

            float clipDuration = Mathf.Max(0.01f, clip.duration);
            AddRange(clip.triggerTime, clip.triggerTime + clipDuration, ref minTime, ref maxTime);
        }

        for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
        {
            WeaponVFXEvent clip = currentAsset.vfxEvents[i];
            if (clip == null)
            {
                continue;
            }

            AddRange(clip.triggerTime, clip.triggerTime + Mathf.Max(0.01f, clip.duration), ref minTime, ref maxTime);
        }

        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent clip = currentAsset.hitboxEvents[i];
            if (clip == null)
            {
                continue;
            }

            AddRange(clip.beginWindowTime, Mathf.Max(clip.beginWindowTime, clip.endWindowTime), ref minTime, ref maxTime);
        }

        return minTime != float.MaxValue && maxTime != float.MinValue;
    }

    private static void AddRange(float start, float end, ref float minTime, ref float maxTime)
    {
        start = Mathf.Max(0f, start);
        end = Mathf.Max(start, end);
        minTime = Mathf.Min(minTime, start);
        maxTime = Mathf.Max(maxTime, end);
    }

    private static HashSet<float> CollectAnimationKeyTimes(AnimationClip clip)
    {
        HashSet<float> times = new HashSet<float>();
        if (clip == null)
        {
            return times;
        }

        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
        for (int i = 0; i < curveBindings.Length; i++)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, curveBindings[i]);
            if (curve == null)
            {
                continue;
            }

            for (int j = 0; j < curve.keys.Length; j++)
            {
                times.Add(curve.keys[j].time);
            }
        }

        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (int i = 0; i < objectBindings.Length; i++)
        {
            ObjectReferenceKeyframe[] objectKeys = AnimationUtility.GetObjectReferenceCurve(clip, objectBindings[i]);
            if (objectKeys == null)
            {
                continue;
            }

            for (int j = 0; j < objectKeys.Length; j++)
            {
                times.Add(objectKeys[j].time);
            }
        }

        times.Add(0f);
        times.Add(clip.length);
        return times;
    }

    private static Rect TimeRangeToRect(float start, float end, float duration, Rect row)
    {
        start = Mathf.Clamp(start, 0f, duration);
        end = Mathf.Clamp(end, start + 0.02f, duration);
        float xMin = TimeToX(start, duration, row.width);
        float xMax = TimeToX(end, duration, row.width);
        const float clipHeight = 24f;
        float y = row.y + Mathf.Max(2f, (row.height - clipHeight) * 0.5f);
        float height = Mathf.Min(clipHeight, row.height - 4f);
        return new Rect(xMin, y, Mathf.Max(18f, xMax - xMin), height);
    }

    private static Rect TimePointToMarkerRect(float time, float duration, Rect row)
    {
        const float markerWidth = 18f;
        const float clipHeight = 24f;
        float x = TimeToX(Mathf.Clamp(time, 0f, duration), duration, row.width);
        float y = row.y + Mathf.Max(2f, (row.height - clipHeight) * 0.5f);
        float height = Mathf.Min(clipHeight, row.height - 4f);
        return new Rect(x - markerWidth * 0.5f, y, markerWidth, height);
    }

    private static float TimeToX(float time, float duration, float width)
    {
        return Mathf.Clamp01(time / Mathf.Max(0.01f, duration)) * width;
    }

    private float GetTimelineCanvasDuration(Rect rect, float timelineDuration, float currentPixelsPerSecond)
    {
        float viewportWidth = Mathf.Max(1f, rect.width - 16f);
        float durationToFillViewport = viewportWidth / Mathf.Max(1f, currentPixelsPerSecond);
        float tailDuration = Mathf.Max(durationToFillViewport * 1.5f, 3f);
        return Mathf.Max(0.01f, timelineDuration + tailDuration, durationToFillViewport * 2f);
    }

    private RulerStep GetRulerStep(float pixelsPerSecond)
    {
        if (rulerDisplayMode == RulerDisplayMode.Frames)
        {
            float pixelsPerFrame = pixelsPerSecond / Mathf.Max(1, previewFps);
            int majorFrames;
            if (pixelsPerFrame >= 10f)
            {
                majorFrames = 5;
            }
            else if (pixelsPerFrame >= 5f)
            {
                majorFrames = 10;
            }
            else if (pixelsPerFrame >= 2.5f)
            {
                majorFrames = 20;
            }
            else if (pixelsPerFrame >= 1f)
            {
                majorFrames = 60;
            }
            else if (pixelsPerFrame >= 0.45f)
            {
                majorFrames = 120;
            }
            else
            {
                majorFrames = 240;
            }

            int minorFrames = Mathf.Max(1, majorFrames / 6);
            return new RulerStep(minorFrames / Mathf.Max(1f, previewFps), majorFrames / Mathf.Max(1f, previewFps), minorFrames, majorFrames);
        }

        if (pixelsPerSecond >= 520f)
        {
            return new RulerStep(0.05f, 0.25f, 0, 0);
        }

        if (pixelsPerSecond >= 220f)
        {
            return new RulerStep(0.1f, 0.5f, 0, 0);
        }

        if (pixelsPerSecond >= 55f)
        {
            return new RulerStep(0.25f, 1f, 0, 0);
        }

        if (pixelsPerSecond >= 25f)
        {
            return new RulerStep(0.5f, 2f, 0, 0);
        }

        return new RulerStep(1f, 5f, 0, 0);
    }

    private string FormatRulerTime(float time)
    {
        if (rulerDisplayMode == RulerDisplayMode.Frames)
        {
            return Mathf.RoundToInt(time * Mathf.Max(1, previewFps)).ToString();
        }

        if (rulerDisplayMode == RulerDisplayMode.Timecode)
        {
            return FormatTimeAsSecondsAndFrames(time);
        }

        return time.ToString("0.####") + "s";
    }

    private void DrawTimeFpsField(Rect rect)
    {
        Rect timeRect = new Rect(rect.x, rect.y, 64f, rect.height);
        Rect fpsRect = new Rect(timeRect.xMax - 1f, rect.y, 42f, rect.height);
        Rect settingsRect = new Rect(fpsRect.xMax + 5f, rect.y, 28f, rect.height);

        if (rulerDisplayMode == RulerDisplayMode.Frames)
        {
            int frame = Mathf.Max(0, EditorGUI.IntField(timeRect, Mathf.RoundToInt(playheadTime * Mathf.Max(1, previewFps)), centeredNumberFieldStyle));
            playheadTime = frame / Mathf.Max(1f, previewFps);
        }
        else if (rulerDisplayMode == RulerDisplayMode.Timecode)
        {
            string timecode = EditorGUI.TextField(timeRect, FormatTimeAsSecondsAndFrames(playheadTime), centeredNumberFieldStyle);
            if (TryParseTimecode(timecode, out float parsedTime))
            {
                playheadTime = Mathf.Max(0f, parsedTime);
            }
        }
        else
        {
            float displayedTime = Mathf.Round(playheadTime * 10000f) / 10000f;
            playheadTime = Mathf.Max(0f, Mathf.Round(EditorGUI.FloatField(timeRect, displayedTime, centeredNumberFieldStyle) * 10000f) / 10000f);
        }

        previewFps = Mathf.Clamp(EditorGUI.IntField(fpsRect, previewFps, centeredNumberFieldStyle), 1, 240);

        if (GUI.Button(settingsRect, EditorGUIUtility.IconContent("_Popup")))
        {
            ShowRulerSettingsMenu();
        }
    }

    private void ShowRulerSettingsMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Frames"), rulerDisplayMode == RulerDisplayMode.Frames, () =>
        {
            rulerDisplayMode = RulerDisplayMode.Frames;
            Repaint();
        });
        menu.AddItem(new GUIContent("Timecode"), rulerDisplayMode == RulerDisplayMode.Timecode, () =>
        {
            rulerDisplayMode = RulerDisplayMode.Timecode;
            Repaint();
        });
        menu.AddItem(new GUIContent("Seconds"), rulerDisplayMode == RulerDisplayMode.Seconds, () =>
        {
            rulerDisplayMode = RulerDisplayMode.Seconds;
            Repaint();
        });
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Frame Rate/24"), previewFps == 24, () => SetPreviewFps(24));
        menu.AddItem(new GUIContent("Frame Rate/30"), previewFps == 30, () => SetPreviewFps(30));
        menu.AddItem(new GUIContent("Frame Rate/60"), previewFps == 60, () => SetPreviewFps(60));
        menu.AddItem(new GUIContent("Frame Rate/120"), previewFps == 120, () => SetPreviewFps(120));
        menu.ShowAsContext();
    }

    private void SetPreviewFps(int fps)
    {
        previewFps = Mathf.Clamp(fps, 1, 240);
        Repaint();
    }

    private float GetPreviewEndTime()
    {
        return TryGetClipTimeRange(out _, out float endTime) ? Mathf.Max(0.01f, endTime) : Mathf.Max(0.01f, currentAsset != null ? currentAsset.totalDuration : 1f);
    }

    private static bool TryGetAnimationClipFromDraggedObject(UnityEngine.Object obj, out AnimationClip clip)
    {
        clip = obj as AnimationClip;
        if (clip != null)
        {
            return true;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        return TryGetAnimationClipFromPath(path, out clip);
    }

    private static bool TryGetAnimationClipFromPath(string path, out AnimationClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
        if (mainAsset is AnimationClip mainClip)
        {
            clip = mainClip;
            return true;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip assetClip && !IsPreviewAnimationClip(assetClip))
            {
                clip = assetClip;
                return true;
            }
        }

        return false;
    }

    private static bool IsPreviewAnimationClip(AnimationClip clip)
    {
        return clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase);
    }

    private int GetTrackIndexAtTimelineY(float mouseY)
    {
        return Mathf.FloorToInt((mouseY - RulerHeight) / TrackHeight);
    }

    private static bool TrackAcceptsClip(WeaponAttackTrackData track, SelectedClip clip)
    {
        return track != null && track.type == GetTrackTypeForClipKind(clip.kind);
    }

    private static WeaponAttackTrackType GetTrackTypeForClipKind(ClipKind kind)
    {
        switch (kind)
        {
            case ClipKind.Animation:
                return WeaponAttackTrackType.Animation;
            case ClipKind.Audio:
                return WeaponAttackTrackType.Audio;
            case ClipKind.Effect:
                return WeaponAttackTrackType.Effect;
            case ClipKind.Hitbox:
                return WeaponAttackTrackType.Hitbox;
            default:
                return WeaponAttackTrackType.Custom;
        }
    }

    private bool WouldFullyOverlapClip(ClipKind kind, int movingIndex, string trackId, float startTime, float duration)
    {
        if (kind != ClipKind.Animation || string.IsNullOrEmpty(trackId))
        {
            return false;
        }

        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            if (i == movingIndex)
            {
                continue;
            }

            WeaponAnimationEvent clip = currentAsset.animationEvents[i];
            if (clip == null || clip.trackId != trackId)
            {
                continue;
            }

            float otherDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
            float endTime = startTime + Mathf.Max(0.01f, duration);
            float otherEndTime = clip.startTime + otherDuration;
            if (duration < otherDuration - 0.0001f && startTime >= clip.startTime - 0.0001f && endTime <= otherEndTime + 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private float SnapTime(float time, SelectedClip movingClip, float contentWidth, float duration)
    {
        float snapWindow = Mathf.Max(0.01f, 8f / Mathf.Max(1f, contentWidth) * duration);
        float bestTime = Mathf.Max(0f, time);
        float bestLineTime = bestTime;
        float bestDistance = snapWindow;
        bool snapped = false;
        TrySnapCandidate(0f, 0f, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        TrySnapCandidate(playheadTime, playheadTime, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);

        float movingDuration = GetClipDuration(movingClip);
        AddAnimationKeyframeSnapCandidates(movingClip, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipSnapCandidates(currentAsset != null ? currentAsset.animationEvents : null, movingClip, ClipKind.Animation, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipSnapCandidates(currentAsset != null ? currentAsset.audioEvents : null, movingClip, ClipKind.Audio, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipSnapCandidates(currentAsset != null ? currentAsset.vfxEvents : null, movingClip, ClipKind.Effect, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipSnapCandidates(currentAsset != null ? currentAsset.hitboxEvents : null, movingClip, ClipKind.Hitbox, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);

        hasSnapLine = snapped && movingClip.kind != ClipKind.None;
        snapLineTime = Mathf.Clamp(bestLineTime, 0f, duration);
        return Mathf.Clamp(bestTime, 0f, duration);
    }

    private float SnapEdgeTime(float edgeTime, SelectedClip resizingClip, float contentWidth, float duration)
    {
        float snapWindow = Mathf.Max(0.01f, 8f / Mathf.Max(1f, contentWidth) * duration);
        float bestTime = Mathf.Max(0f, edgeTime);
        float bestLineTime = bestTime;
        float bestDistance = snapWindow;
        bool snapped = false;

        TrySnapCandidate(0f, 0f, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        TrySnapCandidate(playheadTime, playheadTime, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddAnimationKeyframeEdgeSnapCandidates(edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipEdgeSnapCandidates(currentAsset != null ? currentAsset.animationEvents : null, resizingClip, ClipKind.Animation, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipEdgeSnapCandidates(currentAsset != null ? currentAsset.audioEvents : null, resizingClip, ClipKind.Audio, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipEdgeSnapCandidates(currentAsset != null ? currentAsset.vfxEvents : null, resizingClip, ClipKind.Effect, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        AddClipEdgeSnapCandidates(currentAsset != null ? currentAsset.hitboxEvents : null, resizingClip, ClipKind.Hitbox, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);

        hasSnapLine = snapped && resizingClip.kind != ClipKind.None;
        snapLineTime = Mathf.Clamp(bestLineTime, 0f, duration);
        return Mathf.Clamp(bestTime, 0f, duration);
    }

    private void AddAnimationKeyframeSnapCandidates(SelectedClip movingClip, float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (currentAsset == null || currentAsset.animationKeyframes == null || movingClip.kind == ClipKind.Animation)
        {
            return;
        }

        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            WeaponAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null)
            {
                continue;
            }

            TrySnapCandidate(keyframe.time, keyframe.time, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private void AddAnimationKeyframeEdgeSnapCandidates(float edgeTime, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (currentAsset == null || currentAsset.animationKeyframes == null)
        {
            return;
        }

        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            WeaponAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null)
            {
                continue;
            }

            TrySnapCandidate(keyframe.time, keyframe.time, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private static void TrySnapCandidate(float candidateStart, float candidateLineTime, float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        float distance = Mathf.Abs(candidateStart - time);
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestTime = candidateStart;
            bestLineTime = candidateLineTime;
            snapped = true;
        }
    }

    private void TrySnapEndToCandidate(float candidateTime, float movingDuration, float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        float candidateStart = candidateTime - movingDuration;
        TrySnapCandidate(candidateStart, candidateTime, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
    }

    private void AddClipSnapCandidates<T>(List<T> clips, SelectedClip movingClip, ClipKind kind, float movingDuration, float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (clips == null)
        {
            return;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (movingClip.kind == kind && movingClip.index == i)
            {
                continue;
            }

            SelectedClip clip = new SelectedClip(kind, i);
            float start = GetClipStartTime(clip);
            float end = start + GetClipDuration(clip);
            TrySnapCandidate(start, start, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
            TrySnapCandidate(end, end, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
            TrySnapEndToCandidate(start, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
            TrySnapEndToCandidate(end, movingDuration, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private void AddClipEdgeSnapCandidates<T>(List<T> clips, SelectedClip resizingClip, ClipKind kind, float edgeTime, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (clips == null)
        {
            return;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            if (resizingClip.kind == kind && resizingClip.index == i)
            {
                continue;
            }

            SelectedClip clip = new SelectedClip(kind, i);
            float start = GetClipStartTime(clip);
            float end = start + GetClipDuration(clip);
            TrySnapCandidate(start, start, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
            TrySnapCandidate(end, end, edgeTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private float GetClipDuration(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index))
                {
                    WeaponAnimationEvent animationClip = currentAsset.animationEvents[clip.index];
                    return animationClip.animationClip != null ? Mathf.Max(0.01f, animationClip.animationClip.length) : Mathf.Max(0.01f, animationClip.duration);
                }
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index))
                {
                    WeaponAudioEvent audioClip = currentAsset.audioEvents[clip.index];
                    return Mathf.Max(0.01f, audioClip.duration);
                }
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index))
                {
                    WeaponVFXEvent effectClip = currentAsset.vfxEvents[clip.index];
                    return effectClip.clipType == WeaponVFXClipType.ProjectileTrigger ? 0.05f : Mathf.Max(0.01f, effectClip.duration);
                }
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index))
                {
                    WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[clip.index];
                    return Mathf.Max(0.01f, hitbox.endWindowTime - hitbox.beginWindowTime);
                }
                break;
        }

        return 0.01f;
    }

    private bool CanResizeClip(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                return IsValidIndex(currentAsset.animationEvents, clip.index) && currentAsset.animationEvents[clip.index].animationClip == null;
            case ClipKind.Audio:
                return IsValidIndex(currentAsset.audioEvents, clip.index);
            case ClipKind.Effect:
                return IsValidIndex(currentAsset.vfxEvents, clip.index) && currentAsset.vfxEvents[clip.index].clipType != WeaponVFXClipType.ProjectileTrigger;
            case ClipKind.Hitbox:
                return IsValidIndex(currentAsset.hitboxEvents, clip.index);
            default:
                return false;
        }
    }

    private string FormatTimeAsSecondsAndFrames(float time)
    {
        int wholeSeconds = Mathf.FloorToInt(time);
        int frames = Mathf.RoundToInt((time - wholeSeconds) * Mathf.Max(1, previewFps));
        if (frames >= previewFps)
        {
            wholeSeconds += 1;
            frames = 0;
        }

        return wholeSeconds.ToString() + ":" + frames.ToString("00");
    }

    private bool TryParseTimecode(string value, out float time)
    {
        time = 0f;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out int seconds) || !int.TryParse(parts[1], out int frames))
        {
            return false;
        }

        time = Mathf.Max(0, seconds) + Mathf.Max(0, frames) / Mathf.Max(1f, previewFps);
        return true;
    }

    private static string GetClipLabel(string label, string fallback)
    {
        return string.IsNullOrWhiteSpace(label) ? fallback : label;
    }

    private string GetClipDisplayLabel(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (IsValidIndex(currentAsset.animationEvents, clip.index))
                {
                    WeaponAnimationEvent animationClip = currentAsset.animationEvents[clip.index];
                    return animationClip.animationClip != null ? animationClip.animationClip.name : GetClipLabel(animationClip.label, "Animation Clip");
                }
                break;
            case ClipKind.Audio:
                if (IsValidIndex(currentAsset.audioEvents, clip.index))
                {
                    WeaponAudioEvent audioClip = currentAsset.audioEvents[clip.index];
                    return GetClipLabel(audioClip.label, audioClip.audioClip != null ? audioClip.audioClip.name : "Audio Clip");
                }
                break;
            case ClipKind.Effect:
                if (IsValidIndex(currentAsset.vfxEvents, clip.index))
                {
                    WeaponVFXEvent effectClip = currentAsset.vfxEvents[clip.index];
                    if (effectClip.clipType == WeaponVFXClipType.ProjectileTrigger)
                    {
                        string projectileFallback = string.IsNullOrWhiteSpace(effectClip.projectileVFXKey) ? "Projectile" : effectClip.projectileVFXKey;
                        return GetClipLabel(effectClip.label, projectileFallback);
                    }

                    string effectFallback = effectClip.vfxPrefab != null ? effectClip.vfxPrefab.name : "Effect Clip";
                    return GetClipLabel(effectClip.label, effectFallback);
                }
                break;
            case ClipKind.Hitbox:
                if (IsValidIndex(currentAsset.hitboxEvents, clip.index))
                {
                    return GetClipLabel(currentAsset.hitboxEvents[clip.index].label, "Hitbox Clip");
                }
                break;
        }

        return "Clip";
    }

    private static bool IsValidIndex<T>(List<T> list, int index)
    {
        return list != null && index >= 0 && index < list.Count;
    }

    private static string GetDefaultTrackName(WeaponAttackTrackType type)
    {
        switch (type)
        {
            case WeaponAttackTrackType.Animation:
                return "动画轨道";
            case WeaponAttackTrackType.Audio:
                return "音效轨道";
            case WeaponAttackTrackType.Effect:
                return "特效轨道";
            case WeaponAttackTrackType.Hitbox:
                return "攻击盒轨道";
            default:
                return "自定义轨道";
        }
    }

    private static Color GetDefaultTrackColor(WeaponAttackTrackType type)
    {
        switch (type)
        {
            case WeaponAttackTrackType.Animation:
                return new Color(0.30f, 0.58f, 0.88f);
            case WeaponAttackTrackType.Audio:
                return new Color(0.58f, 0.36f, 0.78f);
            case WeaponAttackTrackType.Effect:
                return new Color(0.95f, 0.56f, 0.16f);
            case WeaponAttackTrackType.Hitbox:
                return new Color(0.83f, 0.26f, 0.22f);
            default:
                return new Color(0.48f, 0.62f, 0.62f);
        }
    }

    private void HandleKeyboardShortcuts()
    {
        Event evt = Event.current;
        if (evt.type != EventType.KeyDown || currentAsset == null)
        {
            return;
        }

        if (evt.keyCode == KeyCode.Space)
        {
            TogglePreview();
            evt.Use();
        }
        else if ((evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) && selectedAnimationKeyframeIndex >= 0)
        {
            DeleteAnimationKeyframe(selectedAnimationKeyframeIndex);
            evt.Use();
        }
        else if ((evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) && selectedClip.kind != ClipKind.None)
        {
            DeleteClip(selectedClip);
            evt.Use();
        }
    }

    private void DrawBackground()
    {
        EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), new Color(0.10f, 0.10f, 0.10f));
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(1f,
            new Vector3(rect.xMin, rect.yMin),
            new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMax, rect.yMax),
            new Vector3(rect.xMin, rect.yMax),
            new Vector3(rect.xMin, rect.yMin));
    }

    private static void DrawBottomLine(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
    }

    private void EnsureStyles()
    {
        if (toolbarTitleStyle != null)
        {
            return;
        }

        toolbarTitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.82f, 0.86f, 0.92f) }
        };

        trackTitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.86f, 0.86f, 0.86f) }
        };

        trackSubTitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.66f, 0.66f, 0.66f) }
        };

        centeredLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.88f, 0.88f, 0.88f) }
        };

        inspectorTitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.88f, 0.88f, 0.88f) }
        };

        miniDimStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.68f, 0.68f, 0.68f) }
        };

        centeredNumberFieldStyle = new GUIStyle(EditorStyles.numberField)
        {
            alignment = TextAnchor.MiddleCenter
        };
    }

    private enum ClipKind
    {
        None,
        Animation,
        Audio,
        Effect,
        Hitbox
    }

    private enum RulerDisplayMode
    {
        Seconds,
        Frames,
        Timecode
    }

    private enum ClipResizeEdge
    {
        None,
        Left,
        Right
    }

    private struct RulerStep
    {
        public readonly float minorSeconds;
        public readonly float majorSeconds;
        public readonly int minorFrames;
        public readonly int majorFrames;

        public RulerStep(float minorSeconds, float majorSeconds, int minorFrames, int majorFrames)
        {
            this.minorSeconds = minorSeconds;
            this.majorSeconds = majorSeconds;
            this.minorFrames = minorFrames;
            this.majorFrames = majorFrames;
        }
    }

    private struct SelectedClip
    {
        public static readonly SelectedClip None = new SelectedClip(ClipKind.None, -1);

        public readonly ClipKind kind;
        public readonly int index;

        public SelectedClip(ClipKind kind, int index)
        {
            this.kind = kind;
            this.index = index;
        }

        public bool Equals(SelectedClip other)
        {
            return kind == other.kind && index == other.index;
        }
    }
}
