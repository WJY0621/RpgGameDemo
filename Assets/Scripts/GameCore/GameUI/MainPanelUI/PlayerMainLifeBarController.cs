using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainLifeBarController
{
    private readonly Transform root;

    private Slider lifeBarSlider;
    private PlayerHealth boundPlayerHealth;

    public PlayerMainLifeBarController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init()
    {
        Transform lifeBarRoot = root.Find("Lifebar");
        if (lifeBarRoot != null)
        {
            lifeBarSlider = lifeBarRoot.GetComponent<Slider>();
            if (lifeBarSlider == null)
            {
                lifeBarSlider = lifeBarRoot.GetComponentInChildren<Slider>(true);
            }
        }

        if (lifeBarSlider != null)
        {
            lifeBarSlider.minValue = 0f;
            lifeBarSlider.maxValue = 1f;
            lifeBarSlider.interactable = false;
            lifeBarSlider.transition = Selectable.Transition.None;
            lifeBarSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            RefreshFromPlayerData();
        }
    }

    public void Tick()
    {
        TryBindPlayerHealth();
    }

    public void Dispose()
    {
        if (boundPlayerHealth == null)
        {
            return;
        }

        boundPlayerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
        boundPlayerHealth = null;
    }

    private void TryBindPlayerHealth()
    {
        if (boundPlayerHealth != null)
        {
            return;
        }

        if (GameMgr.Instance == null || GameMgr.Instance.Player == null)
        {
            RefreshFromPlayerData();
            return;
        }

        PlayerHealth playerHealth = GameMgr.Instance.Player.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        boundPlayerHealth = playerHealth;
        boundPlayerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
        boundPlayerHealth.OnHealthChanged += HandlePlayerHealthChanged;
        HandlePlayerHealthChanged(boundPlayerHealth);
    }

    private void HandlePlayerHealthChanged(PlayerHealth health)
    {
        if (lifeBarSlider == null)
        {
            return;
        }

        if (health == null)
        {
            RefreshFromPlayerData();
            return;
        }

        if (!health.IsInitialized || health.MaxHP <= 0)
        {
            RefreshFromPlayerData();
            return;
        }

        lifeBarSlider.SetValueWithoutNotify(health.NormalizedHP);
    }

    private void RefreshFromPlayerData()
    {
        if (lifeBarSlider == null || GameMgr.Instance == null || GameMgr.Instance.playerData == null)
        {
            return;
        }

        PlayerData data = GameMgr.Instance.playerData;
        float normalized = data.GetMaxHP() <= 0 ? 0f : (float)data.currentHP / data.GetMaxHP();
        lifeBarSlider.SetValueWithoutNotify(normalized);
    }
}
