using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DLNode : Node
{
    public string ID { get; set; }
    public string DialogueName { get; set; }
    public List<DLChoiceSaveData> Choices { get; set; }
    public string Text { get; set; }
    public DialogueType DialogueType { get; set; }
    public DLGroup Group { get; set; }

    protected DialogueGraphView graphView;
    private Color defaultBackgroundColor;

    public virtual void Initialize(string nodeName, DialogueGraphView graphView, Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        DialogueName = nodeName;
        Choices = new List<DLChoiceSaveData>();
        Text = "对话内容";
        this.graphView = graphView;
        defaultBackgroundColor = new Color(0.1568f, 0.2078f, 0.3215f);

        SetPosition(new Rect(position, Vector2.zero));
        ClearContainerBackgrounds();
        mainContainer.AddToClassList("ds-node__main-container");
        extensionContainer.AddToClassList("ds-node__extension-container");
    }

    public virtual void Draw()
    {
        TextField dialogueNameTextField = DLElementUtility.CreateTextField(DialogueName, null, callback =>
        {
            TextField target = (TextField) callback.target;
            target.value = callback.newValue.RemoveWhitespaces().RemoveSpecialCharacters();
            if (string.IsNullOrEmpty(target.value))
            {
                if (!string.IsNullOrEmpty(DialogueName))
                {
                    ++graphView.NameErrorsAmount;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(DialogueName))
                {
                    --graphView.NameErrorsAmount;
                }
            }

            if (Group == null)
            {
                graphView.RemoveUngroupedNode(this);
                DialogueName = target.value;
                graphView.AddUngroupedNode(this);
                return;
            }
            DLGroup currentGroup = Group;
            graphView.RemoveGroupedNode(this, Group);
            DialogueName = target.value;
            graphView.AddGroupedNode(this, currentGroup);
        });
        dialogueNameTextField.AddClasses(
            "ds-node__textfield",
            "ds-node__filename-textfield",
            "ds-node__textfield__hidden"
        );
        titleContainer.Insert(0, dialogueNameTextField);

        Port inputPort = this.CreatePort("连接点", Orientation.Horizontal, Direction.Input, Port.Capacity.Multi);
        inputPort.portColor = new Color(0.35f, 0.64f, 0.88f, 1);
        inputContainer.Add(inputPort);

        VisualElement customDataContainer = new VisualElement();
        customDataContainer.AddToClassList("ds-node__custom-data-container");

        Foldout foldout = DLElementUtility.CreateFoldout("对话内容");

        TextField textTextField = DLElementUtility.CreateTextArea(Text, null, callback =>
        {
            Text = callback.newValue;
        });
        textTextField.AddClasses("ds-node__textfield", "ds-node__quote-textfield");

        foldout.Add(textTextField);
        customDataContainer.Add(foldout);
        extensionContainer.Add(customDataContainer);
    }

    #region Overrided Methods
    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Disconnect Input Ports", actionEvent => DisconnectInputPorts());
        evt.menu.AppendAction("Disconnect Output Ports", actionEvent => DisconnectOutputPorts());
        base.BuildContextualMenu(evt);
    }
    #endregion

    #region Utility Methods
    public void DisconnectAllPorts()
    {
        DisconnectInputPorts();
        DisconnectOutputPorts();
    }

    private void DisconnectInputPorts()
    {
        DisconnectPorts(inputContainer);
    }
    private void DisconnectOutputPorts()
    {
        DisconnectPorts(outputContainer);
    }

    private void DisconnectPorts(VisualElement container)
    {
        foreach(Port port in container.Children())
        {
            if (!port.connected)
            {
                continue;
            }
            graphView.DeleteElements(port.connections);
        }
    }

    public bool IsStartingNode()
    {
        Port inputPort = (Port) inputContainer.Children().First();
        return !inputPort.connected;
    }

    public void SetErrorStyle(Color color)
    {
        mainContainer.style.backgroundColor = color;
    }

    public void ResetStyle()
    {
        mainContainer.style.backgroundColor = defaultBackgroundColor;
    }

    void ClearContainerBackgrounds()
    {
        // 清除titleContainer背景
        titleContainer.style.backgroundColor = Color.clear;
        titleContainer.style.backgroundImage = null;

        // 清除inputContainer背景  
        inputContainer.style.backgroundColor = Color.clear;
        inputContainer.style.backgroundImage = null;
    }
    #endregion
}
