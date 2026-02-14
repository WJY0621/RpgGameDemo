using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UIElements;

public class DialogueGraphView : GraphView
{
    private DialogueEditorWindow editorWindow;
    private DLSearchWindow searchWindow;
    private MiniMap miniMap;
    private SerializableDictionary<string, DLNodeErrorData> ungroupedNodes;
    private SerializableDictionary<string, DLGroupErrorData> groups;
    private SerializableDictionary<Group, SerializableDictionary<string, DLNodeErrorData>> groupedNodes;
    private int nameErrorsAmount;
    
    public int NameErrorsAmount
    {
        get { return nameErrorsAmount; }
        set
        {
            nameErrorsAmount = value;
            if (nameErrorsAmount == 0)
            {
                editorWindow.EnableSaving();
            }
            if (nameErrorsAmount == 1)
            {
                editorWindow.DisableSaving();
            }
        }
    }

    public DialogueGraphView(DialogueEditorWindow editorWindow)
    {
        this.editorWindow = editorWindow;
        ungroupedNodes = new SerializableDictionary<string, DLNodeErrorData>();
        groups = new SerializableDictionary<string, DLGroupErrorData>();
        groupedNodes = new SerializableDictionary<Group, SerializableDictionary<string, DLNodeErrorData>>();

        AddManipulators();
        AddSearchWindow();
        AddMiniMap();
        AddGridBackground();

        OnElementsDeleted();
        OnGroupElementsAdded();
        OnGroupElementsRemoved();
        OnGroupRenamed();
        OnGraphViewChanged();

        AddStyles();
        AddMiniMapStyles();
    }

    #region Overrided Methods
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();

        ports.ForEach(port =>
        {
            if (startPort == port || startPort.node == port.node || startPort.direction == port.direction)
            {
                return;
            }

            compatiblePorts.Add(port);
        });

