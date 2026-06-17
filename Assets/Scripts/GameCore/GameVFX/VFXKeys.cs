/// <summary>
/// 全局特效 Key 常量表。
/// 新增特效时在此处添加对应常量，避免代码中出现魔术字符串。
/// Key 与 VFXConfigSO 中的 entry.key 一一对应。
/// </summary>
public static class VFXKeys
{
    // ── 攻击 ──────────────────────────────────────────────
    public const string AttackSlash  = "AttackSlash";
    public const string AttackHit    = "AttackHit";

    // ── 受击 ──────────────────────────────────────────────
    public const string MonsterHit   = "MonsterHit";
    public const string PlayerHit    = "PlayerHit";

    // ── 物品 ──────────────────────────────────────────────
    public const string ItemDrop     = "ItemDrop";
    public const string ItemPickup   = "ItemPickup";

    // ── 技能 ──────────────────────────────────────────────
    public const string SkillCast    = "SkillCast";
}
