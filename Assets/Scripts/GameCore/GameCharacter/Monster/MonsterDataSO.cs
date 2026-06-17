using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Data/GameCharacter/Monster/Monster Data")]
public class MonsterDataSO : ScriptableObject
{
    public List<MonsterData> monsterList = new List<MonsterData>();

    public MonsterData GetMonsterData(int monsterID)
    {
        return monsterList.Find(data => data != null && data.monsterID == monsterID);
    }

    public MonsterRuntime CreateRuntime(int monsterID)
    {
        MonsterData data = GetMonsterData(monsterID);
        return data != null ? data.ToRuntime() : null;
    }
}

