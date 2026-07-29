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
    /// fréquence d'élite, et par ricochet les champions d'overtime).
    ///
    /// <b>Historique — 4 → 1,5 → 3 → 2,25</b>, les trois derniers réglages mesurés en session jouée
    /// le 2026-07-29. Abaissée à 1,5 quand la survie du joueur était <b>plafonnée</b> en fin de run :
    /// une menace quadratique face à une défense bornée ne laissait aucune fenêtre. Les cartes de
    /// <see cref="OverloadCards"/> ont supprimé ce plafond — la défense croît désormais d'environ
    /// <see cref="OverloadCards.MeasuredHpGainPerOvertimeMinute"/> PV par minute — et la raison de
    /// brider la menace a disparu avec lui.
    ///
    /// <list type="bullet">
    /// <item><b>1,5</b> : la menace passait <i>sous</i> la défense (×2,37 contre ×2,44 à 5 minutes).
    /// Le joueur gagnait la course, l'overtime devenait un plateau — session interrompue
    /// volontairement à 5:18, « elle aurait pu durer beaucoup plus longtemps ».</item>
    /// <item><b>3</b> : mort subie à <b>1:31</b> d'overtime, très en deçà des 5-10 min visées.</item>
    /// <item><b>2,25</b> : retenu, au milieu des deux.</item>
    /// </list>
    ///
    /// <b>Attention en relisant ces mesures : la variance entre runs est énorme.</b> À l'entrée en
    /// overtime — où cette constante n'a encore <i>aucun</i> effet, zéro minute étant écoulée — les
    /// deux sessions différaient déjà d'un facteur 2,4 en survie (1060 PV contre 745, 28,9 dégâts/s
    /// contre 48,9). Selon que l'arsenal se sature vers la 11ᵉ ou la 13ᵉ minute, le joueur aborde
    /// l'overtime avec deux à trois fois moins de cartes de surcharge accumulées. Une session isolée
    /// ne suffit donc pas à trancher un réglage fin.
    ///
    /// La règle du §31.4.3 se lit dans les deux sens : une défense non bornée <b>autorise</b> — et
    /// exige — une menace qui le soit aussi.
    /// </summary>
    public const float StatAcceleration = 2.25f;

    /// <summary>Minutes de courbe ajoutées à la densité pour <paramref name="overtimeMinutes"/> d'overtime.</summary>
    public static float DensityMinutes(float overtimeMinutes)
        => Math.Max(0f, overtimeMinutes) * DensityAcceleration;

    /// <summary>Minutes de courbe ajoutées au scaling pour <paramref name="overtimeMinutes"/> d'overtime.</summary>
    public static float StatMinutes(float overtimeMinutes)
        => Math.Max(0f, overtimeMinutes) * StatAcceleration;
}
