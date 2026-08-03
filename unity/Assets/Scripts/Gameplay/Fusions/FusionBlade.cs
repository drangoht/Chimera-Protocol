using UnityEngine;

/// <summary>
/// Lame de Fusion — évolution de <see cref="PlasmaBlade"/> : arc complet et bien plus large
/// (130 contre 80), donc une arme de nettoyage là où la lame était une arme de front.
/// </summary>
public sealed class FusionBlade : PlasmaBlade
{
    protected override void Awake()
    {
        ArcAngleDeg = 360f;   // plus d'angle mort : la fusion frappe tout autour
        ArcRadius = 130f;
        base.Awake();
    }
}
