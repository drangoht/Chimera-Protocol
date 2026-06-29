using Godot;

/// <summary>
/// VFX du souffle du Jet de Pyre : cône de particules de flammes (jaune chaud → rouge) projeté le
/// long de +X sur ~<see cref="Range"/> px + lueur ardente. Émission one-shot, auto-libérée par Timer.
/// La rotation du Node2D = direction du souffle.
/// </summary>
public partial class PyreFlame : Node2D
{
    public float ConeAngle { get; set; } = 50f;
    public float Range     { get; set; } = 130f;

    private static Texture2D? _flameTex;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        ZIndex = 2;

        _flameTex ??= Player.MakeRadialLightTexture(16);
        var mat = new ParticleProcessMaterial
        {
            Direction = new Vector3(1, 0, 0), Spread = ConeAngle * 0.5f,
            InitialVelocityMin = Range * 2.4f, InitialVelocityMax = Range * 3.6f,
            Gravity = Vector3.Zero, ScaleMin = 2.5f, ScaleMax = 5.0f,
        };
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 0.9f, 0.45f, 0.9f));   // jaune chaud
        grad.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));      // rouge → transparent
        mat.ColorRamp = new GradientTexture1D { Gradient = grad };

        var p = new GpuParticles2D
        {
            Amount = 36, Lifetime = 0.28, OneShot = true, Emitting = true, Explosiveness = 0.35f,
            ProcessMaterial = mat, Texture = _flameTex,
        };
        p.Set("draw_pass_1", new QuadMesh { Size = new Vector2(6f, 6f) });
        AddChild(p);

        _lightTex ??= Player.MakeRadialLightTexture(64);
        var light = new PointLight2D
        {
            Position = new Vector2(Range * 0.4f, 0f),
            Color = new Color(1f, 0.5f, 0.2f), Energy = 1.8f,
            Texture = _lightTex, TextureScale = Range / 26f, BlendMode = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var tw = CreateTween();
        tw.TweenProperty(light, "energy", 0f, 0.25);

        var timer = new Godot.Timer { WaitTime = 0.6, OneShot = true, Autostart = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
    }
}
