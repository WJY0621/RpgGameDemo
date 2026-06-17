using UnityEngine;

[DisallowMultipleComponent]
public class BossBattleTrigger : MonoBehaviour
{
    [SerializeField] private BossController boss;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private GameObject[] arenaLocks;
    [SerializeField] private bool disableTriggerAfterStart = true;

    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (boss == null)
        {
            boss = GetComponentInParent<BossController>();
        }

        SetArenaLocks(false);
    }

    private void OnEnable()
    {
        if (boss != null)
        {
            boss.OnBattleEnded -= HandleBattleEnded;
            boss.OnBattleEnded += HandleBattleEnded;
        }
    }

    private void OnDisable()
    {
        if (boss != null)
        {
            boss.OnBattleEnded -= HandleBattleEnded;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (boss == null || !IsPlayerCollider(other))
        {
            return;
        }

        boss.SetTarget(other.transform.root);
        SetArenaLocks(true);
        boss.StartBattle();

        if (disableTriggerAfterStart && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag) || other.GetComponentInParent<PlayerStateDriver>() != null)
        {
            return true;
        }

        return GameMgr.Instance != null && GameMgr.Instance.Player != null
            && other.transform.IsChildOf(GameMgr.Instance.Player.transform);
    }

    private void HandleBattleEnded(BossController _, bool bossDefeated)
    {
        SetArenaLocks(false);
    }

    private void SetArenaLocks(bool active)
    {
        if (arenaLocks == null)
        {
            return;
        }

        for (int i = 0; i < arenaLocks.Length; i++)
        {
            if (arenaLocks[i] != null)
            {
                arenaLocks[i].SetActive(active);
            }
        }
    }
}
