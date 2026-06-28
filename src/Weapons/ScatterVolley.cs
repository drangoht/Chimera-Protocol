using Godot;
using System.Collections.Generic;

/// <summary>
/// Volée Multiple — tir multi-cible. À chaque salve, envoie ProjectileCount projectiles
/// vers les ProjectileCount ennemis les plus proches (un par cible). 2 projectiles au
/// niveau 1, +1 par niveau. S'il y a moins de cibles que de projectiles, les surplus
/// partent vers la cible la plus proche avec un léger éventail.
/// </summary>
public partial class ScatterVolley : WeaponBase
{
    [Export] public PackedScene BulletScene { get; set; } = null!;

    public int  ProjectileCount { get; set; } = 2;
    public bool IsPiercing      { get; set; } = false;

    public override void _Ready()
    {
        Damage          = 8f;
        Cooldown        = 0.90f;
        ProjectileSpeed = 420f;
        ProjectileCount = 2;
        IsPiercing      = false;
        BulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
        base._Ready();
    }

    protected override void Attack()
    {
        var targets = FindNearestEnemies(ProjectileCount);
        if (targets.Count == 0) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");
        int power = InventorySystem.Instance?.GetWeaponLevel("scatter_volley") ?? 1;

        var baseDir = (targets[0].GlobalPosition - GlobalPosition).Normalized();
        int extra = 0;

        for (int i = 0; i < ProjectileCount; i++)
        {
            Vector2 dir;
            if (i < targets.Count)
            {
                dir = (targets[i].GlobalPosition - GlobalPosition).Normalized();
            }
            else
            {
                // Plus de projectiles que de cibles : éventail autour de la cible la plus proche.
                int   step  = extra / 2 + 1;
                float sign  = (extra % 2 == 0) ? 1f : -1f;
                dir = baseDir.Rotated(Mathf.DegToRad(sign * step * 10f));
                extra++;
            }

            var bullet = BulletScene.Instantiate<Bullet>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition = GlobalPosition;
            bullet.Direction      = dir;
            bullet.Speed          = ProjectileSpeed;
            bullet.Damage         = Damage;
            bullet.IsPiercing     = IsPiercing;
            bullet.Power          = power;

            SpawnMuzzleFlash(GlobalPosition, dir);
        }
    }

    private void SpawnMuzzleFlash(Vector2 pos, Vector2 dir)
    {
        var flash = new MuzzleFlash { Rotation = dir.Angle() };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flash);
        flash.SetDeferred("global_position", pos);
    }

    /// <summary>Retourne les <paramref name="count"/> ennemis les plus proches (distincts).</summary>
    private List<EnemyBase> FindNearestEnemies(int count)
    {
        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        var list    = new List<(float dist, EnemyBase enemy)>();
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            list.Add((GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition), enemy));
        }
        list.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<EnemyBase>();
        for (int i = 0; i < list.Count && result.Count < count; i++)
            result.Add(list[i].enemy);
        return result;
    }
}
