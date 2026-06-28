using System;

/// <summary>
/// Courbe de spawn des ennemis façon Vampire Survivors (logique pure, testable).
/// Toutes les fonctions du temps (minutes) et du multiplicateur de spawn (difficulté).
/// </summary>
public static class SpawnCurve
{
    /// <summary>Cap d'ennemis simultanés (perf cible 200-300).</summary>
    public const int MaxAlive = 300;

    /// <summary>Intervalle entre deux lots de spawn (s).</summary>
    public static float SpawnInterval(float tMinutes) => Math.Max(0.3f, 1.0f - tMinutes * 0.06f);

    /// <summary>Nombre d'ennemis par lot.</summary>
    public static int BatchCount(float tMinutes) => Math.Clamp(2 + (int)(tMinutes * 2f), 1, 10);

    /// <summary>Taille d'une vague périodique (toutes les 25 s).</summary>
    public static int WaveSize(float tMinutes, float spawnMult) => (int)((12 + tMinutes * 4f) * spawnMult);

    /// <summary>Cap d'ennemis vivants courant (croît avec le temps, plafonné à MaxAlive).</summary>
    public static int MaxEnemies(float tMinutes, float spawnMult)
        => Math.Min(MaxAlive, (int)((12 + tMinutes * 30f) * spawnMult));
}
