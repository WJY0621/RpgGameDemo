using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NPCDialogueRuleSet
{
    [Tooltip("NPC ID that these dialogue rules apply to.")]
    public string npcID;

    [Tooltip("Dialogue rules for this NPC. The first matched rule will be used.")]
    public List<NPCDialogueCondition> dialogueConditions = new List<NPCDialogueCondition>();
}

[CreateAssetMenu(fileName = "NPCDialogueRuleConfig", menuName = "Data/GameCharacter/NPC/NPC Dialogue Rule Config")]
public class NPCDialogueRuleConfigSO : ScriptableObject
{
    [Tooltip("Global NPC dialogue rule table. Put this asset under a Resources folder and name it NPCDialogueRuleConfig to auto-load it at runtime.")]
    public List<NPCDialogueRuleSet> npcRuleSets = new List<NPCDialogueRuleSet>();

    public NPCDialogueCondition[] GetRules(string npcID)
    {
        if (string.IsNullOrWhiteSpace(npcID) || npcRuleSets == null)
        {
            return null;
        }

        for (int i = 0; i < npcRuleSets.Count; i++)
        {
            NPCDialogueRuleSet ruleSet = npcRuleSets[i];
            if (ruleSet == null || string.IsNullOrWhiteSpace(ruleSet.npcID))
            {
                continue;
            }

            if (string.Equals(ruleSet.npcID.Trim(), npcID.Trim(), System.StringComparison.Ordinal))
            {
                return ruleSet.dialogueConditions != null
                    ? ruleSet.dialogueConditions.ToArray()
                    : null;
            }
        }

        return null;
    }
}
