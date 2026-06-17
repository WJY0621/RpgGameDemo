using UnityEngine;

public class BuffMgr
{
    private const string ResourcePath = "Buff/BuffDatabase";

    private BuffDatabaseSO database;
    private bool isInitialized;

    public BuffDatabaseSO Database
    {
        get
        {
            EnsureInitialized();
            return database;
        }
    }

    public void Init()
    {
        EnsureInitialized();
    }

    public void Reload()
    {
        isInitialized = false;
        database = null;
        EnsureInitialized();
    }

    public BuffData GetBuffData(int buffID)
    {
        EnsureInitialized();
        return database != null ? database.GetBuff(buffID) : null;
    }

    /// <summary>
    /// 给目标施加 buff。target 必须挂有 BuffComponent；caster 用于伤害归属，可为 null。
    /// </summary>
    public BuffInstance Apply(GameObject target, int buffID, GameObject caster = null)
    {
        if (target == null)
        {
            return null;
        }

        BuffData data = GetBuffData(buffID);
        if (data == null)
        {
            Debug.LogWarning($"[BuffMgr] BuffData not found: id={buffID}");
            return null;
        }

        BuffComponent buffComponent = target.GetComponent<BuffComponent>();
        if (buffComponent == null)
        {
            Debug.LogWarning($"[BuffMgr] Target has no BuffComponent: {target.name}");
            return null;
        }

        return buffComponent.Apply(data, caster);
    }

    public bool Remove(GameObject target, int buffID)
    {
        if (target == null)
        {
            return false;
        }

        BuffComponent buffComponent = target.GetComponent<BuffComponent>();
        return buffComponent != null && buffComponent.Remove(buffID);
    }

    public bool HasBuff(GameObject target, int buffID)
    {
        if (target == null)
        {
            return false;
        }

        BuffComponent buffComponent = target.GetComponent<BuffComponent>();
        return buffComponent != null && buffComponent.HasBuff(buffID);
    }

    internal void RaiseBuffAdded(BuffComponent component, BuffInstance instance)
    {
        // UI 后续接 EventMgr 时直接订阅 BuffComponent.OnBuffAdded
    }

    internal void RaiseBuffRemoved(BuffComponent component, BuffInstance instance)
    {
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        isInitialized = true;
        database = Resources.Load<BuffDatabaseSO>(ResourcePath);
        if (database == null)
        {
            Debug.LogWarning($"[BuffMgr] BuffDatabase not found at Resources/{ResourcePath}.asset. Open Tools/Game Buff/Buff Editor to create one.");
        }
    }
}
