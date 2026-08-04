/// <summary>
/// Paramètres de la formule d'Échos (Lot 5).
///
/// <para><b>Un seul porteur pour treize paramètres.</b> <see cref="EchoFormula"/> en attend treize ;
/// les passer un par un depuis chaque appelant multiplierait les occasions d'en intervertir deux —
/// une erreur qui ne se voit pas, puisque le résultat reste un nombre plausible.</para>
///
/// <para>Les valeurs par défaut sont celles du jeu publié, surchargées par <c>meta_upgrades.json</c>
/// (bloc <c>echoFormula</c>) quand il en définit.</para>
///
/// <para>⚠ <b>Source unique du total.</b> Le bilan de fin de run affiche une somme animée et crédite
/// un total : les deux <b>doivent</b> venir du même calcul. Sous Godot, ils venaient de deux endroits
/// différents et divergeaient dès qu'un multiplicateur de palier entrait en jeu — le joueur voyait
/// un chiffre et en recevait un autre.</para>
/// </summary>
public sealed class EchoSettings
{
    public int    TimeDiv            = 20;
    public int    KillDiv            = 10;
    public int    CoreMult           = 5;
    public int    BaseBonus          = 10;
    public int    CapKills           = 520;
    public int    CapCores           = 22;
    public double OvertimeDampening  = 0.15;
    public int    OvertimeBonusCap   = 100;

    /// <summary>Réglages du jeu publié.</summary>
    public static readonly EchoSettings Default = new();

    /// <summary>
    /// Calcule le total gagné. <b>Point d'entrée unique</b> : tout affichage doit partir de cette
    /// valeur et se contenter de la parcourir.
    /// </summary>
    public int Total(int runSeconds, int kills, int cores, double tierMult = 1.0)
        => EchoFormula.Calculate(runSeconds, kills, cores,
                                 TimeDiv, KillDiv, CoreMult, BaseBonus,
                                 runSeconds, CapKills, CapCores,
                                 OvertimeDampening, OvertimeBonusCap, tierMult);

    /// <summary>Même calcul, en exposant à part le bonus d'overtime pour l'affichage détaillé.</summary>
    public (int Total, int OvertimeBonus) Detailed(int runSeconds, int kills, int cores, double tierMult = 1.0)
        => EchoFormula.CalculateDetailed(runSeconds, kills, cores,
                                         TimeDiv, KillDiv, CoreMult, BaseBonus,
                                         runSeconds, CapKills, CapCores,
                                         OvertimeDampening, OvertimeBonusCap, tierMult);
}
