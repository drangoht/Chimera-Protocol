/// <summary>
/// Calcul des Échos d'Aether gagnés en fin de run (logique pure, testable).
/// Échos = floor(temps/timeDiv) + floor(kills/killDiv) + (noyaux × coreMult) + baseBonus.
/// Les diviseurs viennent de meta_upgrades.json (chargés par MetaProgressionSystem).
/// </summary>
public static class EchoFormula
{
    public static int Calculate(int timeSecs, int kills, int cores,
                                int timeDiv, int killDiv, int coreMult, int baseBonus)
    {
        if (timeDiv  <= 0) timeDiv  = 1;
        if (killDiv  <= 0) killDiv  = 1;
        return (timeSecs / timeDiv)
             + (kills    / killDiv)
             + (cores    * coreMult)
             + baseBonus;
    }
}
