using Godot;

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
}
