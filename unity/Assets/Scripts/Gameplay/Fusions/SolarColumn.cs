using UnityEngine;

/// <summary>
/// Colonne Solaire — évolution de <see cref="PyreStream"/> : la brûlure triple (18 PV/s contre 6)
/// et devient l'essentiel des dégâts. L'arme entretient un feu au lieu de frapper.
/// </summary>
public sealed class SolarColumn : PyreStream
{
    protected override void Awake()
    {
        BaseDamage = 10f;
        BaseCooldown = 0.7f;
        BurnDps = 18f;
        BurnDuration = 3.0f;
        ConeAngle = 70f;
        Range = 190f;
        base.Awake();
    }
}
