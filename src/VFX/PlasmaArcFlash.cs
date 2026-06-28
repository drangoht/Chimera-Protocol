using Godot;

/// <summary>
/// VFX arc plasma instancié à chaque swing de PlasmaBlade.
/// GPUParticles2D en demi-cercle cyan one-shot + PointLight2D flash bref.
/// La rotation du Node2D correspond à la direction d'attaque.
/// Durée totale : 0.2 s (timer), particules lifetime = 0.12 s.
/// </summary>
public partial class PlasmaArcFlash : Node2D
{
    private static Texture2D? _particleTex;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        // ── Texture particule ─────────────────────────────────────────────
        if (_particleTex == null)
        {
            // Carré cyan 4×4 px
            var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            img.Fill(new Color(0.267f, 1f, 0.933f, 1f));
            _particleTex = ImageTexture.CreateFromImage(img);
        }

        // ── Texture lumière radiale ────────────────────────────────────────
        _lightTex ??= Player.MakeRadialLightTexture(64);

        // ── GPUParticles2D arc plasma ──────────────────────────────────────
        var mat = new ParticleProcessMaterial
        {
            Direction         = new Vector3(1, 0, 0), // horizontal, la rotation du Node2D gère l'orientation
            Spread            = 90f,                   // demi-cercle (±90° autour de la direction)
            InitialVelocityMin = 60f,
            InitialVelocityMax = 140f,
            Gravity           = Vector3.Zero,
            ScaleMin          = 3.5f,
            ScaleMax          = 7.0f,
            // Couleur dégradé : cyan opaque → cyan transparent
            Color             = new Color(0.267f, 1f, 0.933f, 0.9f),
        };

        // Gradient couleur : début opaque → fin transparent
        var colorGrad = new Gradient();
        colorGrad.SetColor(0, new Color(0.267f, 1f, 0.933f, 0.9f));
        colorGrad.SetColor(1, new Color(0.267f, 1f, 0.933f, 0f));
        var colorRamp = new GradientTexture1D { Gradient = colorGrad };
        mat.ColorRamp = colorRamp;

        var quadMesh = new QuadMesh { Size = new Vector2(5f, 5f) };

        var particles = new GpuParticles2D
        {
            Name             = "Particles",
            Amount           = 24,
            Lifetime         = 0.12,
            OneShot          = true,
            Emitting         = false,
            Explosiveness    = 1.0f,
            ProcessMaterial  = mat,
            Texture          = _particleTex,
            ZIndex           = 3,
        };
        particles.Set("draw_pass_1", quadMesh);
        AddChild(particles);

        // ── PointLight2D flash bref ────────────────────────────────────────
        var light = new PointLight2D
        {
            Name         = "ArcLight",
            Color        = new Color(0.267f, 1f, 0.933f, 1f),
            Energy       = 2.5f,
            Texture      = _lightTex,
            TextureScale = 5.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            ZIndex       = 4,
        };
        AddChild(light);

        // ── Déclenche les particules ──────────────────────────────────────
        particles.Emitting = true;

        // ── Tween : énergie lumière 2.5 → 0 en 0.18 s ────────────────────
        var tween = CreateTween();
        tween.TweenProperty(light, "energy", 0f, 0.18);

        // ── Timer auto-destruction ─────────────────────────────────────────
        var timer = new Godot.Timer
        {
            WaitTime  = 0.22,
            OneShot   = true,
            Autostart = false,
        };
        AddChild(timer);
        timer.Timeout += QueueFree;
        timer.Start();
    }
}
