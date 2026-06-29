using Godot;

/// <summary>
/// Couche atmosphérique d'arène (Phase 2) : brume animée + rais de lumière (god-rays) + poussière
/// en parallaxe, le tout thématisé par biome. Construit par <see cref="GroundRenderer"/> avec
/// l'accent du biome courant.
///
/// Profondeur : les overlays/poussière sont rendus AU-DESSUS du sol (overlay grille z=-7) mais
/// SOUS les entités (z=0) — atmosphère « traversée » par le joueur, sans gêner la lisibilité.
///
/// Parallaxe : les shaders échantillonnent à FRAGCOORD + cam_offset × parallax (mis à jour chaque
/// frame depuis le centre caméra) ; la poussière est décalée manuellement → couches à profondeurs
/// distinctes qui ne suivent pas le sol au même rythme.
/// </summary>
public partial class BiomeAtmosphere : Node2D
{
    private ShaderMaterial? _fogMat;
    private ShaderMaterial? _shaftMat;
    private Node2D?         _dustFar;
    private Node2D?         _dustNear;
    private Camera2D?       _camera;

    private const float DustFarParallax  = 0.55f;  // < 1 : lointain (suit moins la caméra)
    private const float DustNearParallax = 1.35f;  // > 1 : premier plan (devance la caméra)

    // Réglages d'intensité par biome : (force brume, force rais, couleur brume, angle rais).
    private readonly record struct AtmoCfg(float Fog, float Shaft, Color FogColor, float Angle);

    private static AtmoCfg ConfigFor(string id, Color accent) => id switch
    {
        "sanctuaire" => new(0.07f, 0.09f, new Color(0.40f, 0.55f, 0.70f), 0.55f),
        "aether"     => new(0.12f, 0.17f, new Color(0.55f, 0.42f, 0.85f), 0.70f),
        "fournaise"  => new(0.13f, 0.20f, new Color(0.80f, 0.45f, 0.28f), 0.85f),
        "givre"      => new(0.17f, 0.10f, new Color(0.60f, 0.80f, 0.92f), 0.45f),
        "neon"       => new(0.10f, 0.32f, new Color(0.70f, 0.35f, 0.85f), 0.62f),
        _            => new(0.10f, 0.14f, accent.Lerp(Colors.White, 0.3f), 0.6f),
    };

    /// <summary>Configure et construit toutes les couches pour le biome donné.</summary>
    public void Configure(string biomeId, Color accent)
    {
        var cfg = ConfigFor(biomeId, accent);

        BuildFog(cfg);
        BuildShafts(cfg, accent);
        _dustFar  = BuildDust(accent, amount: 26, scale: 2.2f, zIndex: -6, alpha: 0.16f);
        _dustNear = BuildDust(accent, amount: 16, scale: 3.4f, zIndex: -5, alpha: 0.10f);
    }

    private void BuildFog(AtmoCfg cfg)
    {
        var shader = GD.Load<Shader>("res://assets/shaders/fog.gdshader");
        if (shader == null) return;
        _fogMat = new ShaderMaterial { Shader = shader };
        _fogMat.SetShaderParameter("fog_color", cfg.FogColor);
        _fogMat.SetShaderParameter("strength", cfg.Fog);
        _fogMat.SetShaderParameter("parallax", 0.35f);
        AddChild(FullArenaOverlay(_fogMat, zIndex: -6));
    }

    private void BuildShafts(AtmoCfg cfg, Color accent)
    {
        var shader = GD.Load<Shader>("res://assets/shaders/light_shafts.gdshader");
        if (shader == null) return;
        _shaftMat = new ShaderMaterial { Shader = shader };
        _shaftMat.SetShaderParameter("shaft_color", accent);
        _shaftMat.SetShaderParameter("strength", cfg.Shaft);
        _shaftMat.SetShaderParameter("parallax", 0.15f);
        _shaftMat.SetShaderParameter("angle", cfg.Angle);
        AddChild(FullArenaOverlay(_shaftMat, zIndex: -5));
    }

    /// <summary>Polygon2D couvrant l'arène (+ marge), portant un ShaderMaterial plein écran.</summary>
    private static Polygon2D FullArenaOverlay(ShaderMaterial mat, int zIndex)
    {
        const float hw = Constants.ArenaWidth / 2f + 64f;
        const float hh = Constants.ArenaHeight / 2f + 64f;
        return new Polygon2D
        {
            Polygon  = new Vector2[] { new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh) },
            Color    = Colors.White,
            Material = mat,
            ZIndex   = zIndex,
        };
    }

    private static Texture2D? _moteTex;

    /// <summary>Couche de poussière : motes lentes couvrant l'arène, parallaxée par le parent.</summary>
    private Node2D BuildDust(Color accent, int amount, float scale, int zIndex, float alpha)
    {
        _moteTex ??= Player.MakeRadialLightTexture(16);
        var layer = new Node2D { ZIndex = zIndex };
        AddChild(layer);

        var mat = new ParticleProcessMaterial
        {
            EmissionShape     = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(Constants.ArenaWidth / 2f + 200f, Constants.ArenaHeight / 2f + 200f, 0f),
            Gravity           = Vector3.Zero,
            Direction         = new Vector3(1f, 0.2f, 0f),
            Spread            = 180f,
            InitialVelocityMin = 4f,
            InitialVelocityMax = 14f,
            ScaleMin          = scale * 0.6f,
            ScaleMax          = scale,
            Color             = new Color(accent.R, accent.G, accent.B, alpha),
        };

        var particles = new GpuParticles2D
        {
            Amount          = amount,
            Lifetime        = 9.0,
            Preprocess      = 6.0,
            ProcessMaterial = mat,
            Texture         = _moteTex,
        };
        particles.Set("draw_pass_1", new QuadMesh { Size = new Vector2(8f, 8f) });
        layer.AddChild(particles);
        return layer;
    }

    public override void _Process(double delta)
    {
        _camera ??= GameManager.Instance?.PlayerInstance?.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera == null) return;

        Vector2 cam = _camera.GetScreenCenterPosition();

        _fogMat?.SetShaderParameter("cam_offset", cam);
        _shaftMat?.SetShaderParameter("cam_offset", cam);

        // Parallaxe de la poussière : décalage = cam × (1 - facteur). facteur<1 => lag (lointain),
        // facteur>1 => devance (premier plan).
        if (_dustFar  != null) _dustFar.Position  = cam * (1f - DustFarParallax);
        if (_dustNear != null) _dustNear.Position = cam * (1f - DustNearParallax);
    }
}
