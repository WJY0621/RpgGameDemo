using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DLEventNode : DLNode
{
    private const float NodeWidth = 200f;

    public override void Initialize(string nodeName, DialogueGraphView graphView, Vector2 position)
    {
        base.Initialize(nodeName, graphView, position);

        DialogueType = DialogueType.Event;
        Text = string.Empty;
        style.width = NodeWidth;
        style.minWidth = NodeWidth;

        DLChoiceSaveData choiceData = new DLChoiceSaveData()
        {
            Text = "Next"
        };

        Choices.Add(choiceData);
    }

    public override void Draw()
    {
        DrawTitle();
        DrawInput();
        DrawEventData();
        DrawOutput();

        RefreshExpandedState();
        RefreshPorts();
    }

    private void DrawTitle()
    {
        TextField dialogueNameTextField = DLElementUtility.CreateTextField(DialogueName, null, callback =>
        {
            TextField target = (TextField)callback.target;
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
    }

    private void DrawInput()
    {
        Port inputPort = this.CreatePort("Input", Orientation.Horizontal, Direction.Input, Port.Capacity.Multi);
        inputPort.portColor = new Color(0.35f, 0.64f, 0.88f, 1f);
        inputContainer.Add(inputPort);
    }

    private void DrawEventData()
    {
        VisualElement customDataContainer = new VisualElement();
        customDataContainer.AddToClassList("ds-node__custom-data-container");
        customDataContainer.style.minWidth = 0f;

        Foldout eventFoldout = DLElementUtility.CreateFoldout("Event");
        eventFoldout.style.minWidth = 0f;

        EnumField eventTypeField = new EnumField("Event Type", EventType);
        ApplyCompactEventField(eventTypeField);
        eventTypeField.RegisterValueChangedCallback(callback =>
        {
            EventType = (DialogueEventType)callback.newValue;
        });
        eventFoldout.Add(eventTypeField);

        VisualElement taskContainer = new VisualElement();
        taskContainer.style.minWidth = 0f;
        ObjectField taskDataSOField = new ObjectField("Task Data")
        {
            objectType = typeof(TaskDataSO),
            allowSceneObjects = false,
            value = TaskDataSO
        };
        ApplyCompactEventField(taskDataSOField);
        taskDataSOField.RegisterValueChangedCallback(callback =>
        {
            TaskDataSO = callback.newValue as TaskDataSO;
        });
        taskContainer.Add(taskDataSOField);

        IntegerField taskIDField = new IntegerField("Task ID")
        {
            value = TaskID
        };
        ApplyCompactEventField(taskIDField);
        taskIDField.RegisterValueChangedCallback(callback =>
        {
            TaskID = callback.newValue;
        });
        taskContainer.Add(taskIDField);

        IntegerField taskStepIDField = new IntegerField("Task Step ID")
        {
            value = TaskStepID
        };
        ApplyCompactEventField(taskStepIDField);
        taskStepIDField.RegisterValueChangedCallback(callback =>
        {
            TaskStepID = callback.newValue;
        });
        taskContainer.Add(taskStepIDField);
        eventFoldout.Add(taskContainer);

        VisualElement customEventContainer = new VisualElement();
        customEventContainer.style.minWidth = 0f;
        TextField customEventNameField = DLElementUtility.CreateTextField(CustomEventName, "Event Name", callback =>
        {
            CustomEventName = callback.newValue;
        });
        ApplyCompactEventField(customEventNameField);
        customEventContainer.Add(customEventNameField);
        eventFoldout.Add(customEventContainer);

        void RefreshContainers()
        {
            bool useTask = EventType == DialogueEventType.AcceptTask ||
                EventType == DialogueEventType.CompleteDialogueTask;
            bool useTaskStep = EventType == DialogueEventType.CompleteDialogueTask;
            bool useCustomEvent = EventType == DialogueEventType.BroadcastEvent;

            taskContainer.style.display = useTask ? DisplayStyle.Flex : DisplayStyle.None;
            taskStepIDField.style.display = useTaskStep ? DisplayStyle.Flex : DisplayStyle.None;
            customEventContainer.style.display = useCustomEvent ? DisplayStyle.Flex : DisplayStyle.None;
        }

        eventTypeField.RegisterValueChangedCallback(_ => RefreshContainers());
        RefreshContainers();

        customDataContainer.Add(eventFoldout);
        extensionContainer.Add(customDataContainer);
    }

    private static void ApplyCompactEventField<TValue>(BaseField<TValue> field)
    {
        const float labelWidth = 70f;

        field.style.flexDirection = FlexDirection.Row;
        field.style.alignItems = Align.Center;
        field.style.minWidth = 0f;
        field.style.marginLeft = 0f;
        field.style.marginRight = 0f;
        field.style.marginTop = 2f;
        field.style.marginBottom = 2f;
        field.labelElement.style.width = labelWidth;
        field.labelElement.style.minWidth = labelWidth;
        field.labelElement.style.maxWidth = labelWidth;
        field.labelElement.style.marginRight = 4f;

        VisualElement input = field.Q(className: BaseField<TValue>.inputUssClassName);
        if (input == null)
        {
            return;
        }

        input.style.flexGrow = 1f;
        input.style.minWidth = 0f;
    }

    private void DrawOutput()
    {
        foreach (DLChoiceSaveData choice in Choices)
        {
            Port choicePort = this.CreatePort(choice.Text);
            choicePort.portColor = new Color(0.35f, 0.64f, 0.88f, 1f);
            choicePort.userData = choice;
            outputContainer.Add(choicePort);
        }
    }
}
