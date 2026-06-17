using UnityEngine;

public struct AttackDamageInfo
{
    public int damage;
    public Vector3 hitPoint;
    public Vector3 hitDirection;
    public GameObject attacker;

    public AttackDamageInfo(int damage, Vector3 hitPoint, Vector3 hitDirection, GameObject attacker)
    {
        this.damage = damage;
        this.hitPoint = hitPoint;
        this.hitDirection = hitDirection;
        this.attacker = attacker;
    }
}
