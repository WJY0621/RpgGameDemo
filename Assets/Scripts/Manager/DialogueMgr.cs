using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Threading.Tasks;

public class DialogueMgr
{
    [Header("UI组件")]
    public GameDialoguePanel dialoguePanel;
    private DLSO currentNode;

    public async Task StartDialogue(DLContainerSO dLContainerSO, string GroupName = "Default")
    {
        GetStartNode(dLContainerSO, GroupName);
        dialoguePanel = await GameMgr.UI.ShowPanel<GameDialoguePanel>();
        //TODO
        dialoguePanel.SetSpeakerName("NPCName");
        dialoguePanel.UpdatePanel(currentNode);
        dialoguePanel.OnMoveNext = () => moveNextNode();
        dialoguePanel.OnChoiceSelected = param => moveNextNode(param);
    }

    public void StopDialogue()
    {
        GameMgr.UI.HidePanel<GameDialoguePanel>();
        GameMgr.input.EnablePlayerActionMap();
    }

    private void GetStartNode(DLContainerSO dLContainerSO, string GroupName)
    {
        foreach (DLGroupSO dLGroup in dLContainerSO.DialogueGroups.Keys)
        {
            if (dLGroup.GroupName == GroupName)
            {
                foreach (DLSO node in dLContainerSO.DialogueGroups[dLGroup])
                {
                    if (node.IsStartingDialogue == true)
                    {
                        currentNode = node;
                    }
                }
            }
        }
    }

    private void moveNextNode(int choiceID = 0)
    {
        currentNode = currentNode.Choices[choiceID].NextDialogue;
        if (currentNode != null)
        {
            dialoguePanel.UpdatePanel(currentNode);
        }
        else
        {
            StopDialogue();
        }
    }

}
