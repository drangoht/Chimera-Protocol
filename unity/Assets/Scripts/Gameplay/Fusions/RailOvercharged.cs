using UnityEngine;

/// <summary>
/// Rail Surchargé — évolution de <see cref="ImpulseCannon"/> : cadence doublée et projectiles
/// <b>perforants</b>, qui traversent la file au lieu de s'arrêter au premier corps.
/// </summary>
public sealed class RailOvercharged : ImpulseCannon
{
    protected override void Awake()
    {
        BaseDamage = 22f;
        BaseCooldown = 0.6f;
        BulletSpeed = 800f;
        Piercing = true;
        base.Awake();
    }
}
