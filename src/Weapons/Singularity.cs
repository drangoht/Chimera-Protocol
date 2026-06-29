using Godot;

/// <summary>
/// Singularité (épique) — déploie un puits gravitationnel sur l'ennemi le plus proche : la zone
/// aspire les ennemis vers son centre et leur inflige des dégâts par tick pendant sa durée de vie.
/// Cooldown long, rayon plafonné (anti-trivialisation). Arme de contrôle/zonage. Vide → ne tire pas.
/// </summary>
public partial class Singularity : WeaponBase
{
    public float Radius       { get; set; } = 120f;
    public float PullSpeed    { get; set; } = 90f;
    public float Duration     { get; set; } = 2.2f;
    public float TickInterval { get; set; } = 0.4f;

    public override void _Ready()
    {
        Damage       = 6f;
        Cooldown     = 6.0f;
        Radius       = 120f;
        PullSpeed    = 90f;
        Duration     = 2.2f;
        TickInterval = 0.4f;
        base._Ready();
    }

    protected override void Attack()
    {
        var nearest = AcquireNearestEnemies(1);
        if (nearest.Count == 0) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_overload_pulse");
        int power = InventorySystem.Instance?.GetWeaponLevel("singularity") ?? 1;

        var well = new GravityWell
        {
            Damage = Damage, Radius = Radius, PullSpeed = PullSpeed,
            Duration = Duration, TickInterval = TickInterval, Power = power,
        };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, well);
        well.SetDeferred("global_position", nearest[0].GlobalPosition);
    }
}
