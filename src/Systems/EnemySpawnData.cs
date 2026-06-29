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
