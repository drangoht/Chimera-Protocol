public static class Constants
{
    public const string GroupPlayer  = "player";
    public const string GroupEnemies = "enemies";
    public const string GroupXpOrbs  = "xp_orbs";

    public const int ArenaWidth    = 1920;
    public const int ArenaHeight   = 1216;
    public const int WallThickness = 32;
    public const int SpawnMargin   = 50;

    // MaxEnemies est maintenant dynamique (EnemySpawner le calcule via la formule de scaling).
    // Cette constante reste pour la Phase 1 / tests ; l'EnemySpawner remplace sa valeur au runtime.
    public const int MaxEnemies = 200;
}
