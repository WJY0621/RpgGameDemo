using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MessageMgr
{
    private readonly Queue<GameMessage> messageQueue = new Queue<GameMessage>();
    private readonly List<ActiveMessagePanel> activePanels = new List<ActiveMessagePanel>();

    private GameObject panelPrefab;
    private bool isLoadingPanelPrefab;

    private const float DefaultDisplayDuration = 2f;
    private const string NoSound = "noone";
    private const float MessageSpacing = 8f;
    private const int MaxMessagesSpawnedPerFrame = 6;
    private const int MaxActiveMessagePanels = 12;

    public MessageMgr()
    {
        GameMgr.Event.Register(
            "ShowMessge",
            new GameEventThreeParam<string, string, MessagePriority>(
                new GameActionThreeParam<string, string, MessagePriority>(RegisterMessage)));
    }

    public void Tick(float deltaTime)
    {
        if (panelPrefab == null)
        {
            EnsurePanelPrefabLoaded().Forget();
        }

        UpdateActivePanels(deltaTime);

        if (panelPrefab == null)
        {
            return;
        }

        Transform parent = ResolveMessageParent();
        if (parent == null)
        {
            return;
        }

        SpawnQueuedMessages(parent, MaxMessagesSpawnedPerFrame);
    }

    public void RegisterMessage(string message, string sound = NoSound, MessagePriority priority = MessagePriority.Normal)
    {
        RegisterMessageInternal(message, sound, priority, null);
    }

    public void RegisterMessageAt(string message, Transform targetParent, string sound = NoSound, MessagePriority priority = MessagePriority.Normal)
    {
        RegisterMessageInternal(message, sound, priority, targetParent);
    }

    private void RegisterMessageInternal(string message, string sound, MessagePriority priority, Transform targetParent)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        messageQueue.Enqueue(new GameMessage
        {
            message = message,
            sound = sound,
            priority = priority,
            targetParent = targetParent
        });

        if (panelPrefab == null)
        {
            EnsurePanelPrefabLoaded().Forget();
            return;
        }

        Transform parent = targetParent != null ? targetParent : ResolveMessageParent();
        if (parent != null)
        {
            SpawnQueuedMessages(parent, 1);
        }
    }

    private void SpawnQueuedMessages(Transform parent, int maxCount)
    {
        if (parent == null || maxCount <= 0)
        {
            return;
        }

        int spawned = 0;
        while (messageQueue.Count > 0 && spawned < maxCount)
        {
            SpawnMessagePanel(messageQueue.Dequeue(), parent);
            spawned++;
        }
    }

    public void ShowItemObtained(Item item, int count)
    {
        if (item == null || count <= 0)
        {
            return;
        }

        string colorHex = GetQualityColorHex(item.quality);
        string message = $"获得<color={colorHex}>{item.name}</color> x{count}";
        RegisterMessage(message, NoSound, MessagePriority.Normal);
    }

    public void ShowTaskCompleted(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        RegisterMessage($"任务“{taskName}”已完成", NoSound, MessagePriority.High);
    }

    public void ShowGoldObtained(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        RegisterMessage($"获得金币 x{amount}", NoSound, MessagePriority.Normal);
    }

    private void SpawnMessagePanel(GameMessage message, Transform parent)
    {
        if (panelPrefab == null || parent == null || message == null)
        {
            return;
        }

        TrimActivePanels(MaxActiveMessagePanels - 1);

        Transform targetParent = message.targetParent != null ? message.targetParent : parent;
        GameObject panelObject = Object.Instantiate(panelPrefab, targetParent, false);
        panelObject.name = nameof(MessagePanel);
        panelObject.SetActive(true);

        MessagePanel panel = panelObject.GetComponent<MessagePanel>();
        if (panel == null)
        {
            Object.Destroy(panelObject);
            return;
        }

        panel.Init();
        panel.SetMessage(FormatMessage(message));
        activePanels.Add(new ActiveMessagePanel(panel, targetParent, DefaultDisplayDuration));
        RefreshPanelPositions(targetParent);
        panel.Show();

        if (!string.IsNullOrWhiteSpace(message.sound) && message.sound != NoSound)
        {
            GameMgr.Audio?.PlayUIEffect(message.sound);
        }
    }

    private void TrimActivePanels(int maxCount)
    {
        while (activePanels.Count > maxCount)
        {
            ActiveMessagePanel oldest = activePanels[0];
            activePanels.RemoveAt(0);

            if (oldest?.Panel != null)
            {
                Object.Destroy(oldest.Panel.gameObject);
            }
        }
    }

    private string FormatMessage(GameMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.message))
        {
            return string.Empty;
        }

        switch (message.priority)
        {
            case MessagePriority.Medium:
                return $"<color=#57C7FF>{message.message}</color>";
            case MessagePriority.High:
                return $"<color=#FFD95C>{message.message}</color>";
            case MessagePriority.Highest:
                return $"<color=#FF7A7A>{message.message}</color>";
            default:
                return message.message;
        }
    }

    private async UniTaskVoid EnsurePanelPrefabLoaded()
    {
        if (panelPrefab != null || isLoadingPanelPrefab)
        {
            return;
        }

        isLoadingPanelPrefab = true;
        try
        {
            panelPrefab = await GameMgr.UI.LoadPanel(nameof(MessagePanel));
            if (panelPrefab == null)
            {
                Debug.LogError("[MessageMgr] Failed to load MessagePanel prefab.");
            }
        }
        finally
        {
            isLoadingPanelPrefab = false;
        }
    }

    private void UpdateActivePanels(float deltaTime)
    {
        if (activePanels.Count == 0)
        {
            return;
        }

        bool removedAny = false;
        for (int i = activePanels.Count - 1; i >= 0; i--)
        {
            ActiveMessagePanel activePanel = activePanels[i];
            if (activePanel.Panel == null)
            {
                activePanels.RemoveAt(i);
                removedAny = true;
                continue;
            }

            if (!activePanel.IsHiding)
            {
                activePanel.RemainingTime -= deltaTime;
                if (activePanel.RemainingTime <= 0f)
                {
                    activePanel.IsHiding = true;
                    activePanel.Panel.Hide();
                }
            }

            if (activePanel.IsHiding && activePanel.Panel.CurrentAlpha <= 0.01f)
            {
                Object.Destroy(activePanel.Panel.gameObject);
                activePanels.RemoveAt(i);
                removedAny = true;
            }
        }

        if (removedAny)
        {
            RefreshAllPanelPositions();
        }
    }

    private void RefreshPanelPositions(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        float currentY = 0f;
        for (int i = 0; i < activePanels.Count; i++)
        {
            MessagePanel panel = activePanels[i].Panel;
            if (panel == null)
            {
                continue;
            }

            RectTransform rect = panel.transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            if (activePanels[i].Parent != parent)
            {
                continue;
            }

            rect.SetParent(parent, false);
            panel.SetShownAnchoredPosition(new Vector2(0f, -currentY));
            currentY += panel.PreferredHeight + MessageSpacing;
        }
    }

    private void RefreshAllPanelPositions()
    {
        List<Transform> refreshedParents = new List<Transform>();
        for (int i = 0; i < activePanels.Count; i++)
        {
            Transform parent = activePanels[i].Parent;
            if (parent == null || refreshedParents.Contains(parent))
            {
                continue;
            }

            refreshedParents.Add(parent);
            RefreshPanelPositions(parent);
        }
    }

    private Transform ResolveMessageParent()
    {
        PlayerMainPanel playerMainPanel = GameMgr.UI.GetPanelWithoutLoad<PlayerMainPanel>();
        if (playerMainPanel != null && playerMainPanel.MessageContentRoot != null)
        {
            return playerMainPanel.MessageContentRoot;
        }

        if (GameMgr.UI.mainCanvas != null && GameMgr.UI.mainCanvas.panelsParent != null)
        {
            return GameMgr.UI.mainCanvas.panelsParent;
        }

        return null;
    }

    private static string GetQualityColorHex(ItemQuality quality)
    {
        switch (quality)
        {
            case ItemQuality.Advanced:
                return "#3CCB67";
            case ItemQuality.Rare:
                return "#4DA7FF";
            case ItemQuality.Epic:
                return "#B567FF";
            case ItemQuality.Legendary:
                return "#FF9D2E";
            case ItemQuality.Common:
            default:
                return "#FFFFFF";
        }
    }

    private sealed class ActiveMessagePanel
    {
        public MessagePanel Panel;
        public Transform Parent;
        public float RemainingTime;
        public bool IsHiding;

        public ActiveMessagePanel(MessagePanel panel, Transform parent, float remainingTime)
        {
            Panel = panel;
            Parent = parent;
            RemainingTime = remainingTime;
            IsHiding = false;
        }
    }
}
