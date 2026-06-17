using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家头顶 BuffContent 的运行时控制：
/// - 监听玩家 BuffComponent 的增删事件，按需创建/销毁 BuffIcon
/// - 处理 hover → 通过 UIMgr 加载 BuffInfoPanel 预制体并显示
/// </summary>
public sealed class PlayerMainBuffController
{
    private readonly Transform root;

    private Transform buffContent;
    private GameObject buffIconTemplate;
    private BuffInfoPanel infoPanel;

    private BuffComponent boundComponent;
    private readonly Dictionary<int, BuffIconUI> iconByBuffID = new Dictionary<int, BuffIconUI>();
    private BuffIconUI hoveredIcon;

    public PlayerMainBuffController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init()
    {
        buffContent = root.Find("BuffContent");
        if (buffContent == null)
        {
            Debug.LogWarning("[PlayerMainBuffController] BuffContent not found under PlayerMainPanel.");
            return;
        }

        Transform template = buffContent.Find("BuffIcon");
        if (template != null)
        {
            buffIconTemplate = template.gameObject;
            buffIconTemplate.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[PlayerMainBuffController] BuffIcon template not found under BuffContent.");
        }

        // BuffInfoPanel 是独立预制体，通过 UIMgr 按需加载（首次 hover 触发）
        TryBindToPlayer();
    }

    public void Tick()
    {
        if (boundComponent == null)
        {
            TryBindToPlayer();
        }
        // BuffInfoPanel 自身的 Update 会刷剩余时间，这里不需要再调
    }

    public void Dispose()
    {
        Unbind();
    }

    private void TryBindToPlayer()
    {
        BuffComponent comp = ResolvePlayerBuffComponent();
        if (comp == null || comp == boundComponent) return;

        Unbind();

        boundComponent = comp;
        boundComponent.OnBuffAdded += HandleBuffAdded;
        boundComponent.OnBuffRemoved += HandleBuffRemoved;

        IReadOnlyList<BuffInstance> existing = boundComponent.ActiveBuffs;
        for (int i = 0; i < existing.Count; i++)
        {
            HandleBuffAdded(existing[i]);
        }
    }

    private void Unbind()
    {
        if (boundComponent != null)
        {
            boundComponent.OnBuffAdded -= HandleBuffAdded;
            boundComponent.OnBuffRemoved -= HandleBuffRemoved;
            boundComponent = null;
        }

        foreach (var kv in iconByBuffID)
        {
            if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
        }
        iconByBuffID.Clear();

        hoveredIcon = null;
        infoPanel?.HideTooltip();
    }

    private void HandleBuffAdded(BuffInstance instance)
    {
        if (instance == null || buffContent == null || buffIconTemplate == null) return;

        int id = instance.Data.buffID;
        if (iconByBuffID.ContainsKey(id))
        {
            return;
        }

        GameObject go = Object.Instantiate(buffIconTemplate, buffContent);
        go.name = $"BuffIcon_{id}";
        go.SetActive(true);

        BuffIconUI iconUI = go.GetComponent<BuffIconUI>();
        if (iconUI == null) iconUI = go.AddComponent<BuffIconUI>();
        iconUI.Bind(instance, OnIconHover, OnIconExit);
        iconByBuffID[id] = iconUI;

        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.enabled = false;
            LoadIconSprite(img, instance.Data.iconName, instance);
        }
    }

    private void HandleBuffRemoved(BuffInstance instance)
    {
        if (instance == null) return;

        int id = instance.Data.buffID;
        if (!iconByBuffID.TryGetValue(id, out BuffIconUI iconUI)) return;

        if (iconUI == hoveredIcon)
        {
            hoveredIcon = null;
            infoPanel?.HideTooltip();
        }
        if (iconUI != null) Object.Destroy(iconUI.gameObject);
        iconByBuffID.Remove(id);
    }

    private void OnIconHover(BuffIconUI iconUI)
    {
        if (iconUI == null) return;
        hoveredIcon = iconUI;
        ShowInfoPanel(iconUI).Forget();
    }

    private void OnIconExit(BuffIconUI iconUI)
    {
        if (iconUI == null) return;
        if (hoveredIcon == iconUI)
        {
            hoveredIcon = null;
            infoPanel?.HideTooltip();
        }
    }

    private async UniTaskVoid ShowInfoPanel(BuffIconUI iconUI)
    {
        BuffInstance pending = iconUI != null ? iconUI.Instance : null;
        if (pending == null) return;

        if (infoPanel == null)
        {
            infoPanel = await GameMgr.UI.GetPanel<BuffInfoPanel>();
        }

        // await 期间鼠标可能已经移到别的 icon 或已退出 — 校验
        if (infoPanel == null || hoveredIcon != iconUI || iconUI.Instance != pending) return;

        infoPanel.ShowFor(pending, iconUI.Rect);
    }

    private async void LoadIconSprite(Image target, string iconName, BuffInstance owner)
    {
        if (target == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(iconName))
        {
            target.sprite = null;
            target.enabled = false;
            return;
        }

        Sprite sprite = await GameMgr.IconAtlas.GetBuffIcon(iconName);
        int id = owner != null ? owner.Data.buffID : -1;
        if (target == null || !iconByBuffID.TryGetValue(id, out BuffIconUI iconUI) || iconUI == null) return;
        if (iconUI.Instance != owner) return;

        target.sprite = sprite;
        target.enabled = sprite != null;
    }

    private static BuffComponent ResolvePlayerBuffComponent()
    {
        PlayerStateDriver player = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        return player != null ? player.GetComponent<BuffComponent>() : null;
    }
}
