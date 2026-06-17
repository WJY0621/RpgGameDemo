using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAttackController : MonoBehaviour
{
    [Header("Damage")]
    public int baseDamage = 10;

    [Header("Safety")]
    [SerializeField] private float maxAttackWindowDuration = 0.75f;

    private bool attackWindowOpen;
    private float attackWindowOpenedTime;

    public event System.Action AttackWindowBegan;
    public event System.Action AttackWindowEnded;
    public event System.Action AttackDamageApplied;

    public bool IsAttackWindowOpen => attackWindowOpen;

    public void BeginAttackWindow()
    {
        attackWindowOpen = true;
        attackWindowOpenedTime = Time.time;
        AttackWindowBegan?.Invoke();
    }

    public void EndAttackWindow()
    {
        if (!attackWindowOpen)
        {
            return;
        }

        attackWindowOpen = false;
        AttackWindowEnded?.Invoke();
    }

    public void CancelAttackWindow()
    {
        EndAttackWindow();
    }

    private void Update()
    {
        if (!attackWindowOpen)
        {
            return;
        }

        if (Time.time - attackWindowOpenedTime >= maxAttackWindowDuration)
        {
            EndAttackWindow();
        }
    }

    private void OnDisable()
    {
        if (attackWindowOpen)
        {
            EndAttackWindow();
        }
    }

    public void ApplyAttackDamage()
    {
        if (!attackWindowOpen)
        {
            return;
        }

        AttackDamageApplied?.Invoke();
    }

    public int GetCurrentAttackDamage()
    {
        return GetAttackDamage();
    }

    private int GetAttackDamage()
    {
        if (GameMgr.Instance != null && GameMgr.Instance.playerData != null)
        {
            return Mathf.Max(1, GameMgr.Instance.playerData.GetATK());
        }

        return Mathf.Max(1, baseDamage);
    }
}
