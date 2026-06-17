using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TaskEditorWindow : EditorWindow
{
    private const string GraphFolderPath = "Assets/Editor/TaskSystem/Graphs";

    private TaskGraphView graphView;
    private IMGUIContainer toolbarContainer;

    private string graphFileName = "TaskGraph";
    private TaskDataSO targetTaskDataSO;

    private GUIStyle titleStyle;
    private GUIStyle compactLabelStyle;
    private GUIStyle toolbarButtonStyle;

    [MenuItem("Tools/Task Editor")]
    public static void OpenWindow()
    {
        TaskEditorWindow window = GetWindow<TaskEditorWindow>();
        window.titleContent = new GUIContent("Task Editor");
    }

    private void OnEnable()
    {
        ConstructGraphView();
        ConstructToolbar();
    }

    private void OnDisable()
    {
        if (graphView != null)
        {
            rootVisualElement.Remove(graphView);
        }

        if (toolbarContainer != null)
        {
            rootVisualElement.Remove(toolbarContainer);
        }
    }

    private void ConstructGraphView()
    {
        graphView = new TaskGraphView();
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);
    }

    private void ConstructToolbar()
    {
        toolbarContainer = new IMGUIContainer(DrawToolbarGUI);
        toolbarContainer.style.position = Position.Absolute;
        toolbarContainer.style.left = 8f;
        toolbarContainer.style.right = 8f;
        toolbarContainer.style.top = 8f;
        toolbarContainer.style.height = 34f;
        toolbarContainer.style.paddingLeft = 18f;
        toolbarContainer.style.paddingRight = 18f;
        toolbarContainer.style.paddingTop = 4f;
        toolbarContainer.style.paddingBottom = 4f;
        toolbarContainer.style.borderBottomLeftRadius = 8f;
        toolbarContainer.style.borderBottomRightRadius = 8f;
        toolbarContainer.style.borderTopLeftRadius = 8f;
        toolbarContainer.style.borderTopRightRadius = 8f;
        toolbarContainer.style.backgroundColor = new Color(0.09f, 0.11f, 0.15f, 0.97f);
        toolbarContainer.style.borderLeftWidth = 1f;
        toolbarContainer.style.borderRightWidth = 1f;
        toolbarContainer.style.borderTopWidth = 1f;
        toolbarContainer.style.borderBottomWidth = 1f;
        toolbarContainer.style.borderLeftColor = new Color(0.28f, 0.42f, 0.68f, 0.95f);
        toolbarContainer.style.borderRightColor = new Color(0.28f, 0.42f, 0.68f, 0.95f);
        toolbarContainer.style.borderTopColor = new Color(0.28f, 0.42f, 0.68f, 0.95f);
        toolbarContainer.style.borderBottomColor = new Color(0.28f, 0.42f, 0.68f, 0.95f);
        rootVisualElement.Add(toolbarContainer);
        toolbarContainer.BringToFront();
    }

    private void CreateStyles()
    {
        if (EditorStyles.label == null)
        {
            return;
        }

        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.93f, 0.97f, 1f) }
        };

        compactLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.68f, 0.76f, 0.87f) }
        };

        toolbarButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = 22f,
            margin = new RectOffset(0, 0, 0, 0),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.97f, 0.99f, 1f) },
            hover = { textColor = Color.white },
            active = { textColor = Color.white }
        };
    }

    private void EnsureStyles()
    {
        if (titleStyle == null || compactLabelStyle == null || toolbarButtonStyle == null)
        {
            CreateStyles();
        }
    }

    private void DrawToolbarGUI()
    {
        EnsureStyles();
        if (titleStyle == null)
        {
            return;
        }

        Rect rowRect = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
        rowRect.y += 5f;
        rowRect.x += 10f;
        rowRect.width -= 20f;
        rowRect.height = 22f;

        float x = rowRect.x;

        Rect titleRect = new Rect(x, rowRect.y + 1f, 118f, 20f);
        GUI.Label(titleRect, "Task Graph Editor", titleStyle);
        x += 126f;

        Rect graphLabelRect = new Rect(x, rowRect.y + 2f, 34f, 16f);
        GUI.Label(graphLabelRect, "Graph", compactLabelStyle);
        x += 40f;

        graphFileName = EditorGUI.TextField(new Rect(x, rowRect.y + 1f, 145f, 20f), graphFileName);
        x += 160f;

        Rect taskDataLabelRect = new Rect(x, rowRect.y + 2f, 74f, 16f);
        GUI.Label(taskDataLabelRect, "Task Data SO", compactLabelStyle);
        x += 82f;

        float buttonsWidth = 92f + 8f + 84f + 8f + 84f + 12f + 176f;
        float objectFieldWidth = Mathf.Max(180f, rowRect.xMax - x - buttonsWidth - 20f);
        targetTaskDataSO = (TaskDataSO)EditorGUI.ObjectField(
            new Rect(x, rowRect.y + 1f, objectFieldWidth, 20f),
            targetTaskDataSO,
            typeof(TaskDataSO),
            false);
        x += objectFieldWidth + 20f;

        Color oldColor = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.29f, 0.47f, 0.78f);
        if (GUI.Button(new Rect(x, rowRect.y, 92f, 22f), "Load Graph", toolbarButtonStyle))
        {
            LoadGraph();
        }
        x += 100f;

        GUI.backgroundColor = new Color(0.39f, 0.44f, 0.56f);
        if (GUI.Button(new Rect(x, rowRect.y, 84f, 22f), "\u5168\u90e8\u6298\u53e0", toolbarButtonStyle))
        {
            graphView?.SetSelectedTaskChainsExpanded(false);
        }
        x += 92f;

        GUI.backgroundColor = new Color(0.34f, 0.52f, 0.64f);
        if (GUI.Button(new Rect(x, rowRect.y, 84f, 22f), "\u5168\u90e8\u5c55\u5f00", toolbarButtonStyle))
        {
            graphView?.SetSelectedTaskChainsExpanded(true);
        }
        x += 92f;

        GUI.backgroundColor = new Color(0.22f, 0.62f, 0.34f);
        if (GUI.Button(new Rect(x, rowRect.y, 176f, 22f), "Save Graph + Export Tasks", toolbarButtonStyle))
        {
            SaveGraphAndExport();
        }

        GUI.backgroundColor = oldColor;
    }

    private void LoadGraph()
    {
        TaskGraphSaveDataSO graphData = LoadGraphAsset();
        if (graphData == null)
        {
            EditorUtility.DisplayDialog("Task Editor", "Graph asset not found.", "OK");
            return;
        }

        graphFileName = graphData.graphName;
        targetTaskDataSO = graphData.targetTaskDataSO;
        graphView.LoadGraph(graphData.nodes);
    }

    private void SaveGraphAndExport()
    {
        if (string.IsNullOrWhiteSpace(graphFileName))
        {
            EditorUtility.DisplayDialog("Task Editor", "Graph file name cannot be empty.", "OK");
            return;
        }

        Directory.CreateDirectory(GraphFolderPath);

        TaskGraphSaveDataSO graphData = GetOrCreateGraphAsset();
        graphData.graphName = graphFileName;
        graphData.targetTaskDataSO = targetTaskDataSO;
        graphData.nodes = graphView.BuildSaveData();

        EditorUtility.SetDirty(graphData);
        AssetDatabase.SaveAssets();

        ExportToTaskDataSO(graphData);
        AssetDatabase.Refresh();
    }

    private void ExportToTaskDataSO(TaskGraphSaveDataSO graphData)
    {
        if (graphData == null || graphData.targetTaskDataSO == null || graphData.nodes == null)
        {
            return;
        }

        List<TaskGraphNodeSaveData> rootNodes = graphData.nodes
            .Where(node => node != null && node.nodeType == TaskGraphNodeType.TaskRoot)
            .OrderBy(node => node.position.x)
            .ThenBy(node => node.position.y)
            .ToList();

        foreach (TaskGraphNodeSaveData rootNode in rootNodes)
        {
            TaskData existingTask = graphData.targetTaskDataSO.GetTaskData(rootNode.taskID);
            List<TaskRewardData> rewardList = existingTask != null ? new List<TaskRewardData>(existingTask.rewardList) : new List<TaskRewardData>();
            List<int> prerequisiteTaskIDs = rootNode.prerequisiteTaskID >= 0
                ? new List<int> { rootNode.prerequisiteTaskID }
                : new List<int>();

            TaskData taskData = new TaskData
            {
                taskID = rootNode.taskID,
                taskName = rootNode.taskName,
                taskDescription = rootNode.taskDescription,
                taskType = rootNode.taskType,
                giverNpcID = rootNode.giverNpcID,
                prerequisiteTaskIDs = prerequisiteTaskIDs,
                rewardList = rewardList,
                stepList = BuildOrderedStepList(graphData.nodes, rootNode)
            };

            graphData.targetTaskDataSO.UpsertTaskData(taskData);
        }

        EditorUtility.SetDirty(graphData.targetTaskDataSO);
        AssetDatabase.SaveAssets();
    }

    private List<TaskStepData> BuildOrderedStepList(List<TaskGraphNodeSaveData> nodeSaves, TaskGraphNodeSaveData rootNode)
    {
        List<TaskStepData> steps = new List<TaskStepData>();
        if (nodeSaves == null || rootNode == null || string.IsNullOrWhiteSpace(rootNode.nextNodeID))
        {
            return steps;
        }

        Dictionary<string, TaskGraphNodeSaveData> nodeMap = nodeSaves
            .Where(node => node != null)
            .ToDictionary(node => node.id, node => node);

        string currentNodeID = rootNode.nextNodeID;
        HashSet<string> visited = new HashSet<string>();
        TaskStepData currentStep = null;

        while (!string.IsNullOrWhiteSpace(currentNodeID) && nodeMap.TryGetValue(currentNodeID, out TaskGraphNodeSaveData currentNode))
        {
            if (!visited.Add(currentNode.id))
            {
                break;
            }

            switch (currentNode.nodeType)
            {
                case TaskGraphNodeType.Step:
                    currentStep = ConvertNodeToStepData(currentNode, steps.Count + 1);
                    steps.Add(currentStep);
                    break;
                case TaskGraphNodeType.Reward:
                    if (currentStep != null && currentNode.rewardList != null)
                    {
                        if (currentNode.goldAmount > 0)
                        {
                            currentStep.stepRewardList.Add(new TaskRewardData
                            {
                                rewardType = TaskRewardType.Gold,
                                amount = currentNode.goldAmount
                            });
                        }

                        for (int i = 0; i < currentNode.rewardList.Count; i++)
                        {
                            TaskRewardData reward = currentNode.rewardList[i];
                            if (reward == null)
                            {
                                continue;
                            }

                            currentStep.stepRewardList.Add(CloneRewardData(reward));
                        }
                    }
                    break;
                case TaskGraphNodeType.End:
                    currentNodeID = string.Empty;
                    continue;
            }

            currentNodeID = currentNode.nextNodeID;
        }

        return steps;
    }

    private TaskStepData ConvertNodeToStepData(TaskGraphNodeSaveData nodeSave, int fallbackStepID)
    {
        List<TaskObjectiveData> objectives = new List<TaskObjectiveData>();
        if (nodeSave.objectiveList != null)
        {
            foreach (TaskObjectiveData objective in nodeSave.objectiveList)
            {
                if (objective == null)
                {
                    continue;
                }

                objectives.Add(new TaskObjectiveData
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
        }

        return new TaskStepData
        {
            stepID = nodeSave.stepID > 0 ? nodeSave.stepID : fallbackStepID,
            stepName = nodeSave.stepName,
            stepDescription = nodeSave.stepDescription,
            objectiveList = objectives,
            stepRewardList = new List<TaskRewardData>()
        };
    }

    private static TaskRewardData CloneRewardData(TaskRewardData reward)
    {
        return new TaskRewardData
        {
            rewardType = reward.rewardType,
            amount = reward.amount,
            itemID = reward.itemID,
            equipmentID = reward.equipmentID,
            unlockTaskID = reward.unlockTaskID,
            customRewardID = reward.customRewardID
        };
    }

    private TaskGraphSaveDataSO GetOrCreateGraphAsset()
    {
        TaskGraphSaveDataSO graphData = LoadGraphAsset();
        if (graphData != null)
        {
            return graphData;
        }

        graphData = CreateInstance<TaskGraphSaveDataSO>();
        AssetDatabase.CreateAsset(graphData, GetGraphAssetPath());
        AssetDatabase.SaveAssets();
        return graphData;
    }

    private TaskGraphSaveDataSO LoadGraphAsset()
    {
        return AssetDatabase.LoadAssetAtPath<TaskGraphSaveDataSO>(GetGraphAssetPath());
    }

    private string GetGraphAssetPath()
    {
        return Path.Combine(GraphFolderPath, $"{graphFileName}.asset").Replace("\\", "/");
    }
}
