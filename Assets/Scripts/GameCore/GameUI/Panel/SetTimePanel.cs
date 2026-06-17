using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 设置时间面板（prefab 名 SetTimePanel，Addressable 地址也叫 SetTimePanel）。
/// 结构：根/BK/(12Button, 24Button, CloseButton)。
/// 关键点：12/24 按钮的"功能"不写在 C#，而是转发给 Lua 的 OnTimeButton(which)，
/// 这样按钮逻辑可热更（基线空实现，热更后才真正改时间）。
/// </summary>
public class SetTimePanel : BasePanel
{
    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        Bind("12Button", () => CallLua("noon"));   // 12 点 = 正午
        Bind("24Button", () => CallLua("night"));  // 24 点 = 午夜/凌晨
        Bind("CloseButton", () => GameMgr.UI.HidePanel<SetTimePanel>());
    }

    // 把按钮点击转发给 lua，功能完全由 lua 决定（可热更）
    private void CallLua(string which)
    {
        GameMgr.Lua?.DoString(
            $"if OnTimeButton then OnTimeButton('{which}') else print('[Lua] OnTimeButton 未定义') end");
    }

    private void Bind(string buttonName, UnityAction onClick)
    {
        Transform t = FindDeepChild(transform, buttonName);
        Button button = t != null ? t.GetComponent<Button>() : null;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
