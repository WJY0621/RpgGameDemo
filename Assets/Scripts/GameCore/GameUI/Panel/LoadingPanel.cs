using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingPanel : BasePanel
{
    private static readonly string[] DefaultTips =
    {
        "按B键打开背包",
        "按T键打开建造系统",
        "如果你觉得不知道做些什么，可以去砍树",
        "矿洞中存在蕴藏着金属的矿石，想获得它们你需要用到镐子",
        "跟旺旺勇士对话，他可能会向你出售一些物品",
        "通过床你可以消磨一些时间",
        "打开武器制作台去制作一些武器吧",
        "在野外探索时或许能够找到一些意想不到的东西"
    };

    private const float TipSwitchInterval = 4f;

    private TMP_Text scheduleTmpText;
    private Text scheduleText;
    private TMP_Text tipTmpText;
    private Text tipText;

    private float progress;
    private float tipTimer;

    public override void Init()
    {
        BindText(transform.Find("ScheduleText"), out scheduleTmpText, out scheduleText);
        BindText(transform.Find("TipText"), out tipTmpText, out tipText);

        SetProgress(0f);
        RefreshTip();
    }

    public override void Show()
    {
        transform.SetAsLastSibling();
        base.Show();
        tipTimer = 0f;
        SetProgress(0f);
        RefreshTip();
    }

    public override void Hide(UnityEngine.Events.UnityAction callBack = null)
    {
        SetProgress(1f);
        base.Hide(callBack);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsShow)
        {
            return;
        }

        KeepOnTop();

        tipTimer += Time.unscaledDeltaTime;
        if (tipTimer >= TipSwitchInterval)
        {
            tipTimer = 0f;
            RefreshTip();
        }
    }

    public void ApplyProgress(LoadingProgress loadingProgress)
    {
        SetProgress(loadingProgress.Progress);
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        int percent = Mathf.RoundToInt(progress * 100f);
        SetText(scheduleTmpText, scheduleText, $"{percent}%");

    }

    public void SetTip(string tip)
    {
        if (string.IsNullOrWhiteSpace(tip))
        {
            RefreshTip();
            return;
        }

        SetText(tipTmpText, tipText, tip);
    }

    public void ShowFailure(string errorMessage)
    {
        SetText(scheduleTmpText, scheduleText, "Failed");
        SetTip(string.IsNullOrWhiteSpace(errorMessage) ? "Loading failed." : errorMessage);
    }

    private void RefreshTip()
    {
        if (DefaultTips.Length == 0)
        {
            return;
        }

        int index = Random.Range(0, DefaultTips.Length);
        SetTip(DefaultTips[index]);
    }

    private void BindText(Transform target, out TMP_Text tmpText, out Text legacyText)
    {
        tmpText = target != null ? target.GetComponent<TMP_Text>() : null;
        legacyText = target != null ? target.GetComponent<Text>() : null;
    }

    private void SetText(TMP_Text tmpText, Text legacyText, string content)
    {
        if (tmpText != null)
        {
            tmpText.text = content;
        }

        if (legacyText != null)
        {
            legacyText.text = content;
        }
    }

    private void KeepOnTop()
    {
        if (transform.parent != null && transform.GetSiblingIndex() != transform.parent.childCount - 1)
        {
            transform.SetAsLastSibling();
        }
    }
}
