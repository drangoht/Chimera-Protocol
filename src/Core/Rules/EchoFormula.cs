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
/// </summary>
public static class EchoFormula
{
    public static int Calculate(int timeSecs, int kills, int cores,
                                int timeDiv, int killDiv, int coreMult, int baseBonus,
                                int capTimeSecs, int capKills, int capCores,
                                double overtimeDampening, int overtimeBonusCap)
    {
        if (timeDiv <= 0) timeDiv = 1;
        if (killDiv <= 0) killDiv = 1;

        int standardEchoes = (Math.Min(timeSecs, capTimeSecs) / timeDiv)
                            + (Math.Min(kills, capKills) / killDiv)
                            + (Math.Min(cores, capCores) * coreMult)
                            + baseBonus;

        int overtimeRaw = (int)Math.Floor(Math.Max(0, timeSecs - capTimeSecs) / (double)timeDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, kills - capKills) / (double)killDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, cores - capCores) * coreMult * overtimeDampening);

        int overtimeEchoes = Math.Min(overtimeRaw, overtimeBonusCap);

        return standardEchoes + overtimeEchoes;
    }

    /// <summary>
    /// Variante exposant séparément le bonus de surcharge (overtime), pour l'affichage
    /// dédié dans RunEndScreen (composante "overtime_bonus").
    /// </summary>
    public static (int Total, int OvertimeBonus) CalculateDetailed(int timeSecs, int kills, int cores,
                                int timeDiv, int killDiv, int coreMult, int baseBonus,
                                int capTimeSecs, int capKills, int capCores,
                                double overtimeDampening, int overtimeBonusCap)
    {
        if (timeDiv <= 0) timeDiv = 1;
        if (killDiv <= 0) killDiv = 1;

        int standardEchoes = (Math.Min(timeSecs, capTimeSecs) / timeDiv)
                            + (Math.Min(kills, capKills) / killDiv)
                            + (Math.Min(cores, capCores) * coreMult)
                            + baseBonus;

        int overtimeRaw = (int)Math.Floor(Math.Max(0, timeSecs - capTimeSecs) / (double)timeDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, kills - capKills) / (double)killDiv * overtimeDampening)
                        + (int)Math.Floor(Math.Max(0, cores - capCores) * coreMult * overtimeDampening);

        int overtimeEchoes = Math.Min(overtimeRaw, overtimeBonusCap);

        return (standardEchoes + overtimeEchoes, overtimeEchoes);
    }
}
