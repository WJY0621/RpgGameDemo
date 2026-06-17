using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterPoolConfig", menuName = "Game/Monster Pool Config")]
public class MonsterPoolConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("MonsterData.json 中对应的 monsterID")]
        public int monsterID;

        [Tooltip("怪物 Prefab")]
        public GameObject prefab;

        [Tooltip("场景加载时预热的数量")]
        [Min(0)] public int initialSize = 3;

        [Tooltip("池子允许存活的最大数量（含场景中的活跃个体）")]
        [Min(1)] public int maxSize = 10;

        [Tooltip("死亡动画播放完毕后延迟多久回池（秒），应 >= MonsterHealth.destroyDelay）")]
        [Min(0f)] public float returnDelay = 3f;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry GetEntry(int monsterID)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].monsterID == monsterID)
                return entries[i];
        }
        return null;
    }
}
