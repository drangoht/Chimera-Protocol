using Godot;
using System.Collections.Generic;

public abstract partial class WeaponBase : Node2D
{
    [Export] public float Damage { get; set; } = 10f;
    [Export] public float Cooldown { get; set; } = 0.8f;
    [Export] public float ProjectileSpeed { get; set; } = 400f;

    private float _timer;

    public override void _Ready()
    {
        _timer = Cooldown;
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer <= 0f)
        {
            Attack();
            _timer = Cooldown;
        }
    }

    protected abstract void Attack();

    /// <summary>Les <paramref name="count"/> ennemis les plus proches (triés, distincts).
    /// Nom distinct des helpers privés historiques de certaines armes (évite CS0108).</summary>
    protected List<EnemyBase> AcquireNearestEnemies(int count)
    {
        var list = new List<(float dist, EnemyBase enemy)>();
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
            if (node is EnemyBase e)
                list.Add((GlobalPosition.DistanceSquaredTo(e.GlobalPosition), e));
        list.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<EnemyBase>(count);
        for (int i = 0; i < list.Count && result.Count < count; i++)
            result.Add(list[i].enemy);
        return result;
    }
}
