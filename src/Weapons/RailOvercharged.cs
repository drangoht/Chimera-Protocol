using Godot;

/// <summary>
/// Rail Surchargé — rafale de 3 projectiles (22 dmg, intervalle 0.12 s), cooldown entre rafales 0.6 s.
/// Perforation infinie. Vitesse 600 px/s.
/// </summary>
public partial class RailOvercharged : WeaponBase
{
    private const int   BurstCount    = 3;
    private const float BurstInterval = 0.12f;
    private const float BurstDamage   = 22f;
    private const float BurstSpeed    = 600f;

    private PackedScene? _bulletScene;

    // Timer interne de rafale
    private int   _burstRemaining = 0;
    private float _burstTimer     = 0f;
    private EnemyBase? _burstTarget;

    public override void _Ready()
    {
        Damage   = BurstDamage;
        Cooldown = 0.6f;
        _bulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
        base._Ready();
    }

    public override void _Process(double delta)
    {
        // Gestion de la rafale en cours
        if (_burstRemaining > 0)
        {
            _burstTimer -= (float)delta;
            if (_burstTimer <= 0f)
            {
                FireOne();
                _burstRemaining--;
                _burstTimer = BurstInterval;
            }
        }

        base._Process(delta); // gère le cooldown entre rafales
    }

    protected override void Attack()
    {
        // Commence une nouvelle rafale
        _burstTarget    = FindNearestEnemy();
        _burstRemaining = BurstCount;
        _burstTimer     = 0f; // premier tir immédiat

        // SFX de la rafale Rail (1 son = toute la rafale)
        AudioSystem.Instance?.PlaySfx("sfx_weapon_rail_shoot");
    }

    private void FireOne()
    {
        if (_bulletScene == null) return;
        var target = _burstTarget;
        // Recalcule direction vers la cible actuelle si toujours valide
        Vector2 dir;
        if (target != null && IsInstanceValid(target))
            dir = (target.GlobalPosition - GlobalPosition).Normalized();
        else
        {
            // Re-cible
            _burstTarget = FindNearestEnemy();
            if (_burstTarget == null) return;
            dir = (_burstTarget.GlobalPosition - GlobalPosition).Normalized();
        }

        var bullet = _bulletScene.Instantiate<Bullet>();
        GetTree().Root.AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition;
        bullet.Direction      = dir;
        bullet.Speed          = BurstSpeed;
        bullet.Damage         = BurstDamage;
        bullet.IsPiercing     = true; // perforation infinie
    }

    private EnemyBase? FindNearestEnemy()
    {
        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        EnemyBase? nearest = null;
        float minDist = float.MaxValue;
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (dist < minDist) { minDist = dist; nearest = enemy; }
        }
        return nearest;
    }
}
