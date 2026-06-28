using Godot;

/// <summary>
/// Muzzle flash instancié au point de tir de l'ImpulseCannon.
/// PointLight2D qui s'estompe de 3.0 → 0 en 0.08 s + petites particules cyan.
/// Se détruit après 0.12 s.
/// </summary>
public partial class MuzzleFlash : Node2D
{
    private static Texture2D? _lightTex;
    private static Texture2D? _particleTex;

    public override void _Ready()
    {
        _lightTex ??= Player.MakeRadialLightTexture(32);

        if (_particleTex == null)
        {
            var img = Image.CreateEmpty(3, 3, false, Image.Format.Rgba8);
            img.Fill(new Color(0.267f, 1f, 0.933f, 1f));
            _particleTex = ImageTexture.CreateFromImage(img);
        }

        // ── PointLight2D flash ─────────────────────────────────────────────
        var light = new PointLight2D
        {
            Color        = new Color(0.267f, 1f, 0.933f, 1f),
            Energy       = 3.0f,
            Texture      = _lightTex,
            TextureScale = 3.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            ZIndex       = 4,
        };
        AddChild(light);

        // ── GPUParticles2D mini-burst directionnel ─────────────────────────
        var mat = new ParticleProcessMaterial
        {
            Direction          = new Vector3(1, 0, 0),
            Spread             = 25f,
            InitialVelocityMin = 80f,
            InitialVelocityMax = 160f,
            Gravity            = Vector3.Zero,
            ScaleMin           = 2.0f,
            ScaleMax           = 4.0f,
        };

        var colorGrad = new Gradient();
        colorGrad.SetColor(0, new Color(0.267f, 1f, 0.933f, 1f));
        colorGrad.SetColor(1, new Color(0.267f, 1f, 0.933f, 0f));
        mat.ColorRamp = new GradientTexture1D { Gradient = colorGrad };

        var particles = new GpuParticles2D
        {
            Amount          = 6,
            Lifetime        = 0.08,
            OneShot         = true,
            Emitting        = false,
            Explosiveness   = 1.0f,
            ProcessMaterial = mat,
            Texture         = _particleTex,
            ZIndex          = 4,
        };
        particles.Set("draw_pass_1", new QuadMesh { Size = new Vector2(3f, 3f) });
        AddChild(particles);
        particles.Emitting = true;

        // ── Tween : fade lumière 3.0 → 0 en 0.08 s ────────────────────────
        var tween = CreateTween();
        tween.TweenProperty(light, "energy", 0f, 0.08);

        // ── Auto-destruction ───────────────────────────────────────────────
        var timer = new Godot.Timer { WaitTime = 0.12, OneShot = true, Autostart = false };
        AddChild(timer);
        timer.Timeout += QueueFree;
        timer.Start();
    }
}
