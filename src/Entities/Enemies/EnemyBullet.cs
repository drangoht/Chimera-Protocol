using Godot;

/// <summary>
/// Projectile tiré par la Sentinelle Corrompue.
/// Area2D, vitesse 180 px/s, durée de vie 3 s, un seul hit joueur.
/// </summary>
public partial class EnemyBullet : Area2D
{
    public Vector2 Direction { get; set; } = Vector2.Right;
    public float Speed  { get; set; } = 180f;
    public float Damage { get; set; } = 12f;

    private float _lifetime = 3f;
    private bool  _hasHit   = false;

    private static PackedScene? _impactBurstScene;
    private static Texture2D?   _impactTexture;
    private static Texture2D?   _enemyBulletLightTex;

    public override void _Ready()
    {
        _impactBurstScene    ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_impact_burst.tscn");
        _impactTexture       ??= GD.Load<Texture2D>("res://assets/sprites/vfx/vfx_particle_impact_sentinel.png");
        _enemyBulletLightTex ??= Player.MakeRadialLightTexture(32);

        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.35f, 0.1f, 1f),
            Energy       = 1.0f,
            Texture      = _enemyBulletLightTex,
            TextureScale = 1.6f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * Speed * (float)delta;

        _lifetime -= (float)delta;
        if (_lifetime <= 0f)
            QueueFree();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hasHit) return;
        if (body is not Player player) return;

        _hasHit = true;
        var stats = player.Stats;
        float reduced = Damage * (1f - stats.DamageReduction);
        player.TakeDamage(reduced);

        SpawnImpactBurst();
        QueueFree();
    }

    private void SpawnImpactBurst()
    {
        if (_impactBurstScene == null) return;
        var instance = _impactBurstScene.Instantiate<ImpactBurst>();
        instance.ParticleTexture = _impactTexture;
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
        instance.SetDeferred("global_position", GlobalPosition);
    }
}
