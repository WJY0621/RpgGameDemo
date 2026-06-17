using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainFlyBarController
{
    private readonly Transform root;

    private Transform flyBarRoot;
    private Slider flyBarSlider;

    public PlayerMainFlyBarController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init()
    {
        flyBarRoot = root != null ? root.Find("FlyBar") : null;
        if (flyBarRoot == null)
        {
            return;
        }

        flyBarSlider = flyBarRoot.GetComponent<Slider>();
        if (flyBarSlider == null)
        {
            flyBarSlider = flyBarRoot.GetComponentInChildren<Slider>(true);
        }

        if (flyBarSlider != null)
        {
            flyBarSlider.minValue = 0f;
            flyBarSlider.maxValue = 1f;
            flyBarSlider.interactable = false;
            flyBarSlider.transition = Selectable.Transition.None;
            flyBarSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            flyBarSlider.SetValueWithoutNotify(0f);
        }

        SetVisible(false);
    }

    public void Tick()
    {
        PlayerStateDriver player = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        PlayerContext ctx = player != null ? player.ctx : null;
        bool visible = ctx != null && ctx.flyUnlocked;
        SetVisible(visible);

        if (!visible || flyBarSlider == null)
        {
            return;
        }

        float maxEnergy = Mathf.Max(0f, ctx.maxFlightEnergy);
        float normalized = maxEnergy <= 0f ? 0f : Mathf.Clamp01(ctx.currentFlightEnergy / maxEnergy);
        flyBarSlider.SetValueWithoutNotify(normalized);
    }

    public void Dispose()
    {
    }

    private void SetVisible(bool visible)
    {
        if (flyBarRoot == null || flyBarRoot.gameObject.activeSelf == visible)
        {
            return;
        }

        flyBarRoot.gameObject.SetActive(visible);
    }
}
