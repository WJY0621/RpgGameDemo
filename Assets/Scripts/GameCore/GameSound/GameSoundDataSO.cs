using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/GameSound/GameSoundData", fileName = "GameSoundData")]
public class GameSoundDataSO : ScriptableObject
{
    public List<GameSoundGroupDataSO> gameSoundGroups = new List<GameSoundGroupDataSO>();
}
