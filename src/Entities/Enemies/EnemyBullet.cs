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

    /// <summary>
    /// Tiré par un <b>champion</b> (mini-boss ou boss de fin) ? Le projectile survit à son tireur — il
    /// ne peut donc pas le lui demander au moment de l'impact, et le drapeau doit être posé au tir.
    /// Détermine l'application du plancher du cran de saturation VI
    /// (cf. <see cref="SaturationTable.ChampionDamage"/>).
    /// </summary>
    public bool FromChampion { get; set; }

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
        // Impact ponctuel → coup DISCRET, éligible au plancher du cran VI quand le tireur est un
        // champion. Le calcul ne peut pas passer par EnemyBase.DealDiscreteDamage : le tireur est
        // peut-être déjà mort, d'où le drapeau posé au moment du tir.
        int rank = GameSettings.Instance?.Saturation ?? 0;
        float raw = FromChampion
            ? SaturationTable.ChampionDamage(Damage, stats.MaxHp, rank)
            : Damage;
        float reduced = raw * (1f - stats.DamageReduction);
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
