using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskGraphView : GraphView
{
    private Vector2 lastMouseGraphPosition = new Vector2(200f, 200f);

    public TaskGraphView()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        GridBackground grid = new GridBackground();
        grid.StretchToParentSize();
        Insert(0, grid);

        this.AddStyleSheets(
            "DialogueSystem/DialogueGraphViewStyle.uss",
            "DialogueSystem/DLNodeStyle.uss"
        );

        RegisterCallback<MouseMoveEvent>(evt => UpdateMouseGraphPosition(evt.localMousePosition));
        RegisterCallback<MouseDownEvent>(evt => UpdateMouseGraphPosition(evt.localMousePosition));

        graphViewChanged += OnGraphViewChanged;
        this.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            UpdateMouseGraphPosition(evt.localMousePosition);

            evt.menu.AppendAction("Add Task Node", _ =>
            {
                AddElement(CreateTaskRootNode(lastMouseGraphPosition));
            });

            evt.menu.AppendAction("Add Step Node", _ =>
            {
                AddElement(CreateStepNode(lastMouseGraphPosition));
            });

            evt.menu.AppendAction("Add Reward Node", _ =>
            {
                AddElement(CreateRewardNode(lastMouseGraphPosition));
            });

            evt.menu.AppendAction("Add End Node", _ =>
            {
                AddElement(CreateEndNode(lastMouseGraphPosition));
            });
        }));
    }

    public TaskRootNode CreateTaskRootNode(Vector2 position)
    {
        TaskRootNode node = new TaskRootNode();
        node.Initialize(position);
        node.Draw();
        return node;
    }

    public TaskStepNode CreateStepNode(Vector2 position)
    {
        TaskStepNode node = new TaskStepNode();
        node.Initialize(position);
        node.Draw();
        return node;
    }

    public TaskRewardNode CreateRewardNode(Vector2 position)
    {
        TaskRewardNode node = new TaskRewardNode();
        node.Initialize(position);
        node.Draw();
        return node;
    }

    public TaskEndNode CreateEndNode(Vector2 position)
    {
        TaskEndNode node = new TaskEndNode();
        node.Initialize(position);
        node.Draw();
        return node;
    }

    public List<TaskGraphNodeSaveData> BuildSaveData()
    {
        List<TaskGraphNodeSaveData> saveDataList = new List<TaskGraphNodeSaveData>();

        foreach (TaskRootNode node in nodes.ToList().OfType<TaskRootNode>())
        {
            saveDataList.Add(node.BuildSaveData());
        }

        foreach (TaskStepNode node in nodes.ToList().OfType<TaskStepNode>())
        {
            saveDataList.Add(node.BuildSaveData());
        }

        foreach (TaskRewardNode node in nodes.ToList().OfType<TaskRewardNode>())
        {
            saveDataList.Add(node.BuildSaveData());
        }

        foreach (TaskEndNode node in nodes.ToList().OfType<TaskEndNode>())
        {
            saveDataList.Add(node.BuildSaveData());
        }

        return saveDataList;
    }

    public void LoadGraph(List<TaskGraphNodeSaveData> saveDataList)
    {
        DeleteElements(graphElements.ToList());

        if (saveDataList == null || saveDataList.Count == 0)
        {
            return;
        }

        Dictionary<string, GraphElement> createdNodes = new Dictionary<string, GraphElement>();
        foreach (TaskGraphNodeSaveData saveData in saveDataList)
        {
            switch (saveData.nodeType)
            {
                case TaskGraphNodeType.TaskRoot:
                {
                    TaskRootNode taskNode = new TaskRootNode();
                    taskNode.Initialize(saveData.position);
                    taskNode.ApplySaveData(saveData);
                    taskNode.Draw();
                    AddElement(taskNode);
                    createdNodes[saveData.id] = taskNode;
                    break;
                }
                case TaskGraphNodeType.Step:
                {
                    TaskStepNode stepNode = new TaskStepNode();
                    stepNode.Initialize(saveData.position);
                    stepNode.ApplySaveData(saveData);
                    stepNode.Draw();
                    AddElement(stepNode);
                    createdNodes[saveData.id] = stepNode;
                    break;
                }
                case TaskGraphNodeType.Reward:
                {
                    TaskRewardNode rewardNode = new TaskRewardNode();
                    rewardNode.Initialize(saveData.position);
                    rewardNode.ApplySaveData(saveData);
                    rewardNode.Draw();
                    AddElement(rewardNode);
                    createdNodes[saveData.id] = rewardNode;
                    break;
                }
                case TaskGraphNodeType.End:
                {
                    TaskEndNode endNode = new TaskEndNode();
                    endNode.Initialize(saveData.position);
                    endNode.ApplySaveData(saveData);
                    endNode.Draw();
                    AddElement(endNode);
                    createdNodes[saveData.id] = endNode;
                    break;
                }
            }
        }

        foreach (TaskGraphNodeSaveData saveData in saveDataList)
        {
            if (string.IsNullOrWhiteSpace(saveData.nextNodeID))
            {
                continue;
            }

            if (!createdNodes.TryGetValue(saveData.id, out GraphElement fromElement) ||
                !createdNodes.TryGetValue(saveData.nextNodeID, out GraphElement toElement))
            {
                continue;
            }

            Port outputPort = GetOutputPort(fromElement);
            Port inputPort = GetInputPort(toElement);
            if (outputPort == null || inputPort == null)
            {
                continue;
            }

            Edge edge = outputPort.ConnectTo(inputPort);
            AddElement(edge);
        }
    }

    private void UpdateMouseGraphPosition(Vector2 localMousePosition)
    {
        lastMouseGraphPosition = this.ChangeCoordinatesTo(contentViewContainer, localMousePosition);
    }

    private static Port GetOutputPort(GraphElement element)
    {
        return element switch
        {
            TaskRootNode taskRootNode => taskRootNode.OutputPort,
            TaskStepNode taskStepNode => taskStepNode.OutputPort,
            TaskRewardNode taskRewardNode => taskRewardNode.OutputPort,
            _ => null
        };
    }

    private static Port GetInputPort(GraphElement element)
    {
        return element switch
        {
            TaskStepNode taskStepNode => taskStepNode.InputPort,
            TaskRewardNode taskRewardNode => taskRewardNode.InputPort,
            TaskEndNode taskEndNode => taskEndNode.InputPort,
            _ => null
        };
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        return change;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList()
            .Where(port =>
                port != startPort &&
                port.node != startPort.node &&
                port.direction != startPort.direction)
            .ToList();
    }

    public void SetSelectedTaskChainsExpanded(bool isExpanded)
    {
        List<TaskRootNode> selectedRootNodes = selection
            .OfType<TaskRootNode>()
            .ToList();

        if (selectedRootNodes.Count == 0)
        {
            return;
        }

        List<TaskGraphNodeSaveData> saveDataList = BuildSaveData();
        Dictionary<string, TaskGraphNodeSaveData> saveDataMap = saveDataList
            .Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
            .ToDictionary(node => node.id, node => node);

        Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();

        foreach (TaskRootNode rootNode in nodes.OfType<TaskRootNode>())
        {
            nodeMap[rootNode.ID] = rootNode;
        }

        foreach (TaskStepNode stepNode in nodes.OfType<TaskStepNode>())
        {
            nodeMap[stepNode.ID] = stepNode;
        }

        foreach (TaskRewardNode rewardNode in nodes.OfType<TaskRewardNode>())
        {
            nodeMap[rewardNode.ID] = rewardNode;
        }

        foreach (TaskEndNode endNode in nodes.OfType<TaskEndNode>())
        {
            nodeMap[endNode.ID] = endNode;
        }

        foreach (TaskRootNode rootNode in selectedRootNodes)
        {
            rootNode.SetExpanded(isExpanded);

            if (!saveDataMap.TryGetValue(rootNode.ID, out TaskGraphNodeSaveData rootSave))
            {
                continue;
            }

            string nextNodeID = rootSave.nextNodeID;
            HashSet<string> visited = new HashSet<string>();

            while (!string.IsNullOrWhiteSpace(nextNodeID) &&
                   saveDataMap.TryGetValue(nextNodeID, out TaskGraphNodeSaveData currentSave) &&
                   nodeMap.TryGetValue(nextNodeID, out Node currentNode) &&
                   visited.Add(nextNodeID))
            {
                switch (currentNode)
                {
                    case TaskStepNode stepNode:
                        stepNode.SetExpanded(isExpanded);
                        break;
                    case TaskRewardNode rewardNode:
                        rewardNode.SetExpanded(isExpanded);
                        break;
                    case TaskEndNode endNode:
                        endNode.SetExpanded(isExpanded);
                        break;
                }

                nextNodeID = currentSave.nextNodeID;
            }
        }

        AutoLayoutTaskChains();
        schedule.Execute(AutoLayoutTaskChains).ExecuteLater(0);
    }

    private void AutoLayoutTaskChains()
    {
        const float horizontalSpacing = 20f;

        List<TaskGraphNodeSaveData> saveDataList = BuildSaveData();
        Dictionary<string, TaskGraphNodeSaveData> saveDataMap = saveDataList
            .Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
            .ToDictionary(node => node.id, node => node);

        Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();

        foreach (TaskRootNode rootNode in nodes.OfType<TaskRootNode>())
        {
            nodeMap[rootNode.ID] = rootNode;
        }

        foreach (TaskStepNode stepNode in nodes.OfType<TaskStepNode>())
        {
            nodeMap[stepNode.ID] = stepNode;
        }

        foreach (TaskRewardNode rewardNode in nodes.OfType<TaskRewardNode>())
        {
            nodeMap[rewardNode.ID] = rewardNode;
        }

        foreach (TaskEndNode endNode in nodes.OfType<TaskEndNode>())
        {
            nodeMap[endNode.ID] = endNode;
        }

        List<TaskRootNode> rootNodes = nodes.OfType<TaskRootNode>()
            .OrderBy(node => node.GetPosition().y)
            .ThenBy(node => node.GetPosition().x)
            .ToList();

        foreach (TaskRootNode rootNode in rootNodes)
        {
            if (!saveDataMap.TryGetValue(rootNode.ID, out TaskGraphNodeSaveData rootSave))
            {
                continue;
            }

            float nextX = rootNode.GetPosition().x + rootNode.GetPosition().width + horizontalSpacing;
            string nextNodeID = rootSave.nextNodeID;
            HashSet<string> visited = new HashSet<string>();

            while (!string.IsNullOrWhiteSpace(nextNodeID) &&
                   saveDataMap.TryGetValue(nextNodeID, out TaskGraphNodeSaveData currentSave) &&
                   nodeMap.TryGetValue(nextNodeID, out Node currentNode) &&
                   visited.Add(nextNodeID))
            {
                Rect currentRect = currentNode.GetPosition();
                currentNode.SetPosition(new Rect(nextX, currentRect.y, currentRect.width, currentRect.height));
                nextX += currentRect.width + horizontalSpacing;
                nextNodeID = currentSave.nextNodeID;
            }
        }
    }

}
