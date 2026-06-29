using Godot;

/// <summary>
/// Lame Boomerang — lance des glaives qui partent vers les ennemis les plus proches, atteignent
/// une portée max puis reviennent (2 passages = 2 coups potentiels par cible). 1 glaive au niveau 1,
/// +1 aux niveaux supérieurs. Si l'arène est vide, ne tire pas.
/// </summary>
public partial class Glaive : WeaponBase
{
    public int   GlaiveCount { get; set; } = 1;
    public float Range       { get; set; } = 240f;

    public override void _Ready()
    {
        Damage      = 10f;
        Cooldown    = 1.3f;
        GlaiveCount = 1;
        Range       = 240f;
        base._Ready();
    }

    protected override void Attack()
    {
        var targets = AcquireNearestEnemies(GlaiveCount);
        if (targets.Count == 0) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_plasma_swing");
        int power = InventorySystem.Instance?.GetWeaponLevel("glaive") ?? 1;
        var baseDir = (targets[0].GlobalPosition - GlobalPosition).Normalized();

        for (int i = 0; i < GlaiveCount; i++)
        {
            Vector2 dir = i < targets.Count
                ? (targets[i].GlobalPosition - GlobalPosition).Normalized()
                : baseDir.Rotated(Mathf.DegToRad((i - targets.Count + 1) * 18f));

            var g = new GlaiveProjectile { Damage = Damage, Direction = dir, Range = Range, Power = power };
            GetTree().Root.AddChild(g);
            g.GlobalPosition = GlobalPosition;
        }
    }
}
