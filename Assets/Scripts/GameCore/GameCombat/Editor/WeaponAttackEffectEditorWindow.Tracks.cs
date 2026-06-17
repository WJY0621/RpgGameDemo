using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private void DrawTrackList(Rect rect)
    {
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, RulerHeight);
        EditorGUI.DrawRect(headerRect, new Color(0.12f, 0.12f, 0.12f));
        DrawBottomLine(headerRect, new Color(0.20f, 0.20f, 0.20f));

        if (currentAsset == null)
        {
            GUI.Label(new Rect(rect.x + 10f, rect.y + 58f, rect.width - 20f, 20f), "暂无 SO", centeredLabelStyle);
            return;
        }

        for (int i = 0; i < currentAsset.tracks.Count; i++)
        {
            WeaponAttackTrackData track = currentAsset.tracks[i];
            if (track == null)
            {
                continue;
            }

            track.EnsureId();
            Rect row = new Rect(rect.x, rect.y + RulerHeight + i * TrackHeight, rect.width, TrackHeight);
            bool selected = selectedTrackIndex == i && selectedClip.kind == ClipKind.None;
            Color rowColor = selected ? new Color(0.20f, 0.22f, 0.25f) : (i % 2 == 0 ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.13f, 0.13f, 0.13f));

            EditorGUI.DrawRect(row, rowColor);
            Color stripeColor = track.hiddenInPreview ? new Color(track.color.r, track.color.g, track.color.b, 0.35f) : track.color;
            EditorGUI.DrawRect(new Rect(row.x + 8f, row.y + 6f, 5f, row.height - 12f), stripeColor);
            GUI.Label(new Rect(row.x + 22f, row.y + 2f, 128f, 16f), track.displayName, trackTitleStyle);
            GUI.Label(new Rect(row.x + 22f, row.y + 16f, 128f, 13f), track.hiddenInPreview ? track.type + " / Hidden" : track.type.ToString(), trackSubTitleStyle);

            if ((track.type == WeaponAttackTrackType.Animation || track.type == WeaponAttackTrackType.Hitbox) &&
                GUI.Button(new Rect(row.xMax - 54f, row.y + 6f, 20f, 18f), "K"))
            {
                if (track.type == WeaponAttackTrackType.Animation)
                {
                    AddAnimationKeyframe(track);
                }
                else
                {
                    KeyHitboxApplyDamageTime(track);
                }
            }

            if (GUI.Button(new Rect(row.xMax - 30f, row.y + 6f, 20f, 18f), "+"))
            {
                AddEmptyClip(track);
            }

            HandleTrackMouse(row, i, track);
        }

        if (currentAsset.tracks.Count == 0)
        {
            GUI.Label(new Rect(rect.x + 10f, rect.y + 58f, rect.width - 20f, 42f), "右键这里创建动画 / 音效 / 特效 / 攻击盒轨道", centeredLabelStyle);
        }

        HandleTrackListContext(rect);
    }

    private void HandleTrackMouse(Rect row, int index, WeaponAttackTrackData track)
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
            selectedAnimationKeyframeIndex = -1;
            Repaint();
            evt.Use();
        }
        else if (evt.type == EventType.ContextClick)
        {
            selectedTrackIndex = index;
            selectedClip = SelectedClip.None;
            selectedAnimationKeyframeIndex = -1;
            ShowTrackContextMenu(index, track);
            evt.Use();
        }
    }

    private void HandleTrackListContext(Rect rect)
    {
        Event evt = Event.current;
        if (evt.type != EventType.ContextClick || !rect.Contains(evt.mousePosition))
        {
            return;
        }

        ShowCreateTrackMenu();
        evt.Use();
    }

}
