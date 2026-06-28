using Godot;
using System.Collections.Generic;

public partial class ImpulseCannon : WeaponBase
{
    [Export] public PackedScene BulletScene { get; set; } = null!;

    public int  ProjectileCount { get; set; } = 1;
    public bool IsPiercing      { get; set; } = false;

    public override void _Ready()
    {
        Damage         = 10f;
        Cooldown       = 0.80f;
        ProjectileSpeed = 400f;
        ProjectileCount = 1;
        IsPiercing      = false;
        BulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
        base._Ready();
    }

    protected override void Attack()
    {
        var targets = FindNearestEnemies(ProjectileCount);
        if (targets.Count > 0)
            AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");

        int power = InventorySystem.Instance?.GetWeaponLevel("impulse_cannon") ?? 1;

        foreach (var target in targets)
        {
            var dir    = (target.GlobalPosition - GlobalPosition).Normalized();
            var bullet = BulletScene.Instantiate<Bullet>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition = GlobalPosition;
            bullet.Direction      = dir;
            bullet.Speed          = ProjectileSpeed;
            bullet.Damage         = Damage;
            bullet.IsPiercing     = IsPiercing;
            bullet.Power          = power;

            // ── Muzzle flash au point de tir ──────────────────────────────
            SpawnMuzzleFlash(GlobalPosition, dir);
        }
    }

    private void SpawnMuzzleFlash(Vector2 pos, Vector2 dir)
    {
        // Chargement paresseux sans scène externe — instanciation pure C#
        var flash = new MuzzleFlash();
        flash.Rotation = dir.Angle();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flash);
        flash.SetDeferred("global_position", pos);
    }

    private List<EnemyBase> FindNearestEnemies(int count)
    {
        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        var list    = new List<(float dist, EnemyBase enemy)>();

        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            list.Add((GlobalPosition.DistanceTo(enemy.GlobalPosition), enemy));
        }

        list.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<EnemyBase>();
        for (int i = 0; i < list.Count && result.Count < count; i++)
            result.Add(list[i].enemy);
        return result;
    }
}
