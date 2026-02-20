using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class TipPanel : BasePanel
{
    public Transform UIEnsureButton;
    public Transform UIBackButton;
    public Transform UITipContent;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }
    public override void Init()
    {
        InitUIName();
        InitUIEvent();
    }
    private void InitUIName()
    {
        UIEnsureButton = transform.Find("EnsureButton");
        UIBackButton = transform.Find("BackButton");
        UITipContent = transform.Find("TipContent");
    }

    private void InitUIEvent()
    {
        UIEnsureButton.GetComponent<Button>().onClick.AddListener(OnClickEnsure);
        UIBackButton.GetComponent<Button>().onClick.AddListener(OnClickBack);
    }

    private System.Action onEnsureCallback;
    private System.Action onBackCallback;

    private void OnClickEnsure()
    {
        var callback = onEnsureCallback;
        onEnsureCallback = null; // 先清理引用防止重复触发
        callback?.Invoke();
        GameMgr.UI.HidePanel<TipPanel>();
    }
    private void OnClickBack()
    {
        var callback = onBackCallback;
        onBackCallback = null;
        callback?.Invoke();
        GameMgr.UI.HidePanel<TipPanel>();
    }

    public async Task ShowTip(string tip, System.Action onEnsure = null, System.Action onBack = null)
    {
        onEnsureCallback = onEnsure;
        onBackCallback = onBack;
        
        var textComp = UITipContent.GetComponent<Text>();
        if (textComp != null) textComp.text = tip;
        else
        {
            var tmpComp = UITipContent.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmpComp != null) tmpComp.text = tip;
        }

        await GameMgr.UI.ShowPanel<TipPanel>();
    }
}
