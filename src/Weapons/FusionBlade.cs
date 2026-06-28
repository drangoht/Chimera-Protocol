using Godot;

/// <summary>
/// Lame à Fusion — anneau continu 360°, 55 dps, rayon 130 px, pulse toutes les 0.15 s.
/// Pas de cooldown discret : le timer interne est fixé à damageInterval.
/// </summary>
public partial class FusionBlade : WeaponBase
{
    private const float DamageInterval = 0.15f;
    private const float RingRadius     = 130f;
    private const float Dps            = 55f;

    public override void _Ready()
    {
        // DPS = Damage / DamageInterval → Damage par pulse = 55 * 0.15 = 8.25
        Damage   = Dps * DamageInterval;
        Cooldown = DamageInterval;
        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            if (enemy.GlobalPosition.DistanceTo(player.GlobalPosition) <= RingRadius)
                enemy.TakeDamage(Damage);
        }
    }
}
