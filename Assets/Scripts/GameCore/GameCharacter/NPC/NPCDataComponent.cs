using System;
using UnityEngine;

[Serializable]
public class NPCData
{
    public string npcID;
    public string npcName;
    public DLContainerSO dialogueContainer;
    public ShopDataSO shopData;
    public string defaultDialogueGroup = "Default";
    [Min(0f)] public float triggerDistance = 3f;
    public bool showPrompt = true;
    [TextArea] public string customData;
}

[DisallowMultipleComponent]
public class NPCDataComponent : MonoBehaviour
{
    private const string GlobalDialogueRuleConfigName = "NPCDialogueRuleConfig";
    private const string GlobalDialogueRuleConfigResourcePath = "GameData/NPCDialogueRuleConfig";

    [Header("NPC Basic Info")]
    [SerializeField] private NPCData npcData = new NPCData();

    public bool isPlayerInRange { get; set; }
    public bool isInteracting { get; set; }

    private Animator cachedAnimator;
    private bool hasRegisteredToManager;
    private string currentDialogueGroup;

    private static NPCDialogueRuleConfigSO cachedGlobalDialogueRuleConfig;
    private static bool attemptedLoadGlobalDialogueRuleConfig;

    public NPCData Data => npcData;
    public string NPCID => npcData != null ? npcData.npcID : string.Empty;
    public string NPCName => npcData != null ? npcData.npcName : string.Empty;
    public DLContainerSO DialogueContainer => npcData != null ? npcData.dialogueContainer : null;
    public ShopDataSO ShopData => npcData != null ? npcData.shopData : null;
    public string DefaultDialogueGroup => npcData != null && !string.IsNullOrWhiteSpace(npcData.defaultDialogueGroup)
        ? npcData.defaultDialogueGroup
        : "Default";
    public string CurrentDialogueGroup => string.IsNullOrWhiteSpace(currentDialogueGroup)
        ? DefaultDialogueGroup
        : currentDialogueGroup;
    public float TriggerDistance => npcData != null && npcData.triggerDistance > 0f ? npcData.triggerDistance : 3f;
    public bool ShowPrompt => npcData == null || npcData.showPrompt;

    private void Reset()
    {
        if (npcData == null)
        {
            npcData = new NPCData();
        }

        if (string.IsNullOrWhiteSpace(npcData.defaultDialogueGroup))
        {
            npcData.defaultDialogueGroup = "Default";
        }

        if (npcData.triggerDistance <= 0f)
        {
            npcData.triggerDistance = 3f;
        }

        npcData.showPrompt = true;
    }

    private void Awake()
    {
        cachedAnimator = GetComponent<Animator>();

        if (npcData == null)
        {
            npcData = new NPCData();
        }

        if (string.IsNullOrWhiteSpace(npcData.defaultDialogueGroup))
        {
            npcData.defaultDialogueGroup = "Default";
        }

        currentDialogueGroup = DefaultDialogueGroup;
    }

    private void OnEnable()
    {
        TryRegisterToManager();
    }

    private void Start()
    {
        TryRegisterToManager();
    }

    private void OnDisable()
    {
        TryUnregisterFromManager();
        isPlayerInRange = false;
        isInteracting = false;
    }

    public NPCData GetNPCData()
    {
        return npcData;
    }

    public void StartDialogue()
    {
        if (DialogueContainer == null || isInteracting)
        {
            return;
        }

        isInteracting = true;
        GameMgr.input.EnableUIActionMap();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        RefreshDialogueGroupFromTaskState();
        string groupName = CurrentDialogueGroup;
#pragma warning disable CS4014
        GameMgr.Dialogue.StartDialogue(this, DialogueContainer, groupName);
#pragma warning restore CS4014

        SetTalkingAnimation(true);
    }

    public void StopDialogue()
    {
        GameMgr.Dialogue.StopDialogue();
    }

    public void OnDialogueClosed()
    {
        isInteracting = false;
        SetTalkingAnimation(false);
    }

    public void OnInteraction()
    {
        if (DialogueContainer == null)
        {
            Debug.LogWarning($"[NPC] {name} has no dialogue container assigned.");
            return;
        }

        StartDialogue();
    }

    private void TryRegisterToManager()
    {
        if (hasRegisteredToManager || GameMgr.NPC == null)
        {
            return;
        }

        GameMgr.NPC.RegisterNPC(this);
        hasRegisteredToManager = true;
        RefreshDialogueGroupFromTaskState();
    }

    private void TryUnregisterFromManager()
    {
        if (!hasRegisteredToManager || GameMgr.NPC == null)
        {
            return;
        }

        GameMgr.NPC.UnregisterNPC(this);
        hasRegisteredToManager = false;
    }

    private string ResolveDialogueGroupName()
    {
        if (TryResolveDialogueGroupFromConditions(GetGlobalDialogueConditions(), out string globalGroupName))
        {
            return globalGroupName;
        }

        return DefaultDialogueGroup;
    }

