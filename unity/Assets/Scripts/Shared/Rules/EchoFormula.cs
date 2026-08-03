using System;

/// <summary>
/// Calcul des Échos d'Aether gagnés en fin de run (logique pure, testable).
/// Échos = composantes STANDARD (plafonnées à capTimeSecs/capKills/capCores) + BONUS DE SURCHARGE
/// (overtime, fortement amorti ET plafonné) + baseBonus.
/// standardEchoes = floor(min(t,capTimeSecs)/timeDiv) + floor(min(k,capKills)/killDiv)
///                + min(n,capCores)*coreMult + baseBonus
/// overtimeRaw    = floor(max(0,t-capTimeSecs)/timeDiv*overtimeDampening)
///                + floor(max(0,k-capKills)/killDiv*overtimeDampening)
///                + floor(max(0,n-capCores)*coreMult*overtimeDampening)
/// overtimeEchoes = min(overtimeRaw, overtimeBonusCap)
/// retour = standardEchoes + overtimeEchoes
/// Les diviseurs/caps viennent de meta_upgrades.json (chargés par MetaProgressionSystem).
///
/// <para><b>Palier de menace</b> (<paramref name="tierMult"/>, cf. <see cref="LevelThreat.EchoMult"/>) :
/// multiplicateur de récompense du niveau joué, appliqué <b>composante par composante</b> via
/// <see cref="ApplyTier"/>. Ce découpage n'est pas cosmétique : RunEndScreen anime les composantes une
/// par une et applique exactement la même opération, donc la somme affichée reste égale au total.</para>
/// </summary>
public static class EchoFormula
{
    /// <summary>
    /// Applique le multiplicateur de palier à une composante d'Échos (troncature — jamais d'arrondi
    /// « généreux » qui ferait diverger la somme affichée du total crédité).
    /// </summary>
    public static int ApplyTier(int component, double tierMult) => (int)Math.Floor(component * tierMult);

    public static int Calculate(int timeSecs, int kills, int cores,
                                int timeDiv, int killDiv, int coreMult, int baseBonus,
                                int capTimeSecs, int capKills, int capCores,
                                double overtimeDampening, int overtimeBonusCap,
                                double tierMult = 1.0)
        => CalculateDetailed(timeSecs, kills, cores, timeDiv, killDiv, coreMult, baseBonus,
                             capTimeSecs, capKills, capCores, overtimeDampening, overtimeBonusCap,
                             tierMult).Total;

    /// <summary>
    /// Variante exposant séparément le bonus de surcharge (overtime), pour l'affichage
    /// dédié dans RunEndScreen (composante "overtime_bonus"). Les deux valeurs retournées
    /// incluent déjà le multiplicateur de palier.
    /// </summary>
    public static (int Total, int OvertimeBonus) CalculateDetailed(int timeSecs, int kills, int cores,
                                int timeDiv, int killDiv, int coreMult, int baseBonus,
                                int capTimeSecs, int capKills, int capCores,
                                double overtimeDampening, int overtimeBonusCap,
                                double tierMult = 1.0)
    {
        if (timeDiv <= 0) timeDiv = 1;
        if (killDiv <= 0) killDiv = 1;

        int standardEchoes = ApplyTier(Math.Min(timeSecs, capTimeSecs) / timeDiv,   tierMult)
                            + ApplyTier(Math.Min(kills, capKills) / killDiv,        tierMult)
                            + ApplyTier(Math.Min(cores, capCores) * coreMult,       tierMult)
                            + ApplyTier(baseBonus,                                  tierMult);

        int overtimeRaw = (int)Math.Floor(Math.Max(0, timeSecs - capTimeSecs) / (double)timeDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, kills - capKills) / (double)killDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, cores - capCores) * coreMult * overtimeDampening);

        // Le plafond de surcharge s'applique AVANT le palier : c'est un plafond de design sur la
        // durée de survie, que le palier récompense ensuite comme les autres composantes.
        int overtimeEchoes = ApplyTier(Math.Min(overtimeRaw, overtimeBonusCap), tierMult);

        return (standardEchoes + overtimeEchoes, overtimeEchoes);
    }
}
