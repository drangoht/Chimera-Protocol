using System;

/// <summary>
/// Phases du boss de fin — logique pure, testable (aucune dépendance Godot).
///
/// Le boss traverse trois phases selon son ratio de PV (100→66 %, 66→33 %, 33→0 %). Chaque phase
/// resserre la cadence des salves, des ondes de choc et de la mécanique signature du biome, et la
/// dernière invoque des adds. **Les PV totaux ne changent pas** : les phases redistribuent
/// l'intensité d'un combat calibré au TTK (cf. GDD §20.2), elles ne l'allongent pas.
///
/// Deux propriétés portent tout le design :
/// - la progression est **irréversible** (<see cref="Advance"/>) — un boss qui remonterait
///   au-dessus d'un seuil rejouerait sa bascule en boucle ;
/// - chaque bascule ouvre <see cref="TransitionSeconds"/> d'invulnérabilité télégraphiée, fenêtre
///   de repositionnement pour le joueur avant que la cadence monte.
///
/// Cf. docs/GDD.md §29.
/// </summary>
public static class BossPhases
{
    /// <summary>Nombre de phases (index valides : 0..Count-1).</summary>
    public const int Count = 3;

    /// <summary>
    /// Ratios de PV d'ENTRÉE dans les phases 1 et 2, en ordre décroissant. Passer sous 0,66 fait
    /// entrer en phase II ; sous 0,33 en phase III.
    /// </summary>
    public static readonly float[] Thresholds = { 0.66f, 0.33f };

    /// <summary>Durée de la surcharge de bascule : le boss est immobile, invulnérable et inoffensif.</summary>
    public const float TransitionSeconds = 1.0f;

    // ── Tables par phase (index = phase). Tout le tuning est ici. ─────────────────────────────
    // Intervalles en secondes : plus court = plus pressant. La signature est exprimée en CADENCE
    // (multiplicateur) et non en intervalle, parce que chaque biome part d'une période propre.
    private static readonly float[] BurstIntervals = { 2.00f, 1.55f, 1.20f };
    private static readonly float[] ShockIntervals = { 3.50f, 2.80f, 2.20f };
    private static readonly float[] SignatureRates = { 1.00f, 1.35f, 1.70f };
    private static readonly float[] SpeedMults     = { 1.00f, 1.08f, 1.18f };

    /// <summary>Nombre d'adds invoqués par vague en phase III.</summary>
    public const int AddsPerWave = 4;

    /// <summary>Période d'invocation des adds en phase III (la 1re vague part à l'entrée en phase).</summary>
    public const float AddsIntervalSeconds = 12f;

    /// <summary>Phase correspondant à un ratio de PV, sans mémoire (0 = phase I).</summary>
    public static int PhaseAt(float hpRatio)
    {
        if (float.IsNaN(hpRatio)) return 0;
        float r = Math.Clamp(hpRatio, 0f, 1f);
        int phase = 0;
        for (int i = 0; i < Thresholds.Length; i++)
            if (r < Thresholds[i]) phase = i + 1;
        return phase;
    }

    /// <summary>
    /// Phase suivante en tenant compte de la phase courante : la progression ne recule JAMAIS,
    /// même si le boss regagne des PV.
    /// </summary>
    public static int Advance(int currentPhase, float hpRatio)
        => Math.Clamp(Math.Max(currentPhase, PhaseAt(hpRatio)), 0, Count - 1);

    private static int Clamp(int phase) => Math.Clamp(phase, 0, Count - 1);

    /// <summary>Période entre deux salves radiales, en secondes.</summary>
    public static float BurstInterval(int phase) => BurstIntervals[Clamp(phase)];

    /// <summary>Période entre deux ondes de choc, en secondes.</summary>
    public static float ShockInterval(int phase) => ShockIntervals[Clamp(phase)];

    /// <summary>
    /// Multiplicateur de CADENCE de la mécanique signature du biome. L'intervalle effectif vaut
    /// <c>périodeDeBase / SignatureRate(phase)</c>.
    /// </summary>
    public static float SignatureRate(int phase) => SignatureRates[Clamp(phase)];

    /// <summary>Période effective de la signature pour une période de base propre au biome.</summary>
    public static float SignatureInterval(int phase, float baseInterval)
        => baseInterval / SignatureRate(phase);

    /// <summary>Multiplicateur de vitesse de déplacement du boss.</summary>
    public static float SpeedMult(int phase) => SpeedMults[Clamp(phase)];

    /// <summary>La phase invoque-t-elle des adds ? (phase III uniquement)</summary>
    public static bool SummonsAdds(int phase) => Clamp(phase) >= Count - 1;

    /// <summary>Chiffre romain affiché sur la barre de boss.</summary>
    public static string RomanNumeral(int phase) => Clamp(phase) switch
    {
        0 => "I",
        1 => "II",
        _ => "III",
    };
}
