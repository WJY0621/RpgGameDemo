using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public partial class WeaponAttackEffectEditorWindow : EditorWindow
{
    private PlayableGraph previewGraph;
    private AnimationMixerPlayable previewMixer;
    private Animator previewGraphAnimator;
    private bool previewGraphValid;

    private void SaveAsset(bool closeAfterSave = true)
    {
        if (currentAsset == null)
        {
            return;
        }

        Directory.CreateDirectory(SaveFolder);
        string path = AssetDatabase.GetAssetPath(currentAsset);
        string targetPath = Path.Combine(SaveFolder, BuildAssetFileName()).Replace("\\", "/");

        if (string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(currentAsset, AssetDatabase.GenerateUniqueAssetPath(targetPath));
        }
        else
        {
            string currentDirectory = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string currentFileName = Path.GetFileName(path);
            if (currentDirectory != SaveFolder)
            {
                string moveTargetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);
                string moveError = AssetDatabase.MoveAsset(path, moveTargetPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogWarning("[WeaponAttackEffectEditorWindow] Move SO failed: " + moveError);
                    EditorUtility.SetDirty(currentAsset);
                }
            }
            else if (currentFileName != BuildAssetFileName())
            {
                string renameTargetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);
                string renameError = AssetDatabase.RenameAsset(path, Path.GetFileNameWithoutExtension(renameTargetPath));
                if (!string.IsNullOrEmpty(renameError))
                {
                    Debug.LogWarning("[WeaponAttackEffectEditorWindow] Rename SO failed: " + renameError);
                }
            }

            EditorUtility.SetDirty(currentAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        BindAsset(currentAsset);

        if (closeAfterSave)
        {
            Close();
        }
    }

    private void SaveCurrentAssetChanges()
    {
        if (currentAsset == null)
        {
            return;
        }

        if (serializedAsset != null)
        {
            serializedAsset.ApplyModifiedProperties();
        }

        string path = AssetDatabase.GetAssetPath(currentAsset);
        if (string.IsNullOrEmpty(path))
        {
            SaveAsset(false);
            return;
        }

        EditorUtility.SetDirty(currentAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private string BuildAssetFileName()
    {
        string source = !string.IsNullOrWhiteSpace(currentAsset.weaponName) ? currentAsset.weaponName : currentAsset.weaponId;
        if (string.IsNullOrWhiteSpace(source))
        {
            source = "NewWeapon";
        }

        return $"WeaponAttackEffect_{SanitizeFileName(source)}.asset";
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        List<char> result = new List<char>(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool invalid = false;
            for (int j = 0; j < invalidChars.Length; j++)
            {
                if (c == invalidChars[j])
                {
                    invalid = true;
                    break;
                }
            }

            result.Add(invalid || char.IsWhiteSpace(c) ? '_' : c);
        }

        return new string(result.ToArray());
    }

    private void TogglePreview()
    {
        isPreviewing = !isPreviewing;
        lastPreviewTime = EditorApplication.timeSinceStartup;
        if (isPreviewing)
        {
            playheadTime = 0f;
            ResetPreviewAudioState();
        }
        else
        {
            StopPreviewAudio();
        }
    }

    private void OnEditorUpdate()
    {
        if (!isPreviewing || currentAsset == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastPreviewTime);
        lastPreviewTime = now;
        float previousTime = playheadTime;
        playheadTime += delta;

        float previewEnd = GetPreviewEndTime();
        if (playheadTime >= previewEnd)
        {
            if (loopPreview)
            {
                TriggerPreviewAudioEvents(previousTime, previewEnd);
                playheadTime = Mathf.Repeat(playheadTime, Mathf.Max(0.01f, previewEnd));
                ResetPreviewAudioState();
                TriggerPreviewAudioEvents(0f, playheadTime);
            }
            else
            {
                playheadTime = previewEnd;
                TriggerPreviewAudioEvents(previousTime, playheadTime);
                isPreviewing = false;
                StopPreviewAudio();
            }
        }
        else
        {
            TriggerPreviewAudioEvents(previousTime, playheadTime);
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void ApplyEditorPreviewAtTime()
    {
        if (Application.isPlaying || currentAsset == null || previewTarget == null)
        {
            StopEditorPreviewSampling();
            return;
        }

        if (!TryGetPreviewAnimationAtTime(playheadTime, out AnimationClip clipA, out float localA, out AnimationClip clipB, out float localB, out float weightA))
        {
            StopEditorPreviewSampling();
            return;
        }

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
        }

        editorPreviewSamplingActive = true;

        if (clipB == null)
        {
            DestroyPreviewGraph();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(previewTarget, clipA, localA);
            AnimationMode.EndSampling();
        }
        else
        {
            Animator animator = previewTarget.GetComponent<Animator>();
            if (animator == null)
            {
                DestroyPreviewGraph();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(previewTarget, clipA, localA);
                AnimationMode.EndSampling();
            }
            else
            {
                EnsurePreviewGraph(animator);

                AnimationClipPlayable playableA = AnimationClipPlayable.Create(previewGraph, clipA);
                AnimationClipPlayable playableB = AnimationClipPlayable.Create(previewGraph, clipB);
                playableA.SetTime(localA);
                playableA.SetSpeed(0d);
                playableB.SetTime(localB);
                playableB.SetSpeed(0d);

                previewMixer.DisconnectInput(0);
                previewMixer.DisconnectInput(1);
                previewGraph.Connect(playableB, 0, previewMixer, 0);
                previewGraph.Connect(playableA, 0, previewMixer, 1);
                previewMixer.SetInputWeight(0, Mathf.Clamp01(1f - weightA));
                previewMixer.SetInputWeight(1, Mathf.Clamp01(weightA));

                AnimationMode.BeginSampling();
                AnimationMode.SamplePlayableGraph(previewGraph, 0, 0f);
                AnimationMode.EndSampling();

                playableA.Destroy();
                playableB.Destroy();
            }
        }

        SceneView.RepaintAll();
    }

    private void EnsurePreviewGraph(Animator animator)
    {
        if (previewGraphValid && previewGraphAnimator == animator && previewGraph.IsValid())
        {
            return;
        }

        DestroyPreviewGraph();

        previewGraph = PlayableGraph.Create("WeaponAttackPreviewGraph");
        previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        previewMixer = AnimationMixerPlayable.Create(previewGraph, 2);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(previewGraph, "Preview", animator);
        output.SetSourcePlayable(previewMixer);
        previewGraphAnimator = animator;
        previewGraphValid = true;
    }

    private void DestroyPreviewGraph()
    {
        if (!previewGraphValid)
        {
            return;
        }

        if (previewGraph.IsValid())
        {
            previewGraph.Destroy();
        }

        previewGraphAnimator = null;
        previewGraphValid = false;
    }

    private void StopEditorPreviewSampling()
    {
        DestroyPreviewGraph();
        CleanupPreviewVfxInstances();
        if (!isPreviewing)
        {
            StopPreviewAudio();
        }

        if (!editorPreviewSamplingActive)
        {
            return;
        }

        if (AnimationMode.InAnimationMode())
        {
            AnimationMode.StopAnimationMode();
        }

        editorPreviewSamplingActive = false;
        SceneView.RepaintAll();
    }

    private bool TryGetPreviewAnimationAtTime(float time, out AnimationClip clipA, out float localA, out AnimationClip clipB, out float localB, out float weightA)
    {
        clipA = null;
        clipB = null;
        localA = 0f;
        localB = 0f;
        weightA = 1f;

        if (currentAsset == null)
        {
            return false;
        }

        float firstStart = float.MinValue;
        int firstIndex = -1;
        float secondStart = float.MinValue;
        int secondIndex = -1;

        for (int i = 0; i < currentAsset.animationEvents.Count; i++)
        {
            WeaponAnimationEvent animationEvent = currentAsset.animationEvents[i];
            if (animationEvent == null || animationEvent.animationClip == null)
            {
                continue;
            }

            float clipDuration = Mathf.Max(0.01f, animationEvent.animationClip.length);
            float startTime = Mathf.Max(0f, animationEvent.startTime);
            float endTime = startTime + clipDuration;
            if (time < startTime || time > endTime)
            {
                continue;
            }

            if (startTime > firstStart)
            {
                secondStart = firstStart;
                secondIndex = firstIndex;
                firstStart = startTime;
                firstIndex = i;
            }
            else if (startTime > secondStart)
            {
                secondStart = startTime;
                secondIndex = i;
            }
        }

        if (firstIndex < 0)
        {
            return false;
        }

        WeaponAnimationEvent first = currentAsset.animationEvents[firstIndex];
        float firstClipDuration = Mathf.Max(0.01f, first.animationClip.length);
        float firstStartTime = Mathf.Max(0f, first.startTime);
        clipA = first.animationClip;
        localA = Mathf.Clamp(time - firstStartTime, 0f, firstClipDuration);

        if (secondIndex < 0)
        {
            return true;
        }

        WeaponAnimationEvent second = currentAsset.animationEvents[secondIndex];
        if (!first.blendWithOverlaps && !second.blendWithOverlaps)
        {
            return true;
        }

        float secondClipDuration = Mathf.Max(0.01f, second.animationClip.length);
        float secondStartTime = Mathf.Max(0f, second.startTime);
        float secondEndTime = secondStartTime + secondClipDuration;
        float firstEndTime = firstStartTime + firstClipDuration;

        float overlapStart = firstStartTime;
        float overlapEnd = Mathf.Min(firstEndTime, secondEndTime);

        if (overlapEnd <= overlapStart || time < overlapStart || time > overlapEnd)
        {
            return true;
        }

        clipB = second.animationClip;
        localB = Mathf.Clamp(time - secondStartTime, 0f, secondClipDuration);

        float progress = (time - overlapStart) / (overlapEnd - overlapStart);
        weightA = Mathf.Clamp01(progress);

        return true;
    }

    private void OnSelectionChanged()
    {
        TryUseSelectedAsset();
    }

    private void TryUseSelectedAsset()
    {
        if (Selection.activeObject is WeaponAttackEffectSO asset && asset != currentAsset)
        {
            BindAsset(asset);
            Repaint();
        }
    }

    private void BindAsset(WeaponAttackEffectSO asset)
    {
        currentAsset = asset;
        serializedAsset = currentAsset != null ? new SerializedObject(currentAsset) : null;
        selectedTrackIndex = currentAsset != null && currentAsset.tracks.Count > 0 ? 0 : -1;
        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = -1;
        playheadTime = Mathf.Clamp(playheadTime, 0f, currentAsset != null ? currentAsset.totalDuration : 1f);
    }

    private void CreateEmptyAssetInMemory()
    {
        currentAsset = CreateInstance<WeaponAttackEffectSO>();
        currentAsset.weaponId = "NewWeapon";
        currentAsset.weaponName = "New Weapon";
        currentAsset.totalDuration = 1f;
        serializedAsset = new SerializedObject(currentAsset);
        selectedTrackIndex = -1;
        selectedClip = SelectedClip.None;
        selectedAnimationKeyframeIndex = -1;
        playheadTime = 0f;
    }

    private string GetAssetPreviewPath()
    {
        string path = AssetDatabase.GetAssetPath(currentAsset);
        return string.IsNullOrEmpty(path) ? $"未保存：{SaveFolder}/{BuildAssetFileName()}" : path;
    }

}
