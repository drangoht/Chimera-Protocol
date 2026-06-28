/// <summary>
/// Scaling temporel des stats d'ennemis (logique pure, testable).
/// stat_finale = stat_base × (1 + t_minutes × perMinute) × difficulté.
/// Utilisé par EnemySpawner pour PV et dégâts avant d'instancier un ennemi.
/// </summary>
public static class EnemyScaling
{
    public static float Scaled(float baseValue, float tMinutes, float perMinute, float difficultyMult)
        => baseValue * (1f + tMinutes * perMinute) * difficultyMult;
}
