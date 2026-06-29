using Godot;

/// <summary>
/// Puits gravitationnel de la Singularité. Pendant <see cref="Duration"/> s : aspire les ennemis du
/// rayon vers son centre (déplace leur GlobalPosition, en plus de leur IA) et leur inflige des dégâts
/// toutes les <see cref="TickInterval"/> s. VFX vortex : bras spiralés violets en rotation + cœur
/// sombre + anneau + particules aspirées + lumière. Auto-libéré en fin de vie.
/// </summary>
public partial class GravityWell : Node2D
{
    public float Damage       { get; set; } = 6f;
    public float Radius       { get; set; } = 120f;
    public float PullSpeed    { get; set; } = 90f;
    public float Duration     { get; set; } = 2.2f;
    public float TickInterval { get; set; } = 0.4f;
    public int   Power        { get; set; } = 1;

    private const float MaxRadius   = 200f;   // plafond dur (anti-trivialisation epic)
    private const float InnerRadius = 12f;    // pas d'aspiration au cœur (évite le jitter)

    private float _life;
    private float _tick;
    private float _spin;
    private PointLight2D? _light;

    public override void _Ready()
    {
        Radius = Mathf.Min(Radius, MaxRadius);
        _life  = Duration;
        ZIndex = 1;

        BuildVisual();

        var tw = CreateTween();   // apparition (scale up bref)
        Scale = new Vector2(0.2f, 0.2f);
        tw.TweenProperty(this, "scale", Vector2.One, 0.18).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void BuildVisual()
    {
        // Cœur sombre
        AddChild(new Polygon2D { Polygon = Circle(InnerRadius + 6f, 20), Color = new Color(0.06f, 0.02f, 0.12f, 0.9f), ZIndex = 1 });

        // Bras spiralés (conteneur tourné dans _Process)
        var swirl = new Node2D { Name = "Swirl" };
        AddChild(swirl);
        for (int arm = 0; arm < 3; arm++)
            swirl.AddChild(SpiralArm(arm * Mathf.Tau / 3f));

        // Anneau d'horizon
        AddChild(new Line2D
        {
            Points = Circle(Radius, 40, close: true), Width = 2f,
            DefaultColor = new Color(0.7f, 0.4f, 1f, 0.5f), Closed = true,
        });

        // Particules aspirées
        AddChild(BuildParticles());

        // Lumière violette pulsante
        var tex = Player.MakeRadialLightTexture(64);
        _light = new PointLight2D
        {
            Color = new Color(0.6f, 0.3f, 1f), Energy = 1.3f + Power * 0.12f,
            Texture = tex, TextureScale = Radius / 34f, BlendMode = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_light);
    }

    private Line2D SpiralArm(float baseAngle)
    {
        const int N = 18;
        var pts = new Vector2[N];
        for (int i = 0; i < N; i++)
        {
            float t = i / (float)(N - 1);
            float r = Mathf.Lerp(Radius, InnerRadius, t);
            float a = baseAngle + t * 3.2f;   // resserrement de la spirale
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.7f, 0.4f, 1f, 0.0f));
        grad.SetColor(1, new Color(0.85f, 0.6f, 1f, 0.9f));
        return new Line2D { Points = pts, Width = 2.5f, Gradient = grad };
    }

    private GpuParticles2D BuildParticles()
    {
        var tex = Player.MakeRadialLightTexture(10);
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius      = Radius,
            EmissionRingInnerRadius = Radius * 0.8f,
            EmissionRingHeight      = 0f,
            EmissionRingAxis        = new Vector3(0, 0, 1),
            Gravity = Vector3.Zero,
            OrbitVelocityMin = 0.5f, OrbitVelocityMax = 0.8f,    // tourbillon
            RadialVelocityMin = -Radius * 0.9f, RadialVelocityMax = -Radius * 0.6f, // aspiration vers le centre
            ScaleMin = 1.2f, ScaleMax = 2.2f,
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.8f, 0.55f, 1f, 0.0f));
        grad.SetColor(1, new Color(0.8f, 0.55f, 1f, 0.85f));
        mat.ColorRamp = new GradientTexture1D { Gradient = grad };
        var p = new GpuParticles2D
        {
            Amount = 40, Lifetime = 1.1, Preprocess = 0.5, Emitting = true,
            ProcessMaterial = mat, Texture = tex,
        };
        p.Set("draw_pass_1", new QuadMesh { Size = new Vector2(4f, 4f) });
        return p;
    }

    public override void _Process(double delta)
    {
        _spin += 3.2f * (float)delta;
        GetNodeOrNull<Node2D>("Swirl")?.Set("rotation", _spin);
        if (_light != null) _light.Energy = (1.2f + Power * 0.12f) * (0.85f + 0.15f * Mathf.Sin(_spin * 2f));
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Aspiration + dégâts par tick
        _tick += dt;
        bool doTick = _tick >= TickInterval;
        if (doTick) _tick -= TickInterval;

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            var toCenter = GlobalPosition - e.GlobalPosition;
            float dist = toCenter.Length();
            if (dist > Radius) continue;

            if (dist > InnerRadius)
            {
                float step = Mathf.Min(PullSpeed * dt, dist - InnerRadius);
                e.GlobalPosition += toCenter.Normalized() * step;
            }
            if (doTick) e.TakeDamage(Damage);
        }

        _life -= dt;
        if (_life <= 0f)
        {
            var tw = CreateTween();
            tw.TweenProperty(this, "modulate:a", 0f, 0.2);
            tw.TweenCallback(Callable.From(QueueFree));
            SetPhysicsProcess(false);
            SetProcess(false);
        }
    }

    private static Vector2[] Circle(float r, int seg, bool close = false)
    {
        var pts = new Vector2[seg + (close ? 1 : 0)];
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.Tau;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        if (close) pts[seg] = pts[0];
        return pts;
    }
}
