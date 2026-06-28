using Godot;

/// <summary>
/// Nova d'Aether — détonation circulaire centrée sur le joueur. Inflige des dégâts à
/// tous les ennemis dans le rayon, puis matérialise une onde de choc lumineuse violette
/// qui se dilate. Gros effet visuel à chaque pulse.
/// </summary>
public partial class AetherNova : WeaponBase
{
    public float Radius { get; set; } = 140f;

    private PointLight2D? _coreLight;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage   = 16f;
        Cooldown = 2.2f;

        _lightTex ??= Player.MakeRadialLightTexture(64);
        _coreLight = new PointLight2D
        {
            Color        = new Color(0.667f, 0.267f, 1f),
            Energy       = 0.35f,
            Texture      = _lightTex,
            TextureScale = 3.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_coreLight);

        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_overload_pulse");
        ScreenShake.Instance?.Shake(4f, 0.15f);

        var center = player.GlobalPosition;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            if (center.DistanceTo(e.GlobalPosition) <= Radius)
                e.TakeDamage(Damage);
        }

        var blast = new NovaBlast { MaxRadius = Radius };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, blast);
        blast.SetDeferred("global_position", center);
    }
}

/// <summary>
/// Onde de choc visuelle : anneau violet qui se dilate de 0 → MaxRadius en ~0.35 s,
/// flash lumineux central, particules radiales. Auto-détruit.
/// </summary>
public partial class NovaBlast : Node2D
{
    public float MaxRadius { get; set; } = 140f;

    private Line2D? _ring;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        ZIndex = 4;

        // Anneau (Line2D fermé) qui grandit via scale
        _ring = new Line2D
        {
            Points       = BuildCircle(1f, 48),
            Closed       = true,
            Width        = 6f,
            DefaultColor = new Color(0.75f, 0.4f, 1f, 0.9f),
            JointMode    = Line2D.LineJointMode.Round,
        };
        AddChild(_ring);

        // Disque plein qui fond
        var disc = new Polygon2D
        {
            Polygon = BuildCircle(MaxRadius, 40),
            Color   = new Color(0.667f, 0.267f, 1f, 0.22f),
            ZIndex  = -1,
        };
        AddChild(disc);

        // Flash central
        _lightTex ??= Player.MakeRadialLightTexture(64);
        var flash = new PointLight2D
        {
            Color        = new Color(0.8f, 0.45f, 1f),
            Energy       = 3.4f,
            Texture      = _lightTex,
            TextureScale = MaxRadius / 16f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(flash);

        // Particules radiales
        var mat = new ParticleProcessMaterial
        {
            Direction          = Vector3.Zero,
            Spread             = 180f,
            InitialVelocityMin = MaxRadius * 2.0f,
            InitialVelocityMax = MaxRadius * 3.2f,
            Gravity            = Vector3.Zero,
            ScaleMin           = 2f,
            ScaleMax           = 4f,
            Color              = new Color(0.8f, 0.5f, 1f),
        };
        var particles = new GpuParticles2D
        {
            Amount        = 28,
            Lifetime      = 0.35,
            OneShot       = true,
            Explosiveness = 1f,
            Emitting      = true,
            ProcessMaterial = mat,
        };
        particles.Set("draw_pass_1", new QuadMesh { Size = new Vector2(4f, 4f) });
        AddChild(particles);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(_ring, "scale", new Vector2(MaxRadius, MaxRadius), 0.35f)
          .From(new Vector2(2f, 2f)).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_ring,  "modulate:a", 0f,   0.35f);
        tw.TweenProperty(disc,   "color:a",    0f,   0.30f);
        tw.TweenProperty(flash,  "energy",     0f,   0.28f);
        tw.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private static Vector2[] BuildCircle(float r, int segments)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = 2f * Mathf.Pi * i / segments;
            pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
        return pts;
    }
}
