using Godot;

/// <summary>
/// Jet de Pyre — souffle un cône de flammes courte portée vers l'ennemi le plus proche : faibles
/// dégâts directs mais applique une BRÛLURE (DoT non-stackable, plafonnée par CrowdControlCaps).
/// Cooldown court (vente en continu). Si l'arène est vide, ne tire pas.
/// </summary>
public partial class PyreStream : WeaponBase
{
    public float ConeAngle    { get; set; } = 50f;
    public float Range        { get; set; } = 130f;
    public float BurnDps      { get; set; } = 6f;
    public float BurnDuration { get; set; } = 2.0f;

    public override void _Ready()
    {
        Damage       = 3f;
        Cooldown     = 0.5f;
        ConeAngle    = 50f;
        Range        = 130f;
        BurnDps      = 6f;
        BurnDuration = 2.0f;
        base._Ready();
    }

    protected override void Attack()
    {
        var nearest = AcquireNearestEnemies(1);
        if (nearest.Count == 0) return;
        var dir = (nearest[0].GlobalPosition - GlobalPosition).Normalized();
        float halfRad = Mathf.DegToRad(ConeAngle * 0.5f);

        AudioSystem.Instance?.PlaySfx("sfx_weapon_plasma_swing");

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            var rel = e.GlobalPosition - GlobalPosition;
            if (rel.Length() > Range + 16f) continue;
            if (Mathf.Abs(dir.AngleTo(rel.Normalized())) > halfRad) continue;
            e.TakeDamage(Damage);
            e.ApplyBurn(BurnDps, BurnDuration);
        }

        var flame = new PyreFlame { ConeAngle = ConeAngle, Range = Range, Rotation = dir.Angle() };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flame);
        flame.SetDeferred("global_position", GlobalPosition);
    }
}
