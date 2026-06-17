using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void DrawInspector(Rect rect)
    {
        Rect titleRect = new Rect(rect.x, rect.y, rect.width, 34f);
        EditorGUI.DrawRect(titleRect, new Color(0.15f, 0.15f, 0.15f));
        GUI.Label(new Rect(titleRect.x + 12f, titleRect.y + 8f, titleRect.width - 24f, 18f), selectedClip.kind == ClipKind.None ? "Track" : "Clip", inspectorTitleStyle);

        Rect contentRect = new Rect(rect.x + 12f, rect.y + 42f, rect.width - 24f, rect.height - 54f);
        GUILayout.BeginArea(contentRect);
        inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);

        if (currentAsset == null)
        {
            EditorGUILayout.HelpBox("暂无 SO。", MessageType.Info);
        }
        else if (selectedClip.kind != ClipKind.None)
        {
            DrawSelectedClipInspector();
        }
        else
        {
            DrawSelectedTrackInspector();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSelectedTrackInspector()
    {
        if (selectedTrackIndex < 0 || selectedTrackIndex >= currentAsset.tracks.Count)
        {
            EditorGUILayout.HelpBox("右键左侧区域创建轨道。把动画、音频或特效预制体拖到对应轨道即可生成片段。", MessageType.Info);
            return;
        }

        WeaponAttackTrackData track = currentAsset.tracks[selectedTrackIndex];
        EditorGUI.BeginChangeCheck();
        track.displayName = EditorGUILayout.TextField("Display Name", track.displayName);
        track.color = EditorGUILayout.ColorField("Color", track.color);
        track.hiddenInPreview = EditorGUILayout.Toggle("Hidden In Preview", track.hiddenInPreview);
        EditorGUILayout.LabelField("Type", track.type.ToString());
        EditorGUILayout.LabelField("Clip Count", GetTrackClipCount(track).ToString());
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox("轨道类型创建后不建议修改。需要其他类型时，请新建一条轨道。", MessageType.None);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(currentAsset);
        }
    }

    private void DrawSelectedClipInspector()
    {
        SerializedProperty item = GetSelectedClipProperty();
        if (item == null)
        {
            selectedClip = SelectedClip.None;
            return;
        }

        serializedAsset.Update();
        GUILayout.Label(GetSelectedClipTitle(), inspectorTitleStyle);
        EditorGUILayout.Space(4f);

        if (selectedClip.kind == ClipKind.Effect)
        {
            DrawEffectClipInspector(item);
        }
        else
        {
            SerializedProperty child = item.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                EditorGUILayout.PropertyField(child, true);
                enterChildren = false;
            }
        }

        EditorGUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("跳到该片段"))
        {
            playheadTime = GetClipStartTime(selectedClip);
        }

        GUI.backgroundColor = new Color(0.72f, 0.24f, 0.24f);
        if (GUILayout.Button("删除片段"))
        {
            DeleteClip(selectedClip);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        serializedAsset.ApplyModifiedProperties();
    }

    private void DrawEffectClipInspector(SerializedProperty item)
    {
        DrawProperty(item, "clipType");

        WeaponVFXClipType clipType = (WeaponVFXClipType)item.FindPropertyRelative("clipType").enumValueIndex;
        if (clipType == WeaponVFXClipType.SelfMotionEffect)
        {
            DrawProperty(item, "duration");
            DrawProperty(item, "vfxKey");
            DrawProperty(item, "vfxPrefab");
            DrawProperty(item, "spawnOffset");
            DrawProperty(item, "spawnRotation");
            DrawProperty(item, "attachToWeapon");
            EditorGUILayout.HelpBox("自带位移特效会作为一段持续 Clip 显示，适合刀光、蓄力、持续拖尾等表现。", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4f);
        DrawProperty(item, "projectileVFXKey");
        DrawProperty(item, "vfxPrefab");
        DrawProperty(item, "hitVFXKey");
        DrawProperty(item, "hitVFXRandomEulerRange");
        DrawProperty(item, "damageMultiplier");
        DrawProperty(item, "reverseProjectileTravel");
        DrawProperty(item, "projectileAimSource");
        DrawProperty(item, "projectileMotionMode");
        DrawProperty(item, "projectileLifeTime");

        WeaponProjectileMotionMode motionMode = (WeaponProjectileMotionMode)item.FindPropertyRelative("projectileMotionMode").enumValueIndex;
        if (motionMode == WeaponProjectileMotionMode.CodeDriven)
        {
            DrawProperty(item, "projectileSpeed");
        }
        else if (motionMode == WeaponProjectileMotionMode.VisualSelfMotion)
        {
            DrawProperty(item, "visualForwardDistance");
            DrawProperty(item, "visualForwardCurve");
        }

        DrawProperty(item, "spawnOffset");
        DrawProperty(item, "spawnRotation");
        DrawProperty(item, "attachToWeapon");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Projectile Hitbox", EditorStyles.boldLabel);
        DrawProperty(item, "projectileHitShape");
        DrawProperty(item, "projectileHitRadius");
        WeaponProjectileHitShape hitShape = (WeaponProjectileHitShape)item.FindPropertyRelative("projectileHitShape").enumValueIndex;
        if (hitShape == WeaponProjectileHitShape.Cylinder)
        {
            DrawProperty(item, "projectileHitHeight");
        }
        DrawProperty(item, "projectileHitCenterOffset");
        DrawProperty(item, "pierceMonsters");
        SerializedProperty pierceMonsters = item.FindPropertyRelative("pierceMonsters");
        if (pierceMonsters != null && pierceMonsters.boolValue)
        {
            DrawProperty(item, "pierceCount");
        }
        EditorGUILayout.HelpBox("弹幕触发器在轨道上显示为小方块，表示动作到达这一帧时生成弹幕。选中后可在 Scene 视图看到生成点、方向和伤害检测盒。", MessageType.None);
    }

    private static void DrawProperty(SerializedProperty parent, string propertyName)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

}
