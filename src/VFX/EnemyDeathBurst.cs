using Godot;

/// <summary>
/// VFX instancié à la mort d'un ennemi — burst de particules radial + flash lumineux.
/// L'intensité scale avec <see cref="Tier"/> (0 = fourrage, 3 = mini-boss/colosse).
/// Les gros ennemis (tier ≥ 2) déclenchent en plus un anneau de choc.
/// Se détruit automatiquement via Timer après la durée des particules.
/// </summary>
public partial class EnemyDeathBurst : Node2D
{
    /// <summary>Calibre de l'explosion : 0 fourrage → 3 boss.</summary>
    public int   Tier      { get; set; } = 0;
    public Color FlashTint { get; set; } = new Color(1f, 0.55f, 0.3f);

    private static Texture2D?  _flashTex;
    private static PackedScene? _shockwaveScene;

    public override void _Ready()
    {
        int t = Mathf.Clamp(Tier, 0, 3);

        var particles = GetNode<GpuParticles2D>("Particles");
        particles.Amount = 14 + t * 14;                      // 14 → 56
        if (particles.ProcessMaterial is ParticleProcessMaterial mat)
        {
            mat.InitialVelocityMin = 80f + t * 45f;
            mat.InitialVelocityMax = 150f + t * 80f;
            mat.ScaleMax = 2.4f + t * 0.9f;
        }
        particles.Emitting = true;

        // ── Flash lumineux ────────────────────────────────────────────────
        _flashTex ??= Player.MakeRadialLightTexture(64);
        var light = new PointLight2D
        {
            Texture      = _flashTex,
            Color        = FlashTint,
            Energy       = 2.2f + t * 1.2f,
            TextureScale = 1.6f + t * 1.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        CreateTween().TweenProperty(light, "energy", 0f, 0.22f + t * 0.06f).SetEase(Tween.EaseType.Out);

        // ── Anneau de choc pour les gros ennemis ──────────────────────────
        if (t >= 2)
            SpawnShockwave();

        var timer = GetNode<Godot.Timer>("Timer");
        timer.Timeout += QueueFree;
        timer.Start();
    }

    private void SpawnShockwave()
    {
        _shockwaveScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_shockwave_ring.tscn");
        if (_shockwaveScene == null) return;
        var ring = _shockwaveScene.Instantiate<Node2D>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
        ring.SetDeferred("global_position", GlobalPosition);
    }
}
