using System;

/// <summary>
/// Scaling temporel des stats d'ennemis (logique pure, testable).
/// Deux formules :
///  - <see cref="Scaled"/> : facteur LINÉAIRE historique stat_base × (1 + t×perMinute) × difficulté.
///  - <see cref="ScaledCurved"/> : courbe NON-LINÉAIRE (défaut runtime) qui adoucit le tout début
///    et accélère le mid/late, pour corriger le décrochage « OP » (le build joueur croît plus vite
///    que des ennemis strictement linéaires). Utilisé par EnemySpawner.
/// </summary>
public static class EnemyScaling
{
    /// <summary>Facteur linéaire historique (conservé pour compat + tests de référence).</summary>
    public static float Scaled(float baseValue, float tMinutes, float perMinute, float difficultyMult)
        => baseValue * (1f + tMinutes * perMinute) * difficultyMult;

    // ── Paramètres de la courbe (cf. docs/GDD.md §difficulté) ─────────────────────
    /// <summary>Fin de la « grâce » de début : à t=0 les ennemis sont affaiblis, retour à 1.0 à t=EarlyEndMin.</summary>
    public const float EarlyEndMin = 1.5f;
    /// <summary>Amplitude de l'affaiblissement à t=0 (−15%).</summary>
    public const float EarlyDrop = 0.15f;
    /// <summary>Plancher du facteur (garde-fou tout début).</summary>
    public const float FactorFloor = 0.75f;
    /// <summary>Début de l'accélération quadratique (mid-game).</summary>
    public const float LateStartMin = 4f;
    /// <summary>Pente de l'accélération late (proportionnelle à perMinute → cohérente entre stats).</summary>
    public const float LateCoeff = 0.08f;

    /// <summary>
    /// Facteur multiplicatif non-linéaire remplaçant (1 + t×perMinute) :
    ///  - t &lt; EarlyEndMin : léger malus décroissant (début plus permissif),
    ///  - partie linéaire identique à la formule historique,
    ///  - t &gt; LateStartMin : bonus quadratique (le late rattrape le power-creep du build).
    /// </summary>
    public static float CurvedFactor(float tMinutes, float perMinute)
    {
        float linear    = tMinutes * perMinute;
        float lateAccel = tMinutes > LateStartMin
            ? LateCoeff * perMinute * (tMinutes - LateStartMin) * (tMinutes - LateStartMin)
            : 0f;
        float earlyGrace = tMinutes < EarlyEndMin
            ? EarlyDrop * (1f - tMinutes / EarlyEndMin)
            : 0f;
        return Math.Max(FactorFloor, 1f + linear + lateAccel - earlyGrace);
    }

    /// <summary>Stat scalée via la courbe non-linéaire × difficulté.</summary>
    public static float ScaledCurved(float baseValue, float tMinutes, float perMinute, float difficultyMult)
        => baseValue * CurvedFactor(tMinutes, perMinute) * difficultyMult;
}
