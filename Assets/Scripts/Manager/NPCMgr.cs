using System.Collections.Generic;
using UnityEngine;

public class NPCMgr
{
    private readonly List<NPCDataComponent> npcs = new List<NPCDataComponent>();

    private NPCDataComponent nearestInteractableNPC;
    private float nearestDistance = float.MaxValue;

    public System.Action<NPCDataComponent> OnNPCEnterRange;
    public System.Action<NPCDataComponent> OnNPCExitRange;

    public void RegisterNPC(NPCDataComponent npc)
    {
        if (npc == null || npcs.Contains(npc))
        {
            return;
        }

        npcs.Add(npc);
        npc.RefreshDialogueGroupFromTaskState();
        Debug.Log($"[NPCMgr] Registered NPC: {npc.NPCName}");
    }

    public void UnregisterNPC(NPCDataComponent npc)
    {
        if (npc == null || !npcs.Remove(npc))
        {
            return;
        }

        if (nearestInteractableNPC == npc)
        {
            nearestInteractableNPC = null;
            nearestDistance = float.MaxValue;
        }

        Debug.Log($"[NPCMgr] Unregistered NPC: {npc.NPCName}");
    }

    public void UpdateNPCs()
    {
        var player = GameMgr.Instance.Player;
        if (player == null)
        {
            return;
        }

        Transform playerTransform = player.transform;
        nearestDistance = float.MaxValue;
        nearestInteractableNPC = null;

        foreach (NPCDataComponent npc in npcs)
        {
            if (npc == null || npc.isInteracting)
            {
                continue;
            }

            float distance = Vector3.Distance(npc.transform.position, playerTransform.position);
            bool wasInRange = npc.isPlayerInRange;
            bool isInRange = distance <= npc.TriggerDistance;

            if (isInRange != wasInRange)
            {
                npc.isPlayerInRange = isInRange;

                if (isInRange)
                {
                    OnNPCEnterRange?.Invoke(npc);
                }
                else
                {
                    OnNPCExitRange?.Invoke(npc);
                }
            }

            if (isInRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestInteractableNPC = npc;
            }
        }
    }

    public void HandleInteractionInput()
    {
        if (nearestInteractableNPC != null && nearestInteractableNPC.isPlayerInRange)
        {
            nearestInteractableNPC.OnInteraction();
        }
    }

    public List<NPCDataComponent> GetAllNPCs() => npcs;

    public void RefreshDialogueGroupsFromTaskState()
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            NPCDataComponent npc = npcs[i];
            if (npc == null || !npc.HasConfiguredDialogueRules())
            {
                continue;
            }

            npc.RefreshDialogueGroupFromTaskState();
        }
    }

    public NPCDataComponent GetNPCByID(string npcID)
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            NPCDataComponent npc = npcs[i];
            if (npc != null && npc.NPCID == npcID)
            {
                return npc;
            }
        }

        return null;
    }

    public NPCDataComponent GetNearestInteractableNPC() => nearestInteractableNPC;

    public void StopAllInteractions()
    {
        for (int i = 0; i < npcs.Count; i++)
        {
            NPCDataComponent npc = npcs[i];
            if (npc != null && npc.isInteracting)
            {
                npc.StopDialogue();
            }
        }
    }

    public void Clear()
    {
        npcs.Clear();
        nearestInteractableNPC = null;
        nearestDistance = float.MaxValue;
    }
}
