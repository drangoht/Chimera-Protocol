using Godot;

/// <summary>
/// Missile à tête chercheuse de l'Essaim Traqueur : part dans une direction initiale puis incurve sa
/// trajectoire vers l'ennemi le plus proche (re-cible si la cible meurt). Explose au contact (dégâts
/// + flash violet bref). Visuel : ogive violette + traînée + halo. Collision : mirroir du Bullet.
/// </summary>
public partial class SeekerMissile : Area2D
{
    public float Damage   { get; set; } = 7f;
    public float Speed    { get; set; } = 300f;
    public int   Power     { get; set; } = 1;
    public float TurnRate { get; set; } = 5.0f;   // vitesse d'incurvation (rad/s effectif via lerp)

    private Vector2     _dir = Vector2.Right;
    private EnemyBase?  _target;
    private float       _life = 4f;
    private bool        _hit;
    private static Texture2D? _lightTex;

    public void Launch(Vector2 initialDir, EnemyBase? target)
    {
        _dir    = initialDir.Normalized();
        _target = target;
    }

    public override void _Ready()
    {
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 9f } });

        // Ogive losange violette
        AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(8, 0), new(-5, 4), new(-3, 0), new(-5, -4) },
            Color   = new Color(0.7f, 0.35f, 1f),
        });
        _lightTex ??= Player.MakeRadialLightTexture(32);
        AddChild(new PointLight2D
        {
            Color        = new Color(0.66f, 0.35f, 1f),
            Energy       = 1.0f + Power * 0.18f,
            Texture      = _lightTex,
            TextureScale = 1.8f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        });
        AddChild(BuildTrail());

        BodyEntered += OnBodyEntered;
    }

    private static GpuParticles2D BuildTrail()
    {
        var mat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero, Spread = 0f,
            InitialVelocityMin = 0f, InitialVelocityMax = 0f, Gravity = Vector3.Zero,
            ScaleMin = 1.2f, ScaleMax = 2.2f,
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.7f, 0.35f, 1f, 0.6f));
        grad.SetColor(1, new Color(0.7f, 0.35f, 1f, 0f));
        mat.ColorRamp = new GradientTexture1D { Gradient = grad };
        var tex = Player.MakeRadialLightTexture(12);
        var p = new GpuParticles2D
        {
            Amount = 8, Lifetime = 0.22, Emitting = true, ProcessMaterial = mat,
            Texture = tex, ZIndex = -1,
        };
        p.Set("draw_pass_1", new QuadMesh { Size = new Vector2(4f, 4f) });
        return p;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_target == null || !IsInstanceValid(_target))
            _target = NearestEnemy();

        if (_target != null && IsInstanceValid(_target))
        {
            var desired = (_target.GlobalPosition - GlobalPosition).Normalized();
            _dir = _dir.Lerp(desired, Mathf.Clamp(TurnRate * dt, 0f, 1f)).Normalized();
        }

        Position += _dir * Speed * dt;
        Rotation  = _dir.Angle();

        _life -= dt;
        if (_life <= 0f) QueueFree();
    }

    private EnemyBase? NearestEnemy()
    {
        EnemyBase? best = null;
        float bestSq = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            float d = GlobalPosition.DistanceSquaredTo(e.GlobalPosition);
            if (d < bestSq) { bestSq = d; best = e; }
        }
        return best;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hit || body is not EnemyBase enemy) return;
        _hit = true;
        enemy.TakeDamage(Damage);
        SpawnBloom();
        QueueFree();
    }

    private void SpawnBloom()
    {
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var flash = new PointLight2D
        {
            Color = new Color(0.8f, 0.45f, 1f), Energy = 2.4f,
            Texture = _lightTex, TextureScale = 3.0f, BlendMode = PointLight2D.BlendModeEnum.Add,
        };
        // Timer enfant auto-libérateur : robuste même si le missile est libéré juste après
        // (pas de tween qui exigerait que le flash soit déjà dans l'arbre).
        var timer = new Godot.Timer { WaitTime = 0.18, OneShot = true, Autostart = true };
        timer.Timeout += flash.QueueFree;
        flash.AddChild(timer);
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flash);
        flash.SetDeferred("global_position", GlobalPosition);
    }
}
