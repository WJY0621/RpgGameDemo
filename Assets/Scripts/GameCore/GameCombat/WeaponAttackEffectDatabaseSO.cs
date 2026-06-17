using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAttackEffectDatabase", menuName = "Data/GameCombat/Weapon Attack Effect Database")]
public sealed class WeaponAttackEffectDatabaseSO : ScriptableObject
{
    [SerializeField] private List<WeaponAttackEffectSO> effects = new List<WeaponAttackEffectSO>();

    private Dictionary<string, WeaponAttackEffectSO> byWeaponName;
    private Dictionary<string, WeaponAttackEffectSO> byWeaponId;

    public IReadOnlyList<WeaponAttackEffectSO> Effects => effects;

    public WeaponAttackEffectSO GetEffect(WeaponItem weapon)
    {
        if (weapon == null)
        {
            return null;
        }

        WeaponAttackEffectSO effect = null;
        if (!string.IsNullOrWhiteSpace(weapon.name))
        {
            effect = GetEffectByWeaponName(weapon.name);
        }

        if (effect == null && weapon.id > 0)
        {
            effect = GetEffectByWeaponId(weapon.id.ToString());
        }

        if (effect == null && !string.IsNullOrWhiteSpace(weapon.modelName))
        {
            effect = GetEffectByWeaponName(weapon.modelName);
        }

        return effect;
    }

    public WeaponAttackEffectSO GetEffectByWeaponName(string weaponName)
    {
        EnsureMaps();
        return !string.IsNullOrWhiteSpace(weaponName) && byWeaponName.TryGetValue(weaponName.Trim(), out WeaponAttackEffectSO effect)
            ? effect
            : null;
    }

    public WeaponAttackEffectSO GetEffectByWeaponId(string weaponId)
    {
        EnsureMaps();
        return !string.IsNullOrWhiteSpace(weaponId) && byWeaponId.TryGetValue(weaponId.Trim(), out WeaponAttackEffectSO effect)
            ? effect
            : null;
    }

#if UNITY_EDITOR
    public void SetEffects(IEnumerable<WeaponAttackEffectSO> newEffects)
    {
        effects.Clear();
        if (newEffects != null)
        {
            effects.AddRange(newEffects);
        }

        effects.RemoveAll(effect => effect == null);
        effects.Sort((a, b) => string.Compare(a.weaponName, b.weaponName, StringComparison.Ordinal));
        byWeaponName = null;
        byWeaponId = null;
    }
#endif

    private void OnValidate()
    {
        byWeaponName = null;
        byWeaponId = null;
    }

    private void EnsureMaps()
    {
        if (byWeaponName != null && byWeaponId != null)
        {
            return;
        }

        byWeaponName = new Dictionary<string, WeaponAttackEffectSO>(StringComparer.OrdinalIgnoreCase);
        byWeaponId = new Dictionary<string, WeaponAttackEffectSO>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < effects.Count; i++)
        {
            WeaponAttackEffectSO effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(effect.weaponName))
            {
                byWeaponName[effect.weaponName.Trim()] = effect;
            }

            if (!string.IsNullOrWhiteSpace(effect.weaponId))
            {
                byWeaponId[effect.weaponId.Trim()] = effect;
            }
        }
    }
}
