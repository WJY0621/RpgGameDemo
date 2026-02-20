using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueEditorWindow : EditorWindow
{
    DialogueGraphView graphView;
    private readonly string defaultFileName = "DialoguesFileName";
    private static TextField fileNameTextField;
    private Button saveButton;
    [MenuItem("Tools/Dialogue Graph")]
    public static void ShowExample()
    {
        GetWindow<DialogueEditorWindow>("Dialogue Graph");
    }

    private void OnEnable()
    {
        AddGraphView();
        AddToolbar();
        AddStyles();
    }

    #region Elements Addtion
    //作为主面板显示
    private void AddGraphView()
    {
        //Debug.Log("OnEnable开始执行");
        graphView = new DialogueGraphView(this);
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);
        //Debug.Log("OnEnable执行完成");
    }
    private void AddToolbar()
    {
        Toolbar toolbar = new Toolbar();
        fileNameTextField = DLElementUtility.CreateTextField(defaultFileName, "File Name:", callback =>
        {
            fileNameTextField.value = callback.newValue.RemoveWhitespaces().RemoveSpecialCharacters();
        });
        saveButton = DLElementUtility.CreateButton("Save", () => Save());
        Button loadButton = DLElementUtility.CreateButton("Load", () => Load());
        Button clearButton = DLElementUtility.CreateButton("Clear", () => Clear());
        Button resetButton = DLElementUtility.CreateButton("Reset", () => Reset());
        Button miniMapButton = DLElementUtility.CreateButton("Minimap", () => ToggleMiniMap());

        toolbar.Add(fileNameTextField);
        toolbar.Add(saveButton);
        toolbar.Add(loadButton);
        toolbar.Add(clearButton);
        toolbar.Add(resetButton);
        toolbar.Add(miniMapButton);

        toolbar.AddStyleSheets("DialogueSystem/DLToolbarStyles.uss");
        rootVisualElement.Add(toolbar);
    }

    private void AddStyles()
    {
        rootVisualElement.AddStyleSheets("DialogueSystem/DialogueVariables.uss");
    }
    #endregion

    #region Toolbar Actions
    private void Save()
    {
        if (string.IsNullOrEmpty(fileNameTextField.value))
        {
            EditorUtility.DisplayDialog("无效文件名", "请传入有效文件名", "Roger");
            return;
        }
        DLIOUtility.Initialize(graphView, fileNameTextField.value);
        DLIOUtility.Save();
    }

    private void Clear()
    {
        //Debug.Log($"Clear被调用，graphView为null: {graphView == null}");
        graphView?.ClearGraph();
    }
    private void Reset()
    {
        Clear();
        UpdateFileName(defaultFileName);
    }
    private void Load()
    {
        string filePath = EditorUtility.OpenFilePanel("Dialogue Graphs", "Assets/Editor/DialogueSystem/Graphs", "asset");
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        Clear();
        DLIOUtility.Initialize(graphView, Path.GetFileNameWithoutExtension(filePath));
        DLIOUtility.Load();
    }
    
    private void ToggleMiniMap()
    {
        graphView.ToggleMiniMap();
    }

    #endregion

    #region Utility Methods
    public static void UpdateFileName(string newFileName)
    {
        if (fileNameTextField == null)
        {
            // 延迟到下一帧设置
            EditorApplication.delayCall += () =>
            {
                if (fileNameTextField != null)
                    fileNameTextField.value = newFileName;
            };
            return;
        }
        fileNameTextField.value = newFileName;
    }
    public void EnableSaving()
    {
        saveButton.SetEnabled(true);
    }
    public void DisableSaving()
    {
        saveButton.SetEnabled(false);
    }
    #endregion
}
