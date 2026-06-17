using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void DrawEffectClips(WeaponAttackTrackData track, Rect row, float duration)
    {
        for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
        {
            WeaponVFXEvent clip = currentAsset.vfxEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            string fallback = clip.vfxPrefab != null ? clip.vfxPrefab.name : "Effect Clip";
            string label = clip.clipType == WeaponVFXClipType.ProjectileTrigger
                ? GetClipLabel(clip.label, string.IsNullOrWhiteSpace(clip.projectileVFXKey) ? "Projectile" : clip.projectileVFXKey)
                : GetClipLabel(clip.label, fallback);

            Rect clipRect = clip.clipType == WeaponVFXClipType.ProjectileTrigger
                ? TimePointToMarkerRect(clip.triggerTime, duration, row)
                : TimeRangeToRect(clip.triggerTime, clip.triggerTime + clip.duration, duration, row);
            DrawClipBlock(clipRect, track.color, label, new SelectedClip(ClipKind.Effect, i));
        }
    }

    private void DrawTouchingClipSeparators(WeaponAttackTrackData track, Rect row, float duration)
    {
        List<float> starts = new List<float>();
        List<float> ends = new List<float>();
        CollectTrackClipRanges(track, starts, ends);

        for (int i = 0; i < ends.Count; i++)
        {
            for (int j = 0; j < starts.Count; j++)
            {
                if (i == j || Mathf.Abs(ends[i] - starts[j]) > 0.0001f)
                {
                    continue;
                }

                float x = TimeToX(Mathf.Clamp(ends[i], 0f, duration), duration, row.width);
                EditorGUI.DrawRect(new Rect(x - 0.5f, row.y + 3f, 1f, row.height - 6f), new Color(0.02f, 0.02f, 0.02f, 0.95f));
                break;
            }
        }
    }

    private void CollectTrackClipRanges(WeaponAttackTrackData track, List<float> starts, List<float> ends)
    {
        switch (track.type)
        {
            case WeaponAttackTrackType.Animation:
                for (int i = 0; i < currentAsset.animationEvents.Count; i++)
                {
                    WeaponAnimationEvent clip = currentAsset.animationEvents[i];
                    if (clip == null || clip.trackId != track.id)
                    {
                        continue;
                    }

                    float clipDuration = clip.animationClip != null ? Mathf.Max(0.01f, clip.animationClip.length) : Mathf.Max(0.01f, clip.duration);
                    starts.Add(clip.startTime);
                    ends.Add(clip.startTime + clipDuration);
                }
                break;
            case WeaponAttackTrackType.Audio:
                for (int i = 0; i < currentAsset.audioEvents.Count; i++)
                {
                    WeaponAudioEvent clip = currentAsset.audioEvents[i];
                    if (clip == null || clip.trackId != track.id)
                    {
                        continue;
                    }

                    float clipDuration = Mathf.Max(0.01f, clip.duration);
                    starts.Add(clip.triggerTime);
                    ends.Add(clip.triggerTime + clipDuration);
                }
                break;
            case WeaponAttackTrackType.Effect:
                for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
                {
                    WeaponVFXEvent clip = currentAsset.vfxEvents[i];
                    if (clip == null || clip.trackId != track.id)
                    {
                        continue;
                    }

                    starts.Add(clip.triggerTime);
                    float clipDuration = clip.clipType == WeaponVFXClipType.ProjectileTrigger ? 0.05f : Mathf.Max(0.01f, clip.duration);
                    ends.Add(clip.triggerTime + clipDuration);
                }
                break;
            case WeaponAttackTrackType.Hitbox:
                for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
                {
                    WeaponHitboxEvent clip = currentAsset.hitboxEvents[i];
                    if (clip == null || clip.trackId != track.id)
                    {
                        continue;
                    }

                    starts.Add(clip.beginWindowTime);
                    ends.Add(Mathf.Max(clip.beginWindowTime, clip.endWindowTime));
                }
                break;
        }
    }
}
