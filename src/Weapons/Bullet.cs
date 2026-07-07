using Godot;

public partial class Bullet : Area2D
{
    public Vector2 Direction  { get; set; } = Vector2.Right;
    public float   Speed      { get; set; } = 400f;
    public float   Damage     { get; set; } = 10f;
    public bool    IsPiercing { get; set; } = false;

    /// <summary>Niveau de l'arme tirante (1-5+). Calibre la brillance et l'impact.</summary>
    public int     Power      { get; set; } = 1;

    // Affinité de biome portée par la balle (greffes Œil/Ruche assimilées en Fournaise/Givre, §21).
    // Neutre par défaut (aucun effet) → n'impacte pas les balles d'armes normales.
    public float   BurnDps    { get; set; } = 0f;
    public float   BurnTime   { get; set; } = 0f;
    public float   SlowMult   { get; set; } = 1f;
    public float   SlowTime   { get; set; } = 0f;

    private float _lifetime = 3f;

    private static PackedScene? _impactBurstScene;
    private static Texture2D?   _impactTexture;
    private static Texture2D?   _bulletLightTex;
    private static Texture2D?   _trailParticleTex;

    public override void _Ready()
    {
        _impactBurstScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_impact_burst.tscn");
        _impactTexture    ??= GD.Load<Texture2D>("res://assets/sprites/vfx/vfx_particle_impact_plasma.png");
        _bulletLightTex   ??= Player.MakeRadialLightTexture(32);

        if (_trailParticleTex == null)
        {
            var img = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
            img.Fill(new Color(0.267f, 1f, 0.933f, 0.6f));
            _trailParticleTex = ImageTexture.CreateFromImage(img);
        }

        // ── PointLight2D sur la balle (brillance qui scale avec le niveau) ─
        int p = Mathf.Clamp(Power, 1, 8);
        var light = new PointLight2D
        {
            Color        = new Color(0.267f, 1f, 0.933f, 1f),
            Energy       = 1.4f + p * 0.35f,
            Texture      = _bulletLightTex,
            TextureScale = 2.2f + p * 0.45f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        // ── Trail GPUParticles2D ──────────────────────────────────────────
        var trailMat = new ParticleProcessMaterial
        {
            Direction          = Vector3.Zero,
            Spread             = 0f,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 0f,
            Gravity            = Vector3.Zero,
            ScaleMin           = 1.5f,
            ScaleMax           = 3.0f,
        };
        var trailColorGrad = new Gradient();
        trailColorGrad.SetColor(0, new Color(0.267f, 1f, 0.933f, 0.6f));
        trailColorGrad.SetColor(1, new Color(0.267f, 1f, 0.933f, 0f));
        trailMat.ColorRamp = new GradientTexture1D { Gradient = trailColorGrad };

        var trail = new GpuParticles2D
        {
            Name            = "Trail",
            Amount          = 3,
            Lifetime        = 0.08,
            OneShot         = false,
            Emitting        = true,
            Explosiveness   = 0f,
            ProcessMaterial = trailMat,
            Texture         = _trailParticleTex,
            ZIndex          = -1,
        };
        trail.Set("draw_pass_1", new QuadMesh { Size = new Vector2(3f, 3f) });
        AddChild(trail);

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
        if (body is EnemyBase enemy)
        {
            enemy.TakeDamage(Damage);
            if (BurnDps > 0f && BurnTime > 0f) enemy.ApplyBurn(BurnDps, BurnTime); // affinité Fournaise
            if (SlowMult < 1f && SlowTime > 0f) enemy.ApplySlow(SlowMult, SlowTime); // affinité Givre
            SpawnImpactBurst();
            if (!IsPiercing)
                QueueFree();
        }
    }

    private void SpawnImpactBurst()
    {
        if (_impactBurstScene == null) return;
        var instance = _impactBurstScene.Instantiate<ImpactBurst>();
        instance.ParticleTexture = _impactTexture;
        instance.Power           = Power;
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
        instance.SetDeferred("global_position", GlobalPosition);

        // À haut niveau, chaque impact secoue brièvement l'écran : ça « cogne ».
        if (Power >= 4)
            ScreenShake.Instance?.Shake(1.2f + Power * 0.3f, 0.05f);
    }
}
