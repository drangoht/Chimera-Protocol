using Godot;

/// <summary>
/// VFX instancié à la position d'impact d'un projectile.
/// Déclenche un burst radial de particules one-shot + un flash lumineux, puis se détruit via Timer.
/// L'intensité (nombre de particules, taille du flash) scale avec <see cref="Power"/> = niveau de l'arme.
/// </summary>
public partial class ImpactBurst : Node2D
{
    [Export] public Texture2D? ParticleTexture { get; set; }

    /// <summary>Niveau de l'arme (1-5+). Plus haut = plus de particules, flash plus gros.</summary>
    public int   Power      { get; set; } = 1;
    public Color FlashColor { get; set; } = new Color(0.45f, 1f, 0.95f);

    private static Texture2D? _flashTex;

    public override void _Ready()
    {
        int p = Mathf.Clamp(Power, 1, 8);

        var particles = GetNode<GpuParticles2D>("Particles");
        if (ParticleTexture != null)
            particles.Texture = ParticleTexture;

        // Plus l'arme est forte, plus l'impact projette de débris et vite.
        particles.Amount = 6 + p * 4;                 // 10 → 42
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.InitialVelocityMin = 80f + p * 18f;
            mat.InitialVelocityMax = 160f + p * 30f;
            mat.ScaleMax = 2f + p * 0.25f;
        }
        particles.Emitting = true;

        // ── Flash lumineux ────────────────────────────────────────────────
        _flashTex ??= Player.MakeRadialLightTexture(48);
        var light = new PointLight2D
        {
            Texture      = _flashTex,
            Color        = FlashColor,
            Energy       = 1.6f + p * 0.5f,
            TextureScale = 1.4f + p * 0.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var tw = CreateTween();
        tw.TweenProperty(light, "energy", 0f, 0.18f).SetEase(Tween.EaseType.Out);

        var timer = GetNode<Godot.Timer>("Timer");
        timer.Timeout += QueueFree;
        timer.Start();
    }
}
