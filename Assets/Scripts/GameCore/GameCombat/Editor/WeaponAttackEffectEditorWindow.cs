using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private const string SaveFolder = "Assets/GameData/WeaponData";
    private const float TopHeight = 30f;
    private const float BottomHeight = 34f;
    private const float LeftWidth = 228f;
    private const float InspectorWidth = 300f;
    private const float TrackHeight = 30f;
    private const float RulerHeight = 24f;

    private WeaponAttackEffectSO currentAsset;
    private SerializedObject serializedAsset;
    private GameObject previewTarget;
    private readonly Dictionary<int, GameObject> previewVfxInstances = new Dictionary<int, GameObject>();
    private Vector2 timelineScroll;
    private Vector2 inspectorScroll;
    private int selectedTrackIndex = -1;
    private SelectedClip selectedClip = SelectedClip.None;
    private int selectedAnimationKeyframeIndex = -1;

    private float playheadTime;
    private int previewFps = 60;
    private RulerDisplayMode rulerDisplayMode = RulerDisplayMode.Seconds;
    private float pixelsPerSecond = 190f;
    private bool isPreviewing;
    private bool editorPreviewSamplingActive;
    private bool loopPreview = true;
    private double lastPreviewTime;
    private bool[] previewAudioTriggered;

    private bool isDraggingClip;
    private SelectedClip dragClip = SelectedClip.None;
    private float dragStartMouseX;
    private float dragStartMouseY;
    private float dragStartClipTime;
    private string dragStartTrackId;
    private string dragCandidateTrackId;
    private float dragCandidateClipTime;
    private int dragCandidateTrackIndex = -1;
    private bool dragPlacementInvalid;
    private bool dragInvalidOnActualClip;
    private bool isResizingClip;
    private SelectedClip resizeClip = SelectedClip.None;
    private ClipResizeEdge resizeEdge = ClipResizeEdge.None;
    private float resizeStartMouseX;
    private float resizeStartClipStart;
    private float resizeStartClipDuration;
    private bool isDraggingAnimationKeyframe;
    private int dragAnimationKeyframeIndex = -1;
    private float dragStartKeyframeMouseX;
    private float dragStartKeyframeTime;
    private bool hasSnapLine;
    private float snapLineTime;
    private bool isPanningTimeline;
    private float panStartMouseX;
    private float panStartScrollX;
    private bool isScrubbingTimeline;
    private int timelineScrubControlId;

    private GUIStyle toolbarTitleStyle;
    private GUIStyle trackTitleStyle;
    private GUIStyle trackSubTitleStyle;
    private GUIStyle centeredLabelStyle;
    private GUIStyle inspectorTitleStyle;
    private GUIStyle miniDimStyle;
    private GUIStyle centeredNumberFieldStyle;

    private readonly struct AnimationClipRange
    {
        public readonly float startTime;
        public readonly float endTime;

        public AnimationClipRange(float startTime, float endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;
        }
    }

    private readonly struct AnimationBlendRange
    {
        public readonly float startTime;
        public readonly float endTime;

        public AnimationBlendRange(float startTime, float endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;
        }
    }

    [MenuItem("Tools/Weapon Attack Effect Editor")]
    public static void OpenWindow()
    {
        WeaponAttackEffectEditorWindow window = GetWindow<WeaponAttackEffectEditorWindow>();
        window.titleContent = new GUIContent("武器攻击效果编辑器");
        window.minSize = new Vector2(1080f, 540f);
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        Selection.selectionChanged += OnSelectionChanged;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        TryUseSelectedAsset();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Selection.selectionChanged -= OnSelectionChanged;
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        CleanupPreviewVfxInstances();
        StopEditorPreviewSampling();
        StopPreviewAudio();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            CleanupPreviewVfxInstances();
            StopEditorPreviewSampling();
            StopPreviewAudio();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawBackground();
        DrawTopBar();
        DrawMainArea();
        DrawBottomBar();
        HandleKeyboardShortcuts();
        ApplyEditorPreviewAtTime();
    }

    private void DrawTopBar()
    {
        Rect toolbarRect = new Rect(0f, 0f, position.width, 30f);
        EditorGUI.DrawRect(toolbarRect, new Color(0.15f, 0.15f, 0.15f));
        DrawBorder(toolbarRect, new Color(0.08f, 0.08f, 0.08f));

        Rect row = new Rect(8f, 4f, position.width - 16f, 22f);
        float x = row.x;

        bool hasAsset = serializedAsset != null;
        bool previousEnabled = GUI.enabled;

        EditorGUI.BeginChangeCheck();
        currentAsset = (WeaponAttackEffectSO)EditorGUI.ObjectField(new Rect(x, row.y, 170f, row.height), currentAsset, typeof(WeaponAttackEffectSO), false);
        if (EditorGUI.EndChangeCheck())
        {
            BindAsset(currentAsset);
        }
        x += 176f;

        GUI.enabled = hasAsset;
        if (hasAsset)
        {
            serializedAsset.Update();
        }

        Rect weaponLabelRect = new Rect(x, row.y + 3f, 58f, row.height);
        Rect weaponFieldRect = new Rect(weaponLabelRect.xMax, row.y, 128f, row.height);
        GUI.Label(weaponLabelRect, "武器名称", miniDimStyle);
        if (hasAsset)
        {
            SerializedProperty weaponNameProperty = serializedAsset.FindProperty("weaponName");
            weaponNameProperty.stringValue = EditorGUI.TextField(weaponFieldRect, weaponNameProperty.stringValue);
        }
        else
        {
            EditorGUI.TextField(weaponFieldRect, string.Empty);
        }
        x = weaponFieldRect.xMax + 8f;

        if (hasAsset)
        {
            serializedAsset.ApplyModifiedProperties();
        }

        GUI.enabled = previousEnabled;

        EditorGUI.BeginChangeCheck();
        Rect previewLabelRect = new Rect(x, row.y + 3f, 58f, row.height);
        Rect previewFieldRect = new Rect(previewLabelRect.xMax, row.y, 154f, row.height);
        GUI.Label(previewLabelRect, "预览对象", miniDimStyle);
        previewTarget = (GameObject)EditorGUI.ObjectField(previewFieldRect, previewTarget, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && previewTarget == null)
        {
            StopEditorPreviewSampling();
        }
        x = previewFieldRect.xMax + 8f;

        if (GUI.Button(new Rect(x, row.y, 44f, row.height), "新建"))
        {
            CreateEmptyAssetInMemory();
        }
        x += 50f;

        GUI.enabled = serializedAsset != null;
        if (GUI.Button(new Rect(x, row.y, 48f, row.height), isPreviewing ? "停止" : "播放"))
        {
            TogglePreview();
        }
        x += 52f;

        loopPreview = GUI.Toggle(new Rect(x, row.y, 48f, row.height), loopPreview, "循环", "Button");
        x += 56f;

        GUI.Label(new Rect(x, row.y + 2f, 54f, row.height), "时间/FPS", miniDimStyle);
        x += 58f;
        DrawTimeFpsField(new Rect(x, row.y, 154f, row.height));
        GUI.enabled = true;
    }

    private void DrawBindingBar(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.14f));
        DrawBorder(rect, new Color(0.08f, 0.08f, 0.08f));

        if (serializedAsset == null)
        {
            GUI.Label(new Rect(rect.x + 10f, rect.y + 1f, rect.width - 20f, 20f), "选择一个 WeaponAttackEffectSO，或点击新建。右键左侧轨道区域创建轨道。", miniDimStyle);
            return;
        }

        serializedAsset.Update();

        Rect row = new Rect(rect.x + 10f, rect.y + 1f, rect.width - 20f, 22f);
        float x = row.x;
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 74f;

        EditorGUI.PropertyField(new Rect(x, row.y, 220f, row.height), serializedAsset.FindProperty("weaponId"), new GUIContent("武器ID"));
        x += 230f;
        EditorGUI.PropertyField(new Rect(x, row.y, 260f, row.height), serializedAsset.FindProperty("weaponName"), new GUIContent("武器名称"));

        EditorGUIUtility.labelWidth = oldLabelWidth;
        serializedAsset.ApplyModifiedProperties();
    }

    private void DrawMainArea()
    {
        Rect mainRect = new Rect(0f, TopHeight, position.width, position.height - TopHeight - BottomHeight);
        Rect leftRect = new Rect(mainRect.x, mainRect.y, LeftWidth, mainRect.height);
        Rect timelineRect = new Rect(leftRect.xMax, mainRect.y, mainRect.width - LeftWidth - InspectorWidth, mainRect.height);
        Rect inspectorRect = new Rect(timelineRect.xMax, mainRect.y, InspectorWidth, mainRect.height);

        EditorGUI.DrawRect(leftRect, new Color(0.14f, 0.14f, 0.14f));
        EditorGUI.DrawRect(timelineRect, new Color(0.13f, 0.13f, 0.13f));
        EditorGUI.DrawRect(inspectorRect, new Color(0.16f, 0.16f, 0.16f));

        DrawTrackList(leftRect);
        DrawTimeline(timelineRect);
        DrawInspector(inspectorRect);
    }

}
