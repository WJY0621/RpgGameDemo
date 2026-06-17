using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponTrailEnableMode
{
    AttackOnly = 0,
    AlwaysWhenEquipped = 1
}

[CreateAssetMenu(fileName = "WeaponTrailConfig", menuName = "Data/GameVFX/Weapon Trail Config")]
public class WeaponTrailConfigSO : ScriptableObject
{
    [SerializeField] private WeaponTrailConfigEntry defaultConfig = new WeaponTrailConfigEntry
    {
        weaponModelName = "Default",
        enableMode = WeaponTrailEnableMode.AttackOnly
    };

    [SerializeField] private List<WeaponTrailConfigEntry> entries = new List<WeaponTrailConfigEntry>();

    private Dictionary<string, WeaponTrailConfigEntry> entryMap;

    public WeaponTrailConfigEntry GetConfig(string weaponModelName)
    {
        if (entryMap == null)
        {
            BuildMap();
        }

        if (!string.IsNullOrWhiteSpace(weaponModelName) &&
            entryMap.TryGetValue(weaponModelName, out WeaponTrailConfigEntry entry))
        {
            return entry;
        }

        return defaultConfig;
    }

    private void BuildMap()
    {
        entryMap = new Dictionary<string, WeaponTrailConfigEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            WeaponTrailConfigEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.weaponModelName))
            {
                continue;
            }

            entryMap[entry.weaponModelName] = entry;
        }
    }

    private void OnValidate()
    {
        entryMap = null;
    }
}

[Serializable]
public class WeaponTrailConfigEntry
{
    [Tooltip("对应 WeaponItem.modelName，例如 WeaponModel_01")]
    public string weaponModelName;

    public WeaponTrailEnableMode enableMode = WeaponTrailEnableMode.AttackOnly;
    public Material trailMaterial;
    public Gradient trailGradient = new Gradient();
}