        return compatiblePorts;
    }
    #endregion

    #region Manipulators
    private void AddManipulators()
    {
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.AddManipulator(CreateNodeContextualMenu("Add Node (Single Choice)", DialogueType.SingleChoice));
        this.AddManipulator(CreateNodeContextualMenu("Add Node (Multiple Choice)", DialogueType.MultipleChoice));
        this.AddManipulator(CreateGroupContextualMenu());
    }

    private IManipulator CreateGroupContextualMenu()
    {
        ContextualMenuManipulator contextualMenuManipulator = new ContextualMenuManipulator(
            menuEvent => menuEvent.menu.AppendAction("Add Group", actionEvent =>
            CreateGroup("DialogueGroup", GetLocalMousePosition(actionEvent.eventInfo.localMousePosition)))
        );
        return contextualMenuManipulator;
    }

    private IManipulator CreateNodeContextualMenu(string actionTitle, DialogueType dialogueType)
    {
        ContextualMenuManipulator contextualMenuManipulator = new ContextualMenuManipulator(
            menuEvent => menuEvent.menu.AppendAction(actionTitle, actionEvent =>
            AddElement(CreateNode("对话节点", dialogueType, GetLocalMousePosition(actionEvent.eventInfo.localMousePosition))))
        );
        return contextualMenuManipulator;
    }
    #endregion

    #region Elements Creation
    public DLGroup CreateGroup(string title, Vector2 localMousePosition)
    {
        DLGroup group = new DLGroup(title, localMousePosition);
        AddGroup(group);
        AddElement(group);

        foreach (GraphElement selectedElement in selection)
        {
            if (!(selectedElement is DLNode))
            {
                continue;
            }

            DLNode node = (DLNode)selectedElement;

            group.AddElement(node);
        }

        return group;
    }

    public DLNode CreateNode(string nodeName, DialogueType dialogueType, Vector2 position, bool shouldDraw = true)
    {
        Type nodeType = Type.GetType($"DL{dialogueType}Node");
        DLNode node = (DLNode)Activator.CreateInstance(nodeType);
        node.Initialize(nodeName, this, position);
        if (shouldDraw)
        {
            node.Draw();
        }
        
        AddUngroupedNode(node);

        return node;
    }

    #endregion

    #region Callbacks
    private void OnElementsDeleted()
    {
        deleteSelection = (operationName, askUser) =>
        {
            Type groupType = typeof(DLGroup);
            Type degeType = typeof(Edge);

            List<DLGroup> groupsToDelete = new List<DLGroup>();
            List<Edge> edgesToDelete = new List<Edge>();
            List<DLNode> nodesToDelete = new List<DLNode>();
            foreach (GraphElement element in selection)
            {
                if (element is DLNode node)
                {
                    nodesToDelete.Add(node);
                    continue;
                }

                if (element.GetType() == degeType)
                {
                    Edge edge = (Edge)element;
                    edgesToDelete.Add(edge);
                    continue;
                }

                if (element.GetType() != groupType)
                {
                    continue;
                }
                DLGroup group = (DLGroup)element;
                groupsToDelete.Add(group);
            }

            foreach (DLGroup group in groupsToDelete)
            {
                List<DLNode> groupNodes = new List<DLNode>();

                foreach (GraphElement groupElement in group.containedElements)
                {
                    if (!(groupElement is DLNode))
                    {
                        continue;
                    }

                    groupNodes.Add((DLNode)groupElement);
                }

                group.RemoveElements(groupNodes);
                RemoveGroup(group);
                RemoveElement(group);
            }

            DeleteElements(edgesToDelete);

            foreach (DLNode node in nodesToDelete)
            {
                if (node.Group != null)
                {
                    node.Group.RemoveElement(node);
                }
                RemoveUngroupedNode(node);
                node.DisconnectAllPorts();
                RemoveElement(node);
            }
        };
    }

    private void OnGroupElementsAdded()
    {
        elementsAddedToGroup = (group, elements) =>
        {
            foreach (GraphElement element in elements)
            {
                if (!(element is DLNode))
                {
                    continue;
                }

                DLGroup nodeGroup = (DLGroup)group;
                DLNode node = (DLNode)element;

                RemoveUngroupedNode(node);
                AddGroupedNode(node, nodeGroup);
            }
        };
    }

    private void OnGroupElementsRemoved()
    {
        elementsRemovedFromGroup = (group, elements) =>
        {
            foreach (GraphElement element in elements)
            {
                if (!(element is DLNode))
                {
                    continue;
                }

                DLNode node = (DLNode)element;
                RemoveGroupedNode(node, group);
                AddUngroupedNode(node);
            }
        };
    }

    private void OnGroupRenamed()
    {
        groupTitleChanged = (group, newTitle) =>
        {
            DLGroup dLGroup = (DLGroup)group;
            dLGroup.title = newTitle.RemoveWhitespaces().RemoveSpecialCharacters();

            if (string.IsNullOrEmpty(dLGroup.title))
            {
                if (!string.IsNullOrEmpty(dLGroup.OldTitle))
                {
                    ++NameErrorsAmount;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(dLGroup.OldTitle))
                {
                    --NameErrorsAmount;
                }
            }

            RemoveGroup(dLGroup);
            dLGroup.OldTitle = dLGroup.title;
            AddGroup(dLGroup);
        };
    }

    private void OnGraphViewChanged()
    {
        graphViewChanged = (changes) =>
        {
            if (changes.edgesToCreate != null)
            {
                foreach (Edge edge in changes.edgesToCreate)
                {
                    DLNode nextNode = (DLNode)edge.input.node;
                    DLChoiceSaveData choiceData = (DLChoiceSaveData)edge.output.userData;
                    choiceData.NodeID = nextNode.ID;
                }
            }
            if (changes.elementsToRemove != null)
            {
                Type edgeType = typeof(Edge);
                foreach (GraphElement element in changes.elementsToRemove)
                {
                    if (element.GetType() != edgeType)
                    {
                        continue;
                    }
                    Edge edge = (Edge)element;
                    DLChoiceSaveData choiceData = (DLChoiceSaveData)edge.output.userData;
                    choiceData.NodeID = "";
                }
            }
            return changes;
        };
    }
    #endregion

    #region Repeated Elements
    public void AddUngroupedNode(DLNode node)
    {
        string nodeName = node.DialogueName.ToLower();
        if (!ungroupedNodes.ContainsKey(nodeName))
        {
            DLNodeErrorData nodeErrorData = new DLNodeErrorData();
            nodeErrorData.Nodes.Add(node);
            ungroupedNodes.Add(nodeName, nodeErrorData);
            return;
        }

        List<DLNode> ungroupedNodesList = ungroupedNodes[nodeName].Nodes;

        ungroupedNodesList.Add(node);
        Color errorColor = ungroupedNodes[nodeName].ErrorData.Color;
        node.SetErrorStyle(errorColor);

        if (ungroupedNodesList.Count == 2)
        {
            ++NameErrorsAmount;
            ungroupedNodesList[0].SetErrorStyle(errorColor);
        }
    }

    public void RemoveUngroupedNode(DLNode node)
    {
        string nodeName = node.DialogueName.ToLower();
        ungroupedNodes[nodeName].Nodes.Remove(node);
        node.ResetStyle();
        if (ungroupedNodes[nodeName].Nodes.Count == 1)
        {
            --NameErrorsAmount;
            ungroupedNodes[nodeName].Nodes[0].ResetStyle();
            return;
        }
        if (ungroupedNodes[nodeName].Nodes.Count == 0)
        {
            ungroupedNodes.Remove(nodeName);
        }
    }

    private void AddGroup(DLGroup group)
    {
        string groupName = group.title.ToLower();
        if (!groups.ContainsKey(groupName))
        {
            DLGroupErrorData groupErrorData = new DLGroupErrorData();
            groupErrorData.Groups.Add(group);
            groups.Add(groupName, groupErrorData);
            return;
        }
        List<DLGroup> groupsList = groups[groupName].Groups;
        groupsList.Add(group);

        Color errorColor = groups[groupName].ErrorData.Color;
        group.SetErrorStyle(errorColor);

        if (groupsList.Count == 2)
        {
            ++NameErrorsAmount;
            groupsList[0].SetErrorStyle(errorColor);
        }
    }

    private void RemoveGroup(DLGroup group)
    {
        string oldGroupName = group.OldTitle.ToLower();

        List<DLGroup> groupsList = groups[oldGroupName].Groups;
        groupsList.Remove(group);

        group.ResetStyle();

        if (groupsList.Count == 1)
        {
            --NameErrorsAmount;
            groupsList[0].ResetStyle();
            return;
        }

        if (groupsList.Count == 0)
        {
            groups.Remove(oldGroupName);
        }
    }

    public void AddGroupedNode(DLNode node, DLGroup group)
    {
        string nodeName = node.DialogueName.ToLower();
        node.Group = group;

        if (!groupedNodes.ContainsKey(group))
        {
            groupedNodes.Add(group, new SerializableDictionary<string, DLNodeErrorData>());
        }
        if (!groupedNodes[group].ContainsKey(nodeName))
        {
            DLNodeErrorData nodeErrorData = new DLNodeErrorData();
            nodeErrorData.Nodes.Add(node);
            groupedNodes[group].Add(nodeName, nodeErrorData);
            return;
        }
        List<DLNode> groupedNodesList = groupedNodes[group][nodeName].Nodes;
        groupedNodesList.Add(node);
        Color errorColor = groupedNodes[group][nodeName].ErrorData.Color;
        node.SetErrorStyle(errorColor);

        if (groupedNodesList.Count == 2)
        {
            ++NameErrorsAmount;
            groupedNodesList[0].SetErrorStyle(errorColor);
        }
    }

    public void RemoveGroupedNode(DLNode node, Group group)
    {
        string nodeName = node.DialogueName.ToLower();
        node.Group = null;

        List<DLNode> groupedNodesList = groupedNodes[group][nodeName].Nodes;
        groupedNodesList.Remove(node);
        node.ResetStyle();

        if (groupedNodesList.Count == 1)
        {
            --NameErrorsAmount;
            groupedNodesList[0].ResetStyle();
            return;
        }

        if (groupedNodesList.Count == 0)
        {
            groupedNodes[group].Remove(nodeName);
            if (groupedNodes[group].Count == 0)
            {
                groupedNodes.Remove(group);
            }
        }
    }
    #endregion

    #region Elements Addtion
    private void AddSearchWindow()
    {
        if (searchWindow == null)
        {
            searchWindow = ScriptableObject.CreateInstance<DLSearchWindow>();
            searchWindow.Initialize(this);
        }
        nodeCreationRequest = context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindow);
    }

    private void AddMiniMap()
    {
        miniMap = new MiniMap()
        {
            anchored = true
        };
        miniMap.SetPosition(new Rect(15, 50, 200, 180));
        Add(miniMap);
        miniMap.visible = false;
    }

    private void AddGridBackground()
    {
        GridBackground gridBackground = new GridBackground();
        gridBackground.StretchToParentSize();
        Insert(0, gridBackground);
    }

    private void AddStyles()
    {
        this.AddStyleSheets(
            "DialogueSystem/DialogueGraphViewStyle.uss",
            "DialogueSystem/DLNodeStyle.uss"
            );
    }
    private void AddMiniMapStyles()
    {
        StyleColor backgroundColor = new StyleColor(new Color32(29,29,30,255));
        StyleColor boderColor = new StyleColor(new Color32(51,51,51,255));

        miniMap.style.backgroundColor = backgroundColor;
        miniMap.style.borderTopColor = boderColor;
        miniMap.style.borderBottomColor = boderColor;
        miniMap.style.borderLeftColor = boderColor;
        miniMap.style.borderRightColor = boderColor;
    }

    #endregion

    #region Utilities
    public Vector2 GetLocalMousePosition(Vector2 mousePosition, bool isSearchWindow = false)
    {
        Vector2 worldMousePosition = mousePosition;
        if (isSearchWindow)
        {
            worldMousePosition -= editorWindow.position.position;
        }
        Vector2 localMousePosition = contentViewContainer.WorldToLocal(worldMousePosition);
        return localMousePosition;
    }

    public void ClearGraph()
    {
        graphElements.ForEach(graphElement => RemoveElement(graphElement));
        groups.Clear();
        groupedNodes.Clear();
        ungroupedNodes.Clear();

        nameErrorsAmount = 0;
    }

    public void ToggleMiniMap()
    {
        miniMap.visible = !miniMap.visible;
    }
    #endregion
}