    public bool RefreshDialogueGroupFromTaskState()
    {
        string nextDialogueGroup = ResolveDialogueGroupName();
        if (string.IsNullOrWhiteSpace(nextDialogueGroup))
        {
            nextDialogueGroup = DefaultDialogueGroup;
        }

        nextDialogueGroup = nextDialogueGroup.Trim();

        if (string.Equals(CurrentDialogueGroup, nextDialogueGroup, StringComparison.Ordinal))
        {
            currentDialogueGroup = nextDialogueGroup;
            return false;
        }

        string previousDialogueGroup = CurrentDialogueGroup;
        currentDialogueGroup = nextDialogueGroup;
        Debug.Log($"[NPC] Dialogue group changed for NPC {NPCID} ({NPCName}): {previousDialogueGroup} -> {currentDialogueGroup}");
        return true;
    }

    public bool HasConfiguredDialogueRules()
    {
        NPCDialogueCondition[] rules = GetGlobalDialogueConditions();
        return rules != null && rules.Length > 0;
    }

    private NPCDialogueCondition[] GetGlobalDialogueConditions()
    {
        if (string.IsNullOrWhiteSpace(NPCID))
        {
            return null;
        }

        NPCDialogueRuleConfigSO globalConfig = GetGlobalDialogueRuleConfig();
        return globalConfig != null ? globalConfig.GetRules(NPCID) : null;
    }

    private static NPCDialogueRuleConfigSO GetGlobalDialogueRuleConfig()
    {
        if (!attemptedLoadGlobalDialogueRuleConfig)
        {
            attemptedLoadGlobalDialogueRuleConfig = true;
            cachedGlobalDialogueRuleConfig = Resources.Load<NPCDialogueRuleConfigSO>(GlobalDialogueRuleConfigResourcePath);
            if (cachedGlobalDialogueRuleConfig == null)
            {
                cachedGlobalDialogueRuleConfig = Resources.Load<NPCDialogueRuleConfigSO>(GlobalDialogueRuleConfigName);
            }

            if (cachedGlobalDialogueRuleConfig == null)
            {
                cachedGlobalDialogueRuleConfig = FindLoadedDialogueRuleConfig();
            }

            if (cachedGlobalDialogueRuleConfig == null)
            {
                Debug.LogWarning("[NPC] NPCDialogueRuleConfig was not found. Put it under a Resources folder or keep it loaded in the editor, otherwise NPCs will use their default dialogue group.");
            }
        }

        return cachedGlobalDialogueRuleConfig;
    }

    private static NPCDialogueRuleConfigSO FindLoadedDialogueRuleConfig()
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:NPCDialogueRuleConfigSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            NPCDialogueRuleConfigSO config = UnityEditor.AssetDatabase.LoadAssetAtPath<NPCDialogueRuleConfigSO>(path);
            if (config != null && config.name == GlobalDialogueRuleConfigName)
            {
                return config;
            }
        }

        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            NPCDialogueRuleConfigSO config = UnityEditor.AssetDatabase.LoadAssetAtPath<NPCDialogueRuleConfigSO>(path);
            if (config != null)
            {
                return config;
            }
        }
#endif

        NPCDialogueRuleConfigSO[] loadedConfigs = Resources.FindObjectsOfTypeAll<NPCDialogueRuleConfigSO>();
        if (loadedConfigs == null || loadedConfigs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < loadedConfigs.Length; i++)
        {
            NPCDialogueRuleConfigSO config = loadedConfigs[i];
            if (config != null && config.name == GlobalDialogueRuleConfigName)
            {
                return config;
            }
        }

        return loadedConfigs[0];
    }

    private static bool TryResolveDialogueGroupFromConditions(NPCDialogueCondition[] conditions, out string dialogueGroupName)
    {
        dialogueGroupName = null;

        if (conditions == null || GameMgr.TaskMgr == null)
        {
            return false;
        }

        for (int i = 0; i < conditions.Length; i++)
        {
            NPCDialogueCondition condition = conditions[i];
            if (condition == null || string.IsNullOrWhiteSpace(condition.dialogueGroupName) || condition.taskID < 0)
            {
                continue;
            }

            if (IsDialogueConditionMatched(condition))
            {
                dialogueGroupName = condition.dialogueGroupName;
                return true;
            }
        }

        return false;
    }

    private static bool IsDialogueConditionMatched(NPCDialogueCondition condition)
    {
        switch (condition.conditionType)
        {
            case NPCDialogueConditionType.TaskAvailable:
                return GameMgr.TaskMgr.IsTaskAvailable(condition.taskID);
            case NPCDialogueConditionType.TaskNotAccepted:
                return !GameMgr.TaskMgr.HasAcceptedTask(condition.taskID) && !GameMgr.TaskMgr.IsTaskCompleted(condition.taskID);
            case NPCDialogueConditionType.TaskStepInProgress:
                return condition.stepID >= 0 && GameMgr.TaskMgr.IsTaskAtStep(condition.taskID, condition.stepID);
            case NPCDialogueConditionType.TaskCompleted:
                return GameMgr.TaskMgr.IsTaskCompleted(condition.taskID);
            default:
                return false;
        }
    }

    private void SetTalkingAnimation(bool isTalking)
    {
        if (cachedAnimator != null)
        {
            cachedAnimator.SetBool("IsTalk", isTalking);
        }
    }
}
