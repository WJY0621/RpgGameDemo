using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(DL))]
public class DlInspector : Editor
{
    /* Dialogue Scriptable Objects */
    private SerializedProperty dialogueContainerProperty;
    private SerializedProperty dialogueGroupProperty;
    private SerializedProperty dialogueProperty;

    /* Filters */
    private SerializedProperty groupedDialoguesProperty;
    private SerializedProperty startingDialoguesOnlyProperty;

    /* Indexes */
    private SerializedProperty selectedDialogueGroupIndexProperty;
    private SerializedProperty selectedDialogueIndexProperty;

    // Unity Message | 0 references
    private void OnEnable()
    {
        dialogueContainerProperty = serializedObject.FindProperty("dialogueContainer");
        dialogueGroupProperty = serializedObject.FindProperty("dialogueGroup");
        dialogueProperty = serializedObject.FindProperty("dialogue");

        groupedDialoguesProperty = serializedObject.FindProperty("groupedDialogues");
        startingDialoguesOnlyProperty = serializedObject.FindProperty("startingDialoguesOnly");

        selectedDialogueGroupIndexProperty = serializedObject.FindProperty("selectedDialogueGroupIndex");
        selectedDialogueIndexProperty = serializedObject.FindProperty("selectedDialogueIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDialogueContainerArea();
        DLContainerSO dialogueContainer = (DLContainerSO)dialogueContainerProperty.objectReferenceValue;
        if (dialogueContainer == null)
        {
            StopDrawing("Select a Dialogue Container to see the rest of the Inspector");
            return;
        }
        DrawFiltersArea();
        if (groupedDialoguesProperty.boolValue)
        {
            List<string> dialogueGroupNames = dialogueContainer.GetDialogueGroupNames();

            if (dialogueGroupNames.Count == 0)
            {
                StopDrawing("There are no Dialogue Groups in this Dialogue Container.");
                return;
            }
            DrawDialogueGroupArea(dialogueContainer, dialogueGroupNames);
        }
        DrawDialogueArea();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDialogueContainerArea()
    {
        DlInspectorUtility.DrawHeader("Dialogue Container");
        dialogueContainerProperty.DrawPropertyField();
        EditorGUILayout.PropertyField(dialogueContainerProperty);
        DlInspectorUtility.DrawSpace();
    }

    private void DrawFiltersArea()
    {
        DlInspectorUtility.DrawHeader("Filters");
        groupedDialoguesProperty.DrawPropertyField();
        startingDialoguesOnlyProperty.DrawPropertyField();
        DlInspectorUtility.DrawSpace();
    }

    private void DrawDialogueGroupArea(DLContainerSO dialogueContainer, List<string> dialogueGroupNames)
    {
        DlInspectorUtility.DrawHeader("Dialogue Group");

        int oldSelectedDialogueGroupIndex = selectedDialogueGroupIndexProperty.intValue;
        DLGroupSO oldDialogueGroup = (DLGroupSO)dialogueGroupProperty.objectReferenceValue;
        UpdateIndexOnDialogueGroupUpdate(dialogueGroupNames, oldSelectedDialogueGroupIndex, oldDialogueGroup);

        selectedDialogueGroupIndexProperty.intValue =
            DlInspectorUtility.DrawPopup("Dialogue Group", selectedDialogueGroupIndexProperty.intValue, dialogueGroupNames.ToArray());
        string selectedDialogueGroupName = dialogueGroupNames[selectedDialogueGroupIndexProperty.intValue];

        DLGroupSO selectedDialogueGroup = DLIOUtility.LoadAsset<DLGroupSO>
            ($"Assets/System/DialogueSystem/Dialogues/{dialogueContainer.FileName}/Groups/{selectedDialogueGroupName}"
            , selectedDialogueGroupName);

        dialogueGroupProperty.objectReferenceValue = selectedDialogueGroup;
        dialogueGroupProperty.DrawPropertyField();
        DlInspectorUtility.DrawSpace();
    }

    private void DrawDialogueArea()
    {
        DlInspectorUtility.DrawHeader("Dialogue");
        selectedDialogueIndexProperty.intValue =
            DlInspectorUtility.DrawPopup("Dialogue", selectedDialogueIndexProperty.intValue, new string[] { });
        dialogueProperty.DrawPropertyField();
    }
    private void StopDrawing(string reason)
    {
        DlInspectorUtility.DrawHelpBox(reason);
        serializedObject.ApplyModifiedProperties();
    }

    private void UpdateIndexOnDialogueGroupUpdate(List<string> dialogueGroupNames, int oldSelectedDialogueGroupIndex, DLGroupSO oldDialogueGroup)
    {
        if (oldDialogueGroup == null)
        {
            selectedDialogueGroupIndexProperty.intValue = 0;
        }
        else
        {
            if (oldSelectedDialogueGroupIndex > dialogueGroupNames.Count - 1 ||
                oldDialogueGroup.GroupName != dialogueGroupNames[oldSelectedDialogueGroupIndex])
            {
                if (dialogueGroupNames.Contains(oldDialogueGroup.GroupName))
                {
                    selectedDialogueGroupIndexProperty.intValue = dialogueGroupNames.IndexOf(oldDialogueGroup.GroupName);
                }
                else
                {
                    selectedDialogueGroupIndexProperty.intValue = 0;
                }
            }
        }
    }
}
