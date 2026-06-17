using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainBossLifeBarController
{
    private readonly Transform panelRoot;

    private Transform bossContentRoot;
    private Slider bossLifeSlider;
    private Image bossLifeFillImage;
    private TMP_Text tmpBossNameText;
    private Text legacyBossNameText;
    private BossController boundBoss;
    private BossHealth boundHealth;

    public PlayerMainBossLifeBarController(Transform panelRoot)
    {
        this.panelRoot = panelRoot;
    }

    public void Init()
    {
        bossContentRoot = panelRoot.Find("BossContent");
        if (bossContentRoot == null)
        {
            return;
        }

        Transform lifeBarRoot = bossContentRoot.Find("BossLifebar");
        if (lifeBarRoot != null)
        {
            bossLifeSlider = lifeBarRoot.GetComponent<Slider>();
            if (bossLifeSlider == null)
            {
                bossLifeSlider = lifeBarRoot.GetComponentInChildren<Slider>(true);
            }

            bossLifeFillImage = lifeBarRoot.GetComponent<Image>();
            if (bossLifeFillImage == null)
            {
                bossLifeFillImage = lifeBarRoot.GetComponentInChildren<Image>(true);
            }
        }

        if (bossLifeSlider != null)
        {
            bossLifeSlider.minValue = 0f;
            bossLifeSlider.maxValue = 1f;
            bossLifeSlider.interactable = false;
            bossLifeSlider.transition = Selectable.Transition.None;
            bossLifeSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        Transform nameRoot = bossContentRoot.Find("BossName");
        if (nameRoot != null)
        {
            tmpBossNameText = nameRoot.GetComponent<TMP_Text>();
            legacyBossNameText = nameRoot.GetComponent<Text>();
        }

        BossController.OnAnyBattleStarted -= HandleBossBattleStarted;
        BossController.OnAnyBattleStarted += HandleBossBattleStarted;
        BossController.OnAnyBattleEnded -= HandleBossBattleEnded;
        BossController.OnAnyBattleEnded += HandleBossBattleEnded;

        SetVisible(false);
    }

    public void Tick()
    {
        if (bossContentRoot == null)
        {
            return;
        }

        if (boundBoss == null || !boundBoss.IsBattleActive)
        {
            BossController activeBoss = FindActiveBoss();
            if (activeBoss != null)
            {
                BindBoss(activeBoss);
            }
            else
            {
                UnbindBoss();
                SetVisible(false);
            }
        }
    }

    public void Dispose()
    {
        BossController.OnAnyBattleStarted -= HandleBossBattleStarted;
        BossController.OnAnyBattleEnded -= HandleBossBattleEnded;
        UnbindBoss();
    }

    private void HandleBossBattleStarted(BossController boss)
    {
        BindBoss(boss);
    }

    private void HandleBossBattleEnded(BossController boss, bool bossDefeated)
    {
        if (boss == boundBoss)
        {
            UnbindBoss();
            SetVisible(false);
        }
    }

    private void BindBoss(BossController boss)
    {
        if (boss == null || boss == boundBoss)
        {
            return;
        }

        UnbindBoss();
        boundBoss = boss;
        boundHealth = boss.Health;
        if (boundHealth != null)
        {
            boundHealth.OnHealthChanged -= HandleBossHealthChanged;
            boundHealth.OnHealthChanged += HandleBossHealthChanged;
        }

        SetText(boundBoss.DisplayName);
        RefreshHealth();
        SetVisible(true);
    }

    private void UnbindBoss()
    {
        if (boundHealth != null)
        {
            boundHealth.OnHealthChanged -= HandleBossHealthChanged;
        }

        boundBoss = null;
        boundHealth = null;
    }

    private void HandleBossHealthChanged(BossHealth health)
    {
        RefreshHealth();
        if (health != null && health.IsDead)
        {
            SetVisible(false);
        }
    }

    private void RefreshHealth()
    {
        float normalized = boundHealth != null ? Mathf.Clamp01(boundHealth.NormalizedHP) : 0f;
        if (bossLifeSlider != null)
        {
            bossLifeSlider.SetValueWithoutNotify(normalized);
        }

        if (bossLifeFillImage != null)
        {
            bossLifeFillImage.fillAmount = normalized;
        }
    }

    private BossController FindActiveBoss()
    {
        BossController[] bosses = Object.FindObjectsOfType<BossController>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossController boss = bosses[i];
            if (boss != null && boss.isActiveAndEnabled && boss.IsBattleActive)
            {
                return boss;
            }
        }

        return null;
    }

    private void SetVisible(bool visible)
    {
        if (bossContentRoot != null && bossContentRoot.gameObject.activeSelf != visible)
        {
            bossContentRoot.gameObject.SetActive(visible);
        }
    }

    private void SetText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "Boss";
        }

        if (tmpBossNameText != null)
        {
            tmpBossNameText.text = value;
        }

        if (legacyBossNameText != null)
        {
            legacyBossNameText.text = value;
        }
    }
}
