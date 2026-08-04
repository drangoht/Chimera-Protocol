using UnityEngine;

/// <summary>
/// Ce que le joueur a choisi <b>avant</b> de lancer une run : le niveau, son cran de saturation, et
/// le réglage d'assistance (Lot 6).
///
/// <para>Sous Godot, ces trois valeurs vivaient dans des AutoLoads que la scène de jeu interrogeait.
/// Ici elles sont rassemblées en un point unique, franchi <b>à chaque</b> lancement : c'est ce qui
/// évite qu'une run hérite en silence du réglage de la précédente — un défaut particulièrement
/// pénible à diagnostiquer, puisque le jeu se comporte alors « presque » comme prévu.</para>
/// </summary>
public static class RunConfig
{
    /// <summary>Biome joué. Détermine le palier de menace, la faune et l'incarnation du boss.</summary>
    public static string BiomeId { get; private set; } = LevelThreat.Order[0];

    /// <summary>Cran de l'échelle de saturation choisi pour cette run.</summary>
    public static int Saturation { get; private set; }

    /// <summary>Réglage d'assistance historique : 0 facile, 1 normal, 2 difficile.</summary>
    public static int Difficulty { get; private set; } = 1;

    /// <summary>Palier de menace du niveau joué.</summary>
    public static int ThreatTier => LevelThreat.TierOf(BiomeId);

    /// <summary>Fixe le choix du joueur pour la run à venir, et le mémorise dans ses réglages.</summary>
    public static void Choose(string biomeId, int saturation)
    {
        BiomeId = biomeId;
        Saturation = Mathf.Clamp(saturation, 0, SaturationTable.MaxRank);
        Difficulty = GameSettings.Current.Difficulty;

        // Le cran se règle PAR NIVEAU depuis la 1.25.0 : mémoriser le choix ici, c'est le retrouver
        // au prochain passage sur la carte de ce biome — et nulle part ailleurs.
        GameSettings.Current.SaturationByLevel[biomeId] = Saturation;
        GameSettings.Save();
    }

    /// <summary>
    /// Multiplicateur de densité : les trois axes de difficulté du projet sont <b>multiplicatifs</b>
    /// (réglage du joueur × palier du niveau × cran de saturation).
    /// </summary>
    public static float SpawnMult
        => DifficultyTuning.Spawn(Difficulty)
         * LevelThreat.SpawnMult(ThreatTier)
         * SaturationTable.SpawnMult(Saturation);

    /// <summary>Multiplicateur de PV de la faune.</summary>
    public static float EnemyHpMult
        => DifficultyTuning.EnemyHp(Difficulty)
         * LevelThreat.EnemyHpMult(ThreatTier)
         * SaturationTable.EnemyHpMult(Saturation);

    /// <summary>
    /// Multiplicateur de PV des <b>champions</b>. Volontairement plus doux : battre le boss est la
    /// condition de déblocage du niveau suivant, et un champion aligné sur la faune fermerait la
    /// progression aux paliers élevés (<see cref="LevelThreat.ChampionHpSoftening"/>).
    /// </summary>
    public static float ChampionHpMult
        => DifficultyTuning.EnemyHp(Difficulty)
         * LevelThreat.ChampionHpMult(ThreatTier)
         * SaturationTable.ChampionHpMult(Saturation);

    /// <summary>Multiplicateur de dégâts des ennemis.</summary>
    public static float EnemyDamageMult
        => DifficultyTuning.EnemyDamage(Difficulty)
         * LevelThreat.EnemyDamageMult(ThreatTier)
         * SaturationTable.EnemyDamageMult(Saturation);

    /// <summary>
    /// Minutes d'avance sur la courbe, apportées par le palier du niveau : un biome tardif démarre
    /// plus loin sur la courbe de scaling, sans pour autant démarrer plus dense.
    /// </summary>
    public static float TimeOffsetMinutes => LevelThreat.TimeOffsetMinutes(ThreatTier);

    /// <summary>Multiplicateur d'Échos — source unique, partagée par la fin de run et son animation.</summary>
    public static double EchoMult
        => LevelThreat.EchoMult(ThreatTier) * SaturationTable.EchoMult(Saturation);

    /// <summary>Temps imparti, cran « Compte à rebours » compris (il attaque le temps de build).</summary>
    public static int RunDurationSeconds(int baseSeconds)
        => Mathf.Max(60, Mathf.RoundToInt(baseSeconds * SaturationTable.RunDurationMult(Saturation)));
}
