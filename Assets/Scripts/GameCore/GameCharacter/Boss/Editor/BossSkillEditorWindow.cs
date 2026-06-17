#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class BossSkillEditorWindow : EditorWindow
{
    private const string SaveFolder = "Assets/GameData/BossSkillData";
    private const float TopHeight = 32f;
    private const float BottomHeight = 30f;
    private const float LeftWidth = 230f;
    private const float InspectorWidth = 318f;
    private const float TrackHeight = 32f;
    private const float RulerHeight = 24f;
    private static Type editorAudioUtilType;
    private static MethodInfo playPreviewClipMethod;
    private static MethodInfo stopAllPreviewClipsMethod;

    private BossSkillSO currentAsset;
    private SerializedObject serializedAsset;
    private GameObject previewTarget;
    private Animator previewAnimator;
    private readonly Dictionary<int, GameObject> previewBarrageInstances = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> previewVFXInstances = new Dictionary<int, GameObject>();
    private Vector2 timelineScroll;
    private Vector2 inspectorScroll;
    private int selectedTrackIndex = -1;
    private SelectedClip selectedClip = SelectedClip.None;
    private int selectedKeyframeIndex = -1;
    private float playheadTime;
    private float pixelsPerSecond = 190f;
    private int previewFps = 60;
    private bool isPreviewing;
    private bool loopPreview = true;
    private double lastPreviewTime;
    private bool[] previewAudioTriggered;
    private bool isDraggingClip;
    private SelectedClip dragClip = SelectedClip.None;
    private float dragStartMouseX;
    private float dragStartClipTime;
    private string dragStartTrackId;
    private bool isResizingClip;
    private SelectedClip resizeClip = SelectedClip.None;
    private ClipResizeEdge resizeEdge = ClipResizeEdge.None;
    private float resizeStartMouseX;
    private float resizeStartClipStart;
    private float resizeStartClipDuration;
    private bool isDraggingKeyframe;
    private int dragKeyframeIndex = -1;
    private float dragStartKeyframeMouseX;
    private float dragStartKeyframeTime;
    private bool isDraggingPlayhead;
    private bool hasSnapLine;
    private float snapLineTime;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle centeredStyle;
    private GUIStyle inspectorTitleStyle;
    private GUIStyle dimStyle;

    private enum ClipKind
    {
        None,
        Animation,
        Audio,
        VFX,
        Barrage,
        Hitbox,
        ContactDamage,
        Movement
    }

    private enum ClipResizeEdge
    {
        None,
        Left,
        Right
    }

    private readonly struct SelectedClip
    {
        public readonly ClipKind kind;
        public readonly int index;

        public static SelectedClip None => new SelectedClip(ClipKind.None, -1);

        public SelectedClip(ClipKind kind, int index)
        {
            this.kind = kind;
            this.index = index;
        }

        public bool IsValid => kind != ClipKind.None && index >= 0;
    }

    [MenuItem("Tools/Boss Skill Editor")]
    public static void OpenWindow()
    {
        BossSkillEditorWindow window = GetWindow<BossSkillEditorWindow>();
        window.titleContent = new GUIContent("Boss技能编辑器");
        window.minSize = new Vector2(1100f, 560f);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("Boss技能编辑器");
        EditorApplication.update += OnEditorUpdate;
        Selection.selectionChanged += OnSelectionChanged;
        SceneView.duringSceneGui += OnSceneGUI;
        TryUseSelectedAsset();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Selection.selectionChanged -= OnSelectionChanged;
        SceneView.duringSceneGui -= OnSceneGUI;
        StopPreview();
        StopAnimationSampling();
        CleanupBarragePreviewInstances();
        CleanupVFXPreviewInstances();
    }

    private void OnGUI()
    {
        EnsureStyles();
        SyncDurationFromTimeline();
        DrawBackground();
        DrawTopBar();
        DrawMainArea();
        DrawBottomBar();
        HandleKeyboard();
        ApplyPreviewAtTime();
    }

    private void SyncDurationFromTimeline()
    {
        if (currentAsset == null)
        {
            return;
        }

        float previousDuration = currentAsset.totalDuration;
        currentAsset.SyncDurationFromTimeline();
        if (!Mathf.Approximately(previousDuration, currentAsset.totalDuration))
        {
            EditorUtility.SetDirty(currentAsset);
        }
    }

    private void DrawTopBar()
    {
        Rect rect = new Rect(0f, 0f, position.width, TopHeight);
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.14f));
        DrawLine(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.05f, 0.05f, 0.05f));

        Rect row = new Rect(8f, 5f, position.width - 16f, 22f);
        float x = row.x;

        EditorGUI.BeginChangeCheck();
        currentAsset = (BossSkillSO)EditorGUI.ObjectField(new Rect(x, row.y, 178f, row.height), currentAsset, typeof(BossSkillSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            BindAsset(currentAsset);
        }
        x += 186f;

        bool hasAsset = currentAsset != null;
        GUI.enabled = hasAsset;
        if (hasAsset)
        {
            serializedAsset.Update();
            GUI.Label(new Rect(x, row.y + 3f, 44f, row.height), "名称", dimStyle);
            SerializedProperty displayName = serializedAsset.FindProperty("displayName");
            displayName.stringValue = EditorGUI.TextField(new Rect(x + 40f, row.y, 126f, row.height), displayName.stringValue);
            x += 174f;

            GUI.Label(new Rect(x, row.y + 3f, 44f, row.height), "类型", dimStyle);
            SerializedProperty releaseType = serializedAsset.FindProperty("releaseType");
            EditorGUI.PropertyField(new Rect(x + 40f, row.y, 96f, row.height), releaseType, GUIContent.none);
            x += 144f;

            GUI.Label(new Rect(x, row.y + 3f, 58f, row.height), "距离", dimStyle);
            SerializedProperty minRange = serializedAsset.FindProperty("minUseRange");
            SerializedProperty maxRange = serializedAsset.FindProperty("maxUseRange");
            minRange.floatValue = EditorGUI.FloatField(new Rect(x + 44f, row.y, 42f, row.height), minRange.floatValue);
            maxRange.floatValue = EditorGUI.FloatField(new Rect(x + 90f, row.y, 42f, row.height), maxRange.floatValue);
            x += 140f;

            serializedAsset.ApplyModifiedProperties();
        }

        GUI.enabled = true;
        GUI.Label(new Rect(x, row.y + 3f, 58f, row.height), "预览Boss", dimStyle);
        EditorGUI.BeginChangeCheck();
        previewTarget = (GameObject)EditorGUI.ObjectField(new Rect(x + 58f, row.y, 150f, row.height), previewTarget, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            previewAnimator = previewTarget != null ? previewTarget.GetComponentInChildren<Animator>(true) : null;
            StopAnimationSampling();
            CleanupBarragePreviewInstances();
            CleanupVFXPreviewInstances();
        }
        x += 216f;

        if (GUI.Button(new Rect(x, row.y, 44f, row.height), "新建"))
        {
            CreateAsset();
        }
        x += 50f;

        GUI.enabled = hasAsset;
        if (GUI.Button(new Rect(x, row.y, 48f, row.height), isPreviewing ? "停止" : "播放"))
        {
            TogglePreview();
        }
        x += 54f;

        loopPreview = GUI.Toggle(new Rect(x, row.y, 52f, row.height), loopPreview, "循环", "Button");
        x += 60f;

        GUI.Label(new Rect(x, row.y + 3f, 34f, row.height), "时长", dimStyle);
        if (hasAsset)
        {
            EditorGUI.LabelField(new Rect(x + 34f, row.y + 3f, 52f, row.height), currentAsset.totalDuration.ToString("0.###"), dimStyle);
        }
        x += 94f;

        GUI.Label(new Rect(x, row.y + 3f, 28f, row.height), "FPS", dimStyle);
        previewFps = Mathf.Clamp(EditorGUI.IntField(new Rect(x + 30f, row.y, 42f, row.height), previewFps), 1, 120);
        GUI.enabled = true;
    }

    private void DrawMainArea()
    {
        Rect main = new Rect(0f, TopHeight, position.width, position.height - TopHeight - BottomHeight);
        Rect left = new Rect(main.x, main.y, LeftWidth, main.height);
        Rect timeline = new Rect(left.xMax, main.y, main.width - LeftWidth - InspectorWidth, main.height);
        Rect inspector = new Rect(timeline.xMax, main.y, InspectorWidth, main.height);

        EditorGUI.DrawRect(left, new Color(0.145f, 0.145f, 0.15f));
        EditorGUI.DrawRect(timeline, new Color(0.12f, 0.12f, 0.125f));
        EditorGUI.DrawRect(inspector, new Color(0.155f, 0.155f, 0.16f));

        DrawTrackList(left);
        DrawTimeline(timeline);
        DrawInspector(inspector);
    }

    private void DrawTrackList(Rect rect)
    {
        Rect header = new Rect(rect.x, rect.y, rect.width, RulerHeight);
        EditorGUI.DrawRect(header, new Color(0.11f, 0.11f, 0.115f));
        GUI.Label(new Rect(header.x + 12f, header.y + 4f, header.width - 24f, 16f), "Boss Skill Tracks", dimStyle);

        if (currentAsset == null)
        {
            GUI.Label(new Rect(rect.x + 14f, rect.y + 70f, rect.width - 28f, 40f), "选择或新建 BossSkillSO", centeredStyle);
            return;
        }

        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            BossSkillTrackData track = currentAsset.tracks[i];
            if (track == null)
            {
                continue;
            }

            track.EnsureId();
            Rect row = new Rect(rect.x, rect.y + RulerHeight + i * TrackHeight, rect.width, TrackHeight);
            bool selected = selectedTrackIndex == i && !selectedClip.IsValid;
            EditorGUI.DrawRect(row, selected ? new Color(0.22f, 0.20f, 0.18f) : (i % 2 == 0 ? new Color(0.16f, 0.16f, 0.165f) : new Color(0.135f, 0.135f, 0.14f)));
            EditorGUI.DrawRect(new Rect(row.x + 8f, row.y + 6f, 5f, row.height - 12f), track.color);
            GUI.Label(new Rect(row.x + 22f, row.y + 2f, 124f, 17f), track.displayName, titleStyle);
            GUI.Label(new Rect(row.x + 22f, row.y + 17f, 124f, 13f), track.type.ToString(), subtitleStyle);

            if ((track.type == BossSkillTrackType.Animation || track.type == BossSkillTrackType.Hitbox) &&
                GUI.Button(new Rect(row.xMax - 56f, row.y + 7f, 22f, 18f), "K"))
            {
                if (track.type == BossSkillTrackType.Animation)
                {
                    AddAnimationKeyframe(track);
                }
                else
                {
                    KeyHitboxApplyTime(track);
                }
            }

            if (GUI.Button(new Rect(row.xMax - 30f, row.y + 7f, 22f, 18f), "+"))
            {
                AddEmptyClip(track);
            }

            HandleTrackMouse(row, i, track);
        }

        if (currentAsset.tracks.Count == 0)
        {
            GUI.Label(new Rect(rect.x + 16f, rect.y + 70f, rect.width - 32f, 48f), "右键创建动作 / 音效 / 弹幕 / 伤害检测盒轨道", centeredStyle);
        }

        Event evt = Event.current;
        if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
        {
            ShowCreateTrackMenu();
            evt.Use();
        }
    }

    private void DrawTimeline(Rect rect)
    {
        if (currentAsset == null)
        {
            GUI.Label(new Rect(rect.x, rect.y + 70f, rect.width, 40f), "Boss 技能时间轴会显示在这里", centeredStyle);
            return;
        }

        float timelineDuration = Mathf.Max(0.01f, currentAsset.totalDuration);
        float viewportDuration = Mathf.Max(0.01f, (rect.width - 16f) / Mathf.Max(1f, pixelsPerSecond));
        float duration = Mathf.Max(timelineDuration + Mathf.Max(1f, viewportDuration * 0.5f), viewportDuration);
        float contentWidth = Mathf.Max(rect.width - 16f, duration * pixelsPerSecond);
        float contentHeight = Mathf.Max(rect.height - 16f, RulerHeight + currentAsset.tracks.Count * TrackHeight + 40f);

        Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
        Rect content = new Rect(0f, 0f, contentWidth, contentHeight);

        timelineScroll = GUI.BeginScrollView(view, timelineScroll, content);

        Rect ruler = new Rect(0f, 0f, contentWidth, RulerHeight);
        DrawRuler(ruler, duration);

        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            BossSkillTrackData track = currentAsset.tracks[i];
            Rect row = new Rect(0f, RulerHeight + i * TrackHeight, contentWidth, TrackHeight);
            EditorGUI.DrawRect(row, i % 2 == 0 ? new Color(0.13f, 0.13f, 0.135f) : new Color(0.115f, 0.115f, 0.12f));
            DrawTrackClips(row, track, duration);
            HandleTrackDrop(row, track, duration, contentWidth);
        }

        DrawSnapLine(contentHeight, duration);
        Rect playheadHandle = DrawPlayhead(contentHeight, duration);
        HandlePlayheadDrag(playheadHandle, duration);
        HandleTimelineMouse(new Rect(0f, 0f, contentWidth, contentHeight), duration, contentWidth);
        GUI.EndScrollView();
    }

    private void DrawRuler(Rect rect, float duration)
    {
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.105f));
        int majorCount = Mathf.CeilToInt(duration * 2f);
        for (int i = 0; i <= majorCount; i++)
        {
            float time = i * 0.5f;
            float x = TimeToX(time);
            bool full = i % 2 == 0;
            Color line = full ? new Color(0.32f, 0.32f, 0.34f) : new Color(0.22f, 0.22f, 0.235f);
            EditorGUI.DrawRect(new Rect(x, rect.y + (full ? 3f : 10f), 1f, rect.height), line);
            if (full)
            {
                GUI.Label(new Rect(x + 4f, rect.y + 3f, 48f, 16f), time.ToString("0.0"), subtitleStyle);
            }
        }
    }

    private void DrawTrackClips(Rect row, BossSkillTrackData track, float duration)
    {
        if (track == null)
        {
            return;
        }

        switch (track.type)
        {
            case BossSkillTrackType.Animation:
                DrawAnimationClips(row, track, duration);
                DrawAnimationKeyframes(row, track, duration);
                break;
            case BossSkillTrackType.Audio:
                DrawAudioClips(row, track, duration);
                break;
            case BossSkillTrackType.VFX:
                DrawVFXClips(row, track, duration);
                break;
            case BossSkillTrackType.Barrage:
                DrawBarrageClips(row, track, duration);
                break;
            case BossSkillTrackType.Hitbox:
                DrawHitboxClips(row, track, duration);
                break;
            case BossSkillTrackType.ContactDamage:
                DrawContactDamageClips(row, track, duration);
                break;
            case BossSkillTrackType.Movement:
                DrawMovementClips(row, track, duration);
                break;
        }
    }

    private void DrawAnimationClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            BossAnimationClipEvent clip = currentAsset.animationEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = MakeClipRect(row, clip.startTime, clip.Duration, duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.Animation && selectedClip.index == i, GetClipLabel(clip.label, clip.animationClip));
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.Animation, i), clip.startTime, track.id);
        }
    }

    private void DrawAnimationKeyframes(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            BossAnimationKeyframeEvent key = currentAsset.animationKeyframes[i];
            if (key == null || key.trackId != track.id)
            {
                continue;
            }

            float x = TimeToX(key.time);
            Rect keyRect = new Rect(x - 5f, row.y + 6f, 10f, row.height - 12f);
            Color color = selectedKeyframeIndex == i ? Color.white : new Color(1f, 0.78f, 0.25f);
            EditorGUI.DrawRect(new Rect(keyRect.center.x - 1f, keyRect.y, 2f, keyRect.height), color);
            GUI.Label(new Rect(keyRect.x - 5f, keyRect.y - 1f, 20f, 18f), "K", subtitleStyle);
            HandleKeyframeMouse(keyRect, i, key.time);
        }
    }

    private void DrawAudioClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.audioEvents.Count; i++)
        {
            BossAudioClipEvent clip = currentAsset.audioEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = MakeClipRect(row, clip.triggerTime, clip.Duration, duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.Audio && selectedClip.index == i, GetClipLabel(clip.label, clip.audioClip));
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.Audio, i), clip.triggerTime, track.id);
        }
    }

    private void DrawVFXClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
        {
            BossVFXEvent clip = currentAsset.vfxEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = MakeClipRect(row, clip.startTime, Mathf.Max(0.05f, clip.Duration), duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.VFX && selectedClip.index == i, clip.label);
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.VFX, i), clip.startTime, track.id);
        }
    }

    private void DrawBarrageClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.barrageEvents.Count; i++)
        {
            BossBarrageEvent clip = currentAsset.barrageEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = TimePointToMarkerRect(row, clip.triggerTime, duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.Barrage && selectedClip.index == i, string.Empty, false);
            EditorGUI.DrawRect(new Rect(clipRect.center.x - 1f, row.y + 3f, 2f, row.height - 6f), Color.white);
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.Barrage, i), clip.triggerTime, track.id);
        }
    }

    private void DrawHitboxClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            BossHitboxEvent clip = currentAsset.hitboxEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = MakeClipRect(row, clip.beginWindowTime, Mathf.Max(0.05f, clip.WindowDuration), duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.Hitbox && selectedClip.index == i, clip.label);
            float applyX = TimeToX(clip.applyDamageTime);
            EditorGUI.DrawRect(new Rect(applyX, clipRect.y + 2f, 1f, clipRect.height - 4f), Color.white);
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.Hitbox, i), clip.beginWindowTime, track.id);
        }
    }

    private void DrawContactDamageClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.contactDamageEvents.Count; i++)
        {
            BossContactDamageEvent clip = currentAsset.contactDamageEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = MakeClipRect(row, clip.beginWindowTime, Mathf.Max(0.05f, clip.WindowDuration), duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.ContactDamage && selectedClip.index == i, clip.label);
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.ContactDamage, i), clip.beginWindowTime, track.id);
        }
    }

    private void DrawMovementClips(Rect row, BossSkillTrackData track, float duration)
    {
        for (int i = 0; i < currentAsset.movementEvents.Count; i++)
        {
            BossMovementEvent clip = currentAsset.movementEvents[i];
            if (clip == null || clip.trackId != track.id)
            {
                continue;
            }

            Rect clipRect = TimePointToMarkerRect(row, clip.triggerTime, duration);
            DrawClip(clipRect, track.color, selectedClip.kind == ClipKind.Movement && selectedClip.index == i, string.Empty, false);
            EditorGUI.DrawRect(new Rect(clipRect.center.x - 1f, row.y + 3f, 2f, row.height - 6f), Color.white);
            HandleClipMouse(clipRect, new SelectedClip(ClipKind.Movement, i), clip.triggerTime, track.id);
        }
    }

    private Rect MakeClipRect(Rect row, float start, float length, float duration)
    {
        float x = TimeToX(Mathf.Clamp(start, 0f, duration));
        float width = Mathf.Max(8f, length * pixelsPerSecond);
        return new Rect(x, row.y + 5f, width, row.height - 10f);
    }

    private Rect TimePointToMarkerRect(Rect row, float time, float duration)
    {
        const float markerWidth = 18f;
        float x = TimeToX(Mathf.Clamp(time, 0f, duration));
        return new Rect(x - markerWidth * 0.5f, row.y + 5f, markerWidth, row.height - 10f);
    }

    private void DrawClip(Rect rect, Color color, bool selected, string label, bool drawLabel = true)
    {
        Color body = selected ? Color.Lerp(color, Color.white, 0.25f) : color;
        body.a = 0.88f;
        EditorGUI.DrawRect(rect, body);
        DrawBorder(rect, selected ? Color.white : new Color(0f, 0f, 0f, 0.55f));
        if (drawLabel)
        {
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, Mathf.Max(20f, rect.width - 12f), rect.height - 4f), label, titleStyle);
        }
    }

    private Rect DrawPlayhead(float height, float duration)
    {
        playheadTime = Mathf.Clamp(playheadTime, 0f, duration);
        float x = TimeToX(playheadTime);
        EditorGUI.DrawRect(new Rect(x, 0f, 1f, height), new Color(0.92f, 0.95f, 1f));
        Rect handleRect = new Rect(x - 5f, 0f, 10f, 8f);
        EditorGUI.DrawRect(handleRect, new Color(0.92f, 0.95f, 1f));
        EditorGUIUtility.AddCursorRect(new Rect(x - 5f, 0f, 10f, height), MouseCursor.SlideArrow);
        return new Rect(x - 5f, 0f, 10f, height);
    }

    private void DrawSnapLine(float height, float duration)
    {
        if (!hasSnapLine || (!isDraggingClip && !isResizingClip))
        {
            return;
        }

        float x = TimeToX(Mathf.Clamp(snapLineTime, 0f, duration));
        EditorGUI.DrawRect(new Rect(x, RulerHeight, 1f, Mathf.Max(0f, height - RulerHeight)), new Color(1f, 0.82f, 0.22f, 0.55f));
    }

    private void HandlePlayheadDrag(Rect hitRect, float duration)
    {
        Event evt = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (evt.type == EventType.MouseDown && evt.button == 0 && hitRect.Contains(evt.mousePosition))
        {
            isDraggingPlayhead = true;
            GUIUtility.hotControl = controlId;
            UpdatePlayheadFromTimelineMouse(evt.mousePosition.x, duration);
            evt.Use();
            return;
        }

        if (!isDraggingPlayhead || GUIUtility.hotControl != controlId)
        {
            return;
        }

        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            UpdatePlayheadFromTimelineMouse(evt.mousePosition.x, duration);
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp && evt.button == 0)
        {
            isDraggingPlayhead = false;
            GUIUtility.hotControl = 0;
            evt.Use();
        }
    }

    private void UpdatePlayheadFromTimelineMouse(float mouseX, float duration)
    {
        playheadTime = SnapTime(Mathf.Clamp(XToTime(mouseX), 0f, duration));
        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawInspector(Rect rect)
    {
        Rect title = new Rect(rect.x, rect.y, rect.width, 34f);
        EditorGUI.DrawRect(title, new Color(0.13f, 0.13f, 0.14f));
        GUI.Label(new Rect(title.x + 12f, title.y + 8f, title.width - 24f, 18f), selectedClip.IsValid ? "Clip" : "Track / Skill", inspectorTitleStyle);

        GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, rect.height - 52f));
        inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);

        if (currentAsset == null)
        {
            EditorGUILayout.HelpBox("选择 BossSkillSO，或新建一个技能资源。", MessageType.Info);
        }
        else if (selectedClip.IsValid)
        {
            DrawSelectedClipInspector();
        }
        else
        {
            DrawSkillAndTrackInspector();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSkillAndTrackInspector()
    {
        serializedAsset.Update();
        EditorGUILayout.LabelField("Skill", EditorStyles.boldLabel);
        DrawProperty("skillID");
        DrawProperty("displayName");
        DrawProperty("releaseType");
        DrawProperty("cooldown");
        DrawProperty("minUseRange");
        DrawProperty("maxUseRange");
        DrawProperty("weight");
        EditorGUILayout.LabelField("Total Duration", currentAsset.totalDuration.ToString("0.###"));
        DrawProperty("animationStateName");
        DrawProperty("fallbackDuration");
        DrawProperty("stopMovementWhenCasting");
        DrawProperty("faceTargetDuringCast");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Track", EditorStyles.boldLabel);
        if (selectedTrackIndex < 0 || selectedTrackIndex >= currentAsset.tracks.Count)
        {
            EditorGUILayout.HelpBox("在左侧选择轨道，或右键创建轨道。", MessageType.None);
        }
        else
        {
            BossSkillTrackData track = currentAsset.tracks[selectedTrackIndex];
            EditorGUI.BeginChangeCheck();
            track.displayName = EditorGUILayout.TextField("Display Name", track.displayName);
            track.color = EditorGUILayout.ColorField("Color", track.color);
            track.hiddenInPreview = EditorGUILayout.Toggle("Hidden In Preview", track.hiddenInPreview);
            track.locked = EditorGUILayout.Toggle("Locked", track.locked);
            EditorGUILayout.LabelField("Type", track.type.ToString());
            EditorGUILayout.LabelField("Clips", GetTrackClipCount(track).ToString());
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(currentAsset);
            }
        }

        serializedAsset.ApplyModifiedProperties();
        SyncDurationFromTimeline();
    }

    private void DrawSelectedClipInspector()
    {
        SerializedProperty property = GetSelectedClipProperty();
        if (property == null)
        {
            selectedClip = SelectedClip.None;
            return;
        }

        serializedAsset.Update();
        if (selectedClip.kind == ClipKind.Barrage)
        {
            DrawBarrageClipInspector(property);
            serializedAsset.ApplyModifiedProperties();
            SyncDurationFromTimeline();
            return;
        }

        if (selectedClip.kind == ClipKind.VFX)
        {
            DrawVFXClipInspector(property);
            serializedAsset.ApplyModifiedProperties();
            SyncDurationFromTimeline();
            return;
        }

        SerializedProperty child = property.Copy();
        SerializedProperty end = child.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            EditorGUILayout.PropertyField(child, true);
            enterChildren = false;
        }

        EditorGUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("跳到片段"))
        {
            playheadTime = GetClipStartTime(selectedClip);
            Repaint();
        }

        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.22f);
        if (GUILayout.Button("删除片段"))
        {
            DeleteClip(selectedClip);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        serializedAsset.ApplyModifiedProperties();
        SyncDurationFromTimeline();
    }

    private void DrawBarrageClipInspector(SerializedProperty property)
    {
        EditorGUILayout.LabelField("Barrage Trigger", EditorStyles.boldLabel);
        DrawProperty(property, "label");
        DrawProperty(property, "projectileVFXKey");
        DrawProperty(property, "projectilePrefab");
        DrawProperty(property, "hitVFXKey");
        DrawProperty(property, "hitVFXRandomEulerRange");
        DrawProperty(property, "aimSource");
        DrawProperty(property, "targetAimHeightOffset");
        DrawProperty(property, "aimRandomAngle");
        DrawProperty(property, "speed");
        DrawProperty(property, "lifeTime");
        DrawProperty(property, "spawnOffset");
        DrawProperty(property, "spawnRotation");
        DrawProperty(property, "stabilizeVisualRotation");
        DrawProperty(property, "damageMultiplier");
        DrawProperty(property, "hitRadius");
        DrawProperty(property, "hitCenterOffset");
        DrawProperty(property, "projectileCount");
        DrawProperty(property, "spreadAngle");

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox("弹幕片段是时间点触发器，触发时间由它在时间轴上的位置决定。", MessageType.None);

        EditorGUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("跳到触发点"))
        {
            playheadTime = GetClipStartTime(selectedClip);
            Repaint();
        }

        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.22f);
        if (GUILayout.Button("删除触发器"))
        {
            DeleteClip(selectedClip);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    private void DrawVFXClipInspector(SerializedProperty property)
    {
        EditorGUILayout.LabelField("VFX Clip", EditorStyles.boldLabel);
        DrawProperty(property, "label");
        DrawProperty(property, "startTime");
        DrawProperty(property, "endTime");
        DrawProperty(property, "vfxKey");
        DrawProperty(property, "previewPrefab");
        DrawProperty(property, "offset");
        DrawProperty(property, "rotation");
        DrawProperty(property, "followBoss");

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox("普通特效片段会在片段开头出现，在片段末尾停止。Preview Prefab 只用于编辑器预览；运行时优先用 VFX Key 走 GameMgr.VFX。", MessageType.None);

        EditorGUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("跳到片段"))
        {
            playheadTime = GetClipStartTime(selectedClip);
            Repaint();
        }

        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.22f);
        if (GUILayout.Button("删除片段"))
        {
            DeleteClip(selectedClip);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    private void DrawBottomBar()
    {
        Rect rect = new Rect(0f, position.height - BottomHeight, position.width, BottomHeight);
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.14f));
        DrawLine(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.05f, 0.05f, 0.05f));

        float duration = currentAsset != null ? Mathf.Max(0.01f, currentAsset.totalDuration) : 1f;
        Rect sliderRect = new Rect(12f, rect.y + 7f, position.width - 250f, 18f);
        playheadTime = GUI.HorizontalSlider(sliderRect, playheadTime, 0f, duration);
        GUI.Label(new Rect(sliderRect.xMax + 12f, rect.y + 6f, 120f, 18f), $"{playheadTime:0.000}s / {duration:0.000}s", dimStyle);
        GUI.Label(new Rect(rect.xMax - 118f, rect.y + 6f, 104f, 18f), "空格播放  K关键帧", dimStyle);
    }

    private void HandleTrackMouse(Rect row, int index, BossSkillTrackData track)
    {
        Event evt = Event.current;
        if (!row.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            selectedTrackIndex = index;
            selectedClip = SelectedClip.None;
            selectedKeyframeIndex = -1;
            Repaint();
            evt.Use();
        }
        else if (evt.type == EventType.ContextClick)
        {
            selectedTrackIndex = index;
            selectedClip = SelectedClip.None;
            ShowTrackContextMenu(index, track);
            evt.Use();
        }
    }

    private void HandleClipMouse(Rect rect, SelectedClip clip, float startTime, string trackId)
    {
        HandleClipResizeCursor(rect, clip);

        Event evt = Event.current;
        if (!rect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            selectedClip = clip;
            selectedKeyframeIndex = -1;
            ClipResizeEdge edge = GetClipResizeEdge(rect, clip, evt.mousePosition);
            if (edge != ClipResizeEdge.None)
            {
                BeginClipResize(clip, edge, evt.mousePosition.x);
            }
            else
            {
                isDraggingClip = true;
                dragClip = clip;
                dragStartMouseX = evt.mousePosition.x;
                dragStartClipTime = startTime;
                dragStartTrackId = trackId;
                hasSnapLine = false;
            }

            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            evt.Use();
        }
        else if (evt.type == EventType.ContextClick)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete Clip"), false, () => DeleteClip(clip));
            menu.ShowAsContext();
            evt.Use();
        }
    }

    private void HandleClipResizeCursor(Rect rect, SelectedClip clip)
    {
        if (!CanResizeClip(clip))
        {
            return;
        }

        const float handleWidth = 7f;
        EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, handleWidth, rect.height), MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(new Rect(rect.xMax - handleWidth, rect.y, handleWidth, rect.height), MouseCursor.ResizeHorizontal);
    }

    private ClipResizeEdge GetClipResizeEdge(Rect rect, SelectedClip clip, Vector2 mousePosition)
    {
        if (!CanResizeClip(clip))
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

    private bool CanResizeClip(SelectedClip clip)
    {
        return (clip.kind == ClipKind.VFX && clip.index >= 0 && clip.index < currentAsset.vfxEvents.Count) ||
               (clip.kind == ClipKind.Hitbox && clip.index >= 0 && clip.index < currentAsset.hitboxEvents.Count) ||
               (clip.kind == ClipKind.ContactDamage && clip.index >= 0 && clip.index < currentAsset.contactDamageEvents.Count);
    }

    private void BeginClipResize(SelectedClip clip, ClipResizeEdge edge, float mouseX)
    {
        isResizingClip = true;
        resizeClip = clip;
        resizeEdge = edge;
        resizeStartMouseX = mouseX;
        resizeStartClipStart = GetClipStartTime(clip);
        resizeStartClipDuration = GetClipDuration(clip);
        hasSnapLine = false;
    }

    private void HandleKeyframeMouse(Rect rect, int index, float time)
    {
        Event evt = Event.current;
        if (!rect.Contains(evt.mousePosition))
        {
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0)
        {
            selectedClip = SelectedClip.None;
            selectedKeyframeIndex = index;
            isDraggingKeyframe = true;
            dragKeyframeIndex = index;
            dragStartKeyframeMouseX = evt.mousePosition.x;
            dragStartKeyframeTime = time;
            evt.Use();
        }
        else if (evt.type == EventType.ContextClick)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Delete Keyframe"), false, () => DeleteAnimationKeyframe(index));
            menu.ShowAsContext();
            evt.Use();
        }
    }

    private void HandleTimelineMouse(Rect contentRect, float duration, float contentWidth)
    {
        Event evt = Event.current;

        if (isResizingClip && resizeClip.IsValid)
        {
            if (evt.type == EventType.MouseDrag)
            {
                HandleClipResize(evt, contentWidth, duration);
                return;
            }

            if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
            {
                isResizingClip = false;
                resizeClip = SelectedClip.None;
                resizeEdge = ClipResizeEdge.None;
                hasSnapLine = false;
                GUIUtility.hotControl = 0;
                Repaint();
                evt.Use();
                return;
            }
        }

        if (isDraggingClip && dragClip.IsValid)
        {
            if (evt.type == EventType.MouseDrag)
            {
                float delta = (evt.mousePosition.x - dragStartMouseX) / pixelsPerSecond;
                MoveClip(dragClip, SnapClipStartTime(Mathf.Clamp(dragStartClipTime + delta, 0f, duration), dragClip, contentWidth, duration, true), dragStartTrackId);
                EditorUtility.SetDirty(currentAsset);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                isDraggingClip = false;
                dragClip = SelectedClip.None;
                hasSnapLine = false;
                GUIUtility.hotControl = 0;
                evt.Use();
            }
        }

        if (isDraggingKeyframe && dragKeyframeIndex >= 0)
        {
            if (evt.type == EventType.MouseDrag)
            {
                float delta = (evt.mousePosition.x - dragStartKeyframeMouseX) / pixelsPerSecond;
                currentAsset.animationKeyframes[dragKeyframeIndex].time = SnapTime(Mathf.Clamp(dragStartKeyframeTime + delta, 0f, duration));
                EditorUtility.SetDirty(currentAsset);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                isDraggingKeyframe = false;
                dragKeyframeIndex = -1;
                evt.Use();
            }
        }

        if (evt.type == EventType.MouseDown && evt.button == 0 && contentRect.Contains(evt.mousePosition))
        {
            UpdatePlayheadFromTimelineMouse(evt.mousePosition.x, duration);
            selectedClip = SelectedClip.None;
            selectedKeyframeIndex = -1;
            Repaint();
            evt.Use();
        }
    }

    private void HandleClipResize(Event evt, float contentWidth, float duration)
    {
        if (!CanResizeClip(resizeClip))
        {
            return;
        }

        float delta = (evt.mousePosition.x - resizeStartMouseX) / pixelsPerSecond;
        const float minDuration = 0.01f;
        float start = resizeStartClipStart;
        float end = resizeStartClipStart + resizeStartClipDuration;

        if (resizeEdge == ClipResizeEdge.Left)
        {
            start = Mathf.Clamp(resizeStartClipStart + delta, 0f, end - minDuration);
            start = SnapEdgeTime(start, contentWidth, duration, true);
            start = Mathf.Clamp(start, 0f, end - minDuration);
        }
        else if (resizeEdge == ClipResizeEdge.Right)
        {
            end = Mathf.Clamp(resizeStartClipStart + resizeStartClipDuration + delta, start + minDuration, duration);
            end = SnapEdgeTime(end, contentWidth, duration, true);
            end = Mathf.Clamp(end, start + minDuration, duration);
        }

        SetClipTimeRange(resizeClip, start, end);
        currentAsset.SyncDurationFromTimeline();
        EditorUtility.SetDirty(currentAsset);
        Repaint();
        evt.Use();
    }

    private void HandleTrackDrop(Rect row, BossSkillTrackData track, float duration, float contentWidth)
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
        float dropTime = SnapClipStartTime(Mathf.Clamp(XToTime(evt.mousePosition.x), 0f, duration), SelectedClip.None, contentWidth, duration, false);
        if (AddDraggedAssetsToTrack(track, dropTime))
        {
            selectedTrackIndex = currentAsset.tracks.IndexOf(track);
            currentAsset.SyncDurationFromTimeline();
            EditorUtility.SetDirty(currentAsset);
            AssetDatabase.SaveAssets();
            Repaint();
        }
    }

    private bool CanAcceptDraggedObjects(BossSkillTrackData track)
    {
        if (track == null || track.locked)
        {
            return false;
        }

        if (track.type == BossSkillTrackType.Animation)
        {
            return CollectDraggedAnimationClips().Count > 0;
        }

        return track.type switch
        {
            BossSkillTrackType.Audio => CollectDraggedAudioClips().Count > 0,
            BossSkillTrackType.VFX => CollectDraggedPrefabs().Count > 0,
            BossSkillTrackType.Barrage => CollectDraggedPrefabs().Count > 0,
            _ => false
        };
    }

    private bool AddDraggedAssetsToTrack(BossSkillTrackData track, float dropTime)
    {
        if (track == null)
        {
            return false;
        }

        bool added = false;
        switch (track.type)
        {
            case BossSkillTrackType.Animation:
            {
                List<AnimationClip> clips = CollectDraggedAnimationClips();
                for (int i = 0; i < clips.Count; i++)
                {
                    AnimationClip clip = clips[i];
                    AddAnimationClip(track, clip, dropTime);
                    dropTime += Mathf.Max(0.01f, clip.length);
                    added = true;
                }
                break;
            }
            case BossSkillTrackType.Audio:
            {
                List<AudioClip> clips = CollectDraggedAudioClips();
                for (int i = 0; i < clips.Count; i++)
                {
                    AudioClip clip = clips[i];
                    AddAudioClip(track, clip, dropTime);
                    dropTime += Mathf.Max(0.2f, clip.length);
                    added = true;
                }
                break;
            }
            case BossSkillTrackType.Barrage:
            {
                List<GameObject> prefabs = CollectDraggedPrefabs();
                for (int i = 0; i < prefabs.Count; i++)
                {
                    AddBarrageClip(track, prefabs[i], dropTime);
                    dropTime += 0.5f;
                    added = true;
                }
                break;
            }
            case BossSkillTrackType.VFX:
            {
                List<GameObject> prefabs = CollectDraggedPrefabs();
                for (int i = 0; i < prefabs.Count; i++)
                {
                    AddVFXClip(track, prefabs[i], dropTime);
                    dropTime += 1f;
                    added = true;
                }
                break;
            }
        }

        return added;
    }

    private void ShowCreateTrackMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Animation Track"), false, () => AddTrack(BossSkillTrackType.Animation));
        menu.AddItem(new GUIContent("Audio Track"), false, () => AddTrack(BossSkillTrackType.Audio));
        menu.AddItem(new GUIContent("VFX Track"), false, () => AddTrack(BossSkillTrackType.VFX));
        menu.AddItem(new GUIContent("Barrage Track"), false, () => AddTrack(BossSkillTrackType.Barrage));
        menu.AddItem(new GUIContent("Hitbox Track"), false, () => AddTrack(BossSkillTrackType.Hitbox));
        menu.AddItem(new GUIContent("Contact Damage Track"), false, () => AddTrack(BossSkillTrackType.ContactDamage));
        menu.AddItem(new GUIContent("Movement Track"), false, () => AddTrack(BossSkillTrackType.Movement));
        menu.ShowAsContext();
    }

    private void ShowTrackContextMenu(int index, BossSkillTrackData track)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Clip"), false, () => AddEmptyClip(track));
        menu.AddItem(new GUIContent("Duplicate Track"), false, () => DuplicateTrack(track));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Move Up"), false, () => MoveTrack(index, -1));
        menu.AddItem(new GUIContent("Move Down"), false, () => MoveTrack(index, 1));
        menu.AddSeparator(string.Empty);
        menu.AddItem(new GUIContent("Delete Track"), false, () => DeleteTrack(index));
        menu.ShowAsContext();
    }

    private void AddTrack(BossSkillTrackType type)
    {
        BossSkillTrackData track = new BossSkillTrackData
        {
            type = type,
            displayName = GetDefaultTrackName(type),
            color = GetDefaultTrackColor(type)
        };
        track.EnsureId();
        currentAsset.tracks.Add(track);
        selectedTrackIndex = currentAsset.tracks.Count - 1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void AddEmptyClip(BossSkillTrackData track)
    {
        if (track == null)
        {
            return;
        }

        switch (track.type)
        {
            case BossSkillTrackType.Animation:
                AddAnimationClip(track, null, playheadTime);
                break;
            case BossSkillTrackType.Audio:
                AddAudioClip(track, null, playheadTime);
                break;
            case BossSkillTrackType.VFX:
                AddVFXClip(track, null, playheadTime);
                break;
            case BossSkillTrackType.Barrage:
                AddBarrageClip(track, null, playheadTime);
                break;
            case BossSkillTrackType.Hitbox:
                AddHitboxClip(track, playheadTime);
                break;
            case BossSkillTrackType.ContactDamage:
                AddContactDamageClip(track, playheadTime);
                break;
            case BossSkillTrackType.Movement:
                AddMovementClip(track, playheadTime);
                break;
        }

        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void AddAnimationClip(BossSkillTrackData track, AnimationClip clip, float time)
    {
        BossAnimationClipEvent item = new BossAnimationClipEvent
        {
            trackId = track.id,
            animationClip = clip,
            label = clip != null ? clip.name : "Boss Animation",
            startTime = time,
            duration = clip != null ? Mathf.Max(0.01f, clip.length) : 0.5f,
            animatorStateName = clip != null ? clip.name : string.Empty
        };
        currentAsset.animationEvents.Add(item);
        currentAsset.animationStateName = string.IsNullOrWhiteSpace(currentAsset.animationStateName) || currentAsset.animationStateName == "Attack"
            ? item.animatorStateName
            : currentAsset.animationStateName;
        selectedClip = new SelectedClip(ClipKind.Animation, currentAsset.animationEvents.Count - 1);
    }

    private void AddAudioClip(BossSkillTrackData track, AudioClip clip, float time)
    {
        BossAudioClipEvent item = new BossAudioClipEvent
        {
            trackId = track.id,
            audioClip = clip,
            label = clip != null ? clip.name : "Boss Audio",
            soundName = clip != null ? clip.name : string.Empty,
            triggerTime = time,
            duration = clip != null ? Mathf.Max(0.01f, clip.length) : 0.2f
        };
        currentAsset.audioEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.Audio, currentAsset.audioEvents.Count - 1);
    }

    private void AddVFXClip(BossSkillTrackData track, GameObject prefab, float time)
    {
        BossVFXEvent item = new BossVFXEvent
        {
            trackId = track.id,
            previewPrefab = prefab,
            label = prefab != null ? prefab.name : "Boss VFX",
            vfxKey = prefab != null ? prefab.name : string.Empty,
            startTime = time,
            endTime = time + 1f
        };
        currentAsset.vfxEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.VFX, currentAsset.vfxEvents.Count - 1);
    }

    private void AddBarrageClip(BossSkillTrackData track, GameObject prefab, float time)
    {
        BossBarrageEvent item = new BossBarrageEvent
        {
            trackId = track.id,
            projectilePrefab = prefab,
            label = prefab != null ? prefab.name : "Boss Barrage",
            projectileVFXKey = prefab != null ? prefab.name : string.Empty,
            triggerTime = time
        };
        currentAsset.barrageEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.Barrage, currentAsset.barrageEvents.Count - 1);
    }

    private void AddHitboxClip(BossSkillTrackData track, float time)
    {
        BossHitboxEvent item = new BossHitboxEvent
        {
            trackId = track.id,
            beginWindowTime = time,
            applyDamageTime = time + 0.1f,
            endWindowTime = time + 0.35f
        };
        currentAsset.hitboxEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.Hitbox, currentAsset.hitboxEvents.Count - 1);
    }

    private void AddContactDamageClip(BossSkillTrackData track, float time)
    {
        BossContactDamageEvent item = new BossContactDamageEvent
        {
            trackId = track.id,
            beginWindowTime = time,
            endWindowTime = time + 0.8f,
            label = "Body Contact"
        };
        currentAsset.contactDamageEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.ContactDamage, currentAsset.contactDamageEvents.Count - 1);
    }

    private void AddMovementClip(BossSkillTrackData track, float time)
    {
        BossMovementEvent item = new BossMovementEvent
        {
            trackId = track.id,
            triggerTime = time,
            label = "Boss Movement"
        };
        currentAsset.movementEvents.Add(item);
        selectedClip = new SelectedClip(ClipKind.Movement, currentAsset.movementEvents.Count - 1);
    }

    private void AddAnimationKeyframe(BossSkillTrackData track)
    {
        currentAsset.animationKeyframes.Add(new BossAnimationKeyframeEvent
        {
            trackId = track.id,
            time = playheadTime
        });
        selectedKeyframeIndex = currentAsset.animationKeyframes.Count - 1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void KeyHitboxApplyTime(BossSkillTrackData track)
    {
        BossHitboxEvent closest = null;
        float best = float.MaxValue;
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            BossHitboxEvent hitbox = currentAsset.hitboxEvents[i];
            if (hitbox == null || hitbox.trackId != track.id)
            {
                continue;
            }

            if (playheadTime < hitbox.beginWindowTime || playheadTime > hitbox.endWindowTime)
            {
                continue;
            }

            float distance = Mathf.Abs(playheadTime - hitbox.applyDamageTime);
            if (distance < best)
            {
                best = distance;
                closest = hitbox;
            }
        }

        if (closest == null)
        {
            AddHitboxClip(track, playheadTime);
            return;
        }

        closest.applyDamageTime = playheadTime;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void MoveClip(SelectedClip clip, float newStartTime, string trackId)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (clip.index < currentAsset.animationEvents.Count)
                {
                    currentAsset.animationEvents[clip.index].startTime = newStartTime;
                    currentAsset.animationEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.Audio:
                if (clip.index < currentAsset.audioEvents.Count)
                {
                    currentAsset.audioEvents[clip.index].triggerTime = newStartTime;
                    currentAsset.audioEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.VFX:
                if (clip.index < currentAsset.vfxEvents.Count)
                {
                    BossVFXEvent vfx = currentAsset.vfxEvents[clip.index];
                    float duration = vfx.Duration;
                    vfx.startTime = newStartTime;
                    vfx.endTime = newStartTime + duration;
                    vfx.trackId = trackId;
                }
                break;
            case ClipKind.Barrage:
                if (clip.index < currentAsset.barrageEvents.Count)
                {
                    currentAsset.barrageEvents[clip.index].triggerTime = newStartTime;
                    currentAsset.barrageEvents[clip.index].trackId = trackId;
                }
                break;
            case ClipKind.Hitbox:
                if (clip.index < currentAsset.hitboxEvents.Count)
                {
                    BossHitboxEvent hitbox = currentAsset.hitboxEvents[clip.index];
                    float duration = hitbox.WindowDuration;
                    float applyOffset = hitbox.applyDamageTime - hitbox.beginWindowTime;
                    hitbox.beginWindowTime = newStartTime;
                    hitbox.endWindowTime = newStartTime + duration;
                    hitbox.applyDamageTime = newStartTime + applyOffset;
                    hitbox.trackId = trackId;
                }
                break;
            case ClipKind.ContactDamage:
                if (clip.index < currentAsset.contactDamageEvents.Count)
                {
                    BossContactDamageEvent contact = currentAsset.contactDamageEvents[clip.index];
                    float duration = contact.WindowDuration;
                    contact.beginWindowTime = newStartTime;
                    contact.endWindowTime = newStartTime + duration;
                    contact.trackId = trackId;
                }
                break;
            case ClipKind.Movement:
                if (clip.index < currentAsset.movementEvents.Count)
                {
                    currentAsset.movementEvents[clip.index].triggerTime = newStartTime;
                    currentAsset.movementEvents[clip.index].trackId = trackId;
                }
                break;
        }
    }

    private void SetClipTimeRange(SelectedClip clip, float start, float end)
    {
        if (clip.kind == ClipKind.Hitbox)
        {
            SetHitboxTimeRange(clip.index, start, end);
            return;
        }

        if (clip.kind == ClipKind.VFX)
        {
            SetVFXTimeRange(clip.index, start, end);
            return;
        }

        if (clip.kind == ClipKind.ContactDamage)
        {
            SetContactDamageTimeRange(clip.index, start, end);
        }
    }

    private void SetVFXTimeRange(int index, float start, float end)
    {
        if (index < 0 || index >= currentAsset.vfxEvents.Count)
        {
            return;
        }

        BossVFXEvent vfx = currentAsset.vfxEvents[index];
        if (vfx == null)
        {
            return;
        }

        vfx.startTime = Mathf.Max(0f, start);
        vfx.endTime = Mathf.Max(vfx.startTime + 0.01f, end);
    }

    private void SetHitboxTimeRange(int index, float start, float end)
    {
        if (index < 0 || index >= currentAsset.hitboxEvents.Count)
        {
            return;
        }

        BossHitboxEvent hitbox = currentAsset.hitboxEvents[index];
        if (hitbox == null)
        {
            return;
        }

        hitbox.beginWindowTime = Mathf.Max(0f, start);
        hitbox.endWindowTime = Mathf.Max(hitbox.beginWindowTime + 0.01f, end);
        hitbox.applyDamageTime = Mathf.Clamp(hitbox.applyDamageTime, hitbox.beginWindowTime, hitbox.endWindowTime);
    }

    private void SetContactDamageTimeRange(int index, float start, float end)
    {
        if (index < 0 || index >= currentAsset.contactDamageEvents.Count)
        {
            return;
        }

        BossContactDamageEvent contact = currentAsset.contactDamageEvents[index];
        if (contact == null)
        {
            return;
        }

        contact.beginWindowTime = Mathf.Max(0f, start);
        contact.endWindowTime = Mathf.Max(contact.beginWindowTime + 0.01f, end);
    }

    private void DeleteClip(SelectedClip clip)
    {
        switch (clip.kind)
        {
            case ClipKind.Animation:
                if (clip.index < currentAsset.animationEvents.Count) currentAsset.animationEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Audio:
                if (clip.index < currentAsset.audioEvents.Count) currentAsset.audioEvents.RemoveAt(clip.index);
                break;
            case ClipKind.VFX:
                if (clip.index < currentAsset.vfxEvents.Count) currentAsset.vfxEvents.RemoveAt(clip.index);
                CleanupVFXPreviewInstances();
                break;
            case ClipKind.Barrage:
                if (clip.index < currentAsset.barrageEvents.Count) currentAsset.barrageEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Hitbox:
                if (clip.index < currentAsset.hitboxEvents.Count) currentAsset.hitboxEvents.RemoveAt(clip.index);
                break;
            case ClipKind.ContactDamage:
                if (clip.index < currentAsset.contactDamageEvents.Count) currentAsset.contactDamageEvents.RemoveAt(clip.index);
                break;
            case ClipKind.Movement:
                if (clip.index < currentAsset.movementEvents.Count) currentAsset.movementEvents.RemoveAt(clip.index);
                break;
        }

        selectedClip = SelectedClip.None;
        serializedAsset?.Update();
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void DeleteAnimationKeyframe(int index)
    {
        if (index >= 0 && index < currentAsset.animationKeyframes.Count)
        {
            currentAsset.animationKeyframes.RemoveAt(index);
            selectedKeyframeIndex = -1;
            EditorUtility.SetDirty(currentAsset);
            Repaint();
        }
    }

    private void DuplicateTrack(BossSkillTrackData track)
    {
        BossSkillTrackData copy = new BossSkillTrackData
        {
            displayName = track.displayName + " Copy",
            type = track.type,
            color = track.color,
            muted = track.muted,
            locked = track.locked,
            hiddenInPreview = track.hiddenInPreview
        };
        copy.EnsureId();
        currentAsset.tracks.Add(copy);
        selectedTrackIndex = currentAsset.tracks.Count - 1;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void MoveTrack(int index, int direction)
    {
        int target = index + direction;
        if (index < 0 || index >= currentAsset.tracks.Count || target < 0 || target >= currentAsset.tracks.Count)
        {
            return;
        }

        BossSkillTrackData item = currentAsset.tracks[index];
        currentAsset.tracks.RemoveAt(index);
        currentAsset.tracks.Insert(target, item);
        selectedTrackIndex = target;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void DeleteTrack(int index)
    {
        if (index < 0 || index >= currentAsset.tracks.Count)
        {
            return;
        }

        string id = currentAsset.tracks[index].id;
        currentAsset.tracks.RemoveAt(index);
        currentAsset.animationEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.animationKeyframes.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.audioEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.vfxEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.barrageEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.hitboxEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.contactDamageEvents.RemoveAll(item => item != null && item.trackId == id);
        currentAsset.movementEvents.RemoveAll(item => item != null && item.trackId == id);
        CleanupVFXPreviewInstances();
        selectedTrackIndex = Mathf.Clamp(index - 1, -1, currentAsset.tracks.Count - 1);
        selectedClip = SelectedClip.None;
        EditorUtility.SetDirty(currentAsset);
        Repaint();
    }

    private void TogglePreview()
    {
        if (isPreviewing)
        {
            StopPreview();
            return;
        }

        isPreviewing = true;
        lastPreviewTime = EditorApplication.timeSinceStartup;
        previewAudioTriggered = currentAsset != null ? new bool[currentAsset.audioEvents.Count] : null;
    }

    private void StopPreview()
    {
        isPreviewing = false;
        previewAudioTriggered = null;
        StopPreviewAudio();
    }

    private void OnEditorUpdate()
    {
        if (!isPreviewing || currentAsset == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float delta = Mathf.Min(0.1f, (float)(now - lastPreviewTime));
        lastPreviewTime = now;
        float previousTime = playheadTime;
        playheadTime += delta;

        if (playheadTime >= currentAsset.totalDuration)
        {
            if (loopPreview)
            {
                playheadTime = Mathf.Repeat(playheadTime, Mathf.Max(0.01f, currentAsset.totalDuration));
                previewAudioTriggered = new bool[currentAsset.audioEvents.Count];
            }
            else
            {
                playheadTime = currentAsset.totalDuration;
                StopPreview();
            }
        }

        TriggerPreviewAudio(previousTime, playheadTime);
        Repaint();
        SceneView.RepaintAll();
    }

    private void TriggerPreviewAudio(float previousTime, float currentTime)
    {
        if (currentAsset == null || previewAudioTriggered == null)
        {
            return;
        }

        for (int i = 0; i < currentAsset.audioEvents.Count && i < previewAudioTriggered.Length; i++)
        {
            BossAudioClipEvent audioEvent = currentAsset.audioEvents[i];
            if (audioEvent == null || previewAudioTriggered[i])
            {
                continue;
            }

            if (audioEvent.triggerTime < previousTime || audioEvent.triggerTime > currentTime)
            {
                continue;
            }

            if (audioEvent.audioClip != null)
            {
                TryPlayEditorPreviewClip(audioEvent.audioClip);
            }

            previewAudioTriggered[i] = true;
        }
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

    private void ApplyPreviewAtTime()
    {
        if (currentAsset == null || previewTarget == null)
        {
            StopAnimationSampling();
            return;
        }

        BossAnimationClipEvent clip = GetAnimationClipAtTime(playheadTime);
        if (clip == null || clip.animationClip == null)
        {
            return;
        }

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        AnimationMode.BeginSampling();
        float clipTime = Mathf.Clamp((playheadTime - clip.startTime) * Mathf.Max(0.01f, clip.playbackSpeed), 0f, clip.animationClip.length);
        AnimationMode.SampleAnimationClip(previewTarget, clip.animationClip, clipTime);
        AnimationMode.EndSampling();
    }

    private void StopAnimationSampling()
    {
        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }
    }

    private BossAnimationClipEvent GetAnimationClipAtTime(float time)
    {
        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            BossAnimationClipEvent clip = currentAsset.animationEvents[i];
            if (clip == null || clip.animationClip == null)
            {
                continue;
            }

            if (time >= clip.startTime && time <= clip.startTime + clip.Duration)
            {
                return clip;
            }
        }

        return null;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (currentAsset == null || previewTarget == null)
        {
            CleanupBarragePreviewInstances();
            CleanupVFXPreviewInstances();
            return;
        }

        Handles.color = new Color(1f, 0.18f, 0.08f, 0.95f);
        for (int i = 0; i < currentAsset.hitboxEvents.Count; i++)
        {
            BossHitboxEvent hitbox = currentAsset.hitboxEvents[i];
            if (hitbox == null || playheadTime < hitbox.beginWindowTime || playheadTime > hitbox.endWindowTime)
            {
                continue;
            }

            Vector3 center = previewTarget.transform.TransformPoint(hitbox.offset);
            DrawBossHitboxShape(center, previewTarget.transform.rotation, hitbox);
            Handles.Label(center + Vector3.up * hitbox.radius, $"Hitbox {i}");
        }

        BossBodyContactCapsuleSet contactCapsules = previewTarget.GetComponentInChildren<BossBodyContactCapsuleSet>(true);
        if (contactCapsules != null)
        {
            Handles.color = new Color(1f, 0.38f, 0.05f, 0.95f);
            for (int i = 0; i < currentAsset.contactDamageEvents.Count; i++)
            {
                BossContactDamageEvent contact = currentAsset.contactDamageEvents[i];
                if (contact == null || playheadTime < contact.beginWindowTime || playheadTime > contact.endWindowTime)
                {
                    continue;
                }

                for (int capsuleIndex = 0; capsuleIndex < contactCapsules.Count; capsuleIndex++)
                {
                    if (!contactCapsules.ShouldUseCapsule(contact, capsuleIndex) ||
                        !contactCapsules.TryGetCapsule(capsuleIndex, out Vector3 start, out Vector3 end, out float radius))
                    {
                        continue;
                    }

                    DrawContactCapsuleShape(start, end, radius);
                }
            }
        }

        HashSet<int> visibleBarragePreviews = new HashSet<int>();
        Handles.color = new Color(1f, 0.68f, 0.12f, 0.95f);
        for (int i = 0; i < currentAsset.barrageEvents.Count; i++)
        {
            BossBarrageEvent barrage = currentAsset.barrageEvents[i];
            if (barrage == null || !ShouldDrawBarragePreview(i, barrage))
            {
                continue;
            }

            DrawBarrageTriggerPreview(i, barrage);
            visibleBarragePreviews.Add(i);
        }

        CleanupHiddenBarragePreviewInstances(visibleBarragePreviews);

        HashSet<int> visibleVFXPreviews = new HashSet<int>();
        Handles.color = new Color(0.78f, 0.45f, 1f, 0.95f);
        for (int i = 0; i < currentAsset.vfxEvents.Count; i++)
        {
            BossVFXEvent vfx = currentAsset.vfxEvents[i];
            if (vfx == null || !ShouldDrawVFXPreview(i, vfx))
            {
                continue;
            }

            DrawVFXTriggerPreview(i, vfx);
            visibleVFXPreviews.Add(i);
        }

        CleanupHiddenVFXPreviewInstances(visibleVFXPreviews);

        Handles.color = new Color(0.25f, 0.82f, 1f, 0.9f);
        for (int i = 0; i < currentAsset.movementEvents.Count; i++)
        {
            BossMovementEvent movement = currentAsset.movementEvents[i];
            if (movement == null)
            {
                continue;
            }

            if (ShouldDrawMovementWarningPreview(i, movement))
            {
                DrawMovementWarningPreview(movement);
            }

            if (ShouldDrawMovementPreview(i, movement))
            {
                DrawMovementTriggerPreview(movement);
            }
        }
    }

    private bool ShouldDrawMovementWarningPreview(int index, BossMovementEvent movement)
    {
        if (!ShouldShowMovementWarningPreview(movement))
        {
            return false;
        }

        if (selectedClip.kind == ClipKind.Movement && selectedClip.index == index)
        {
            return true;
        }

        float warningEndTime = movement.triggerTime + Mathf.Max(0f, movement.warningDuration);
        return playheadTime >= movement.triggerTime && playheadTime <= warningEndTime;
    }

    private bool ShouldDrawMovementPreview(int index, BossMovementEvent movement)
    {
        if (selectedClip.kind == ClipKind.Movement && selectedClip.index == index)
        {
            return true;
        }

        return playheadTime >= movement.triggerTime;
    }

    private void DrawMovementTriggerPreview(BossMovementEvent movement)
    {
        Vector3 destination = GetMovementPreviewDestination(movement);
        float size = HandleUtility.GetHandleSize(destination);
        Handles.DrawWireDisc(destination, Vector3.up, size * 0.22f);
        Handles.SphereHandleCap(0, destination, Quaternion.identity, size * 0.08f, EventType.Repaint);
        Handles.DrawAAPolyLine(2f, previewTarget.transform.position, destination);
    }

    private void DrawMovementWarningPreview(BossMovementEvent movement)
    {
        Vector3 destination = GetMovementPreviewDestination(movement) + movement.warningOffset;
        float size = HandleUtility.GetHandleSize(destination);
        Color oldColor = Handles.color;
        Handles.color = new Color(1f, 0.05f, 0.02f, 0.95f);
        Handles.DrawWireDisc(destination, Vector3.up, size * 0.42f);
        Handles.DrawWireDisc(destination, Vector3.up, size * 0.30f);
        Handles.color = oldColor;
    }

    private Vector3 GetMovementPreviewDestination(BossMovementEvent movement)
    {
        Vector3 destination = previewTarget.transform.position;
        Transform selected = Selection.activeTransform;
        bool hasSelectionTarget = selected != null && selected != previewTarget.transform && !selected.IsChildOf(previewTarget.transform);

        if (movement.targetSource == BossMovementTargetSource.CurrentTargetPosition ||
            movement.targetSource == BossMovementTargetSource.SkillStartTargetPosition)
        {
            destination = hasSelectionTarget ? GetPreviewTargetFootPosition(selected) : previewTarget.transform.position;
        }

        destination += new Vector3(movement.targetOffset.x, 0f, movement.targetOffset.z);
        if (movement.keepCurrentY)
        {
            destination.y = previewTarget.transform.position.y + movement.targetOffset.y;
        }
        else if (movement.projectToGround)
        {
            Vector3 origin = destination + Vector3.up * movement.groundRayStartHeight;
            float distance = movement.groundRayStartHeight + movement.groundRayDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, movement.groundLayers, QueryTriggerInteraction.Ignore))
            {
                destination.y = hit.point.y + movement.targetOffset.y;
            }
        }
        else
        {
            destination.y += movement.targetOffset.y;
        }

        return destination;
    }

    private static bool ShouldShowMovementWarningPreview(BossMovementEvent movement)
    {
        return movement != null &&
               (movement.showTargetWarning ||
                movement.warningPrefab != null ||
                !string.IsNullOrWhiteSpace(movement.warningVFXKey));
    }

    private static Vector3 GetPreviewTargetFootPosition(Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        CharacterController characterController = target.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            Vector3 center = characterController.transform.TransformPoint(characterController.center);
            float bottomY = center.y - characterController.height * 0.5f;
            return new Vector3(characterController.transform.position.x, bottomY, characterController.transform.position.z);
        }

        Collider collider = target.GetComponentInParent<Collider>();
        if (collider == null)
        {
            collider = target.GetComponentInChildren<Collider>();
        }

        if (collider != null)
        {
            return new Vector3(collider.transform.position.x, collider.bounds.min.y, collider.transform.position.z);
        }

        return target.position;
    }

    private bool ShouldDrawVFXPreview(int index, BossVFXEvent vfx)
    {
        if (selectedClip.kind == ClipKind.VFX && selectedClip.index == index)
        {
            return true;
        }

        return playheadTime >= vfx.startTime && playheadTime <= vfx.endTime;
    }

    private void DrawVFXTriggerPreview(int index, BossVFXEvent vfx)
    {
        GetVFXPreviewPose(vfx, out Vector3 position, out Quaternion rotation);
        Handles.SphereHandleCap(0, position, rotation, HandleUtility.GetHandleSize(position) * 0.08f, EventType.Repaint);
        Handles.ArrowHandleCap(0, position, rotation, HandleUtility.GetHandleSize(position) * 0.45f, EventType.Repaint);
        UpdateVFXPreviewInstance(index, vfx, position, rotation);
    }

    private void GetVFXPreviewPose(BossVFXEvent vfx, out Vector3 position, out Quaternion rotation)
    {
        Transform root = previewTarget.transform;
        position = root.TransformPoint(vfx.offset);
        rotation = root.rotation * Quaternion.Euler(vfx.rotation);
    }

    private void DrawProjectileHitShape(Vector3 center, Quaternion rotation, BossBarrageEvent barrage)
    {
        Handles.DrawWireDisc(center, Vector3.up, barrage.hitRadius);
        Handles.DrawWireDisc(center, rotation * Vector3.right, barrage.hitRadius);
        Handles.DrawWireDisc(center, rotation * Vector3.forward, barrage.hitRadius);
    }

    private bool ShouldDrawBarragePreview(int index, BossBarrageEvent barrage)
    {
        if (selectedClip.kind == ClipKind.Barrage && selectedClip.index == index)
        {
            return true;
        }

        return playheadTime >= barrage.triggerTime && playheadTime <= barrage.triggerTime + Mathf.Max(0.01f, barrage.lifeTime);
    }

    private void DrawBarrageTriggerPreview(int index, BossBarrageEvent barrage)
    {
        GetBarragePreviewPose(barrage, out Vector3 spawnPosition, out Quaternion baseRotation);
        int projectileCount = Mathf.Max(1, barrage.projectileCount);
        float spread = projectileCount <= 1 ? 0f : barrage.spreadAngle;
        float step = projectileCount <= 1 ? 0f : spread / (projectileCount - 1);
        float startAngle = -spread * 0.5f;
        float localTime = Mathf.Clamp(playheadTime - barrage.triggerTime, 0f, Mathf.Max(0.01f, barrage.lifeTime));
        float previewDistance = Mathf.Max(0f, barrage.speed) * Mathf.Max(0.01f, barrage.lifeTime);

        Handles.SphereHandleCap(0, spawnPosition, baseRotation, HandleUtility.GetHandleSize(spawnPosition) * 0.1f, EventType.Repaint);
        DrawBarrageRandomAimCone(spawnPosition, baseRotation, startAngle, step, projectileCount, barrage.aimRandomAngle, previewDistance);

        Vector3 primaryProjectilePosition = spawnPosition;
        Quaternion primaryRotation = baseRotation;
        for (int i = 0; i < projectileCount; i++)
        {
            Quaternion shotRotation = baseRotation * Quaternion.Euler(0f, startAngle + step * i, 0f);
            Vector3 direction = shotRotation * Vector3.forward;
            Vector3 projectilePosition = spawnPosition + direction * (Mathf.Max(0f, barrage.speed) * localTime);
            Vector3 rangeEnd = spawnPosition + direction * (Mathf.Max(0f, barrage.speed) * Mathf.Max(0.01f, barrage.lifeTime));
            Handles.DrawAAPolyLine(2f, spawnPosition, rangeEnd);
            Handles.ConeHandleCap(0, rangeEnd, CreateLookRotation(direction, shotRotation), HandleUtility.GetHandleSize(rangeEnd) * 0.12f, EventType.Repaint);
            DrawProjectileHitShape(projectilePosition + shotRotation * barrage.hitCenterOffset, shotRotation, barrage);

            if (i == projectileCount / 2)
            {
                primaryProjectilePosition = projectilePosition;
                primaryRotation = shotRotation;
            }
        }

        UpdateBarragePreviewInstance(index, barrage, primaryProjectilePosition, primaryRotation);
    }

    private static void DrawBarrageRandomAimCone(Vector3 spawnPosition, Quaternion baseRotation, float startAngle, float step, int projectileCount, float randomAngle, float distance)
    {
        if (randomAngle <= 0f || distance <= 0f)
        {
            return;
        }

        Handles.color = new Color(1f, 0.82f, 0.2f, 0.32f);
        for (int i = 0; i < projectileCount; i++)
        {
            float centerAngle = startAngle + step * i;
            Quaternion centerRotation = baseRotation * Quaternion.Euler(0f, centerAngle, 0f);
            Vector3 left = centerRotation * Quaternion.Euler(0f, -randomAngle, 0f) * Vector3.forward;
            Vector3 right = centerRotation * Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            Vector3 up = centerRotation * Quaternion.Euler(-randomAngle, 0f, 0f) * Vector3.forward;
            Vector3 down = centerRotation * Quaternion.Euler(randomAngle, 0f, 0f) * Vector3.forward;
            Handles.DrawDottedLine(spawnPosition, spawnPosition + left * distance, 4f);
            Handles.DrawDottedLine(spawnPosition, spawnPosition + right * distance, 4f);
            Handles.DrawDottedLine(spawnPosition, spawnPosition + up * distance, 4f);
            Handles.DrawDottedLine(spawnPosition, spawnPosition + down * distance, 4f);
        }

        Handles.color = new Color(1f, 0.68f, 0.12f, 0.95f);
    }

    private void UpdateBarragePreviewInstance(int index, BossBarrageEvent barrage, Vector3 position, Quaternion rotation)
    {
        if (barrage.projectilePrefab == null)
        {
            DestroyBarragePreviewInstance(index);
            return;
        }

        if (!previewBarrageInstances.TryGetValue(index, out GameObject instance) || instance == null || !instance.name.Contains(barrage.projectilePrefab.name))
        {
            DestroyBarragePreviewInstance(index);
            UnityEngine.Object prefabInstance = PrefabUtility.InstantiatePrefab(barrage.projectilePrefab);
            instance = prefabInstance as GameObject;
            if (instance == null)
            {
                instance = Instantiate(barrage.projectilePrefab);
            }

            instance.name = "__preview_boss_barrage__" + index + "_" + barrage.projectilePrefab.name;
            SetPreviewObjectFlags(instance);
            DisablePreviewColliders(instance);
            previewBarrageInstances[index] = instance;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        if (!instance.activeSelf)
        {
            instance.SetActive(true);
        }
    }

    private void UpdateVFXPreviewInstance(int index, BossVFXEvent vfx, Vector3 position, Quaternion rotation)
    {
        if (vfx.previewPrefab == null)
        {
            DestroyVFXPreviewInstance(index);
            return;
        }

        if (!previewVFXInstances.TryGetValue(index, out GameObject instance) || instance == null || !instance.name.Contains(vfx.previewPrefab.name))
        {
            DestroyVFXPreviewInstance(index);
            UnityEngine.Object prefabInstance = PrefabUtility.InstantiatePrefab(vfx.previewPrefab);
            instance = prefabInstance as GameObject;
            if (instance == null)
            {
                instance = Instantiate(vfx.previewPrefab);
            }

            instance.name = "__preview_boss_vfx__" + index + "_" + vfx.previewPrefab.name;
            SetPreviewObjectFlags(instance);
            DisablePreviewColliders(instance);
            previewVFXInstances[index] = instance;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        if (vfx.followBoss)
        {
            instance.transform.SetParent(previewTarget.transform, true);
        }
        else
        {
            instance.transform.SetParent(null, true);
        }

        if (!instance.activeSelf)
        {
            instance.SetActive(true);
        }
    }

    private void CleanupHiddenBarragePreviewInstances(HashSet<int> visible)
    {
        List<int> remove = null;
        foreach (KeyValuePair<int, GameObject> pair in previewBarrageInstances)
        {
            if (visible.Contains(pair.Key))
            {
                continue;
            }

            remove ??= new List<int>();
            remove.Add(pair.Key);
        }

        if (remove == null)
        {
            return;
        }

        for (int i = 0; i < remove.Count; i++)
        {
            DestroyBarragePreviewInstance(remove[i]);
        }
    }

    private void CleanupHiddenVFXPreviewInstances(HashSet<int> visible)
    {
        List<int> remove = null;
        foreach (KeyValuePair<int, GameObject> pair in previewVFXInstances)
        {
            if (visible.Contains(pair.Key))
            {
                continue;
            }

            remove ??= new List<int>();
            remove.Add(pair.Key);
        }

        if (remove == null)
        {
            return;
        }

        for (int i = 0; i < remove.Count; i++)
        {
            DestroyVFXPreviewInstance(remove[i]);
        }
    }

    private void CleanupVFXPreviewInstances()
    {
        List<int> keys = new List<int>(previewVFXInstances.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            DestroyVFXPreviewInstance(keys[i]);
        }
    }

    private void DestroyVFXPreviewInstance(int index)
    {
        if (previewVFXInstances.TryGetValue(index, out GameObject instance) && instance != null)
        {
            DestroyImmediate(instance);
        }

        previewVFXInstances.Remove(index);
    }

    private void CleanupBarragePreviewInstances()
    {
        List<int> keys = new List<int>(previewBarrageInstances.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            DestroyBarragePreviewInstance(keys[i]);
        }
    }

    private void DestroyBarragePreviewInstance(int index)
    {
        if (previewBarrageInstances.TryGetValue(index, out GameObject instance) && instance != null)
        {
            DestroyImmediate(instance);
        }

        previewBarrageInstances.Remove(index);
    }

    private static void SetPreviewObjectFlags(GameObject root)
    {
        root.hideFlags = HideFlags.HideAndDontSave;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private static void DisablePreviewColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void DrawBossHitboxShape(Vector3 center, Quaternion rotation, BossHitboxEvent hitbox)
    {
        float radius = Mathf.Max(0.01f, hitbox.radius);
        Quaternion hitboxRotation = rotation * Quaternion.Euler(hitbox.rotation);
        if (hitbox.shape == BossHitboxShape.Cylinder)
        {
            float height = Mathf.Max(0.01f, hitbox.height);
            Vector3 up = hitboxRotation * Vector3.up;
            Vector3 right = hitboxRotation * Vector3.right;
            Vector3 forward = hitboxRotation * Vector3.forward;
            Vector3 bottom = center - up * (height * 0.5f);
            Vector3 top = center + up * (height * 0.5f);
            Handles.DrawWireDisc(bottom, up, radius);
            Handles.DrawWireDisc(top, up, radius);
            Handles.DrawLine(bottom + right * radius, top + right * radius);
            Handles.DrawLine(bottom - right * radius, top - right * radius);
            Handles.DrawLine(bottom + forward * radius, top + forward * radius);
            Handles.DrawLine(bottom - forward * radius, top - forward * radius);
            return;
        }

        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, hitboxRotation * Vector3.forward, radius);
    }

    private static void DrawContactCapsuleShape(Vector3 start, Vector3 end, float radius)
    {
        Vector3 axis = end - start;
        Vector3 up = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(up, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        Vector3 forward = Vector3.Cross(right, up).normalized;
        Handles.DrawWireDisc(start, right, radius);
        Handles.DrawWireDisc(start, forward, radius);
        Handles.DrawWireDisc(end, right, radius);
        Handles.DrawWireDisc(end, forward, radius);
        Handles.DrawWireDisc(start, up, radius);
        Handles.DrawWireDisc(end, up, radius);
        Handles.DrawLine(start + right * radius, end + right * radius);
        Handles.DrawLine(start - right * radius, end - right * radius);
        Handles.DrawLine(start + forward * radius, end + forward * radius);
        Handles.DrawLine(start - forward * radius, end - forward * radius);
    }

    private void GetBarragePreviewPose(BossBarrageEvent barrage, out Vector3 position, out Quaternion rotation)
    {
        Transform root = previewTarget.transform;
        position = root.TransformPoint(barrage.spawnOffset);
        Vector3 forward = root.forward;
        if (barrage.aimSource == BossProjectileAimSource.TargetPosition && Selection.activeTransform != null && Selection.activeTransform != previewTarget.transform)
        {
            Vector3 targetPosition = Selection.activeTransform.position + Vector3.up * Mathf.Max(0f, barrage.targetAimHeightOffset);
            forward = targetPosition - position;
        }

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = root.forward;
        }

        rotation = CreateLookRotation(forward, root.rotation) * Quaternion.Euler(barrage.spawnRotation);
    }

    private static Quaternion CreateLookRotation(Vector3 forward, Quaternion fallback)
    {
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return fallback;
        }

        Vector3 normalizedForward = forward.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(normalizedForward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(normalizedForward, up);
    }

    private void HandleKeyboard()
    {
        Event evt = Event.current;
        if (evt.type != EventType.KeyDown)
        {
            return;
        }

        if (evt.keyCode == KeyCode.Space && currentAsset != null)
        {
            TogglePreview();
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.K && currentAsset != null && selectedTrackIndex >= 0 && selectedTrackIndex < currentAsset.tracks.Count)
        {
            BossSkillTrackData track = currentAsset.tracks[selectedTrackIndex];
            if (track.type == BossSkillTrackType.Animation)
            {
                AddAnimationKeyframe(track);
            }
            else if (track.type == BossSkillTrackType.Hitbox)
            {
                KeyHitboxApplyTime(track);
            }
            evt.Use();
        }
        else if (evt.keyCode == KeyCode.Delete && selectedClip.IsValid)
        {
            DeleteClip(selectedClip);
            evt.Use();
        }
    }

    private void CreateAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
        {
            AssetDatabase.CreateFolder("Assets", "GameData");
        }

        if (!AssetDatabase.IsValidFolder(SaveFolder))
        {
            AssetDatabase.CreateFolder("Assets/GameData", "BossSkillData");
        }

        BossSkillSO asset = CreateInstance<BossSkillSO>();
        asset.skillID = Guid.NewGuid().ToString("N");
        asset.displayName = "New Boss Skill";
        AddDefaultTracks(asset);

        string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(SaveFolder, "BossSkill_New.asset").Replace("\\", "/"));
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        BindAsset(asset);
        Selection.activeObject = asset;
    }

    private static void AddDefaultTracks(BossSkillSO asset)
    {
        BossSkillTrackType[] types =
        {
            BossSkillTrackType.Animation,
            BossSkillTrackType.Audio,
            BossSkillTrackType.VFX,
            BossSkillTrackType.Barrage,
            BossSkillTrackType.Movement,
            BossSkillTrackType.Hitbox,
            BossSkillTrackType.ContactDamage
        };

        for (int i = 0; i < types.Length; i++)
        {
            BossSkillTrackData track = new BossSkillTrackData
            {
                type = types[i],
                displayName = GetDefaultTrackName(types[i]),
                color = GetDefaultTrackColor(types[i])
            };
            track.EnsureId();
            asset.tracks.Add(track);
        }
    }

    private void BindAsset(BossSkillSO asset)
    {
        currentAsset = asset;
        serializedAsset = currentAsset != null ? new SerializedObject(currentAsset) : null;
        selectedTrackIndex = currentAsset != null && currentAsset.tracks.Count > 0 ? 0 : -1;
        selectedClip = SelectedClip.None;
        selectedKeyframeIndex = -1;
        playheadTime = 0f;
        previewAudioTriggered = null;
        CleanupBarragePreviewInstances();
        CleanupVFXPreviewInstances();
        Repaint();
    }

    private void TryUseSelectedAsset()
    {
        if (Selection.activeObject is BossSkillSO skill)
        {
            BindAsset(skill);
        }
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeObject is BossSkillSO skill)
        {
            BindAsset(skill);
        }
    }

    private SerializedProperty GetSelectedClipProperty()
    {
        if (serializedAsset == null || !selectedClip.IsValid)
        {
            return null;
        }

        string propertyName = selectedClip.kind switch
        {
            ClipKind.Animation => "animationEvents",
            ClipKind.Audio => "audioEvents",
            ClipKind.VFX => "vfxEvents",
            ClipKind.Barrage => "barrageEvents",
            ClipKind.Hitbox => "hitboxEvents",
            ClipKind.ContactDamage => "contactDamageEvents",
            ClipKind.Movement => "movementEvents",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(propertyName))
        {
            return null;
        }

        SerializedProperty list = serializedAsset.FindProperty(propertyName);
        return list != null && selectedClip.index >= 0 && selectedClip.index < list.arraySize
            ? list.GetArrayElementAtIndex(selectedClip.index)
            : null;
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedAsset.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private static void DrawProperty(SerializedProperty parent, string propertyName)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private int GetTrackClipCount(BossSkillTrackData track)
    {
        int count = 0;
        if (track == null)
        {
            return count;
        }

        count += currentAsset.animationEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.animationKeyframes.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.audioEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.vfxEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.barrageEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.hitboxEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.contactDamageEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        count += currentAsset.movementEvents.FindAll(item => item != null && item.trackId == track.id).Count;
        return count;
    }

    private float GetClipStartTime(SelectedClip clip)
    {
        return clip.kind switch
        {
            ClipKind.Animation when clip.index < currentAsset.animationEvents.Count => currentAsset.animationEvents[clip.index].startTime,
            ClipKind.Audio when clip.index < currentAsset.audioEvents.Count => currentAsset.audioEvents[clip.index].triggerTime,
            ClipKind.VFX when clip.index < currentAsset.vfxEvents.Count => currentAsset.vfxEvents[clip.index].startTime,
            ClipKind.Barrage when clip.index < currentAsset.barrageEvents.Count => currentAsset.barrageEvents[clip.index].triggerTime,
            ClipKind.Hitbox when clip.index < currentAsset.hitboxEvents.Count => currentAsset.hitboxEvents[clip.index].beginWindowTime,
            ClipKind.ContactDamage when clip.index < currentAsset.contactDamageEvents.Count => currentAsset.contactDamageEvents[clip.index].beginWindowTime,
            ClipKind.Movement when clip.index < currentAsset.movementEvents.Count => currentAsset.movementEvents[clip.index].triggerTime,
            _ => 0f
        };
    }

    private float GetClipDuration(SelectedClip clip)
    {
        return clip.kind switch
        {
            ClipKind.Animation when clip.index < currentAsset.animationEvents.Count => Mathf.Max(0.01f, currentAsset.animationEvents[clip.index].Duration),
            ClipKind.Audio when clip.index < currentAsset.audioEvents.Count => Mathf.Max(0.01f, currentAsset.audioEvents[clip.index].Duration),
            ClipKind.VFX when clip.index < currentAsset.vfxEvents.Count => Mathf.Max(0.01f, currentAsset.vfxEvents[clip.index].Duration),
            ClipKind.Barrage when clip.index < currentAsset.barrageEvents.Count => 0.05f,
            ClipKind.Hitbox when clip.index < currentAsset.hitboxEvents.Count => Mathf.Max(0.01f, currentAsset.hitboxEvents[clip.index].endWindowTime - currentAsset.hitboxEvents[clip.index].beginWindowTime),
            ClipKind.ContactDamage when clip.index < currentAsset.contactDamageEvents.Count => Mathf.Max(0.01f, currentAsset.contactDamageEvents[clip.index].endWindowTime - currentAsset.contactDamageEvents[clip.index].beginWindowTime),
            ClipKind.Movement when clip.index < currentAsset.movementEvents.Count => 0.05f,
            _ => 0f
        };
    }

    private List<AnimationClip> CollectDraggedAnimationClips()
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        HashSet<int> ids = new HashSet<int>();
        HashSet<string> requestedNames = new HashSet<string>(StringComparer.Ordinal);

        AddAnimationClipsFromObjects(DragAndDrop.objectReferences, clips, ids, true);
        AddRequestedAnimationNames(DragAndDrop.objectReferences, requestedNames);
        AddRequestedAnimationNames(Selection.objects, requestedNames);
        if (clips.Count == 0)
        {
            AddAnimationClipsFromObjects(Selection.objects, clips, ids, true);
        }

        if (clips.Count == 0)
        {
            AddNamedAnimationClipsFromObjects(DragAndDrop.objectReferences, requestedNames, clips, ids);
            AddNamedAnimationClipsFromObjects(Selection.objects, requestedNames, clips, ids);
        }

        if (clips.Count == 0 && DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddNamedAnimationClipsFromPath(DragAndDrop.paths[i], requestedNames, clips, ids);
            }
        }

        if (clips.Count == 0)
        {
            AddAnimationClipsFromObjects(DragAndDrop.objectReferences, clips, ids, false);
        }

        if (clips.Count == 0)
        {
            AddAnimationClipsFromObjects(Selection.objects, clips, ids, false);
        }

        if (clips.Count == 0 && DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddAnimationClipsFromPath(DragAndDrop.paths[i], clips, ids);
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

    private static void AddAnimationClipsFromObjects(UnityEngine.Object[] objects, List<AnimationClip> clips, HashSet<int> ids, bool directOnly)
    {
        for (int i = 0; objects != null && i < objects.Length; i++)
        {
            if (objects[i] is AnimationClip clip && !IsPreviewAnimationClip(clip))
            {
                AddUniqueClip(clip, clips, ids);
                continue;
            }

            if (!directOnly)
            {
                AddAnimationClipsFromPath(AssetDatabase.GetAssetPath(objects[i]), clips, ids);
            }
        }
    }

    private static void AddNamedAnimationClipsFromObjects(UnityEngine.Object[] objects, HashSet<string> requestedNames, List<AnimationClip> clips, HashSet<int> ids)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            AddNamedAnimationClipsFromPath(AssetDatabase.GetAssetPath(objects[i]), requestedNames, clips, ids);
        }
    }

    private static void AddAnimationClipsFromPath(string path, List<AnimationClip> clips, HashSet<int> ids)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !IsPreviewAnimationClip(clip))
            {
                AddUniqueClip(clip, clips, ids);
            }
        }
    }

    private static void AddNamedAnimationClipsFromPath(string path, HashSet<string> requestedNames, List<AnimationClip> clips, HashSet<int> ids)
    {
        if (string.IsNullOrWhiteSpace(path) || requestedNames == null || requestedNames.Count == 0)
        {
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !IsPreviewAnimationClip(clip) && requestedNames.Contains(clip.name))
            {
                AddUniqueClip(clip, clips, ids);
            }
        }
    }

    private List<AudioClip> CollectDraggedAudioClips()
    {
        List<AudioClip> clips = new List<AudioClip>();
        HashSet<int> ids = new HashSet<int>();
        AddAudioClipsFromObjects(DragAndDrop.objectReferences, clips, ids);
        if (DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddAudioClipsFromPath(DragAndDrop.paths[i], clips, ids);
            }
        }

        return clips;
    }

    private static void AddAudioClipsFromObjects(UnityEngine.Object[] objects, List<AudioClip> clips, HashSet<int> ids)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is AudioClip clip)
            {
                AddUniqueAudioClip(clip, clips, ids);
                continue;
            }

            AddAudioClipsFromPath(AssetDatabase.GetAssetPath(objects[i]), clips, ids);
        }
    }

    private static void AddAudioClipsFromPath(string path, List<AudioClip> clips, HashSet<int> ids)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AudioClip clip)
            {
                AddUniqueAudioClip(clip, clips, ids);
            }
        }
    }

    private static void AddUniqueAudioClip(AudioClip clip, List<AudioClip> clips, HashSet<int> ids)
    {
        if (clip != null && ids.Add(clip.GetInstanceID()))
        {
            clips.Add(clip);
        }
    }

    private List<GameObject> CollectDraggedPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
        HashSet<int> ids = new HashSet<int>();
        AddPrefabsFromObjects(DragAndDrop.objectReferences, prefabs, ids);
        if (DragAndDrop.paths != null)
        {
            for (int i = 0; i < DragAndDrop.paths.Length; i++)
            {
                AddPrefabFromPath(DragAndDrop.paths[i], prefabs, ids);
            }
        }

        return prefabs;
    }

    private static void AddPrefabsFromObjects(UnityEngine.Object[] objects, List<GameObject> prefabs, HashSet<int> ids)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] is GameObject prefab)
            {
                AddUniquePrefab(prefab, prefabs, ids);
                continue;
            }

            AddPrefabFromPath(AssetDatabase.GetAssetPath(objects[i]), prefabs, ids);
        }
    }

    private static void AddPrefabFromPath(string path, List<GameObject> prefabs, HashSet<int> ids)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
        if (mainAsset is GameObject prefab)
        {
            AddUniquePrefab(prefab, prefabs, ids);
        }
    }

    private static void AddUniquePrefab(GameObject prefab, List<GameObject> prefabs, HashSet<int> ids)
    {
        if (prefab != null && ids.Add(prefab.GetInstanceID()))
        {
            prefabs.Add(prefab);
        }
    }

    private static void AddUniqueClip(AnimationClip clip, List<AnimationClip> clips, HashSet<int> ids)
    {
        if (clip != null && ids.Add(clip.GetInstanceID()))
        {
            clips.Add(clip);
        }
    }

    private static bool IsPreviewAnimationClip(AnimationClip clip)
    {
        return clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase);
    }

    private float TimeToX(float time)
    {
        return time * pixelsPerSecond;
    }

    private float XToTime(float x)
    {
        return x / Mathf.Max(1f, pixelsPerSecond);
    }

    private float SnapTime(float time)
    {
        float frame = 1f / Mathf.Max(1, previewFps);
        return Mathf.Round(time / frame) * frame;
    }

    private float SnapClipStartTime(float time, SelectedClip movingClip, float contentWidth, float duration, bool showSnapLine)
    {
        float frameSnappedTime = SnapTime(Mathf.Max(0f, time));
        float snapWindow = Mathf.Max(0.01f, 8f / Mathf.Max(1f, contentWidth) * duration);
        float bestTime = frameSnappedTime;
        float bestLineTime = frameSnappedTime;
        float bestDistance = snapWindow;
        bool snapped = false;

        AddAnimationKeyframeSnapCandidates(frameSnappedTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);

        float movingDuration = GetClipDuration(movingClip);
        if (movingDuration > 0f)
        {
            AddAnimationKeyframeEndSnapCandidates(frameSnappedTime, movingDuration, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }

        if (showSnapLine)
        {
            hasSnapLine = snapped;
            snapLineTime = Mathf.Clamp(bestLineTime, 0f, duration);
        }

        return Mathf.Clamp(bestTime, 0f, duration);
    }

    private float SnapEdgeTime(float time, float contentWidth, float duration, bool showSnapLine)
    {
        float frameSnappedTime = SnapTime(Mathf.Max(0f, time));
        float snapWindow = Mathf.Max(0.01f, 8f / Mathf.Max(1f, contentWidth) * duration);
        float bestTime = frameSnappedTime;
        float bestLineTime = frameSnappedTime;
        float bestDistance = snapWindow;
        bool snapped = false;

        AddAnimationKeyframeSnapCandidates(frameSnappedTime, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);

        if (showSnapLine)
        {
            hasSnapLine = snapped;
            snapLineTime = Mathf.Clamp(bestLineTime, 0f, duration);
        }

        return Mathf.Clamp(bestTime, 0f, duration);
    }

    private void AddAnimationKeyframeSnapCandidates(float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (currentAsset == null || currentAsset.animationKeyframes == null)
        {
            return;
        }

        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            BossAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null)
            {
                continue;
            }

            TrySnapCandidate(keyframe.time, keyframe.time, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private void AddAnimationKeyframeEndSnapCandidates(float time, float movingDuration, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (currentAsset == null || currentAsset.animationKeyframes == null)
        {
            return;
        }

        for (int i = 0; i < currentAsset.animationKeyframes.Count; i++)
        {
            BossAnimationKeyframeEvent keyframe = currentAsset.animationKeyframes[i];
            if (keyframe == null)
            {
                continue;
            }

            TrySnapCandidate(keyframe.time - movingDuration, keyframe.time, time, ref bestTime, ref bestLineTime, ref bestDistance, ref snapped);
        }
    }

    private static void TrySnapCandidate(float candidateStart, float candidateLineTime, float time, ref float bestTime, ref float bestLineTime, ref float bestDistance, ref bool snapped)
    {
        if (candidateStart < 0f)
        {
            return;
        }

        float distance = Mathf.Abs(candidateStart - time);
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestTime = candidateStart;
            bestLineTime = candidateLineTime;
            snapped = true;
        }
    }

    private static string GetClipLabel(string label, UnityEngine.Object obj)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return obj != null ? obj.name : "Clip";
    }

    private static string GetDefaultTrackName(BossSkillTrackType type)
    {
        return type switch
        {
            BossSkillTrackType.Animation => "动作",
            BossSkillTrackType.Audio => "音效",
            BossSkillTrackType.VFX => "特效",
            BossSkillTrackType.Barrage => "弹幕特效",
            BossSkillTrackType.Hitbox => "伤害检测盒",
            BossSkillTrackType.ContactDamage => "身体接触伤害",
            BossSkillTrackType.Movement => "位移触发器",
            _ => "Custom"
        };
    }

    private static Color GetDefaultTrackColor(BossSkillTrackType type)
    {
        return type switch
        {
            BossSkillTrackType.Animation => new Color(0.30f, 0.58f, 0.90f),
            BossSkillTrackType.Audio => new Color(0.42f, 0.76f, 0.38f),
            BossSkillTrackType.VFX => new Color(0.66f, 0.42f, 0.92f),
            BossSkillTrackType.Barrage => new Color(0.90f, 0.50f, 0.20f),
            BossSkillTrackType.Hitbox => new Color(0.86f, 0.22f, 0.18f),
            BossSkillTrackType.ContactDamage => new Color(0.96f, 0.34f, 0.12f),
            BossSkillTrackType.Movement => new Color(0.22f, 0.72f, 0.88f),
            _ => new Color(0.6f, 0.6f, 0.6f)
        };
    }

    private void DrawBackground()
    {
        EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), new Color(0.12f, 0.12f, 0.125f));
    }

    private void EnsureStyles()
    {
        titleStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white },
            clipping = TextClipping.Clip
        };

        subtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.70f, 0.70f, 0.72f) },
            clipping = TextClipping.Clip
        };

        centeredStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.65f, 0.65f, 0.67f) }
        };

        inspectorTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        dimStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.72f, 0.72f, 0.74f) }
        };
    }

    private static void DrawLine(Rect rect, Color color)
    {
        EditorGUI.DrawRect(rect, color);
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
#endif
