using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class DLSearchWindow : ScriptableObject, ISearchWindowProvider
{
    private DialogueGraphView graphView;
    private Texture2D indentationIcon;
    public void Initialize(DialogueGraphView dialogueGraphView)
    {
        graphView = dialogueGraphView;
        indentationIcon = new Texture2D(1, 1);
        indentationIcon.SetPixel(0, 0, Color.clear);
        indentationIcon.Apply();
    }
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        List<SearchTreeEntry> searchTreeEntries = new List<SearchTreeEntry>()
        {
            new SearchTreeGroupEntry(new GUIContent("Create Element")),
            new SearchTreeGroupEntry(new GUIContent("Dialogue Node"), 1),
            new SearchTreeEntry(new GUIContent("Single Choice", indentationIcon))
            {
                level = 2,
                userData = DialogueType.SingleChoice
            },
            new SearchTreeEntry(new GUIContent("Multiple Choice", indentationIcon))
            {
                level = 2,
                userData = DialogueType.MultipleChoice
            },
            new SearchTreeGroupEntry(new GUIContent("Dialogue Group"), 1),
            new SearchTreeEntry(new GUIContent("Single Group", indentationIcon))
            {
                level = 2,
                userData = new Group()
            }
        };
        return searchTreeEntries;
    }

    public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
    {
        Vector2 localMousePosition = graphView.GetLocalMousePosition(context.screenMousePosition, true);
        switch (SearchTreeEntry.userData)
        {
            case DialogueType.SingleChoice:
                DLSingleChoiceNode singleChoiceNode = (DLSingleChoiceNode)graphView.CreateNode
                    ("对话节点", DialogueType.SingleChoice, localMousePosition);
                graphView.AddElement(singleChoiceNode);
                return true;
            case DialogueType.MultipleChoice:
                DLMultipleChoiceNode multipleChoiceNode = (DLMultipleChoiceNode)graphView.CreateNode
                    ("对话节点", DialogueType.MultipleChoice, localMousePosition);
                graphView.AddElement(multipleChoiceNode);
                return true;
            case Group _:
                graphView.CreateGroup("DialogueGroup", localMousePosition);
                return true;
            default:
                return false;
        }
    }
}
