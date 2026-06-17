using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskStepNode : Node
{
    private const float ExpandedStepNodeWidth = 236f;
    private const float CollapsedStepNodeWidth = 196f;
    private const float CollapsedObjectiveFoldoutWidth = 172f;
    private const float ExpandedObjectiveFoldoutWidth = 208f;
    private const float CollapsedObjectiveRowWidth = 148f;
    private const float ExpandedObjectiveRowWidth = 184f;
    private const float CollapsedObjectiveLabelWidth = 84f;
    private const float ExpandedObjectiveLabelWidth = 100f;
    private const float CollapsedObjectiveFieldWidth = 56f;
    private const float ExpandedObjectiveFieldWidth = 76f;

    public string ID { get; set; }
    public int StepID { get; set; }
    public string StepName { get; set; }
    public string StepDescription { get; set; }
    public List<TaskObjectiveData> Objectives { get; set; }

    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }

    private VisualElement objectiveListContainer;
    private bool collapseHookRegistered;

    public void Initialize(Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        StepID = 1;
        StepName = "New Step";
        StepDescription = string.Empty;
        Objectives = new List<TaskObjectiveData> { CreateDefaultObjective() };
        SetPosition(new Rect(position, new Vector2(CollapsedStepNodeWidth, 280f)));
        ApplyNodeWidth(CollapsedStepNodeWidth);
    }

    public void ApplySaveData(TaskGraphNodeSaveData saveData)
    {
        ID = saveData.id;
        StepID = saveData.stepID > 0 ? saveData.stepID : 1;
        StepName = saveData.stepName;
        StepDescription = saveData.stepDescription;
        Objectives = saveData.objectiveList != null && saveData.objectiveList.Count > 0
            ? CloneObjectives(saveData.objectiveList)
            : new List<TaskObjectiveData> { CreateDefaultObjective() };
        SetPosition(new Rect(saveData.position, new Vector2(CollapsedStepNodeWidth, 280f)));
        ApplyNodeWidth(CollapsedStepNodeWidth);
    }

    public void Draw()
    {
        title = "Task Step";
        ApplyNodeStyle();

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
        InputPort.portName = "Prev";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        OutputPort.portName = "Next";
        outputContainer.Add(OutputPort);

        TextField stepIDField = new TextField("Step ID") { value = StepID.ToString() };
        ApplyFieldStyle(stepIDField);
        stepIDField.RegisterValueChangedCallback(evt =>
        {
            if (int.TryParse(evt.newValue, out int parsedValue))
            {
                StepID = Mathf.Max(1, parsedValue);
            }
        });
        extensionContainer.Add(stepIDField);

        TextField stepNameField = CreateEditableDescriptionField(StepName);
        stepNameField.style.minHeight = 58f;
        stepNameField.style.maxHeight = 58f;
        stepNameField.RegisterValueChangedCallback(evt => StepName = evt.newValue);
        extensionContainer.Add(CreateDescriptionSection("Step Name", stepNameField));

        Button addObjectiveButton = new Button(() =>
        {
            Objectives.Add(CreateDefaultObjective());
            RedrawObjectives();
        })
        {
            text = "Add Objective"
        };
        ApplyPrimaryButtonStyle(addObjectiveButton);
        extensionContainer.Add(addObjectiveButton);

        objectiveListContainer = new VisualElement();
        objectiveListContainer.style.marginTop = 6f;
        extensionContainer.Add(objectiveListContainer);
        RedrawObjectives();
        RefreshExpandedState();
        RefreshPorts();
        ApplyExpandedState();
        RegisterCollapseToggle();
    }

    public TaskGraphNodeSaveData BuildSaveData()
    {
        return new TaskGraphNodeSaveData
        {
            nodeType = TaskGraphNodeType.Step,
            id = ID,
            position = GetPosition().position,
            nextNodeID = GetNextNodeID(),
            stepID = StepID,
            stepName = StepName,
            stepDescription = StepDescription,
            objectiveList = CloneObjectives(Objectives)
        };
    }

    private void ApplyNodeStyle()
    {
        titleContainer.style.backgroundColor = new Color(0.18f, 0.28f, 0.45f, 0.95f);
        titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainContainer.style.backgroundColor = new Color(0.11f, 0.12f, 0.16f, 0.98f);
        mainContainer.style.borderBottomLeftRadius = 10f;
        mainContainer.style.borderBottomRightRadius = 10f;
        mainContainer.style.borderTopLeftRadius = 10f;
        mainContainer.style.borderTopRightRadius = 10f;
        mainContainer.style.borderLeftWidth = 1f;
        mainContainer.style.borderRightWidth = 1f;
        mainContainer.style.borderTopWidth = 1f;
        mainContainer.style.borderBottomWidth = 1f;
        mainContainer.style.borderLeftColor = new Color(0.26f, 0.45f, 0.73f, 0.9f);
        mainContainer.style.borderRightColor = new Color(0.26f, 0.45f, 0.73f, 0.9f);
        mainContainer.style.borderTopColor = new Color(0.26f, 0.45f, 0.73f, 0.9f);
        mainContainer.style.borderBottomColor = new Color(0.26f, 0.45f, 0.73f, 0.9f);

        inputContainer.style.backgroundColor = new Color(0.13f, 0.14f, 0.19f, 0.9f);
        outputContainer.style.backgroundColor = new Color(0.13f, 0.14f, 0.19f, 0.9f);
        inputContainer.style.justifyContent = Justify.SpaceBetween;
        outputContainer.style.justifyContent = Justify.SpaceBetween;
        extensionContainer.style.paddingLeft = 8f;
        extensionContainer.style.paddingRight = 8f;
        extensionContainer.style.paddingBottom = 8f;
    }

    private void ApplyNodeWidth(float width)
    {
        float containerWidth = width - 6f;

        style.width = width;
        style.minWidth = width;
        style.maxWidth = width;
        style.paddingRight = 0f;
        style.marginRight = 0f;

        mainContainer.style.width = containerWidth;
        mainContainer.style.minWidth = containerWidth;
        mainContainer.style.maxWidth = containerWidth;
        mainContainer.style.paddingRight = 0f;
        mainContainer.style.marginRight = 0f;

        titleContainer.style.width = containerWidth;
        titleContainer.style.minWidth = containerWidth;
        titleContainer.style.maxWidth = containerWidth;

        extensionContainer.style.width = containerWidth;
        extensionContainer.style.minWidth = containerWidth;
        extensionContainer.style.maxWidth = containerWidth;
    }

    private static void ApplyFieldStyle(VisualElement field)
    {
        field.style.marginTop = 4f;
        field.style.marginBottom = 2f;
    }

    private static void ApplyPrimaryButtonStyle(Button button)
    {
        button.style.marginTop = 8f;
        button.style.height = 26f;
        button.style.backgroundColor = new Color(0.23f, 0.39f, 0.66f, 0.95f);
        button.style.color = new Color(0.95f, 0.97f, 1f);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
    }

    private static void ApplySecondaryButtonStyle(Button button)
    {
        button.style.marginTop = 6f;
        button.style.height = 24f;
        button.style.backgroundColor = new Color(0.45f, 0.22f, 0.22f, 0.9f);
        button.style.color = new Color(1f, 0.94f, 0.94f);
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

    private VisualElement CreateObjectiveFieldRow(string labelText, VisualElement field)
    {
        VisualElement row = new VisualElement();
        row.name = "objective-row";
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignSelf = Align.Center;
        row.style.marginTop = 4f;
        row.style.marginBottom = 2f;

        Label label = new Label(labelText);
        label.name = "objective-label";
        label.style.color = new Color(0.75f, 0.8f, 0.86f);
        row.Add(label);

        field.name = "objective-field";
        row.Add(field);

        ApplyObjectiveRowLayout(row, label, field, false);
        return row;
    }

    private static VisualElement CreateDescriptionSection(string labelText, VisualElement field)
    {
        VisualElement container = new VisualElement();
        container.style.marginTop = 4f;
        container.style.marginBottom = 2f;

        Label label = new Label(labelText);
        label.style.color = new Color(0.75f, 0.8f, 0.86f);
        label.style.marginBottom = 2f;
        container.Add(label);

        container.Add(field);

        return container;
    }

    private static TextField CreateEditableDescriptionField(string value)
    {
        TextField textField = DLElementUtility.CreateTextArea(value, null, null);
        textField.style.marginTop = 0f;
        textField.style.marginBottom = 0f;
        textField.AddClasses("ds-node__textfield", "ds-node__quote-textfield");
        return textField;
    }

    private static TextField CreateReadonlyDescriptionField()
    {
        TextField textField = DLElementUtility.CreateTextArea(string.Empty, null, null);
        textField.style.minHeight = 58f;
        textField.style.maxHeight = 58f;
        textField.style.marginTop = 0f;
        textField.style.marginBottom = 0f;
        textField.AddClasses("ds-node__textfield", "ds-node__quote-textfield");
        textField.SetEnabled(false);
        return textField;
    }

    private void RedrawObjectives()
    {
        if (objectiveListContainer == null)
        {
            return;
        }

        objectiveListContainer.Clear();

        for (int i = 0; i < Objectives.Count; i++)
        {
            int index = i;
            TaskObjectiveData objective = Objectives[index];
            if (objective == null)
            {
                objective = CreateDefaultObjective();
                Objectives[index] = objective;
            }

            Foldout foldout = new Foldout
            {
                text = $"Objective {index + 1}",
                value = false
            };
            foldout.name = "objective-foldout";
            foldout.style.alignSelf = Align.Center;
            foldout.style.marginTop = 8f;
            foldout.style.paddingLeft = 6f;
            foldout.style.paddingRight = 6f;
            foldout.style.paddingTop = 4f;
            foldout.style.paddingBottom = 6f;
            foldout.style.backgroundColor = new Color(0.15f, 0.17f, 0.22f, 0.95f);
            foldout.style.borderBottomLeftRadius = 8f;
            foldout.style.borderBottomRightRadius = 8f;
            foldout.style.borderTopLeftRadius = 8f;
            foldout.style.borderTopRightRadius = 8f;
            foldout.style.borderLeftWidth = 1f;
            foldout.style.borderRightWidth = 1f;
            foldout.style.borderTopWidth = 1f;
            foldout.style.borderBottomWidth = 1f;
            foldout.style.borderLeftColor = new Color(0.24f, 0.33f, 0.46f, 0.9f);
            foldout.style.borderRightColor = new Color(0.24f, 0.33f, 0.46f, 0.9f);
            foldout.style.borderTopColor = new Color(0.24f, 0.33f, 0.46f, 0.9f);
            foldout.style.borderBottomColor = new Color(0.24f, 0.33f, 0.46f, 0.9f);

            EnumField typeField = new EnumField(objective.objectiveType);
            foldout.Add(CreateObjectiveFieldRow("Objective Type", typeField));

            IntegerField targetCountField = new IntegerField { value = objective.targetCount };
            targetCountField.RegisterValueChangedCallback(evt =>
            {
                objective.targetCount = Mathf.Max(1, evt.newValue);
                UpdateGeneratedStepDescription();
            });
            foldout.Add(CreateObjectiveFieldRow("Target Count", targetCountField));

            VisualElement targetFieldContainer = new VisualElement();
            targetFieldContainer.style.marginTop = 4f;
            foldout.Add(targetFieldContainer);

            void RedrawTargetFields()
            {
                targetFieldContainer.Clear();

                switch (objective.objectiveType)
                {
                    case TaskStepType.Dialogue:
                    {
                        IntegerField targetNpcField = new IntegerField { value = objective.targetNpcID };
                        targetNpcField.RegisterValueChangedCallback(evt =>
                        {
                            objective.targetNpcID = evt.newValue;
                            UpdateGeneratedStepDescription();
                        });
                        targetFieldContainer.Add(CreateObjectiveFieldRow("Target Npc ID", targetNpcField));
                        break;
                    }
                    case TaskStepType.Kill:
                    {
                        IntegerField targetMonsterField = new IntegerField { value = objective.targetMonsterID };
                        targetMonsterField.RegisterValueChangedCallback(evt =>
                        {
                            objective.targetMonsterID = evt.newValue;
                            UpdateGeneratedStepDescription();
                        });
                        targetFieldContainer.Add(CreateObjectiveFieldRow("Target Monster ID", targetMonsterField));
                        break;
                    }
                    case TaskStepType.Collect:
                    {
                        IntegerField targetItemField = new IntegerField { value = objective.targetItemID };
                        targetItemField.RegisterValueChangedCallback(evt =>
                        {
                            objective.targetItemID = evt.newValue;
                            UpdateGeneratedStepDescription();
                        });
                        targetFieldContainer.Add(CreateObjectiveFieldRow("Target Item ID", targetItemField));
                        break;
                    }
                }
            }

            typeField.RegisterValueChangedCallback(evt =>
            {
                objective.objectiveType = (TaskStepType)evt.newValue;
                RedrawTargetFields();
                UpdateGeneratedStepDescription();
            });
            foldout.RegisterValueChangedCallback(_ =>
            {
                bool anyExpanded = IsAnyObjectiveExpanded();
                UpdateNodeWidth();
                UpdateObjectiveLayout(anyExpanded);
            });
            RedrawTargetFields();

            Button removeButton = new Button(() =>
            {
                Objectives.RemoveAt(index);
                if (Objectives.Count == 0)
                {
                    Objectives.Add(CreateDefaultObjective());
                }
                RedrawObjectives();
            })
            {
                text = "Remove Objective"
            };
            removeButton.name = "objective-remove-button";
            ApplySecondaryButtonStyle(removeButton);
            removeButton.style.alignSelf = Align.Center;
            foldout.Add(removeButton);

            objectiveListContainer.Add(foldout);
        }

        UpdateObjectiveLayout(IsAnyObjectiveExpanded());
        RefreshExpandedState();
    }

    private bool IsAnyObjectiveExpanded()
    {
        if (objectiveListContainer == null)
        {
            return false;
        }

        for (int i = 0; i < objectiveListContainer.childCount; i++)
        {
            if (objectiveListContainer[i] is Foldout foldout && foldout.value)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateObjectiveLayout(bool expanded)
    {
        if (objectiveListContainer == null)
        {
            return;
        }

        float foldoutWidth = expanded ? ExpandedObjectiveFoldoutWidth : CollapsedObjectiveFoldoutWidth;
        float rowWidth = expanded ? ExpandedObjectiveRowWidth : CollapsedObjectiveRowWidth;
        float labelWidth = expanded ? ExpandedObjectiveLabelWidth : CollapsedObjectiveLabelWidth;
        float fieldWidth = expanded ? ExpandedObjectiveFieldWidth : CollapsedObjectiveFieldWidth;

        for (int i = 0; i < objectiveListContainer.childCount; i++)
        {
            if (objectiveListContainer[i] is not Foldout foldout)
            {
                continue;
            }

            foldout.style.width = foldoutWidth;
            foldout.style.minWidth = foldoutWidth;
            foldout.style.maxWidth = foldoutWidth;

            foreach (VisualElement child in foldout.Children())
            {
                if (child.name == "objective-row" && child.childCount >= 2)
                {
                    ApplyObjectiveRowLayout(child, child[0] as Label, child[1], expanded);
                }
                else if (child.name == "objective-remove-button")
                {
                    child.style.width = rowWidth;
                    child.style.minWidth = rowWidth;
                    child.style.maxWidth = rowWidth;
                }
                else
                {
                    for (int j = 0; j < child.childCount; j++)
                    {
                        VisualElement grandChild = child[j];
                        if (grandChild.name == "objective-row" && grandChild.childCount >= 2)
                        {
                            ApplyObjectiveRowLayout(grandChild, grandChild[0] as Label, grandChild[1], expanded);
                        }
                    }
                }
            }
        }
    }

    private static void ApplyObjectiveRowLayout(VisualElement row, Label label, VisualElement field, bool expanded)
    {
        float rowWidth = expanded ? ExpandedObjectiveRowWidth : CollapsedObjectiveRowWidth;
        float labelWidth = expanded ? ExpandedObjectiveLabelWidth : CollapsedObjectiveLabelWidth;
        float fieldWidth = expanded ? ExpandedObjectiveFieldWidth : CollapsedObjectiveFieldWidth;

        row.style.width = rowWidth;
        row.style.minWidth = rowWidth;
        row.style.maxWidth = rowWidth;

        if (label != null)
        {
            label.style.width = labelWidth;
            label.style.minWidth = labelWidth;
            label.style.maxWidth = labelWidth;
        }

        if (field != null)
        {
            field.style.width = fieldWidth;
            field.style.minWidth = fieldWidth;
            field.style.maxWidth = fieldWidth;
        }
    }

    private void UpdateNodeWidth()
    {
        bool anyObjectiveExpanded = IsAnyObjectiveExpanded();

        float targetWidth = anyObjectiveExpanded ? ExpandedStepNodeWidth : CollapsedStepNodeWidth;
        Rect currentRect = GetPosition();
        ApplyNodeWidth(targetWidth);
        UpdateObjectiveLayout(anyObjectiveExpanded);

        if (!Mathf.Approximately(currentRect.width, targetWidth))
        {
            SetPosition(new Rect(currentRect.x, currentRect.y, targetWidth, currentRect.height));
        }
    }

    private void UpdateGeneratedStepDescription()
    {
        StepDescription = BuildGeneratedStepDescription();
    }

    private string BuildGeneratedStepDescription()
    {
        if (Objectives == null || Objectives.Count == 0)
        {
            return string.Empty;
        }

        List<string> lines = new List<string>();
        foreach (TaskObjectiveData objective in Objectives)
        {
            if (objective == null)
            {
                continue;
            }

            switch (objective.objectiveType)
            {
                case TaskStepType.Dialogue:
                    lines.Add($"请和{objective.targetNpcID}对话");
                    break;
                case TaskStepType.Kill:
                    lines.Add($"请消灭{objective.targetMonsterID}(0/{Mathf.Max(1, objective.targetCount)})");
                    break;
                case TaskStepType.Collect:
                    lines.Add($"请收集{objective.targetItemID}(0/{Mathf.Max(1, objective.targetCount)})");
                    break;
            }
        }

        return string.Join("\n", lines);
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

    private static TaskObjectiveData CreateDefaultObjective()
    {
        return new TaskObjectiveData
        {
            objectiveID = 1,
            objectiveType = TaskStepType.Dialogue,
            targetCount = 1,
            targetNpcID = -1,
            targetMonsterID = -1,
            targetItemID = -1,
            targetDialogueGroup = "Default"
        };
    }

    private static List<TaskObjectiveData> CloneObjectives(List<TaskObjectiveData> source)
    {
        List<TaskObjectiveData> result = new List<TaskObjectiveData>();
        if (source == null)
        {
            return result;
        }

        foreach (TaskObjectiveData objective in source)
        {
            if (objective == null)
            {
                continue;
            }

            result.Add(new TaskObjectiveData
            {
                objectiveID = objective.objectiveID,
                objectiveType = objective.objectiveType,
                targetCount = objective.targetCount,
                targetNpcID = objective.targetNpcID,
                targetMonsterID = objective.targetMonsterID,
                targetItemID = objective.targetItemID,
                targetAreaID = objective.targetAreaID,
                targetDialogueGroup = objective.targetDialogueGroup,
                customTargetID = objective.customTargetID
            });
        }

        return result;
    }
}
