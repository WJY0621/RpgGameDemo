using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public static class DLIOUtility
{
    private static DialogueGraphView graphView;
    private static string graphFileName;
    private static string containerFolderPath;

    private static List<DLGroup> groups;
    private static List<DLNode> nodes;

    private static Dictionary<string, DLGroupSO> createDLGroups;
    private static Dictionary<string, DLSO> createdDialogues;
    private static Dictionary<string, DLGroup> loadedGroups;
    private static Dictionary<string, DLNode> loadedNodes;

    public static void Initialize(DialogueGraphView dlGraphView, string graphName)
    {
        graphView = dlGraphView;
        graphFileName = graphName;
        containerFolderPath = $"Assets/System/DialogueSystem/Dialogues/{graphFileName}";
        groups = new List<DLGroup>();
        nodes = new List<DLNode>();
        createDLGroups = new Dictionary<string, DLGroupSO>();
        createdDialogues = new Dictionary<string, DLSO>();
        loadedGroups = new Dictionary<string, DLGroup>();
        loadedNodes = new Dictionary<string, DLNode>();
    }
    #region Save Methods
    public static void Save()
    {
        CreateStaticFolders();
        GetElementsFormGraphViwe();
        DLGraphSaveDataSO graphData = CreateAsset<DLGraphSaveDataSO>("Assets/Editor/DialogueSystem/Graphs", $"{graphFileName}Graph");
        graphData.Initialize(graphFileName);
        DLContainerSO container = CreateAsset<DLContainerSO>(containerFolderPath, graphFileName);
        container.Initialize(graphFileName);

        SaveGroups(graphData, container);
        SaveNodes(graphData, container);

        SaveAsset(graphData);
        SaveAsset(container);
    }

    #region Nodes
    private static void SaveNodes(DLGraphSaveDataSO graphData, DLContainerSO container)
    {
        //处于组中的对话节点的名称列表用于 更新
        SerializableDictionary<string, List<string>> groupedNodeNames = new SerializableDictionary<string, List<string>>();
        //不在组中的对象节点列表
        List<string> ungroupedNodeNames = new List<string>();

        foreach (DLNode node in nodes)
        {
            //保存节点信息到Data数据中
            SaveNodeToGraph(node, graphData);
            SaveNodeToScriptableObject(node, container);
            //如果当前有组 则将组中节点添加进去
            if(node.Group != null)
            {
                groupedNodeNames.AddItem(node.Group.title, node.DialogueName);
                continue;
            }

            ungroupedNodeNames.Add(node.DialogueName);
        }
        UpdateDlChoicesConnections();
        UpdateOldGroupedNodes(groupedNodeNames, graphData);
        UpdateOldUngroupedNodes(ungroupedNodeNames, graphData);
    }

    private static void SaveNodeToGraph(DLNode node, DLGraphSaveDataSO graphData)
    {
        List<DLChoiceSaveData> choices = CloneNodeChoices(node.Choices);
        DLNodeSaveData nodeData = new DLNodeSaveData()
        {
            ID = node.ID,
            Name = node.DialogueName,
            Choices = choices,
            Text = node.Text,
            GroupID = node.Group?.ID,
            DialogueType = node.DialogueType,
            Position = node.GetPosition().position
        };
        graphData.Nodes.Add(nodeData);
    }

    private static List<DLChoiceSaveData> CloneNodeChoices(List<DLChoiceSaveData> nodeChoices)
    {
        List<DLChoiceSaveData> choices = new List<DLChoiceSaveData>();
        foreach (DLChoiceSaveData choice in nodeChoices)
        {
            DLChoiceSaveData choiceDate = new DLChoiceSaveData()
            {
                Text = choice.Text,
                NodeID = choice.NodeID,
            };

            choices.Add(choiceDate);
        }
        return choices;
    }

    private static void SaveNodeToScriptableObject(DLNode node, DLContainerSO container)
    {
        DLSO dialogue;
        if (node.Group != null)
        {
            dialogue = CreateAsset<DLSO>($"{containerFolderPath}/Groups/{node.Group.title}", node.DialogueName);
            container.DialogueGroups.AddItem(createDLGroups[node.Group.ID], dialogue);
        }
        else
        {
            dialogue = CreateAsset<DLSO>($"{containerFolderPath}/Global", node.DialogueName);
            container.UngroupedDialogues.Add(dialogue);
        }

        dialogue.Initialize(
            node.DialogueName,
            node.Text,
            NodeChoicesToDLChoices(node.Choices),
            node.DialogueType,
            node.IsStartingNode()
        );
        createdDialogues.Add(node.ID, dialogue);
        SaveAsset(dialogue);
    }

    private static List<DLChoiceData> NodeChoicesToDLChoices(List<DLChoiceSaveData> nodeChoices)
    {
        List<DLChoiceData> dLChoices = new List<DLChoiceData>();
        foreach (DLChoiceSaveData nodeChoice in nodeChoices)
        {
            DLChoiceData choiceData = new DLChoiceData()
            {
                Text = nodeChoice.Text
            };
            dLChoices.Add(choiceData);
        }
        return dLChoices;
    }

    private static void UpdateDlChoicesConnections()
    {
        foreach(DLNode node in nodes)
        {
            DLSO dialogue = createdDialogues[node.ID];
            for(int choiceIndex = 0; choiceIndex < node.Choices.Count; ++choiceIndex)
            {
                DLChoiceSaveData nodeChoice = node.Choices[choiceIndex];
                if (string.IsNullOrEmpty(nodeChoice.NodeID))
                {
                    continue;
                }
                dialogue.Choices[choiceIndex].NextDialogue = createdDialogues[nodeChoice.NodeID];
                SaveAsset(dialogue);
            }
        }
    }

    private static void UpdateOldGroupedNodes(SerializableDictionary<string, List<string>> currentGroupedNodeNames, DLGraphSaveDataSO graphData)
    {
        if(graphData.OldGroupedNodeNames != null && graphData.OldGroupedNodeNames.Count != 0)
        {
            foreach(KeyValuePair<string, List<string>> oldGroupedNode in graphData.OldGroupedNodeNames)
            {
                List<string> nodesToRemove = new List<string>();
                if (currentGroupedNodeNames.ContainsKey(oldGroupedNode.Key))
                {
                    nodesToRemove = oldGroupedNode.Value.Except(currentGroupedNodeNames[oldGroupedNode.Key]).ToList();
                }

                foreach(string nodeToRemove in nodesToRemove)
                {
                    RemoveAsset($"{containerFolderPath}/Groups/{oldGroupedNode.Key}/Dialogue", nodeToRemove);
                }
            }
        }
        graphData.OldGroupedNodeNames = new SerializableDictionary<string, List<string>>(currentGroupedNodeNames);
    }

    private static void UpdateOldUngroupedNodes(List<string> currentUngroupedNodeNames, DLGraphSaveDataSO graphData)
    {
        if(graphData.OldUnGroupedNodeNames != null && graphData.OldUnGroupedNodeNames.Count != 0)
        {
            List<string> nodesToRemove = graphData.OldUnGroupedNodeNames.Except(currentUngroupedNodeNames).ToList();

            foreach(string nodeToRemove in nodesToRemove)
            {
                RemoveAsset($"{containerFolderPath}/Global/Dialogues", nodeToRemove);
            }
        }

        graphData.OldUnGroupedNodeNames = new List<string>(currentUngroupedNodeNames);
    }

    #endregion

    #region Groups
    private static void SaveGroups(DLGraphSaveDataSO graphData, DLContainerSO container)
    {
        List<string> groupNames = new List<string>();
        foreach (DLGroup group in groups)
        {
            SaveGroupToGraph(group, graphData);
            SaveGroupToScriptableObject(group, container);

            groupNames.Add(group.title);
        }

        UpadateOldGroups(groupNames, graphData);
    }

    private static void SaveGroupToGraph(DLGroup group, DLGraphSaveDataSO graphData)
    {
        DLGroupSaveData groupData = new DLGroupSaveData()
        {
            ID = group.ID,
            Name = group.title,
            Position = group.GetPosition().position
        };
        graphData.Groups.Add(groupData);
    }

    private static void SaveGroupToScriptableObject(DLGroup group, DLContainerSO dLContainer)
    {
        string groupName = group.title;
        CreateFolder($"{containerFolderPath}/Groups", groupName);
        DLGroupSO dLGroup = CreateAsset<DLGroupSO>($"{containerFolderPath}/Groups/{groupName}", groupName);
        dLGroup.Initialize(groupName);
        createDLGroups.Add(group.ID, dLGroup);
        dLContainer.DialogueGroups.Add(dLGroup, new List<DLSO>());

        SaveAsset(dLGroup);
    }

    private static void UpadateOldGroups(List<string> currentGroupNames, DLGraphSaveDataSO graphData)
    {
        if(graphData.OldGroupNames != null && graphData.OldGroupNames.Count != 0)
        {
            List<string> groupsToRemove = graphData.OldGroupNames.Except(currentGroupNames).ToList();
            foreach(string groupToRemove in groupsToRemove)
            {
                RemoveFolder($"{containerFolderPath}/Groups/{groupToRemove}");
            }
        }

        graphData.OldGroupNames = new List<string>(currentGroupNames);
    }

    #endregion

    #endregion

    #region Load Methods
    public static void Load()
    {
        DLGraphSaveDataSO graphData = LoadAsset<DLGraphSaveDataSO>("Assets/Editor/DialogueSystem/Graphs", graphFileName);
        if(graphData == null)
        {
            EditorUtility.DisplayDialog("Couldn't load the file", 
            "The file at the following path could not be found:\n\n" + 
            $"Assets/Editor/DialogueSystem/Graphs/{graphFileName}",
            "Thanks!");
            return;
        }
        DialogueEditorWindow.UpdateFileName(graphData.FileName);
        LoadGroups(graphData.Groups);
        LoadNodes(graphData.Nodes);
        LoadNodesConnections();
    }

    private static void LoadGroups(List<DLGroupSaveData> groups)
    {
        foreach(DLGroupSaveData groupData in groups)
        {
            DLGroup group = graphView.CreateGroup(groupData.Name, groupData.Position);

            group.ID = groupData.ID;
            loadedGroups.Add(group.ID, group);
        }
    }
    private static void LoadNodes(List<DLNodeSaveData> nodes)
    {
        foreach(DLNodeSaveData nodeData in nodes)
        {
            List<DLChoiceSaveData> choices = CloneNodeChoices(nodeData.Choices);
            DLNode node = graphView.CreateNode(nodeData.Name, nodeData.DialogueType, nodeData.Position, false);
            node.ID = nodeData.ID;
            node.Choices = choices;
            node.Text = nodeData.Text;

            node.Draw();
            graphView.AddElement(node);
            loadedNodes.Add(node.ID, node);

            if (string.IsNullOrEmpty(nodeData.GroupID))
            {
                continue;
            }

            DLGroup group = loadedGroups[nodeData.GroupID];
            node.Group = group;
            group.AddElement(node);
        }
    }
    private static void LoadNodesConnections()
    {
        foreach(KeyValuePair<string, DLNode> loadedNode in loadedNodes)
        {
            foreach (Port choicePort in loadedNode.Value.outputContainer.Children())
            {
                DLChoiceSaveData choiceData = (DLChoiceSaveData) choicePort.userData;
                if (string.IsNullOrEmpty(choiceData.NodeID))
                {
                    continue;
                }

                DLNode nextNode = loadedNodes[choiceData.NodeID];
                Port nextNodeInputPort = (Port) nextNode.inputContainer.Children().Single();
                choicePort.ConnectTo(nextNodeInputPort);
                Edge edge = choicePort.ConnectTo(nextNodeInputPort);
                graphView.AddElement(edge);
                loadedNode.Value.RefreshPorts();
            }
        }
    }

    #endregion

    #region Creation Methods
    private static void CreateStaticFolders()
    {
        CreateFolder("Assets/Editor/DialogueSystem", "Graphs");
        CreateFolder("Assets/System", "DialogueSystem");
        CreateFolder("Assets/System/DialogueSystem", "Dialogues");
        CreateFolder("Assets/System/DialogueSystem/Dialogues", graphFileName);
        CreateFolder(containerFolderPath, "Global");
        CreateFolder(containerFolderPath, "Groups");
    }
    #endregion

    #region Get Methods
    private static void GetElementsFormGraphViwe()
    {
        Type groupType = typeof(DLGroup);
        graphView.graphElements.ForEach(graphElement =>
        {
            if (graphElement is DLNode node)
            {
                nodes.Add(node);
                return;
            }

            if (graphElement.GetType() == typeof(DLGroup))
            {
                DLGroup group = (DLGroup)graphElement;
                groups.Add(group);
                return;
            }
        });
    }
    #endregion

    #region Utility Methods
    public static void CreateFolder(string path, string folderName)
    {
        if (AssetDatabase.IsValidFolder($"{path}/{folderName}"))
        {
            return;
        }
        AssetDatabase.CreateFolder(path, folderName);
    }
    public static T CreateAsset<T>(string path, string assetName) where T : ScriptableObject
    {
        string fullPath = $"{path}/{assetName}.asset";
        T asset = LoadAsset<T>(path, assetName);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, fullPath);
        }
        return asset;
    }

    public static T LoadAsset<T>(string path, string assetName) where T : ScriptableObject
    {
        string fullPath = $"{path}/{assetName}.asset";
        return AssetDatabase.LoadAssetAtPath<T>(fullPath);
    }

    public static void SaveAsset(UnityEngine.Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    public static void RemoveFolder(string fullPath)
    {
        FileUtil.DeleteFileOrDirectory($"{fullPath}.meta");
        FileUtil.DeleteFileOrDirectory($"{fullPath}/");
    }
    public static void RemoveAsset(string path, string assetName)
    {
        AssetDatabase.DeleteAsset($"{path}/{assetName}.asset");
    }

    #endregion

}
