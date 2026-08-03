using UnityEngine;

/// <summary>
/// Essaim de Frelons — évolution de <see cref="SeekerSwarm"/> : sept missiles par salve au lieu de
/// deux, répartis sur autant de cibles. Une salve nettoie une vague entière.
/// </summary>
public sealed class HornetSwarm : SeekerSwarm
{
    protected override void Awake()
    {
        MissileCount = 7;
        BaseDamage = 12f;
        BaseCooldown = 0.7f;
        ProjectileSpeed = 380f;
        base.Awake();
    }
}
