using System;
using UnityEngine;

public enum WeaponProjectileHitShape
{
    Sphere = 0,
    Cylinder = 1
}

public enum WeaponProjectileMotionMode
{
    CodeDriven = 0,
    VisualSelfMotion = 1
}

[Serializable]
public class WeaponProjectileConfigEntry
{
    public string weaponModelName;
    public string projectileVFXKey;
    public string hitVFXKey;
    public Vector3 hitVFXRandomEulerRange;
    [Min(0f)] public float damageMultiplier = 1f;
    public WeaponProjectileMotionMode motionMode = WeaponProjectileMotionMode.CodeDriven;
    [Min(0f)] public float speed = 12f;
    [Min(0.01f)] public float lifeTime = 2f;
    [Min(0f)] public float visualForwardDistance = 6f;
    public AnimationCurve visualForwardCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public WeaponProjectileHitShape hitShape = WeaponProjectileHitShape.Sphere;
    [Min(0.01f)] public float hitRadius = 0.35f;
    [Min(0.01f)] public float hitHeight = 1f;
    public Vector3 hitCenterOffset = Vector3.zero;
    [Min(0)] public int pierceCount;
    public Vector3 spawnOffset;
    public bool usePitchDirection;
}
