using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家死亡面板。显示死亡提示并提供复活按钮（ContinueButton）。
/// 点击复活按钮后调用 <see cref="PlayerDeathController.ReviveAsync"/> 执行复活。
/// 面板 Prefab 的 Addressables key 必须与类名一致：PlayerDeadPanel。
/// </summary>
public class PlayerDeadPanel : BasePanel
{
    private Transform UIContinueButton;
    private PlayerDeathController deathController;
    private bool isReviving;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        UIContinueButton = transform.Find("ContinueButton");

        Button button = UIContinueButton != null ? UIContinueButton.GetComponent<Button>() : null;
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickContinue);
            button.onClick.AddListener(OnClickContinue);
        }
    }

    /// <summary>
    /// 绑定触发死亡的玩家控制器，复活时优先使用它。
    /// </summary>
    public void Bind(PlayerDeathController controller)
    {
        deathController = controller;
    }

    private void OnClickContinue()
    {
        if (isReviving)
        {
            return;
        }

        isReviving = true;
        ReviveAsync().Forget();
    }

    private async UniTaskVoid ReviveAsync()
    {
        try
        {
            PlayerDeathController controller = ResolveController();
            if (controller != null)
            {
                await controller.ReviveAsync();
            }
            else
            {
                Debug.LogWarning("[PlayerDeadPanel] 找不到 PlayerDeathController，无法复活。");
            }
        }
        finally
        {
            isReviving = false;
        }
    }

    private PlayerDeathController ResolveController()
    {
        if (deathController != null)
        {
            return deathController;
        }

        if (GameMgr.Instance != null && GameMgr.Instance.Player != null)
        {
            return GameMgr.Instance.Player.GetComponent<PlayerDeathController>();
        }

        return null;
    }
}
