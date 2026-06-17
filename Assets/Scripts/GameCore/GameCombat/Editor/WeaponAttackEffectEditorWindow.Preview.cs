using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private static Type editorAudioUtilType;
    private static MethodInfo playPreviewClipMethod;
    private static MethodInfo stopAllPreviewClipsMethod;

    private void OnSceneGUI(SceneView sceneView)
    {
        if (currentAsset == null || previewTarget == null)
        {
            CleanupPreviewVfxInstances();
            return;
        }

        DrawPreviewHitboxes();
        DrawPreviewVfxMarkers();
    }

    private void DrawPreviewHitboxes()
    {
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent hitbox = currentAsset.hitboxEvents[i];
            if (hitbox == null || IsTrackHiddenInPreview(hitbox.trackId) || playheadTime < hitbox.beginWindowTime || playheadTime > hitbox.endWindowTime)
            {
                continue;
            }

            Vector3 center = previewTarget.transform.TransformPoint(hitbox.offset);
            float radius = Mathf.Max(0.01f, hitbox.radius);
            Handles.color = new Color(1f, 0.24f, 0.18f, 0.82f);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
            Handles.Label(center + Vector3.up * radius, hitbox.label);
        }
    }

    private void DrawPreviewVfxMarkers()
    {
        HashSet<int> visibleVfx = new HashSet<int>();
        for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
        {
            WeaponVFXEvent vfx = currentAsset.vfxEvents[i];
            if (vfx == null || IsTrackHiddenInPreview(vfx.trackId) || !ShouldDrawPreviewVfx(i, vfx))
            {
                continue;
            }

            Vector3 position = vfx.attachToWeapon ? previewTarget.transform.TransformPoint(vfx.spawnOffset) : vfx.spawnOffset;
            Quaternion rotation = vfx.attachToWeapon ? previewTarget.transform.rotation * Quaternion.Euler(vfx.spawnRotation) : Quaternion.Euler(vfx.spawnRotation);
            visibleVfx.Add(i);
            Vector3 visualPosition = GetPreviewVfxInstancePosition(vfx, position, rotation);
            UpdatePreviewVfxInstance(i, vfx, visualPosition, rotation);

            float handleSize = HandleUtility.GetHandleSize(position) * 0.18f;
            Handles.color = new Color(0.28f, 0.72f, 1f, 0.9f);
            Handles.ArrowHandleCap(0, position, rotation, handleSize, EventType.Repaint);
            Handles.Label(position + Vector3.up * handleSize, string.IsNullOrWhiteSpace(vfx.label) ? "VFX" : vfx.label);

            if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger)
            {
                DrawPreviewProjectile(vfx, position, rotation);
            }
        }

        CleanupHiddenPreviewVfxInstances(visibleVfx);
    }

    private bool IsTrackHiddenInPreview(string trackId)
    {
        if (currentAsset == null || string.IsNullOrEmpty(trackId))
        {
            return false;
        }

        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            WeaponAttackTrackData track = currentAsset.tracks[i];
            if (track != null && track.id == trackId)
            {
                return track.hiddenInPreview;
            }
        }

        return false;
    }

    private bool ShouldDrawPreviewVfx(int index, WeaponVFXEvent vfx)
    {
        bool selected = selectedClip.kind == ClipKind.Effect && selectedClip.index == index;
        if (selected)
        {
            return true;
        }

        if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger)
        {
            return playheadTime >= vfx.triggerTime &&
                   playheadTime <= vfx.triggerTime + Mathf.Max(0.01f, vfx.projectileLifeTime);
        }

        return playheadTime >= vfx.triggerTime && playheadTime <= vfx.triggerTime + Mathf.Max(0.01f, vfx.duration);
    }

    private void DrawPreviewProjectile(WeaponVFXEvent vfx, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Vector3 forward = spawnRotation * Vector3.forward;
        Vector3 rangeEnd = spawnPosition + forward * GetProjectilePreviewDistance(vfx);
        Handles.color = new Color(0.36f, 1f, 0.82f, 0.82f);
        Handles.DrawAAPolyLine(2f, spawnPosition, rangeEnd);
        Handles.ConeHandleCap(0, rangeEnd, Quaternion.LookRotation(GetProjectilePreviewDirection(vfx, spawnRotation), Vector3.up), HandleUtility.GetHandleSize(rangeEnd) * 0.12f, EventType.Repaint);

        Vector3 hitCenter = GetPreviewProjectileLogicCenter(vfx, spawnPosition, spawnRotation);
        DrawProjectileHitShape(vfx, hitCenter, spawnRotation);

        string hitLabel = string.IsNullOrWhiteSpace(vfx.hitVFXKey) ? "Hit VFX: None" : "Hit VFX: " + vfx.hitVFXKey;
        Handles.Label(hitCenter + Vector3.up * (vfx.projectileHitRadius + 0.15f), hitLabel);
    }

    private Vector3 GetPreviewProjectileLogicCenter(WeaponVFXEvent vfx, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        Vector3 projectilePosition = GetPreviewProjectilePosition(vfx, spawnPosition, spawnRotation);
        return projectilePosition + spawnRotation * vfx.projectileHitCenterOffset;
    }

    private Vector3 GetPreviewProjectilePosition(WeaponVFXEvent vfx, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (vfx.projectileMotionMode == WeaponProjectileMotionMode.VisualSelfMotion)
        {
            float normalizedTime = GetPreviewProjectileNormalizedTime(vfx);
            AnimationCurve curve = vfx.visualForwardCurve;
            float distance01 = curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
            return spawnPosition + GetProjectilePreviewDirection(vfx, spawnRotation) * (Mathf.Max(0f, vfx.visualForwardDistance) * distance01);
        }

        if (vfx.projectileMotionMode == WeaponProjectileMotionMode.CodeDriven)
        {
            float localTime = Mathf.Clamp(playheadTime - vfx.triggerTime, 0f, Mathf.Max(0.01f, vfx.projectileLifeTime));
            return spawnPosition + GetProjectilePreviewDirection(vfx, spawnRotation) * (Mathf.Max(0f, vfx.projectileSpeed) * localTime);
        }

        return spawnPosition;
    }

    private Vector3 GetPreviewVfxInstancePosition(WeaponVFXEvent vfx, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger &&
            vfx.projectileMotionMode == WeaponProjectileMotionMode.CodeDriven)
        {
            return GetPreviewProjectilePosition(vfx, spawnPosition, spawnRotation);
        }

        return spawnPosition;
    }

    private float GetPreviewProjectileNormalizedTime(WeaponVFXEvent vfx)
    {
        float lifeTime = Mathf.Max(0.01f, vfx.projectileLifeTime);
        return Mathf.Clamp01((playheadTime - vfx.triggerTime) / lifeTime);
    }

    private void UpdatePreviewVfxInstance(int index, WeaponVFXEvent vfx, Vector3 position, Quaternion rotation)
    {
        if (vfx.vfxPrefab == null)
        {
            DestroyPreviewVfxInstance(index);
            return;
        }

        if (!previewVfxInstances.TryGetValue(index, out GameObject instance) || instance == null)
        {
            instance = Instantiate(vfx.vfxPrefab);
            instance.name = "[Preview] " + vfx.vfxPrefab.name;
            instance.hideFlags = HideFlags.HideAndDontSave;
            SetChildHideFlags(instance, HideFlags.HideAndDontSave);
            previewVfxInstances[index] = instance;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = vfx.vfxPrefab.transform.localScale;

        if (!instance.activeSelf)
        {
            instance.SetActive(true);
        }

        SimulatePreviewParticles(instance, GetPreviewVfxLocalTime(vfx));
    }

    private float GetPreviewVfxLocalTime(WeaponVFXEvent vfx)
    {
        if (vfx.clipType == WeaponVFXClipType.ProjectileTrigger)
        {
            return Mathf.Max(0f, playheadTime - vfx.triggerTime);
        }

        return Mathf.Clamp(playheadTime - vfx.triggerTime, 0f, Mathf.Max(0.01f, vfx.duration));
    }

    private static void SetChildHideFlags(GameObject root, HideFlags flags)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.hideFlags = flags;
        }
    }

    private static void SimulatePreviewParticles(GameObject instance, float time)
    {
        // 只对根粒子系统调用 Simulate，withChildren=true 负责处理所有子系统
        // 对所有系统逐个调用会导致子系统被重复模拟
        ParticleSystem[] all = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (IsRootParticleSystem(all[i]))
            {
                all[i].Simulate(Mathf.Max(0f, time), true, true, false);
            }
        }
    }

    private static bool IsRootParticleSystem(ParticleSystem ps)
    {
        Transform parent = ps.transform.parent;
        while (parent != null)
        {
            if (parent.GetComponent<ParticleSystem>() != null)
            {
                return false;
            }
            parent = parent.parent;
        }
        return true;
    }

    private void CleanupHiddenPreviewVfxInstances(HashSet<int> visibleVfx)
    {
        List<int> toRemove = new List<int>();
        foreach (KeyValuePair<int, GameObject> pair in previewVfxInstances)
        {
            if (!visibleVfx.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            DestroyPreviewVfxInstance(toRemove[i]);
        }
    }

    private void CleanupPreviewVfxInstances()
    {
        List<int> keys = new List<int>(previewVfxInstances.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            DestroyPreviewVfxInstance(keys[i]);
        }
    }

    private void DestroyPreviewVfxInstance(int index)
    {
        if (!previewVfxInstances.TryGetValue(index, out GameObject instance))
        {
            return;
        }

        previewVfxInstances.Remove(index);
        if (instance != null)
        {
            DestroyImmediate(instance);
        }
    }

    private static float GetProjectilePreviewDistance(WeaponVFXEvent vfx)
    {
        float sign = vfx.reverseProjectileTravel ? -1f : 1f;
        if (vfx.projectileMotionMode == WeaponProjectileMotionMode.VisualSelfMotion)
        {
            return sign * Mathf.Max(0f, vfx.visualForwardDistance);
        }

        return sign * Mathf.Max(0f, vfx.projectileSpeed) * Mathf.Max(0.01f, vfx.projectileLifeTime);
    }

    private static Vector3 GetProjectilePreviewDirection(WeaponVFXEvent vfx, Quaternion spawnRotation)
    {
        Vector3 direction = spawnRotation * Vector3.forward;
        return vfx.reverseProjectileTravel ? -direction : direction;
    }

    private void DrawProjectileHitShape(WeaponVFXEvent vfx, Vector3 center, Quaternion rotation)
    {
        Handles.color = new Color(1f, 0.22f, 0.15f, 0.88f);
        float radius = Mathf.Max(0.01f, vfx.projectileHitRadius);
        if (vfx.projectileHitShape == WeaponProjectileHitShape.Cylinder)
        {
            DrawPreviewCylinder(center, rotation, radius, Mathf.Max(0.01f, vfx.projectileHitHeight));
        }
        else
        {
            Handles.DrawWireDisc(center, rotation * Vector3.up, radius);
            Handles.DrawWireDisc(center, rotation * Vector3.right, radius);
            Handles.DrawWireDisc(center, rotation * Vector3.forward, radius);
        }
    }

    private void DrawPreviewCylinder(Vector3 center, Quaternion rotation, float radius, float height)
    {
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 halfHeight = up * (height * 0.5f);
        Vector3 bottom = center - halfHeight;
        Vector3 top = center + halfHeight;

        Handles.DrawWireDisc(bottom, up, radius);
        Handles.DrawWireDisc(top, up, radius);
        Handles.DrawLine(bottom + right * radius, top + right * radius);
        Handles.DrawLine(bottom - right * radius, top - right * radius);
        Handles.DrawLine(bottom + forward * radius, top + forward * radius);
        Handles.DrawLine(bottom - forward * radius, top - forward * radius);
    }

    private void ResetPreviewAudioState()
    {
        int count = currentAsset != null && currentAsset.audioEvents != null ? currentAsset.audioEvents.Count : 0;
        previewAudioTriggered = new bool[count];
        StopPreviewAudio();
    }

    private void TriggerPreviewAudioEvents(float previousTime, float currentTime)
    {
        if (!isPreviewing || currentAsset == null || currentAsset.audioEvents == null)
        {
            return;
        }

        if (previewAudioTriggered == null || previewAudioTriggered.Length != currentAsset.audioEvents.Count)
        {
            ResetPreviewAudioState();
        }

        float start = Mathf.Min(previousTime, currentTime);
        float end = Mathf.Max(previousTime, currentTime);
        const float epsilon = 0.0001f;

        for (int i = 0; i < currentAsset.audioEvents.Count; i++)
        {
            WeaponAudioEvent audioEvent = currentAsset.audioEvents[i];
            if (audioEvent == null || previewAudioTriggered[i])
            {
                continue;
            }

            if (IsTrackHiddenInPreview(audioEvent.trackId))
            {
                continue;
            }

            if (audioEvent.triggerTime <= start + epsilon || audioEvent.triggerTime > end + epsilon)
            {
                continue;
            }

            PlayPreviewAudio(audioEvent);
            previewAudioTriggered[i] = true;
        }
    }

    private void PlayPreviewAudio(WeaponAudioEvent audioEvent)
    {
        if (audioEvent == null || audioEvent.audioClip == null)
        {
            return;
        }

        if (TryPlayEditorPreviewClip(audioEvent.audioClip))
        {
            return;
        }

        Debug.LogWarning("[WeaponAttackEffectEditor] 当前 Unity 版本无法调用编辑器音频预览。进入 Play Mode 后会通过 AudioMgr 播放该音效。");
    }

    private static bool TryPlayEditorPreviewClip(AudioClip clip)
    {
        if (clip == null)
        {
            return false;
        }

        EnsureEditorAudioUtilMethods();
        if (playPreviewClipMethod == null)
        {
            return false;
        }

        ParameterInfo[] parameters = playPreviewClipMethod.GetParameters();
        object[] args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType == typeof(AudioClip))
            {
                args[i] = clip;
            }
            else if (parameterType == typeof(int))
            {
                args[i] = 0;
            }
            else if (parameterType == typeof(bool))
            {
                args[i] = false;
            }
            else
            {
                args[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
            }
        }

        playPreviewClipMethod.Invoke(null, args);
        return true;
    }

    private static void StopPreviewAudio()
    {
        EnsureEditorAudioUtilMethods();
        stopAllPreviewClipsMethod?.Invoke(null, null);
    }

    private static void EnsureEditorAudioUtilMethods()
    {
        if (editorAudioUtilType != null)
        {
            return;
        }

        editorAudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (editorAudioUtilType == null)
        {
            return;
        }

        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        playPreviewClipMethod = editorAudioUtilType.GetMethod("PlayPreviewClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (playPreviewClipMethod == null)
        {
            playPreviewClipMethod = editorAudioUtilType.GetMethod("PlayClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        }

        stopAllPreviewClipsMethod = editorAudioUtilType.GetMethod("StopAllPreviewClips", flags)
            ?? editorAudioUtilType.GetMethod("StopAllClips", flags);
    }
}
