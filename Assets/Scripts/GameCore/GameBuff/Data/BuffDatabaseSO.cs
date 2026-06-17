using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Buff/BuffDatabase", fileName = "BuffDatabase")]
public class BuffDatabaseSO : ScriptableObject
{
    public List<BuffData> buffs = new List<BuffData>();

    public BuffData GetBuff(int buffID)
    {
        if (buffs == null)
        {
            return null;
        }

        for (int i = 0; i < buffs.Count; i++)
        {
            BuffData data = buffs[i];
            if (data != null && data.buffID == buffID)
            {
                return data;
            }
        }

        return null;
    }
}
