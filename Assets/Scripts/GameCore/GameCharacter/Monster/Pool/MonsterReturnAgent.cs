using System.Collections;
using UnityEngine;

/// <summary>
/// 挂载在怪物身上，监听死亡事件并在延迟后将其归还对象池。
/// 由 MonsterPool 在创建时动态添加并绑定，无需手动挂载。
/// </summary>
[DisallowMultipleComponent]
public class MonsterReturnAgent : MonoBehaviour
{
    private MonsterPool pool;
    private MonsterController controller;
    private MonsterHealth health;
    private float returnDelay;
    private Coroutine returnRoutine;

    public void Bind(MonsterPool pool, MonsterController controller, float returnDelay)
    {
        this.pool = pool;
        this.controller = controller;
        this.returnDelay = returnDelay;
        health = controller.Health;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }

    private void HandleDied(MonsterHealth h)
    {
        if (returnRoutine == null)
            returnRoutine = StartCoroutine(ReturnAfterDelay());
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        returnRoutine = null;
        pool?.Return(controller);
    }
}
