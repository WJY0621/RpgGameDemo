using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private BossController boss;
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text nameText;
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideWhenInactive = true;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>(true);
        }

        if (boss != null && bossHealth == null)
        {
            bossHealth = boss.Health;
        }

        canvasGroup = root != null ? root.GetComponent<CanvasGroup>() : null;
        if (root != null && canvasGroup == null)
        {
            canvasGroup = root.AddComponent<CanvasGroup>();
        }

        SetVisible(!hideWhenInactive);
        Refresh();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void SetBoss(BossController targetBoss)
    {
        Unbind();
        boss = targetBoss;
        bossHealth = boss != null ? boss.Health : null;
        Bind();
        Refresh();
    }

    private void Bind()
    {
        if (boss != null)
        {
            boss.OnBattleStarted -= HandleBattleStarted;
            boss.OnBattleStarted += HandleBattleStarted;
            boss.OnBattleEnded -= HandleBattleEnded;
            boss.OnBattleEnded += HandleBattleEnded;
        }

        if (bossHealth != null)
        {
            bossHealth.OnHealthChanged -= HandleHealthChanged;
            bossHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void Unbind()
    {
        if (boss != null)
        {
            boss.OnBattleStarted -= HandleBattleStarted;
            boss.OnBattleEnded -= HandleBattleEnded;
        }

        if (bossHealth != null)
        {
            bossHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleBattleStarted(BossController controller)
    {
        SetVisible(true);
        Refresh();
    }

    private void HandleBattleEnded(BossController controller, bool bossDefeated)
    {
        if (hideWhenInactive)
        {
            SetVisible(false);
        }
    }

    private void HandleHealthChanged(BossHealth health)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
            hpSlider.SetValueWithoutNotify(bossHealth != null ? bossHealth.NormalizedHP : 0f);
        }

        if (nameText != null && boss != null && boss.Config != null)
        {
            nameText.text = boss.Config.bossName;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (root != null && root != gameObject)
        {
            root.SetActive(visible);
        }
    }
}
