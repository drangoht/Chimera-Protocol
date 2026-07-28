using System;

/// <summary>
/// Extrapolation des passifs au-delà des niveaux définis dans <c>weapons.json</c>
/// (logique pure, testable).
///
/// Les 4 passifs ne définissent que <b>3 niveaux</b> pour un plafond de <b>20</b>. Jusqu'ici, chaque
/// niveau supplémentaire réappliquait le delta du dernier niveau défini, en <b>additif non borné</b> :
/// <c>thermal_core</c> atteignait ×4,00 de dégâts à L20, et surtout <c>capacitor</c> franchissait
/// <b>100 % de réduction de recharge dès le niveau 8</b> — toutes les armes tombaient alors au
/// plancher <see cref="StatCaps.MinCooldown"/>, quelle que soit leur cadence de fiche : une arme
/// lourde tirait aussi vite qu'un canon léger et la différenciation des armes disparaissait.
/// Mesuré au banc (cf. <c>docs/TEST_REPORT.md</c>) : l'indice de puissance du loadout faisait
/// <b>×6,4 en 12 minutes d'overtime</b> pendant que les PV du boss ne faisaient que ×2,8.
///
/// La progression continue donc au-delà des niveaux définis — c'est ce qui rend les cartes de passif
/// encore intéressantes en fin de run — mais en <b>rendements décroissants</b> : le n-ième niveau
/// supplémentaire ne rapporte plus que <c>1 / (1 + Falloff × n)</c> du delta de fiche. La somme croît
/// comme un logarithme : toujours croissante, jamais explosive.
///
/// Design : <c>docs/GDD.md</c> §30.
/// </summary>
public static class PassiveScaling
{
    /// <summary>
    /// Pente de l'amortissement. 0 = comportement additif historique ; plus la valeur est grande,
    /// plus les niveaux au-delà des niveaux définis rapportent peu. 0,20 conserve un gain nettement
    /// perceptible sur les premiers niveaux extrapolés (−17 % au 1er, −50 % au 5e) tout en divisant
    /// par ~2,5 le cumul atteint à L20.
    /// </summary>
    public const float Falloff = 0.20f;

    /// <summary>
    /// Delta effectif du passage au niveau <paramref name="level"/>, à partir du delta du dernier
    /// niveau défini. Aux niveaux définis (≤ <paramref name="definedMax"/>), la valeur de fiche est
    /// rendue telle quelle : l'équilibrage du early game ne bouge pas.
    /// </summary>
    public static float ExtrapolatedDelta(float definedDelta, int level, int definedMax)
    {
        if (level <= definedMax) return definedDelta;
        int extra = level - definedMax;
        return definedDelta / (1f + Falloff * extra);
    }

    /// <summary>
    /// Cumul de tous les deltas jusqu'au niveau <paramref name="level"/> inclus, en supposant le
    /// même delta de fiche à chaque niveau (c'est le cas des 4 passifs du jeu : leurs 3 niveaux
    /// définis portent des valeurs quasi identiques). Sert aux tests et au calibrage — le runtime,
    /// lui, applique les deltas un par un au fil des cartes.
    /// </summary>
    public static float CumulativeBonus(float definedDelta, int level, int definedMax)
    {
        float total = 0f;
        for (int n = 1; n <= level; n++)
            total += ExtrapolatedDelta(definedDelta, n, definedMax);
        return total;
    }
}
