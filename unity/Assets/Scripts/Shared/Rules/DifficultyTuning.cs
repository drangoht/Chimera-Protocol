/// <summary>
/// Multiplicateurs de difficulté appliqués aux ennemis (logique pure, testable).
/// Index : 0 = Facile, 1 = Normal, 2 = Difficile (cf. GameSettings.GameDifficulty).
/// </summary>
public static class DifficultyTuning
{
    /// <summary>Multiplicateur de dégâts des ennemis.</summary>
    public static float EnemyDamage(int difficulty) => difficulty switch
    {
        0 => 0.60f,   // Facile
        2 => 1.35f,   // Difficile
        _ => 1.00f,   // Normal
    };

    /// <summary>Multiplicateur de PV des ennemis.</summary>
    public static float EnemyHp(int difficulty) => difficulty switch
    {
        0 => 0.80f,
        2 => 1.30f,
        _ => 1.00f,
    };

    /// <summary>Multiplicateur du volume de spawn.</summary>
    public static float Spawn(int difficulty) => difficulty switch
    {
        0 => 0.70f,
        2 => 1.25f,
        _ => 1.00f,
    };
}
