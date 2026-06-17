using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskEndNode : Node
{
    private const float NodeWidth = 150f;

    public string ID { get; set; }
    public Port InputPort { get; private set; }

    private bool collapseHookRegistered;

    public void Initialize(Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        SetPosition(new Rect(position, new Vector2(NodeWidth, 100f)));
    }

    public void ApplySaveData(TaskGraphNodeSaveData saveData)
    {
        ID = saveData.id;
        SetPosition(new Rect(saveData.position, new Vector2(NodeWidth, 100f)));
    }

    public void Draw()
    {
        title = "Task End";
        ApplyNodeStyle();

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "Prev";
        inputContainer.Add(InputPort);

        Label infoLabel = new Label("\u4efb\u52a1\u5728\u8fd9\u91cc\u7ed3\u675f");
        infoLabel.style.marginTop = 8f;
        infoLabel.style.marginBottom = 8f;
        infoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        infoLabel.style.color = new Color(0.95f, 0.92f, 0.92f);
        extensionContainer.Add(infoLabel);

        RefreshExpandedState();
        RefreshPorts();
        ApplyExpandedState();
        RegisterCollapseToggle();
    }

    public TaskGraphNodeSaveData BuildSaveData()
    {
        return new TaskGraphNodeSaveData
        {
            nodeType = TaskGraphNodeType.End,
            id = ID,
            position = GetPosition().position,
            nextNodeID = string.Empty
        };
    }

    private void ApplyNodeStyle()
    {
        titleContainer.style.backgroundColor = new Color(0.42f, 0.18f, 0.18f, 0.96f);
        titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainContainer.style.backgroundColor = new Color(0.16f, 0.1f, 0.1f, 0.98f);
        mainContainer.style.borderBottomLeftRadius = 10f;
        mainContainer.style.borderBottomRightRadius = 10f;
        mainContainer.style.borderTopLeftRadius = 10f;
        mainContainer.style.borderTopRightRadius = 10f;
        mainContainer.style.borderLeftWidth = 1f;
        mainContainer.style.borderRightWidth = 1f;
        mainContainer.style.borderTopWidth = 1f;
        mainContainer.style.borderBottomWidth = 1f;
        mainContainer.style.borderLeftColor = new Color(0.74f, 0.29f, 0.29f, 0.95f);
        mainContainer.style.borderRightColor = new Color(0.74f, 0.29f, 0.29f, 0.95f);
        mainContainer.style.borderTopColor = new Color(0.74f, 0.29f, 0.29f, 0.95f);
        mainContainer.style.borderBottomColor = new Color(0.74f, 0.29f, 0.29f, 0.95f);

        inputContainer.style.backgroundColor = new Color(0.17f, 0.11f, 0.11f, 0.9f);
        inputContainer.style.justifyContent = Justify.SpaceBetween;
        extensionContainer.style.paddingLeft = 8f;
        extensionContainer.style.paddingRight = 8f;
        extensionContainer.style.paddingBottom = 8f;
    }

    private void RegisterCollapseToggle()
    {
        if (collapseHookRegistered)
        {
            return;
        }

        collapseHookRegistered = true;
        titleContainer.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0)
            {
                return;
            }

            if (evt.localMousePosition.x < titleContainer.layout.width - 28f)
            {
                return;
            }

            expanded = !expanded;
            ApplyExpandedState();

            evt.StopImmediatePropagation();
        }, TrickleDown.TrickleDown);
    }

    private void ApplyExpandedState()
    {
        extensionContainer.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshExpandedState();
        RefreshPorts();
    }

    public void SetExpanded(bool isExpanded)
    {
        expanded = isExpanded;
        ApplyExpandedState();
    }
}
