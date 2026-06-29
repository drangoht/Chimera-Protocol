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

    /// <summary>Taille d'une vague périodique (toutes les 25 s). Pente relevée le 2026-06-29
    /// (4→6) pour densifier le mid/end game.</summary>
    public static int WaveSize(float tMinutes, float spawnMult) => (int)((12 + tMinutes * 6f) * spawnMult);

    /// <summary>Cap d'ennemis vivants courant (croît avec le temps, plafonné à MaxAlive).
    /// Pente relevée le 2026-06-29 (30→36) : l'écran se remplit plus tôt en mid-game.</summary>
    public static int MaxEnemies(float tMinutes, float spawnMult)
        => Math.Min(MaxAlive, (int)((12 + tMinutes * 36f) * spawnMult));
}
