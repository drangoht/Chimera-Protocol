using Godot;

/// <summary>
/// Lance Cryo — rayon perçant instantané (hitscan) tiré vers l'ennemi le plus proche : touche tous
/// les ennemis dans une bande [0, Range] × ±largeur, leur inflige des dégâts ET les ralentit
/// (slow plafonné -40 % par CrowdControlCaps). Arme d'utilité/contrôle. Si l'arène est vide, ne tire pas.
/// </summary>
public partial class CryoLance : WeaponBase
{
    public float Range        { get; set; } = 360f;
    public float SlowMult     { get; set; } = 0.80f;
    public float SlowDuration { get; set; } = 1.5f;
    private const float HalfWidth = 16f;

    public override void _Ready()
    {
        Damage       = 9f;
        Cooldown     = 1.4f;
        Range        = 360f;
        SlowMult     = 0.80f;
        SlowDuration = 1.5f;
        base._Ready();
    }

    protected override void Attack()
    {
        var nearest = AcquireNearestEnemies(1);
        if (nearest.Count == 0) return;
        var dir  = (nearest[0].GlobalPosition - GlobalPosition).Normalized();
        var perp = new Vector2(-dir.Y, dir.X);

        AudioSystem.Instance?.PlaySfx("sfx_weapon_rail_shoot");
        int power = InventorySystem.Instance?.GetWeaponLevel("cryo_lance") ?? 1;

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            var rel   = e.GlobalPosition - GlobalPosition;
            float along = rel.Dot(dir);
            if (along < 0f || along > Range) continue;
            if (Mathf.Abs(rel.Dot(perp)) > HalfWidth + 16f) continue;   // +16 ≈ rayon ennemi
            e.TakeDamage(Damage);
            e.ApplySlow(SlowMult, SlowDuration);
        }

        var beam = new CryoBeam { Length = Range, Power = power, Rotation = dir.Angle() };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, beam);
        beam.SetDeferred("global_position", GlobalPosition);
    }
}
