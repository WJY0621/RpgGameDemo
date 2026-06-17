using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void DrawTimeline(Rect rect)
    {
        if (currentAsset == null)
        {
            GUI.Label(rect, "暂无可编辑的武器攻击效果。", centeredLabelStyle);
            return;
        }

        float timelineDuration = GetTimelineDuration();
        HandleTimelineWheelScale(rect, timelineDuration);
        float canvasDuration = GetTimelineCanvasDuration(rect, timelineDuration, pixelsPerSecond);
        playheadTime = Mathf.Clamp(playheadTime, 0f, canvasDuration);

        float contentWidth = canvasDuration * pixelsPerSecond;
        float contentHeight = RulerHeight + Mathf.Max(1, currentAsset.tracks.Count) * TrackHeight;
        Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);
        timelineScroll.x = Mathf.Clamp(timelineScroll.x, 0f, Mathf.Max(0f, contentWidth - rect.width));
        HandleTimelinePan(rect, contentWidth);
        timelineScroll = GUI.BeginScrollView(rect, timelineScroll, viewRect);

        DrawTimeRuler(new Rect(0f, 0f, contentWidth, RulerHeight), canvasDuration);
        DrawPreviewRangeFill(canvasDuration, contentWidth);

        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            WeaponAttackTrackData track = currentAsset.tracks[i];
            Rect row = new Rect(0f, RulerHeight + i * TrackHeight, contentWidth, TrackHeight);
            EditorGUI.DrawRect(row, i % 2 == 0 ? new Color(0.16f, 0.17f, 0.18f) : new Color(0.13f, 0.14f, 0.15f));
            DrawVerticalGrid(row, canvasDuration, contentWidth);
            DrawTrackClips(track, row, canvasDuration);
            HandleTrackDrop(rect, row, track, canvasDuration, contentWidth);
        }

        if (currentAsset.tracks.Count == 0)
        {
            GUI.Label(new Rect(0f, RulerHeight + 20f, contentWidth, 28f), "右键左侧区域新建轨道，然后把动画、音频或特效资源拖到轨道上。", centeredLabelStyle);
        }

        DrawInvalidDragGhost(contentWidth, canvasDuration);
        DrawSnapLine(contentWidth, canvasDuration, contentHeight);
        DrawPreviewRangeBoundaries(canvasDuration, contentWidth, contentHeight);
        DrawPlayhead(contentWidth, canvasDuration, contentHeight);
        HandleClipDrag(contentWidth, canvasDuration);
        HandleTimelineScrub(rect, contentWidth, canvasDuration);

        GUI.EndScrollView();
    }

    private void DrawTrackClips(WeaponAttackTrackData track, Rect row, float duration)
    {
        switch (track.type)
        {
            case WeaponAttackTrackType.Animation:
                DrawAnimationClips(track, row, duration);
                break;
            case WeaponAttackTrackType.Audio:
                DrawAudioClips(track, row, duration);
                break;
            case WeaponAttackTrackType.Effect:
                DrawEffectClips(track, row, duration);
                break;
            case WeaponAttackTrackType.Hitbox:
                DrawHitboxClips(track, row, duration);
                break;
        }
    }

    private void DrawAnimationClips(WeaponAttackTrackData track, Rect row, float duration)
    {
        HandleManualAnimationKeyframeInput(track, row, duration);

        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentAsset.animationEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? clip.animationClip.length : clip.duration;
            Rect clipRect = TimeRangeToRect(clip.startTime, clip.startTime + clipDuration, duration, row);
            DrawClipBlock(clipRect, track.color, clip.animationClip != null ? clip.animationClip.name : GetClipLabel(clip.label, "Animation Clip"), new SelectedClip(ClipKind.Animation, i));
        }

        DrawTouchingClipSeparators(track, row, duration);
        DrawAnimationBlendRegions(track, row, duration);
        DrawManualAnimationKeyframes(track, row, duration);
        HandleAnimationKeyframeDrag(row.width, duration);
    }

    private void DrawAudioClips(WeaponAttackTrackData track, Rect row, float duration)
    {
        for (int i = 0; i < currentAsset.audioEvents.Count; i++)
        {
            WeaponAudioEvent clip = currentAsset.audioEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            float clipDuration = Mathf.Max(0.01f, clip.duration);
            Rect clipRect = TimeRangeToRect(clip.triggerTime, clip.triggerTime + clipDuration, duration, row);
            DrawClipBlock(clipRect, track.color, GetClipLabel(clip.label, clip.audioClip != null ? clip.audioClip.name : "Audio Clip"), new SelectedClip(ClipKind.Audio, i));
        }
    }

    private void DrawHitboxClips(WeaponAttackTrackData track, Rect row, float duration)
    {
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            WeaponHitboxEvent clip = currentAsset.hitboxEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = TimeRangeToRect(clip.beginWindowTime, clip.endWindowTime, duration, row);
            DrawClipBlock(clipRect, track.color, GetClipLabel(clip.label, "Hitbox Clip"), new SelectedClip(ClipKind.Hitbox, i));

            float applyX = TimeToX(clip.applyDamageTime, duration, row.width);
            EditorGUI.DrawRect(new Rect(applyX - 0.5f, clipRect.y, 1f, clipRect.height), Color.white);
        }
    }

    private void DrawAnimationKeyMarkers(AnimationClip clip, float startTime, Rect row, float duration)
    {
        if (clip == null)
        {
            return;
        }

        HashSet<float> keyTimes = CollectAnimationKeyTimes(clip);
        foreach (float keyTime in keyTimes)
        {
            float x = TimeToX(Mathf.Clamp(startTime + keyTime, 0f, duration), duration, row.width);
            EditorGUI.DrawRect(new Rect(x, row.y + 5f, 1f, row.height - 10f), new Color(0.24f, 0.95f, 0.42f, 0.48f));
        }
    }

    private void DrawClipBlock(Rect rect, Color color, string label, SelectedClip clipRef)
    {
        HandleClipResizeCursor(rect, clipRef);

        bool selected = selectedClip.Equals(clipRef);
        bool invalidDragClip = dragInvalidOnActualClip && dragPlacementInvalid && dragClip.Equals(clipRef);
        Rect selectedRect = new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f);
        if (selected)
        {
            EditorGUI.DrawRect(selectedRect, invalidDragClip ? new Color(1f, 0.58f, 0.58f) : new Color(0.78f, 0.86f, 1f));
        }

        Rect bodyRect = new Rect(rect.x, rect.y, rect.width, rect.height);
        EditorGUI.DrawRect(bodyRect, invalidDragClip ? new Color(0.46f, 0.13f, 0.13f) : new Color(0.22f, 0.24f, 0.27f));
        EditorGUI.DrawRect(new Rect(bodyRect.x, bodyRect.yMax - 4f, bodyRect.width, 4f), invalidDragClip ? new Color(1f, 0.22f, 0.22f) : color);
        EditorGUI.DrawRect(new Rect(bodyRect.x, bodyRect.y, bodyRect.width, 1f), invalidDragClip ? new Color(1f, 0.65f, 0.65f) : new Color(0.34f, 0.36f, 0.39f));
        GUI.Label(new Rect(bodyRect.x + 4f, bodyRect.y + 1f, bodyRect.width - 8f, bodyRect.height - 5f), label, centeredLabelStyle);

        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            selectedClip = clipRef;
            selectedAnimationKeyframeIndex = -1;
            selectedTrackIndex = FindTrackIndexForClip(clipRef);
            GUI.FocusControl(null);

            if (evt.button == 1)
            {
                ShowClipContextMenu(clipRef);
            }
            else if (evt.button == 0)
            {
                ClipResizeEdge edge = GetClipResizeEdge(rect, clipRef, evt.mousePosition);
                if (edge != ClipResizeEdge.None)
                {
                    BeginClipResize(clipRef, edge, evt.mousePosition.x);
                }
                else
                {
                    isDraggingClip = true;
                    dragClip = clipRef;
                    dragStartMouseX = evt.mousePosition.x;
                    dragStartMouseY = evt.mousePosition.y;
                    dragStartClipTime = GetClipStartTime(clipRef);
                    dragStartTrackId = GetClipTrackId(clipRef);
                    dragCandidateTrackId = dragStartTrackId;
                    dragCandidateClipTime = dragStartClipTime;
                    dragCandidateTrackIndex = selectedTrackIndex;
                    dragPlacementInvalid = false;
                    dragInvalidOnActualClip = false;
                    hasSnapLine = false;
                }
            }

            Repaint();
            evt.Use();
        }
    }

    private void HandleClipResizeCursor(Rect rect, SelectedClip clipRef)
    {
        if (!CanResizeClip(clipRef))
        {
            return;
        }

        const float handleWidth = 6f;
        EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, handleWidth, rect.height), MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(new Rect(rect.xMax - handleWidth, rect.y, handleWidth, rect.height), MouseCursor.ResizeHorizontal);
    }

    private ClipResizeEdge GetClipResizeEdge(Rect rect, SelectedClip clipRef, Vector2 mousePosition)
    {
        if (!CanResizeClip(clipRef))
        {
            return ClipResizeEdge.None;
        }

        const float handleWidth = 7f;
        if (new Rect(rect.x, rect.y, handleWidth, rect.height).Contains(mousePosition))
        {
            return ClipResizeEdge.Left;
        }

        if (new Rect(rect.xMax - handleWidth, rect.y, handleWidth, rect.height).Contains(mousePosition))
        {
            return ClipResizeEdge.Right;
        }

        return ClipResizeEdge.None;
    }

    private void BeginClipResize(SelectedClip clipRef, ClipResizeEdge edge, float mouseX)
    {
        isResizingClip = true;
        resizeClip = clipRef;
        resizeEdge = edge;
        resizeStartMouseX = mouseX;
        resizeStartClipStart = GetClipStartTime(clipRef);
        resizeStartClipDuration = GetClipDuration(clipRef);
        hasSnapLine = false;
    }

    private void HandleClipDrag(float contentWidth, float duration)
    {
        if (isResizingClip)
        {
            HandleClipResize(contentWidth, duration);
            return;
        }

        if (!isDraggingClip)
        {
            return;
        }

        Event evt = Event.current;
        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            float deltaX = evt.mousePosition.x - dragStartMouseX;
            float deltaTime = deltaX / Mathf.Max(1f, contentWidth) * duration;
            float targetTime = Mathf.Clamp(dragStartClipTime + deltaTime, 0f, duration);
            int targetTrackIndex = GetTrackIndexAtTimelineY(evt.mousePosition.y);
            WeaponAttackTrackData targetTrack = IsValidIndex(currentAsset.tracks, targetTrackIndex) ? currentAsset.tracks[targetTrackIndex] : null;
            bool validTargetTrack = targetTrack != null && !targetTrack.locked && TrackAcceptsClip(targetTrack, dragClip);
            string targetTrackId = validTargetTrack ? targetTrack.id : dragStartTrackId;

            dragCandidateTrackIndex = targetTrackIndex;
            dragCandidateTrackId = targetTrackId;
            float snappedTime = SnapTime(targetTime, dragClip, contentWidth, duration);
            dragCandidateClipTime = snappedTime;

            bool invalidTrack = targetTrack != null && !validTargetTrack;
            bool invalidOverlap = validTargetTrack && WouldFullyOverlapClip(dragClip.kind, dragClip.index, targetTrackId, snappedTime, GetClipDuration(dragClip));
            dragPlacementInvalid = invalidTrack || invalidOverlap;
            dragInvalidOnActualClip = validTargetTrack && dragPlacementInvalid;

            if (validTargetTrack)
            {
                SetClipTrackId(dragClip, targetTrackId);
                SelectTrack(targetTrackId);
                SetClipStartTime(dragClip, snappedTime, duration);
            }
            else
            {
                SetClipTrackId(dragClip, dragStartTrackId);
                SetClipStartTime(dragClip, dragStartClipTime, duration);
                hasSnapLine = false;
            }

            EditorUtility.SetDirty(currentAsset);
            Repaint();
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
        {
            if (dragPlacementInvalid)
            {
                SetClipTrackId(dragClip, dragStartTrackId);
                SetClipStartTime(dragClip, dragStartClipTime, duration);
                SelectTrack(dragStartTrackId);
                EditorUtility.SetDirty(currentAsset);
            }

            isDraggingClip = false;
            dragClip = SelectedClip.None;
            dragStartTrackId = string.Empty;
            dragCandidateTrackId = string.Empty;
            dragCandidateTrackIndex = -1;
            dragPlacementInvalid = false;
            dragInvalidOnActualClip = false;
            hasSnapLine = false;
            Repaint();
        }
    }

    private void HandleClipResize(float contentWidth, float duration)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            float deltaX = evt.mousePosition.x - resizeStartMouseX;
            float deltaTime = deltaX / Mathf.Max(1f, contentWidth) * duration;
            const float minClipDuration = 0.01f;
            float start = resizeStartClipStart;
            float end = resizeStartClipStart + resizeStartClipDuration;

            if (resizeEdge == ClipResizeEdge.Left)
            {
                start = Mathf.Clamp(resizeStartClipStart + deltaTime, 0f, end - minClipDuration);
                start = SnapEdgeTime(start, resizeClip, contentWidth, duration);
                start = Mathf.Clamp(start, 0f, end - minClipDuration);
            }
            else if (resizeEdge == ClipResizeEdge.Right)
            {
                float rawEnd = Mathf.Clamp(resizeStartClipStart + resizeStartClipDuration + deltaTime, start + minClipDuration, duration);
                end = SnapEdgeTime(rawEnd, resizeClip, contentWidth, duration);
                end = Mathf.Clamp(end, start + minClipDuration, duration);
            }

            SetClipTimeRange(resizeClip, start, end);
            EditorUtility.SetDirty(currentAsset);
            Repaint();
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
        {
            isResizingClip = false;
            resizeClip = SelectedClip.None;
            resizeEdge = ClipResizeEdge.None;
            hasSnapLine = false;
            Repaint();
            evt.Use();
        }
    }

    private void DrawInvalidDragGhost(float contentWidth, float duration)
    {
        if (!isDraggingClip || !dragPlacementInvalid || dragInvalidOnActualClip || !IsValidIndex(currentAsset.tracks, dragCandidateTrackIndex))
        {
            return;
        }

        Rect row = new Rect(0f, RulerHeight + dragCandidateTrackIndex * TrackHeight, contentWidth, TrackHeight);
        Rect clipRect = TimeRangeToRect(dragCandidateClipTime, dragCandidateClipTime + GetClipDuration(dragClip), duration, row);
        DrawDragGhostClip(clipRect, GetClipDisplayLabel(dragClip));
    }

    private void DrawDragGhostClip(Rect rect, string label)
    {
        EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(1f, 0.58f, 0.58f));
        EditorGUI.DrawRect(rect, new Color(0.46f, 0.13f, 0.13f, 0.82f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 4f, rect.width, 4f), new Color(1f, 0.22f, 0.22f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 0.65f, 0.65f));
        GUI.Label(new Rect(rect.x + 4f, rect.y + 1f, rect.width - 8f, rect.height - 5f), label, centeredLabelStyle);
    }

    private void DrawManualAnimationKeyframes(WeaponAttackTrackData track, Rect row, float duration)
    {
        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            WeaponAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null || keyframe.trackId != track.id)
            {
                continue;
            }

            float x = TimeToX(Mathf.Clamp(keyframe.time, 0f, duration), duration, row.width);
            bool selected = selectedAnimationKeyframeIndex == i;
            Color color = selected ? new Color(0.42f, 1f, 0.48f) : new Color(0.18f, 0.86f, 0.38f);
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(x - 5f, row.y + 1f, 10f, row.height - 2f), new Color(color.r, color.g, color.b, 0.18f));
                EditorGUI.DrawRect(new Rect(x - 2f, row.y + 1f, 4f, row.height - 2f), new Color(color.r, color.g, color.b, 0.98f));
            }
            else
            {
                EditorGUI.DrawRect(new Rect(x, row.y + 3f, 1f, row.height - 6f), new Color(color.r, color.g, color.b, 0.72f));
            }
        }
    }

    private void HandleManualAnimationKeyframeInput(WeaponAttackTrackData track, Rect row, float duration)
    {
        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || currentAsset.animationKeyframes == null)
        {
            return;
        }

        for (int i = currentAsset.animationKeyframes.Count - 1; i >= 0; i--)
        {
            WeaponAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null || keyframe.trackId != track.id)
            {
                continue;
            }

            float x = TimeToX(Mathf.Clamp(keyframe.time, 0f, duration), duration, row.width);
            Rect hitRect = new Rect(x - 8f, row.y, 16f, row.height);
            HandleAnimationKeyframeMouse(hitRect, i, track, row.width, duration);
            if (evt.type == EventType.Used)
            {
                return;
            }
        }
    }

    private void DrawAnimationBlendRegions(WeaponAttackTrackData track, Rect row, float duration)
    {
        List<AnimationBlendRange> ranges = CollectAnimationBlendRanges(track, duration);
        if (ranges.Count == 0)
        {
            return;
        }

        // 先一次性画底色
        Color fillColor = new Color(0.24f, 0.95f, 0.70f, 0.16f);
        for (int i = 0; i < ranges.Count; i++)
        {
            float xMin = TimeToX(ranges[i].startTime, duration, row.width);
            float xMax = TimeToX(ranges[i].endTime, duration, row.width);
            Rect blendRect = new Rect(xMin, row.y + 4f, Mathf.Max(2f, xMax - xMin), row.height - 8f);
            EditorGUI.DrawRect(blendRect, fillColor);
        }

        // 统一一次 BeginGUI/EndGUI 画全部斜纹
        Handles.BeginGUI();
        Handles.color = new Color(0.52f, 1f, 0.78f, 0.28f);
        for (int i = 0; i < ranges.Count; i++)
        {
            float xMin = TimeToX(ranges[i].startTime, duration, row.width);
            float xMax = TimeToX(ranges[i].endTime, duration, row.width);
            Rect blendRect = new Rect(xMin, row.y + 4f, Mathf.Max(2f, xMax - xMin), row.height - 8f);

            // 斜线从 blendRect.x 起步（不再超出左侧）；步长按矩形高度的一半避免过密
            float stripeStep = Mathf.Max(5f, blendRect.height * 0.55f);
            for (float x = blendRect.x; x < blendRect.xMax; x += stripeStep)
            {
                float xEnd = Mathf.Min(x + blendRect.height, blendRect.xMax);
                float yStart = blendRect.yMax;
                float yEnd = blendRect.yMax - (xEnd - x);
                Handles.DrawAAPolyLine(1f, new Vector3(x, yStart, 0f), new Vector3(xEnd, yEnd, 0f));
            }
        }
        Handles.EndGUI();

        // 文字标签在最上层
        for (int i = 0; i < ranges.Count; i++)
        {
            float xMin = TimeToX(ranges[i].startTime, duration, row.width);
            float xMax = TimeToX(ranges[i].endTime, duration, row.width);
            Rect blendRect = new Rect(xMin, row.y + 4f, Mathf.Max(2f, xMax - xMin), row.height - 8f);
            if (blendRect.width >= 34f)
            {
                GUI.Label(new Rect(blendRect.x, blendRect.y + 1f, blendRect.width, blendRect.height - 2f), "Blend", centeredLabelStyle);
            }
        }
    }

    private List<AnimationBlendRange> CollectAnimationBlendRanges(WeaponAttackTrackData track, float duration)
    {
        List<AnimationClipRange> clips = new List<AnimationClipRange>();
        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            WeaponAnimationEvent clip = currentAsset.animationEvents[i];
            if (clip == null || clip.trackId != track.id || !clip.blendWithOverlaps)
            {
                continue;
            }

            float clipDuration = clip.animationClip != null ? clip.animationClip.length : clip.duration;
            float start = Mathf.Clamp(clip.startTime, 0f, duration);
            float end = Mathf.Clamp(clip.startTime + Mathf.Max(0.01f, clipDuration), start, duration);
            clips.Add(new AnimationClipRange(start, end));
        }

        // 两两比较，检测所有 clip 对之间的重叠（不只是相邻对）
        List<AnimationBlendRange> ranges = new List<AnimationBlendRange>();
        for (int i = 0; i < clips.Count; i++)
        {
            for (int j = i + 1; j < clips.Count; j++)
            {
                float overlapStart = Mathf.Max(clips[i].startTime, clips[j].startTime);
                float overlapEnd = Mathf.Min(clips[i].endTime, clips[j].endTime);
                if (overlapEnd > overlapStart)
                {
                    ranges.Add(new AnimationBlendRange(overlapStart, overlapEnd));
                }
            }
        }

        return ranges;
    }

    private void HandleAnimationKeyframeMouse(Rect hitRect, int index, WeaponAttackTrackData track, float contentWidth, float duration)
    {
        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || !hitRect.Contains(evt.mousePosition))
        {
            return;
        }

        selectedAnimationKeyframeIndex = index;
        selectedClip = SelectedClip.None;
        SelectTrack(track.id);
        GUI.FocusControl(null);

        if (evt.button == 0)
        {
            isDraggingAnimationKeyframe = true;
            dragAnimationKeyframeIndex = index;
            dragStartKeyframeMouseX = evt.mousePosition.x;
            dragStartKeyframeTime = currentAsset.animationKeyframes[index].time;
        }
        else if (evt.button == 1)
        {
            ShowAnimationKeyframeContextMenu(index);
        }

        Repaint();
        evt.Use();
    }

    private void HandleAnimationKeyframeDrag(float contentWidth, float duration)
    {
        if (!isDraggingAnimationKeyframe)
        {
            return;
        }

        Event evt = Event.current;
        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            if (IsValidIndex(currentAsset.animationKeyframes, dragAnimationKeyframeIndex))
            {
                float deltaX = evt.mousePosition.x - dragStartKeyframeMouseX;
                float deltaTime = deltaX / Mathf.Max(1f, contentWidth) * duration;
                WeaponAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[dragAnimationKeyframeIndex];
                keyframe.time = Mathf.Clamp(dragStartKeyframeTime + deltaTime, 0f, duration);
                selectedAnimationKeyframeIndex = dragAnimationKeyframeIndex;
                EditorUtility.SetDirty(currentAsset);
                Repaint();
                evt.Use();
            }
        }
        else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
        {
            isDraggingAnimationKeyframe = false;
            dragAnimationKeyframeIndex = -1;
            Repaint();
            evt.Use();
        }
    }

    private void HandleTimelineScrub(Rect visibleRect, float contentWidth, float duration)
    {
        Event evt = Event.current;
        if (isDraggingClip || isResizingClip || isDraggingAnimationKeyframe)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        Rect rulerBounds = new Rect(0f, 0f, contentWidth, RulerHeight);

        if (evt.type == EventType.MouseDown && evt.button == 0 && rulerBounds.Contains(evt.mousePosition))
        {
            isScrubbingTimeline = true;
            timelineScrubControlId = controlId;
            GUIUtility.hotControl = controlId;
            UpdatePlayheadFromMouse(evt.mousePosition.x, contentWidth, duration);
            evt.Use();
            return;
        }

        if (!isScrubbingTimeline)
        {
            return;
        }

        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            UpdatePlayheadFromMouse(evt.mousePosition.x, contentWidth, duration);
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
        {
            if (GUIUtility.hotControl == timelineScrubControlId)
            {
                GUIUtility.hotControl = 0;
            }

            isScrubbingTimeline = false;
            timelineScrubControlId = 0;
            evt.Use();
        }
    }

    private void UpdatePlayheadFromMouse(float mouseX, float contentWidth, float duration)
    {
        playheadTime = Mathf.Clamp(mouseX / Mathf.Max(1f, contentWidth) * duration, 0f, duration);
        Repaint();
    }

    private void DrawTimeRuler(Rect rect, float duration)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.14f));
        DrawBottomLine(rect, new Color(0.23f, 0.25f, 0.27f));

        float effectivePixelsPerSecond = rect.width / Mathf.Max(0.01f, duration);
        RulerStep step = GetRulerStep(effectivePixelsPerSecond);
        float minorStep = Mathf.Max(0.001f, step.minorSeconds);
        float majorStep = Mathf.Max(minorStep, step.majorSeconds);
        int minorCount = Mathf.CeilToInt(duration / minorStep);

        for (int i = 0; i <= minorCount; i++)
        {
            float time = Mathf.Min(duration, i * minorStep);
            float x = TimeToX(time, duration, rect.width);
            bool major = Mathf.Abs(Mathf.Repeat(time, majorStep)) <= minorStep * 0.5f || Mathf.Approximately(time, duration);
            int minorIndexInMajor = Mathf.RoundToInt(Mathf.Repeat(time, majorStep) / minorStep);
            bool middle = !major && minorIndexInMajor == Mathf.RoundToInt(majorStep / minorStep * 0.5f);
            float height = major ? 15f : (middle ? 10f : 5f);
            Color tickColor = major ? new Color(0.66f, 0.70f, 0.76f) : new Color(0.36f, 0.40f, 0.46f);
            EditorGUI.DrawRect(new Rect(x, rect.yMax - height, 1f, height), tickColor);

            if (major)
            {
                GUI.Label(new Rect(x + 3f, rect.y + 1f, 56f, 15f), FormatRulerTime(time), miniDimStyle);
            }
        }
    }

    private void DrawVerticalGrid(Rect row, float duration, float width)
    {
        float effectivePixelsPerSecond = width / Mathf.Max(0.01f, duration);
        RulerStep step = GetRulerStep(effectivePixelsPerSecond);
        float minorStep = Mathf.Max(0.001f, step.minorSeconds);
        float majorStep = Mathf.Max(minorStep, step.majorSeconds);
        int minorCount = Mathf.CeilToInt(duration / minorStep);

        for (int i = 0; i <= minorCount; i++)
        {
            float time = Mathf.Min(duration, i * minorStep);
            float x = TimeToX(time, duration, width);
            bool major = Mathf.Abs(Mathf.Repeat(time, majorStep)) <= minorStep * 0.5f || Mathf.Approximately(time, duration);
            Color color = major ? new Color(0.24f, 0.26f, 0.28f) : new Color(0.17f, 0.18f, 0.19f);
            EditorGUI.DrawRect(new Rect(x, row.y, 1f, row.height), color);
        }
    }

    private void DrawPreviewRangeFill(float timelineDuration, float contentWidth)
    {
        if (!TryGetClipTimeRange(out float startTime, out float endTime))
        {
            return;
        }

        startTime = 0f;
        endTime = Mathf.Clamp(endTime, startTime, timelineDuration);
        float startX = TimeToX(startTime, timelineDuration, contentWidth);
        float endX = TimeToX(endTime, timelineDuration, contentWidth);

        EditorGUI.DrawRect(new Rect(startX, 0f, Mathf.Max(1f, endX - startX), RulerHeight), new Color(0.22f, 0.46f, 1f, 0.14f));
    }

    private void DrawPreviewRangeBoundaries(float timelineDuration, float contentWidth, float height)
    {
        if (!TryGetClipTimeRange(out float startTime, out float endTime))
        {
            return;
        }

        startTime = 0f;
        endTime = Mathf.Clamp(endTime, startTime, timelineDuration);
        float endX = TimeToX(endTime, timelineDuration, contentWidth);

        Color boundaryColor = new Color(0.24f, 0.50f, 1f, 0.62f);
        EditorGUI.DrawRect(new Rect(endX, 0f, 1f, height), boundaryColor);
    }

    private void DrawPlayhead(float width, float duration, float height)
    {
        float x = TimeToX(playheadTime, duration, width);
        EditorGUI.DrawRect(new Rect(x, 0f, 1f, height), new Color(0.92f, 0.95f, 1f));
        EditorGUI.DrawRect(new Rect(x - 4f, 0f, 8f, 7f), new Color(0.92f, 0.95f, 1f));
    }

    private void DrawSnapLine(float width, float duration, float height)
    {
        if (!hasSnapLine || (!isDraggingClip && !isResizingClip))
        {
            return;
        }

        float x = TimeToX(snapLineTime, duration, width);
        Color lineColor = new Color(1f, 0.82f, 0.22f, 0.42f);
        EditorGUI.DrawRect(new Rect(x, RulerHeight, 1f, Mathf.Max(0f, height - RulerHeight)), lineColor);
    }

    private void HandleTimelinePan(Rect rect, float contentWidth)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 2 && rect.Contains(evt.mousePosition))
        {
            isPanningTimeline = true;
            panStartMouseX = evt.mousePosition.x;
            panStartScrollX = timelineScroll.x;
            evt.Use();
            return;
        }

        if (!isPanningTimeline)
        {
            return;
        }

        if (evt.type == EventType.MouseDrag && evt.button == 2)
        {
            float deltaX = evt.mousePosition.x - panStartMouseX;
            float maxScrollX = Mathf.Max(0f, contentWidth - rect.width);
            timelineScroll.x = Mathf.Clamp(panStartScrollX - deltaX, 0f, maxScrollX);
            Repaint();
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
        {
            isPanningTimeline = false;
            evt.Use();
        }
    }

    private void HandleTimelineWheelScale(Rect rect, float timelineDuration)
    {
        Event evt = Event.current;
        if (evt.type != EventType.ScrollWheel || !rect.Contains(evt.mousePosition))
        {
            return;
        }

        float oldPixelsPerSecond = pixelsPerSecond;
        float scaleFactor = evt.delta.y > 0f ? 0.88f : 1.14f;
        float newPixelsPerSecond = Mathf.Clamp(oldPixelsPerSecond * scaleFactor, 20f, 720f);
        if (Mathf.Approximately(oldPixelsPerSecond, newPixelsPerSecond))
        {
            evt.Use();
            return;
        }

        float mouseXInViewport = evt.mousePosition.x - rect.x;
        float timeUnderMouse = (timelineScroll.x + mouseXInViewport) / oldPixelsPerSecond;
        pixelsPerSecond = newPixelsPerSecond;

        float newCanvasDuration = GetTimelineCanvasDuration(rect, timelineDuration, pixelsPerSecond);
        float newContentWidth = newCanvasDuration * pixelsPerSecond;
        timelineScroll.x = Mathf.Clamp(timeUnderMouse * pixelsPerSecond - mouseXInViewport, 0f, Mathf.Max(0f, newContentWidth - rect.width));

        Repaint();
        evt.Use();
    }

}
