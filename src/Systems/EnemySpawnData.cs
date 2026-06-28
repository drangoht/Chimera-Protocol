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
}
