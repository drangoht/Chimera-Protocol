/// <summary>Données de spawn d'un ennemi (chargées depuis enemies.json par EnemySpawner).</summary>
internal sealed class EnemySpawnData
{
    public string Id                     { get; set; } = "";
    public float  MaxHp                  { get; set; }
    public float  Speed                  { get; set; }
    public float  Damage                 { get; set; }
    public int    XpValue                { get; set; }
    public float  SpawnStartMinute       { get; set; }
    public float  SpawnWeight            { get; set; }
    public float  HpScalingPerMinute     { get; set; }
    public float  DamageScalingPerMinute { get; set; }
    public int    MaxSimultaneous        { get; set; } = 0; // 0 = illimité

    /// <summary>
    /// Type d'archétype d'IA (« ai.type » du JSON : straight_chase/erratic_chase/ranged_kiter/
    /// slow_hunter…). Sert UNIQUEMENT à résoudre quelle scène archétype instancier pour les ids
    /// sans scène dédiée (cf. EnemySpawner.ArchetypeScenePaths) — le comportement réel reste
    /// hardcodé dans la sous-classe C# de cette scène (RustSwarm/CorruptedDrone/CorruptedSentinel/
    /// GraftedColossus), ce champ ne pilote aucune logique de jeu.
    /// </summary>
    public string AiType                 { get; set; } = "";

    /// <summary>
    /// Chemin res:// d'un SpriteFrames dédié à charger dynamiquement sur l'AnimatedSprite2D de la
    /// scène instanciée (cf. EnemyBase.SetSpriteFrames, sur le modèle de Player.SetCharacterFrames).
    /// Vide = garder le SpriteFrames posé en dur dans le .tscn (rétro-compatible, ennemis existants).
    /// </summary>
    public string FramesPath             { get; set; } = "";

    /// <summary>Biomes où cet ennemi peut apparaître (ids). Vide = tous les biomes (défaut).</summary>
    public string[] Biomes               { get; set; } = System.Array.Empty<string>();

    /// <summary>Cet ennemi peut-il apparaître dans le biome courant ? (vide ou biome inconnu = oui).</summary>
    public bool IsAllowedInBiome(string? biomeId)
    {
        if (Biomes.Length == 0 || string.IsNullOrEmpty(biomeId)) return true;
        foreach (var b in Biomes) if (b == biomeId) return true;
        return false;
    }
}
