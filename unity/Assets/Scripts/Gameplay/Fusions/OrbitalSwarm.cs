using UnityEngine;

/// <summary>
/// Essaim Orbital — évolution de <see cref="DroneSwarm"/> : trois fois plus de drones, sur une
/// orbite plus large. La couverture devient continue au lieu d'intermittente.
/// </summary>
public sealed class OrbitalSwarm : DroneSwarm
{
    protected override void Awake()
    {
        DroneCount = 6;
        OrbitRadius = 95f;
        BaseDamage = 24f;
        DamageInterval = 0.4f;
        base.Awake();
    }
}
