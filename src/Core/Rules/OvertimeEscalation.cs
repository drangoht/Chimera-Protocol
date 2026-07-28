using System;

/// <summary>
/// Escalade de l'overtime (logique pure, testable).
///
/// Passé le temps imparti, la run ne se termine plus qu'à la mort du joueur : la menace doit donc
/// croître jusqu'à finir par tuer. La question n'est pas <i>si</i> l'overtime tue, mais <b>en combien
/// de temps</b> — l'économie d'Échos est dimensionnée sur des runs d'overtime de <b>5 à 10 minutes</b>
/// (bonus de surcharge jusqu'à +100 Échos, cf. <c>docs/GDD.md</c> §9.2).
///
/// Historiquement, une seule accélération (<c>×4</c>) était appliquée au temps de référence, puis
/// partagée par la densité <b>et</b> le scaling des stats. Or à l'entrée en overtime (13 min) tous
/// les leviers de densité sont <b>déjà saturés</b> : le cap simultané <see cref="SpawnCurve.MaxAlive"/>
/// est atteint depuis la 8ᵉ minute, <see cref="SpawnCurve.SpawnInterval"/> est à son plancher depuis
/// la 11ᵉ et <see cref="SpawnCurve.BatchCount"/> est clampé depuis la 4ᵉ. L'accélérateur n'avait donc
/// plus aucun effet sur le nombre d'ennemis à l'écran : son seul effet réel était de gonfler les
/// <b>PV et les dégâts</b>, à travers le terme quadratique de <see cref="EnemyScaling.CurvedFactor"/>
/// — qui reçoit un temps déjà multiplié par 4, et l'élève au carré.
///
/// Mesuré sur la session jouée du 2026-07-28 (Fournaise, palier 3) : les dégâts subis passent de
/// ~30/s à <b>92,5/s</b> en 54 s d'overtime, pendant que la survie du joueur est <b>triplement
/// plafonnée</b> — <c>reinforced_plating</c> à son niveau maximum (20), réduction de dégâts au cap
/// <see cref="StatCaps.MaxDamageReduction"/> et vitesse au cap <see cref="StatCaps.MaxSpeed"/>. Une
/// menace quadratique non bornée face à une défense plafonnée ne laisse aucune fenêtre : la fenêtre
/// de 5-10 minutes visée par le design était structurellement inatteignable.
///
/// Les deux temps de référence sont donc <b>découplés</b> : la densité conserve son accélération
/// franche (sans effet pratique, mais elle reste juste dès qu'un futur cap sera relevé), le scaling
/// des stats reçoit une pente nettement plus douce.
///
/// Design : <c>docs/GDD.md</c> §31.
/// </summary>
public static class OvertimeEscalation
{
    /// <summary>
    /// Accélération du temps de référence de la <b>densité</b> (cadence de lots, taille des vagues,
    /// cap simultané) : une minute d'overtime vaut quatre minutes de courbe. Valeur historique,
    /// conservée — tous ces leviers sont déjà à leur plafond quand l'overtime commence.
    /// </summary>
    public const float DensityAcceleration = 4f;

    /// <summary>
    /// Accélération du temps de référence du <b>scaling</b> (PV, dégâts, variété d'ennemis tirables,
    /// fréquence d'élite, et par ricochet les champions d'overtime). Abaissée de 4 à 1,5 : le terme
    /// quadratique de <see cref="EnemyScaling.CurvedFactor"/> élevant ce temps au carré, ×4 faisait
    /// des dégâts entrants ×11 en dix minutes d'overtime, contre ×4,5 à cette pente.
    /// </summary>
    public const float StatAcceleration = 1.5f;

    /// <summary>Minutes de courbe ajoutées à la densité pour <paramref name="overtimeMinutes"/> d'overtime.</summary>
    public static float DensityMinutes(float overtimeMinutes)
        => Math.Max(0f, overtimeMinutes) * DensityAcceleration;

    /// <summary>Minutes de courbe ajoutées au scaling pour <paramref name="overtimeMinutes"/> d'overtime.</summary>
    public static float StatMinutes(float overtimeMinutes)
        => Math.Max(0f, overtimeMinutes) * StatAcceleration;
}
