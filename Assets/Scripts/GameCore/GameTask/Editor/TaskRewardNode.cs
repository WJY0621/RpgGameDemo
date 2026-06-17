using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskRewardNode : Node
{
    private const float NodeWidth = 176f;
    private const float RewardFoldoutWidth = 136f;
    private const float RewardRowWidth = 112f;
    private const float RewardLabelWidth = 64f;
    private const float RewardFieldWidth = 52f;
    private const float GoldFieldWidth = 58f;

    public string ID { get; set; }
    public int GoldAmount { get; set; }
    public List<TaskRewardData> RewardList { get; set; }

    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }

    private VisualElement rewardListContainer;
    private bool collapseHookRegistered;

    public void Initialize(Vector2 position)
    {
        ID = Guid.NewGuid().ToString();
        GoldAmount = 0;
        RewardList = new List<TaskRewardData> { CreateDefaultReward() };
        SetPosition(new Rect(position, new Vector2(NodeWidth, 220f)));
    }

    public void ApplySaveData(TaskGraphNodeSaveData saveData)
    {
        ID = saveData.id;
        GoldAmount = Mathf.Max(0, saveData.goldAmount);
        RewardList = CloneRewards(saveData.rewardList);
        if (RewardList.Count == 0)
        {
            RewardList.Add(CreateDefaultReward());
        }

        SetPosition(new Rect(saveData.position, new Vector2(NodeWidth, 220f)));
    }

    public void Draw()
    {
        title = "Task Reward";
        ApplyNodeStyle();

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
        InputPort.portName = "Prev";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        OutputPort.portName = "Next";
        outputContainer.Add(OutputPort);

        TextField goldAmountField = new TextField { value = GoldAmount.ToString() };
        goldAmountField.RegisterValueChangedCallback(evt =>
        {
            if (int.TryParse(evt.newValue, out int parsedValue))
            {
                GoldAmount = Mathf.Max(0, parsedValue);
            }
        });
        extensionContainer.Add(CreateGoldFieldRow("Gold Amount", goldAmountField));

        Button addRewardButton = new Button(() =>
        {
            RewardList.Add(CreateDefaultReward());
            RedrawRewards();
        })
        {
            text = "Add Reward"
        };
        addRewardButton.style.marginTop = 8f;
        addRewardButton.style.height = 26f;
        addRewardButton.style.backgroundColor = new Color(0.72f, 0.47f, 0.21f, 0.95f);
        addRewardButton.style.color = new Color(0.99f, 0.97f, 0.93f);
        addRewardButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        extensionContainer.Add(addRewardButton);

        rewardListContainer = new VisualElement();
        rewardListContainer.style.marginTop = 6f;
        extensionContainer.Add(rewardListContainer);

        RedrawRewards();
        RefreshExpandedState();
        RefreshPorts();
        ApplyExpandedState();
        RegisterCollapseToggle();
    }

    public TaskGraphNodeSaveData BuildSaveData()
    {
        return new TaskGraphNodeSaveData
        {
            nodeType = TaskGraphNodeType.Reward,
            id = ID,
            position = GetPosition().position,
            nextNodeID = GetNextNodeID(),
            goldAmount = GoldAmount,
            rewardList = CloneRewards(RewardList)
        };
    }

    private void ApplyNodeStyle()
    {
        titleContainer.style.backgroundColor = new Color(0.46f, 0.31f, 0.13f, 0.96f);
        titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainContainer.style.backgroundColor = new Color(0.16f, 0.13f, 0.1f, 0.98f);
        mainContainer.style.borderBottomLeftRadius = 10f;
        mainContainer.style.borderBottomRightRadius = 10f;
        mainContainer.style.borderTopLeftRadius = 10f;
        mainContainer.style.borderTopRightRadius = 10f;
        mainContainer.style.borderLeftWidth = 1f;
        mainContainer.style.borderRightWidth = 1f;
        mainContainer.style.borderTopWidth = 1f;
        mainContainer.style.borderBottomWidth = 1f;
        mainContainer.style.borderLeftColor = new Color(0.82f, 0.59f, 0.3f, 0.95f);
        mainContainer.style.borderRightColor = new Color(0.82f, 0.59f, 0.3f, 0.95f);
        mainContainer.style.borderTopColor = new Color(0.82f, 0.59f, 0.3f, 0.95f);
        mainContainer.style.borderBottomColor = new Color(0.82f, 0.59f, 0.3f, 0.95f);

        inputContainer.style.backgroundColor = new Color(0.17f, 0.14f, 0.11f, 0.9f);
        outputContainer.style.backgroundColor = new Color(0.17f, 0.14f, 0.11f, 0.9f);
        inputContainer.style.justifyContent = Justify.SpaceBetween;
        outputContainer.style.justifyContent = Justify.SpaceBetween;
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

    private void RedrawRewards()
    {
        if (rewardListContainer == null)
        {
            return;
        }

        rewardListContainer.Clear();

        for (int i = 0; i < RewardList.Count; i++)
        {
            int index = i;
            TaskRewardData reward = RewardList[index];
            if (reward == null)
            {
                reward = CreateDefaultReward();
                RewardList[index] = reward;
            }

            Foldout foldout = new Foldout
            {
                text = $"Reward {index + 1}",
                value = false
            };
            foldout.style.alignSelf = Align.Center;
            foldout.style.width = RewardFoldoutWidth;
            foldout.style.minWidth = RewardFoldoutWidth;
            foldout.style.maxWidth = RewardFoldoutWidth;
            foldout.style.marginTop = 8f;
            foldout.style.paddingLeft = 6f;
            foldout.style.paddingRight = 6f;
            foldout.style.paddingTop = 4f;
            foldout.style.paddingBottom = 6f;
            foldout.style.backgroundColor = new Color(0.2f, 0.16f, 0.11f, 0.95f);
            foldout.style.borderBottomLeftRadius = 8f;
            foldout.style.borderBottomRightRadius = 8f;
            foldout.style.borderTopLeftRadius = 8f;
            foldout.style.borderTopRightRadius = 8f;
            foldout.style.borderLeftWidth = 1f;
            foldout.style.borderRightWidth = 1f;
            foldout.style.borderTopWidth = 1f;
            foldout.style.borderBottomWidth = 1f;
            foldout.style.borderLeftColor = new Color(0.56f, 0.41f, 0.23f, 0.9f);
            foldout.style.borderRightColor = new Color(0.56f, 0.41f, 0.23f, 0.9f);
            foldout.style.borderTopColor = new Color(0.56f, 0.41f, 0.23f, 0.9f);
            foldout.style.borderBottomColor = new Color(0.56f, 0.41f, 0.23f, 0.9f);

            IntegerField itemIDField = new IntegerField { value = reward.itemID };
            itemIDField.RegisterValueChangedCallback(evt => reward.itemID = evt.newValue);
            foldout.Add(CreateRewardFieldRow("Item ID", itemIDField));

            IntegerField amountField = new IntegerField { value = Mathf.Max(1, reward.amount) };
            amountField.RegisterValueChangedCallback(evt => reward.amount = Mathf.Max(1, evt.newValue));
            foldout.Add(CreateRewardFieldRow("Amount", amountField));

            Button removeButton = new Button(() =>
            {
                RewardList.RemoveAt(index);
                if (RewardList.Count == 0)
                {
                    RewardList.Add(CreateDefaultReward());
                }

                RedrawRewards();
            })
            {
                text = "Remove Reward"
            };
            removeButton.style.alignSelf = Align.Center;
            removeButton.style.marginTop = 6f;
            removeButton.style.height = 24f;
            removeButton.style.width = RewardRowWidth;
            removeButton.style.backgroundColor = new Color(0.45f, 0.22f, 0.22f, 0.9f);
            removeButton.style.color = new Color(1f, 0.94f, 0.94f);
            foldout.Add(removeButton);

            rewardListContainer.Add(foldout);
        }
    }

    private static VisualElement CreateRewardFieldRow(string labelText, VisualElement field)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignSelf = Align.Center;
        row.style.width = RewardRowWidth;
        row.style.minWidth = RewardRowWidth;
        row.style.maxWidth = RewardRowWidth;
        row.style.marginTop = 4f;
        row.style.marginBottom = 2f;

        Label label = new Label(labelText);
        label.style.width = RewardLabelWidth;
        label.style.minWidth = RewardLabelWidth;
        label.style.maxWidth = RewardLabelWidth;
        label.style.color = new Color(0.82f, 0.83f, 0.86f);
        row.Add(label);

        field.style.width = RewardFieldWidth;
        field.style.minWidth = RewardFieldWidth;
        field.style.maxWidth = RewardFieldWidth;
        row.Add(field);

        return row;
    }

    private static VisualElement CreateGoldFieldRow(string labelText, VisualElement field)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignSelf = Align.Center;
        row.style.width = RewardFoldoutWidth;
        row.style.minWidth = RewardFoldoutWidth;
        row.style.maxWidth = RewardFoldoutWidth;
        row.style.marginTop = 6f;
        row.style.marginBottom = 2f;

        Label label = new Label(labelText);
        label.style.width = RewardLabelWidth;
        label.style.minWidth = RewardLabelWidth;
        label.style.maxWidth = RewardLabelWidth;
        label.style.color = new Color(0.82f, 0.83f, 0.86f);
        row.Add(label);

        field.style.width = GoldFieldWidth;
        field.style.minWidth = GoldFieldWidth;
        field.style.maxWidth = GoldFieldWidth;
        row.Add(field);

        return row;
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

    private static TaskRewardData CreateDefaultReward()
    {
        return new TaskRewardData
        {
            rewardType = TaskRewardType.Item,
            itemID = -1,
            amount = 1
        };
    }

    private static List<TaskRewardData> CloneRewards(List<TaskRewardData> source)
    {
        List<TaskRewardData> result = new List<TaskRewardData>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            TaskRewardData reward = source[i];
            if (reward == null)
            {
                continue;
            }

            result.Add(new TaskRewardData
            {
                rewardType = reward.rewardType,
                amount = reward.amount,
                itemID = reward.itemID,
                equipmentID = reward.equipmentID,
                unlockTaskID = reward.unlockTaskID,
                customRewardID = reward.customRewardID
            });
        }

        return result;
    }
}
