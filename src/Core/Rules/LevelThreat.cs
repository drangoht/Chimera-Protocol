using System;

/// <summary>
/// Paliers de menace des NIVEAUX (biomes) — logique pure, testable.
///
/// Les niveaux se débloquent en séquence (<see cref="Order"/>) et, entre deux déblocages, le joueur
/// dépense des Échos au Hub : PV, dégâts, réduction de dégâts, vies de secours, slots de greffe…
/// Sans contre-partie, un joueur qui arrive au dernier niveau écrase un contenu calibré pour un
/// personnage nu. Chaque niveau porte donc un **palier de menace** croissant : les ennemis y sont
/// plus coriaces, plus dangereux, plus nombreux et plus variés tôt — et le niveau rapporte
/// proportionnellement plus d'Échos, pour que « monter d'un palier » soit le chemin optimal plutôt
/// que farmer le 1er niveau.
///
/// Multiplicatif avec <see cref="DifficultyTuning"/> (Facile/Normal/Difficile, choix du joueur) :
/// les deux axes sont indépendants — le palier vient du niveau joué, la difficulté du réglage.
/// Cf. docs/GDD.md §28.
/// </summary>
public static class LevelThreat
{
    /// <summary>
    /// Ordre de déblocage des niveaux : le 1er est jouable d'office, chacun se débloque quand le
    /// précédent est complété (boss de fin battu). L'index dans ce tableau EST le palier de menace.
    /// Source de vérité unique — <c>GameSettings.LevelOrder</c> s'y délègue.
    /// </summary>
    public static readonly string[] Order = { "sanctuaire", "aether", "givre", "fournaise", "neon" };

    /// <summary>Palier maximum (dernier niveau).</summary>
    public static readonly int MaxTier = Order.Length - 1;

    // ── Tables de palier (index = palier). Tout est réglable ici. ──────────────────────────────
    // Calibrage : un joueur qui débloque le palier N a typiquement acheté ~N/4 de l'arbre du Hub
    // (PV ×2,2 et dégâts ×1,7 à l'arbre complet, plus greffes et perks de départ). Les paliers
    // reprennent une partie de cette marge sans jamais l'annuler : le joueur reste plus fort qu'à
    // ses débuts, mais le dernier niveau redevient un test.
    private static readonly float[] HpMults     = { 1.00f, 1.10f, 1.22f, 1.35f, 1.50f };
    private static readonly float[] DamageMults = { 1.00f, 1.10f, 1.20f, 1.32f, 1.45f };
    private static readonly float[] SpawnMults  = { 1.00f, 1.04f, 1.08f, 1.12f, 1.16f };
    private static readonly float[] TimeOffsets = { 0.00f, 0.60f, 1.20f, 1.80f, 2.40f };
    private static readonly double[] EchoMults  = { 1.00,  1.10,  1.20,  1.32,  1.45  };

    /// <summary>
    /// Fraction du bonus de PV de palier que reçoivent les CHAMPIONS (mini-boss + boss de fin).
    /// Ils sont des gates de survie calibrés au TTK (cf. GDD §17/§18/§20) et, surtout, battre le boss
    /// est la condition de déblocage du niveau suivant : leur appliquer le bonus plein transformerait
    /// le palier en mur infranchissable. Les dégâts, eux, montent au taux plein (menace, pas éponge).
    /// </summary>
    public const float ChampionHpSoftening = 0.55f;

    /// <summary>Palier du niveau (0 si id inconnu, vide ou null → aucun bonus).</summary>
    public static int TierOf(string? biomeId)
    {
        if (string.IsNullOrEmpty(biomeId)) return 0;
        int idx = Array.IndexOf(Order, biomeId);
        return idx < 0 ? 0 : idx;
    }

    private static int Clamp(int tier) => Math.Clamp(tier, 0, MaxTier);

    /// <summary>Multiplicateur de PV des ennemis BASIQUES pour ce palier.</summary>
    public static float EnemyHpMult(int tier) => HpMults[Clamp(tier)];

    /// <summary>
    /// Multiplicateur de PV des CHAMPIONS (mini-boss / boss de fin) : bonus de palier amorti par
    /// <see cref="ChampionHpSoftening"/> pour préserver la fenêtre de victoire du boss.
    /// </summary>
    public static float ChampionHpMult(int tier) => 1f + (HpMults[Clamp(tier)] - 1f) * ChampionHpSoftening;

    /// <summary>Multiplicateur de dégâts des ennemis (tous, champions inclus) pour ce palier.</summary>
    public static float EnemyDamageMult(int tier) => DamageMults[Clamp(tier)];

    /// <summary>Multiplicateur de densité de spawn (cap simultané + taille des vagues).</summary>
    public static float SpawnMult(int tier) => SpawnMults[Clamp(tier)];

    /// <summary>
    /// Décalage (minutes) appliqué au temps de référence du SCALING et du tirage des ennemis :
    /// un palier élevé démarre plus avancé sur la courbe (ennemis lourds et élites plus tôt).
    /// N'affecte PAS la cadence/densité de spawn, pilotée par <see cref="SpawnMult"/> — sinon les
    /// premières secondes d'un haut palier basculeraient d'un coup en mid-game.
    /// </summary>
    public static float TimeOffsetMinutes(int tier) => TimeOffsets[Clamp(tier)];

    /// <summary>
    /// Multiplicateur d'Échos gagnés sur ce niveau. Croît avec le palier pour que le niveau le plus
    /// dur soit aussi le plus rentable : sans lui, farmer le 1er niveau resterait optimal.
    /// </summary>
    public static double EchoMult(int tier) => EchoMults[Clamp(tier)];
}
