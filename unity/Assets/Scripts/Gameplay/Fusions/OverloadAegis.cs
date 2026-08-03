using UnityEngine;

/// <summary>
/// Égide de Surcharge — évolution de <see cref="OverloadField"/> : rayon doublé et cadence
/// nettement resserrée (1 s contre 2,5), ce qui la fait passer d'une impulsion ponctuelle à une
/// zone d'exclusion permanente autour du porteur.
/// </summary>
public sealed class OverloadAegis : OverloadField
{
    protected override void Awake()
    {
        Radius = 200f;
        BaseDamage = 18f;
        BaseCooldown = 1.0f;
        Knockback = 70f;
        base.Awake();
    }
}
