using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DialogueMgr
{
    [Header("UI")]
    public GameDialoguePanel dialoguePanel;

    private DLSO currentNode;
    private NPCDataComponent currentNPC;
    private DialogueCameraRig dialogueCameraRig;
    private TaskManager boundTaskManager;

    public void Init(TaskManager taskManager)
    {
        if (boundTaskManager == taskManager)
        {
            return;
        }

        if (boundTaskManager != null)
        {
            boundTaskManager.OnTaskAccepted -= HandleTaskDialogueStateChanged;
            boundTaskManager.OnTaskCompleted -= HandleTaskDialogueStateChanged;
            boundTaskManager.OnTaskStepChanged -= HandleTaskStepChanged;
        }

        boundTaskManager = taskManager;
        if (boundTaskManager == null)
        {
            return;
        }

        boundTaskManager.OnTaskAccepted += HandleTaskDialogueStateChanged;
        boundTaskManager.OnTaskCompleted += HandleTaskDialogueStateChanged;
        boundTaskManager.OnTaskStepChanged += HandleTaskStepChanged;
    }

    public void RefreshDialogueGroupsFromTaskState()
    {
        GameMgr.NPC?.RefreshDialogueGroupsFromTaskState();
    }

    public Task StartDialogue(DLContainerSO dialogueContainer, string groupName = "Default")
    {
        return StartDialogue(null, dialogueContainer, groupName);
    }

    public async Task StartDialogue(NPCDataComponent npc, DLContainerSO dialogueContainer, string groupName = "Default")
    {
        currentNPC = npc;
        string requestedGroupName = NormalizeDialogueGroupName(groupName);
        currentNode = GetStartNodeWithFallback(dialogueContainer, requestedGroupName);

        if (currentNode == null)
        {
            currentNPC?.OnDialogueClosed();
            currentNPC = null;
            return;
        }

        dialoguePanel = await GameMgr.UI.ShowPanel<GameDialoguePanel>();
        if (dialoguePanel == null)
        {
            currentNPC?.OnDialogueClosed();
            currentNPC = null;
            return;
        }

        dialoguePanel.SetSpeakerName(currentNPC != null && !string.IsNullOrWhiteSpace(currentNPC.NPCName) ? currentNPC.NPCName : "NPC");
        dialoguePanel.OnMoveNext = MoveNextNode;
        dialoguePanel.OnChoiceSelected = MoveNextNode;

        FaceParticipants();
        StartDialogueCamera();
        await ProcessCurrentNodeAsync();
    }

    private void HandleTaskDialogueStateChanged(TaskRuntime runtime)
    {
        RefreshDialogueGroupsFromTaskState();
    }

    private void HandleTaskStepChanged(TaskRuntime runtime, TaskStepRuntime previousStep, TaskStepRuntime currentStep)
    {
        RefreshDialogueGroupsFromTaskState();
    }

    public void StopDialogue()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.HideChoices();
            dialoguePanel.OnMoveNext = null;
            dialoguePanel.OnChoiceSelected = null;
        }

        GameMgr.UI.HidePanel<GameDialoguePanel>();
        GameMgr.input.EnablePlayerActionMap();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        dialogueCameraRig?.EndDialogue();
        currentNPC?.OnDialogueClosed();

        currentNPC = null;
        currentNode = null;
    }

    private DLSO GetStartNode(DLContainerSO dialogueContainer, string groupName)
    {
        if (dialogueContainer == null || dialogueContainer.DialogueGroups == null)
        {
            Debug.LogError("[DialogueMgr] Dialogue container is null.");
            return null;
        }

        foreach (DLGroupSO dialogueGroup in dialogueContainer.DialogueGroups.Keys)
        {
            if (dialogueGroup == null)
            {
                continue;
            }

            string requestedGroupName = NormalizeDialogueGroupName(groupName);
            if (!IsDialogueGroupNameMatch(dialogueGroup.GroupName, requestedGroupName))
            {
                continue;
            }

            DLSO firstNodeInGroup = null;
            List<DLSO> groupNodes = dialogueContainer.DialogueGroups[dialogueGroup];
            if (groupNodes == null || groupNodes.Count == 0)
            {
                Debug.LogWarning($"[DialogueMgr] Group '{dialogueGroup.GroupName}' matched request '{requestedGroupName}', but contains no nodes. Container='{GetContainerName(dialogueContainer)}'.");
                return null;
            }

            foreach (DLSO node in groupNodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (firstNodeInGroup == null)
                {
                    firstNodeInGroup = node;
                }

                if (node.IsStartingDialogue)
                {
                    return node;
                }
            }

            if (firstNodeInGroup != null)
            {
                Debug.LogWarning($"[DialogueMgr] Group '{requestedGroupName}' has no explicit start node. Using first node '{firstNodeInGroup.DialogueName}' in the group. Container='{GetContainerName(dialogueContainer)}'.");
                return firstNodeInGroup;
            }
        }

        Debug.LogWarning($"[DialogueMgr] Cannot find dialogue group '{NormalizeDialogueGroupName(groupName)}'. Container='{GetContainerName(dialogueContainer)}', availableGroups={BuildAvailableGroupNames(dialogueContainer)}.");
        return null;
    }

    private static bool IsDialogueGroupNameMatch(string configuredGroupName, string requestedGroupName)
    {
        return string.Equals(
            configuredGroupName?.Trim(),
            requestedGroupName?.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDialogueGroupName(string groupName)
    {
        return string.IsNullOrWhiteSpace(groupName) ? "Default" : groupName.Trim();
    }

    private static string GetContainerName(DLContainerSO dialogueContainer)
    {
        return dialogueContainer != null && !string.IsNullOrWhiteSpace(dialogueContainer.FileName)
            ? dialogueContainer.FileName
            : "<null>";
    }

    private static string BuildAvailableGroupNames(DLContainerSO dialogueContainer)
    {
        if (dialogueContainer == null || dialogueContainer.DialogueGroups == null)
        {
            return "<none>";
        }

        List<string> groupNames = new List<string>();
        foreach (DLGroupSO group in dialogueContainer.DialogueGroups.Keys)
        {
            groupNames.Add(group != null ? group.GroupName : "<null>");
        }

        return groupNames.Count > 0 ? string.Join(", ", groupNames) : "<none>";
    }

    private DLSO GetStartNodeWithFallback(DLContainerSO dialogueContainer, string groupName)
    {
        string requestedGroupName = NormalizeDialogueGroupName(groupName);
        DLSO startNode = GetStartNode(dialogueContainer, requestedGroupName);
        if (startNode != null)
        {
            return startNode;
        }

        if (!string.IsNullOrWhiteSpace(requestedGroupName) && requestedGroupName != "Default")
        {
            Debug.LogWarning($"[DialogueMgr] Requested group '{requestedGroupName}' could not be started. Falling back to 'Default'. Container='{GetContainerName(dialogueContainer)}'.");
            startNode = GetStartNode(dialogueContainer, "Default");
            if (startNode != null)
            {
                return startNode;
            }
        }

        if (dialogueContainer == null || dialogueContainer.DialogueGroups == null)
        {
            return null;
        }

        foreach (DLGroupSO dialogueGroup in dialogueContainer.DialogueGroups.Keys)
        {
            foreach (DLSO node in dialogueContainer.DialogueGroups[dialogueGroup])
            {
                if (node != null && node.IsStartingDialogue)
                {
                    Debug.LogWarning($"[DialogueMgr] Falling back from requested group '{requestedGroupName}' to first available start node '{node.DialogueName}' in group '{dialogueGroup.GroupName}'. Container='{GetContainerName(dialogueContainer)}'.");
                    return node;
                }
            }
        }

        return null;
    }

    private void MoveNextNode()
    {
        MoveNextNode(0);
    }

    private async void MoveNextNode(int choiceID)
    {
        if (currentNode == null)
        {
            StopDialogue();
            return;
        }

        if (currentNode.Choices == null || choiceID < 0 || choiceID >= currentNode.Choices.Count)
        {
            StopDialogue();
            return;
        }

        DLChoiceData selectedChoice = currentNode.Choices[choiceID];
        if (selectedChoice == null || selectedChoice.NextDialogue == null)
        {
            StopDialogue();
            return;
        }

        currentNode = selectedChoice.NextDialogue;
        await ProcessCurrentNodeAsync();
    }

    private async Task ProcessCurrentNodeAsync()
    {
        HashSet<DLSO> processedEventNodes = new HashSet<DLSO>();
        int processedEventCount = 0;
        const int maxEventNodesPerStep = 32;

        while (currentNode != null && currentNode.DialogueType == DialogueType.Event)
        {
            if (!processedEventNodes.Add(currentNode) || processedEventCount >= maxEventNodesPerStep)
            {
                StopDialogue();
                return;
            }

            processedEventCount++;
            bool shouldContinueDialogue = await ExecuteEventNodeAsync(currentNode);
            if (!shouldContinueDialogue)
            {
                return;
            }

            currentNode = GetDefaultNextNode(currentNode);
        }

        if (currentNode == null)
        {
            StopDialogue();
            return;
        }

        dialoguePanel.UpdatePanel(currentNode);
    }

    private DLSO GetDefaultNextNode(DLSO node)
    {
        if (node == null || node.Choices == null || node.Choices.Count == 0)
        {
            return null;
        }

        return node.Choices[0].NextDialogue;
    }

    private async Task<bool> ExecuteEventNodeAsync(DLSO node)
    {
        if (node == null)
        {
            return true;
        }

        switch (node.EventType)
        {
            case DialogueEventType.OpenShop:
                NPCDataComponent shopNPC = currentNPC;
                StopDialogue();
                await GameMgr.Shop.OpenShop(shopNPC);
                return false;
            case DialogueEventType.AcceptTask:
                await AcceptTaskAsync(node);
                break;
            case DialogueEventType.CompleteDialogueTask:
                CompleteDialogueTask(node);
                break;
            case DialogueEventType.BroadcastEvent:
                TriggerDialogueEvent(node.CustomEventName);
                break;
        }

        return true;
    }

    private async Task AcceptTaskAsync(DLSO node)
    {
        TaskRuntime runtime = GameMgr.TaskMgr?.AcceptTask(node.TaskDataSO, node.TaskID);
        if (runtime == null)
        {
            return;
        }

        PlayerMainPanel playerMainPanel = GameMgr.UI.GetPanelWithoutLoad<PlayerMainPanel>();
        if (playerMainPanel == null)
        {
            playerMainPanel = await GameMgr.UI.ShowPanel<PlayerMainPanel>();
            playerMainPanel?.SetTrackedTask(runtime.sourceTaskDataSO, runtime.taskID, runtime.currentStepIndex);
        }

        if (GameMgr.Event != null && GameMgr.Event.eventsDic.ContainsKey("AcceptTask"))
        {
            GameMgr.Event.Broadcast("AcceptTask", new GameEventParameter<int>(node.TaskID));
        }
    }

    private void CompleteDialogueTask(DLSO node)
    {
        if (node == null)
        {
            return;
        }

        bool completed = GameMgr.TaskMgr != null && GameMgr.TaskMgr.CompleteDialogueTask(node.TaskID, node.TaskStepID);
        if (!completed)
        {
            Debug.LogWarning($"[DialogueMgr] CompleteDialogueTask failed. taskID={node.TaskID}, stepID={node.TaskStepID}");
            return;
        }

        if (GameMgr.Event != null && GameMgr.Event.eventsDic.ContainsKey("CompleteDialogueTask"))
        {
            GameMgr.Event.Broadcast("CompleteDialogueTask", new GameEventParameter<int>(node.TaskID));
        }
    }

    private void TriggerDialogueEvent(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            Debug.LogWarning("[DialogueMgr] Event node event name is empty.");
            return;
        }

        if (GameMgr.Event != null && GameMgr.Event.eventsDic.ContainsKey(eventName))
        {
            GameMgr.Event.Broadcast(eventName, null);
            return;
        }

        Debug.Log($"[DialogueMgr] Triggered dialogue event: {eventName}");
    }

    private void StartDialogueCamera()
    {
        if (currentNPC == null || GameMgr.Instance == null || GameMgr.Instance.Player == null)
        {
            return;
        }

        if (dialogueCameraRig == null)
        {
            dialogueCameraRig = GameMgr.Instance.GetComponent<DialogueCameraRig>();
            if (dialogueCameraRig == null)
            {
                dialogueCameraRig = GameMgr.Instance.gameObject.AddComponent<DialogueCameraRig>();
            }
        }

        dialogueCameraRig.BeginDialogue(GameMgr.Instance.Player.transform, currentNPC.transform);
    }

    private void FaceParticipants()
    {
        if (currentNPC == null || GameMgr.Instance == null || GameMgr.Instance.Player == null)
        {
            return;
        }

        Transform playerTransform = GameMgr.Instance.Player.transform;
        Transform npcTransform = currentNPC.transform;

        FaceTargetOnGround(playerTransform, npcTransform.position);
        FaceTargetOnGround(npcTransform, playerTransform.position);
    }

    private void FaceTargetOnGround(Transform source, Vector3 targetPosition)
    {
        Vector3 lookDirection = targetPosition - source.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        source.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }
}
