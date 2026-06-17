using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskRootNode : Node
{
    private const float NodeWidth = 222f;
    private const float LabelWidth = 96f;
    private const float RowWidth = 186f;
    private const float DescriptionWidth = RowWidth;
    private const float FieldWidth = RowWidth - LabelWidth;

    public string ID { get; set; }
    public int TaskID { get; set; }
    public string TaskName { get; set; }
    public string TaskDescription { get; set; }
    public TaskType TaskType { get; set; }
    public int GiverNpcID { get; set; }
    public int PrerequisiteTaskID { get; set; }

    public Port OutputPort { get; private set; }

    private bool collapseHookRegistered;

    public void Initialize(Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        TaskID = 1;
        TaskName = "New Task";
        TaskDescription = string.Empty;
        TaskType = TaskType.Side;
        GiverNpcID = -1;
        PrerequisiteTaskID = -1;
        SetPosition(new Rect(position, new Vector2(NodeWidth, 340f)));
    }

    public void ApplySaveData(TaskGraphNodeSaveData saveData)
    {
        ID = saveData.id;
        TaskID = saveData.taskID;
        TaskName = saveData.taskName;
        TaskDescription = saveData.taskDescription;
        TaskType = saveData.taskType;
        GiverNpcID = saveData.giverNpcID;
        PrerequisiteTaskID = saveData.prerequisiteTaskID;
        SetPosition(new Rect(saveData.position, new Vector2(NodeWidth, 340f)));
    }

    public void Draw()
    {
        ApplyNodeStyle();
        UpdateNodeTitle();

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        OutputPort.portName = "First Step";
        outputContainer.Add(OutputPort);

        TextField taskIDField = new TextField { value = TaskID.ToString() };
        taskIDField.style.width = FieldWidth;
        taskIDField.RegisterValueChangedCallback(evt =>
        {
            if (int.TryParse(evt.newValue, out int parsedValue))
            {
                TaskID = parsedValue;
                UpdateNodeTitle();
            }
        });
        extensionContainer.Add(CreateLabeledRow("Task ID", taskIDField));

        TextField taskNameField = new TextField { value = TaskName };
        taskNameField.style.width = FieldWidth;
        taskNameField.RegisterValueChangedCallback(evt =>
        {
            TaskName = evt.newValue;
            UpdateNodeTitle();
        });
        extensionContainer.Add(CreateLabeledRow("Task Name", taskNameField));

        VisualElement descriptionSection = new VisualElement();
        descriptionSection.style.alignSelf = Align.Center;
        descriptionSection.style.width = RowWidth;
        descriptionSection.style.minWidth = RowWidth;
        descriptionSection.style.maxWidth = RowWidth;
        descriptionSection.style.marginTop = 4f;
        descriptionSection.style.marginBottom = 2f;

        Label descriptionLabel = CreateRowLabel("Task Description");
        descriptionLabel.style.width = RowWidth;
        descriptionLabel.style.minWidth = RowWidth;
        descriptionLabel.style.maxWidth = RowWidth;
        descriptionLabel.style.marginTop = 0f;
        descriptionLabel.style.marginBottom = 2f;
        descriptionSection.Add(descriptionLabel);

        TextField taskDescriptionField = DLElementUtility.CreateTextArea(TaskDescription, null, callback =>
        {
            TaskDescription = callback.newValue;
        });
        taskDescriptionField.style.alignSelf = Align.Stretch;
        taskDescriptionField.style.width = DescriptionWidth;
        taskDescriptionField.style.minWidth = DescriptionWidth;
        taskDescriptionField.style.maxWidth = DescriptionWidth;
        taskDescriptionField.style.minHeight = 80f;
        taskDescriptionField.style.maxHeight = 80f;
        taskDescriptionField.style.marginLeft = 0f;
        taskDescriptionField.style.marginRight = 0f;
        taskDescriptionField.style.marginTop = 0f;
        taskDescriptionField.style.marginBottom = 0f;
        taskDescriptionField.AddClasses("ds-node__textfield", "ds-node__quote-textfield");
        descriptionSection.Add(taskDescriptionField);
        extensionContainer.Add(descriptionSection);

        EnumField taskTypeField = new EnumField(TaskType);
        taskTypeField.style.width = FieldWidth;
        taskTypeField.RegisterValueChangedCallback(evt => TaskType = (TaskType)evt.newValue);
        extensionContainer.Add(CreateLabeledRow("Task Type", taskTypeField));

        IntegerField giverField = new IntegerField { value = GiverNpcID };
        giverField.style.width = FieldWidth;
        giverField.RegisterValueChangedCallback(evt => GiverNpcID = evt.newValue);
        extensionContainer.Add(CreateLabeledRow("Giver Npc ID", giverField));

        IntegerField prerequisiteField = new IntegerField { value = PrerequisiteTaskID };
        prerequisiteField.style.width = FieldWidth;
        prerequisiteField.RegisterValueChangedCallback(evt => PrerequisiteTaskID = evt.newValue);
        extensionContainer.Add(CreateLabeledRow("Prerequisite ID", prerequisiteField));

        RefreshExpandedState();
        RefreshPorts();
        ApplyExpandedState();
        RegisterCollapseToggle();
    }

    public TaskGraphNodeSaveData BuildSaveData()
    {
        return new TaskGraphNodeSaveData
        {
            nodeType = TaskGraphNodeType.TaskRoot,
            id = ID,
            position = GetPosition().position,
            nextNodeID = GetNextNodeID(),
            taskID = TaskID,
            taskName = TaskName,
            taskDescription = TaskDescription,
            taskType = TaskType,
            giverNpcID = GiverNpcID,
            prerequisiteTaskID = PrerequisiteTaskID
        };
    }

    private void UpdateNodeTitle()
    {
        title = $"Task {TaskID}: {(string.IsNullOrWhiteSpace(TaskName) ? "Untitled" : TaskName)}";
    }

    private void ApplyNodeStyle()
    {
        titleContainer.style.backgroundColor = new Color(0.2f, 0.39f, 0.27f, 0.98f);
        titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainContainer.style.backgroundColor = new Color(0.11f, 0.14f, 0.12f, 0.98f);
        mainContainer.style.borderBottomLeftRadius = 10f;
        mainContainer.style.borderBottomRightRadius = 10f;
        mainContainer.style.borderTopLeftRadius = 10f;
        mainContainer.style.borderTopRightRadius = 10f;
        mainContainer.style.borderLeftWidth = 1f;
        mainContainer.style.borderRightWidth = 1f;
        mainContainer.style.borderTopWidth = 1f;
        mainContainer.style.borderBottomWidth = 1f;
        mainContainer.style.borderLeftColor = new Color(0.36f, 0.62f, 0.42f, 0.95f);
        mainContainer.style.borderRightColor = new Color(0.36f, 0.62f, 0.42f, 0.95f);
        mainContainer.style.borderTopColor = new Color(0.36f, 0.62f, 0.42f, 0.95f);
        mainContainer.style.borderBottomColor = new Color(0.36f, 0.62f, 0.42f, 0.95f);

        outputContainer.style.backgroundColor = new Color(0.13f, 0.18f, 0.15f, 0.9f);
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

    private string GetNextNodeID()
    {
        if (OutputPort == null || !OutputPort.connected)
        {
            return string.Empty;
        }

        Edge edge = OutputPort.connections.FirstOrDefault();
        if (edge?.input?.node is TaskStepNode nextStepNode)
        {
            return nextStepNode.ID;
        }

        if (edge?.input?.node is TaskRewardNode nextRewardNode)
        {
            return nextRewardNode.ID;
        }

        if (edge?.input?.node is TaskEndNode endNode)
        {
            return endNode.ID;
        }

        return string.Empty;
    }

    private VisualElement CreateLabeledRow(string labelText, VisualElement field)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.alignSelf = Align.Center;
        row.style.width = RowWidth;
        row.style.minWidth = RowWidth;
        row.style.maxWidth = RowWidth;
        row.style.marginTop = 4f;
        row.style.marginBottom = 2f;

        Label label = CreateRowLabel(labelText);
        row.Add(label);

        field.style.minWidth = FieldWidth;
        field.style.maxWidth = FieldWidth;
        row.Add(field);

        return row;
    }

    private Label CreateRowLabel(string labelText)
    {
        Label label = new Label(labelText);
        label.style.width = LabelWidth;
        label.style.minWidth = LabelWidth;
        label.style.maxWidth = LabelWidth;
        label.style.color = new Color(0.75f, 0.8f, 0.86f);
        return label;
    }
}
