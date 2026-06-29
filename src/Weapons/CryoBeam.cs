using Godot;

/// <summary>
/// VFX du rayon de la Lance Cryo : faisceau glacé (halo cyan large + cœur blanc) le long de +X sur
/// <see cref="Length"/> px, qui s'illumine puis s'efface (~0.18 s) + éclats de givre. Auto-libéré.
/// La rotation du Node2D = direction du tir.
/// </summary>
public partial class CryoBeam : Node2D
{
    public float Length { get; set; } = 360f;
    public int   Power  { get; set; } = 1;

    private static Texture2D? _lightTex;
    private static Texture2D? _frostTex;

    public override void _Ready()
    {
        ZIndex = 2;
        var end = new Vector2(Length, 0f);

        AddChild(new Line2D
        {
            Points = new[] { Vector2.Zero, end }, Width = 10f,
            DefaultColor = new Color(0.45f, 0.85f, 1f, 0.5f), BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
        });
        AddChild(new Line2D
        {
            Points = new[] { Vector2.Zero, end }, Width = 3.5f,
            DefaultColor = new Color(0.9f, 1f, 1f, 0.95f), BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
        });

        _lightTex ??= Player.MakeRadialLightTexture(64);
        var light = new PointLight2D
        {
            Position = new Vector2(Length * 0.5f, 0f),
            Color = new Color(0.5f, 0.85f, 1f), Energy = 1.5f + Power * 0.15f,
            Texture = _lightTex, TextureScale = Length / 38f, BlendMode = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        AddChild(BuildFrost());

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(this, "modulate:a", 0f, 0.18);
        tw.TweenProperty(light, "energy", 0f, 0.18);
        tw.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private GpuParticles2D BuildFrost()
    {
        _frostTex ??= Player.MakeRadialLightTexture(10);
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(Length * 0.5f, 4f, 0f),
            Direction = new Vector3(0, 1, 0), Spread = 180f,
            InitialVelocityMin = 20f, InitialVelocityMax = 60f, Gravity = Vector3.Zero,
            ScaleMin = 1f, ScaleMax = 2f,
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.8f, 0.95f, 1f, 0.8f));
        grad.SetColor(1, new Color(0.8f, 0.95f, 1f, 0f));
        mat.ColorRamp = new GradientTexture1D { Gradient = grad };
        var p = new GpuParticles2D
        {
            Position = new Vector2(Length * 0.5f, 0f),
            Amount = 18, Lifetime = 0.3, OneShot = true, Emitting = true, Explosiveness = 0.7f,
            ProcessMaterial = mat, Texture = _frostTex,
        };
        p.Set("draw_pass_1", new QuadMesh { Size = new Vector2(4f, 4f) });
        return p;
    }
}
