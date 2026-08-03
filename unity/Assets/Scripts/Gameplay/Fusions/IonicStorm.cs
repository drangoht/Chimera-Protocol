using UnityEngine;

/// <summary>
/// Tempête Ionique — évolution de <see cref="TeslaCoil"/> : trois fois plus de rebonds et une
/// cadence triplée. La chaîne cesse d'être un appoint pour devenir le cœur du DPS.
/// </summary>
public sealed class IonicStorm : TeslaCoil
{
    protected override void Awake()
    {
        BaseDamage = 20f;
        BaseCooldown = 0.4f;
        ChainCount = 6;
        ChainRange = 200f;
        base.Awake();
    }
}
