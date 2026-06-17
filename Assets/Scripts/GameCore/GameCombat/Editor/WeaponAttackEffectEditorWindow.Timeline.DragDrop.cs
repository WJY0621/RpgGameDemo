using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void HandleTrackDrop(Rect visibleRect, Rect row, WeaponAttackTrackData track, float duration, float contentWidth)
    {
        Event evt = Event.current;
        if ((evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) || !row.Contains(evt.mousePosition))
        {
            return;
        }

        bool canAccept = CanAcceptDraggedObjects(track);
        DragAndDrop.visualMode = canAccept ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        if (!canAccept)
        {
            return;
        }

        bool isPerform = evt.type == EventType.DragPerform;
        evt.Use();

        if (!isPerform)
        {
            return;
        }

        DragAndDrop.AcceptDrag();
        float dropTime = SnapTime(Mathf.Clamp(evt.mousePosition.x / contentWidth * duration, 0f, duration), SelectedClip.None, contentWidth, duration);
        bool addedClip = false;
        if (track.type == WeaponAttackTrackType.Animation)
        {
            List<AnimationClip> animationClips = CollectDraggedAnimationClips();
            for (int i = 0; i < animationClips.Count; i++)
            {
                if (AddAnimationClip(track, animationClips[i], dropTime))
                {
                    dropTime += Mathf.Max(0.01f, animationClips[i].length);
                    addedClip = true;
                }
            }
        }
        else if (DragAndDrop.objectReferences != null)
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                float previousTime = dropTime;
                dropTime = AddDraggedObjectAsClip(track, DragAndDrop.objectReferences[i], dropTime);
                addedClip |= !Mathf.Approximately(previousTime, dropTime);
            }
        }

        if (addedClip)
        {
            EditorUtility.SetDirty(currentAsset);
            Repaint();
        }
    }

    private bool CanAcceptDraggedObjects(WeaponAttackTrackData track)
    {
        if (track == null || track.locked)
        {
            return false;
        }

        if (track.type == WeaponAttackTrackType.Animation)
        {
            return CollectDraggedAnimationClips().Count > 0;
        }

        if (DragAndDrop.objectReferences != null)
        {
            for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
            {
                if (CanAcceptObject(track.type, DragAndDrop.objectReferences[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CanAcceptObject(WeaponAttackTrackType type, UnityEngine.Object obj)
    {
        switch (type)
        {
            case WeaponAttackTrackType.Animation:
                return TryGetAnimationClipFromDraggedObject(obj, out _);
            case WeaponAttackTrackType.Audio:
                return obj is AudioClip;
            case WeaponAttackTrackType.Effect:
                return obj is GameObject;
            case WeaponAttackTrackType.Hitbox:
                return false;
            case WeaponAttackTrackType.Custom:
                return obj != null;
            default:
                return false;
        }
    }

    private float AddDraggedObjectAsClip(WeaponAttackTrackData track, UnityEngine.Object obj, float time)
    {
        switch (track.type)
        {
            case WeaponAttackTrackType.Animation:
                if (TryGetAnimationClipFromDraggedObject(obj, out AnimationClip animationClip))
                {
                    AddAnimationClip(track, animationClip, time);
                    return time + Mathf.Max(0.01f, animationClip.length);
                }
                break;
            case WeaponAttackTrackType.Audio:
                if (obj is AudioClip audioClip)
                {
                    AddAudioClip(track, audioClip, time);
                    return time + Mathf.Max(0.2f, audioClip.length);
                }
                break;
            case WeaponAttackTrackType.Effect:
                if (obj is GameObject prefab)
                {
                    AddEffectClip(track, prefab, time);
                    return time + 0.5f;
                }
                break;
        }

        return time;
    }

    private List<AnimationClip> CollectDraggedAnimationClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        HashSet<int> clipIds = new HashSet<int>();
        HashSet<string> requestedNames = new HashSet<string>(StringComparer.Ordinal);

        AddAnimationClipsFromObjects(DragAndDrop.objectReferences, clips, clipIds, true);
        AddRequestedAnimationNames(DragAndDrop.objectReferences, requestedNames);
        AddRequestedAnimationNames(Selection.objects, requestedNames);
        if (clips.Count > 0)
        {
            return clips;
        }

        AddAnimationClipsFromObjects(Selection.objects, clips, clipIds, true);
        if (clips.Count > 0)
        {
            return clips;
        }

        AddNamedAnimationClipsFromObjects(DragAndDrop.objectReferences, requestedNames, clips, clipIds);
        AddNamedAnimationClipsFromObjects(Selection.objects, requestedNames, clips, clipIds);
        if (clips.Count > 0)
        {
            return clips;
        }

        if (DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddNamedAnimationClipsFromPath(DragAndDrop.paths[i], requestedNames, clips, clipIds);
            }
        }

        if (clips.Count > 0)
        {
            return clips;
        }

        AddAnimationClipsFromObjects(DragAndDrop.objectReferences, clips, clipIds, false);
        if (clips.Count == 0 && DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddAnimationClipsFromPath(DragAndDrop.paths[i], clips, clipIds);
            }
        }

        return clips;
    }

    private static void AddRequestedAnimationNames(UnityEngine.Object[] objects, HashSet<string> requestedNames)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && !string.IsNullOrEmpty(objects[i].name))
            {
                requestedNames.Add(objects[i].name);
            }
        }
    }

    private static void AddAnimationClipsFromObjects(UnityEngine.Object[] objects, List<AnimationClip> clips, HashSet<int> clipIds, bool directOnly)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is AnimationClip clip && !IsPreviewAnimationClip(clip))
            {
                AddUniqueAnimationClip(clip, clips, clipIds);
                continue;
            }

            if (!directOnly)
            {
                AddAnimationClipsFromPath(AssetDatabase.GetAssetPath(objects[i]), clips, clipIds);
            }
        }
    }

    private static void AddNamedAnimationClipsFromObjects(UnityEngine.Object[] objects, HashSet<string> requestedNames, List<AnimationClip> clips, HashSet<int> clipIds)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            AddNamedAnimationClipsFromPath(AssetDatabase.GetAssetPath(objects[i]), requestedNames, clips, clipIds);
        }
    }

    private static void AddAnimationClipsFromPath(string path, List<AnimationClip> clips, HashSet<int> clipIds)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
        if (mainAsset is AnimationClip mainClip && !IsPreviewAnimationClip(mainClip))
        {
            AddUniqueAnimationClip(mainClip, clips, clipIds);
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip assetClip && !IsPreviewAnimationClip(assetClip))
            {
                AddUniqueAnimationClip(assetClip, clips, clipIds);
            }
        }
    }

    private static void AddNamedAnimationClipsFromPath(string path, HashSet<string> requestedNames, List<AnimationClip> clips, HashSet<int> clipIds)
    {
        if (string.IsNullOrEmpty(path) || requestedNames == null || requestedNames.Count == 0)
        {
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip assetClip && !IsPreviewAnimationClip(assetClip) && requestedNames.Contains(assetClip.name))
            {
                AddUniqueAnimationClip(assetClip, clips, clipIds);
            }
        }
    }

    private static void AddUniqueAnimationClip(AnimationClip clip, List<AnimationClip> clips, HashSet<int> clipIds)
    {
        if (clip == null || !clipIds.Add(clip.GetInstanceID()))
        {
            return;
        }

        clips.Add(clip);
    }
}
