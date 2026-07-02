using Godot;
using System.Collections.Generic;

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
    private ShaderMaterial? _backdropMat;
    private Node2D?         _dustFar;
    private Node2D?         _dustNear;
    private Node2D?         _motifLayer;
    private Camera2D?       _camera;

    // Marge du fond "sous l'arène" : doit couvrir la caméra à son excursion max
    // (clamp joueur ~928×576, cf. Player.cs) + demi-viewport (640×360 à zoom 1).
    private const float BackdropMargin = 650f;

    private const float DustFarParallax  = 0.55f;  // < 1 : lointain (suit moins la caméra)
    private const float DustNearParallax = 1.35f;  // > 1 : premier plan (devance la caméra)
    // Beaucoup plus lent que la grille (shader, parallax 0.30) : le décalage relatif entre les deux
    // couches est ce qui rend la profondeur perceptible à travers les tuiles vitrées.
    private const float MotifParallax = 0.06f;

    // Réglages d'intensité par biome : (force brume, force rais, couleur brume, angle rais).
    private readonly record struct AtmoCfg(float Fog, float Shaft, Color FogColor, float Angle);

    private static AtmoCfg ConfigFor(string id, Color accent) => id switch
    {
        "sanctuaire" => new(0.20f, 0.16f, new Color(0.40f, 0.55f, 0.70f), 0.55f),
        "aether"     => new(0.30f, 0.28f, new Color(0.55f, 0.42f, 0.85f), 0.70f),
        "fournaise"  => new(0.32f, 0.32f, new Color(0.80f, 0.45f, 0.28f), 0.85f),
        "givre"      => new(0.36f, 0.18f, new Color(0.60f, 0.80f, 0.92f), 0.45f),
        "neon"       => new(0.26f, 0.48f, new Color(0.70f, 0.35f, 0.85f), 0.62f),
        _            => new(0.10f, 0.14f, accent.Lerp(Colors.White, 0.3f), 0.6f),
    };

    /// <summary>Configure et construit toutes les couches pour le biome donné.</summary>
    public void Configure(string biomeId, Color accent, IReadOnlyList<Vector2> glassClusterCenters)
    {
        var cfg = ConfigFor(biomeId, accent);

        _motifLayer = BuildDeepMotifs(accent, glassClusterCenters);
        BuildBackdropVoid(accent);
        BuildFog(cfg);
        BuildShafts(cfg, accent);
        _dustFar  = BuildDust(accent, amount: 26, scale: 2.2f, zIndex: -6, alpha: 0.34f);
        _dustNear = BuildDust(accent, amount: 16, scale: 3.4f, zIndex: -5, alpha: 0.24f);
    }

    /// <summary>
    /// Glyphes lointains (<see cref="DeepMotifShape"/>), décalés manuellement en parallaxe (comme
    /// la poussière) à un facteur très différent de la grille (<see cref="MotifParallax"/> vs 0.30
    /// du shader) : c'est cet écart de vitesse relative, entre deux couches distinctes, qui rend
    /// l'effet de profondeur lisible à travers les tuiles vitrées.
    ///
    /// Un motif est placé (avec un léger jitter) au centre de CHAQUE amas de tuiles vitrées
    /// (<paramref name="glassClusterCenters"/>, fourni par GroundRenderer) : un tirage purement
    /// aléatoire indépendant du placement des vitres en ratait trop souvent — l'utilisateur doit
    /// voir le parallaxe à travers CHAQUE fenêtre, pas seulement « parfois ». Quelques motifs
    /// additionnels dispersés plus largement ajoutent de l'ambiance au-delà des murs.
    /// </summary>
    private Node2D BuildDeepMotifs(Color accent, IReadOnlyList<Vector2> glassClusterCenters)
    {
        var layer = new Node2D { ZIndex = -11 };
        AddChild(layer);

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        foreach (var center in glassClusterCenters)
        {
            var jitter = new Vector2(rng.RandfRange(-24f, 24f), rng.RandfRange(-24f, 24f));
            layer.AddChild(new DeepMotifShape
            {
                Position = center + jitter,
                Rotation = rng.RandfRange(0f, Mathf.Tau),
                Scale    = Vector2.One * rng.RandfRange(1.9f, 2.7f),
                Modulate = new Color(accent.R, accent.G, accent.B, 0.58f),
            });
        }

        // Zone plus resserrée que BackdropMargin : la densité doit surtout payer là où le joueur
        // peut réellement voir à travers (arène + juste au-delà des murs), pas dans la marge
        // caméra lointaine que le clamp joueur n'atteint quasiment jamais.
        const float spread = 320f;
        float hw = Constants.ArenaWidth  / 2f + spread;
        float hh = Constants.ArenaHeight / 2f + spread;
        for (int i = 0; i < 22; i++)
        {
            layer.AddChild(new DeepMotifShape
            {
                Position = new Vector2(rng.RandfRange(-hw, hw), rng.RandfRange(-hh, hh)),
                Rotation = rng.RandfRange(0f, Mathf.Tau),
                Scale    = Vector2.One * rng.RandfRange(1.4f, 2.4f),
                Modulate = new Color(accent.R, accent.G, accent.B, 0.40f),
            });
        }
        return layer;
    }

    /// <summary>
    /// Fond parallax visible au-delà des murs (zone de marge de la caméra) : tuile transparente
    /// répétée à l'infini par le shader (indépendant de la taille du polygone), teintée par
    /// l'accent du biome. ZIndex -11 : au-dessus du fond opaque (-12) de GroundRenderer.AddBackdrop,
    /// sous les tuiles de sol (-10) qui le masquent partout où l'arène est réellement jouable.
    /// </summary>
    private void BuildBackdropVoid(Color accent)
    {
        var shader = GD.Load<Shader>("res://assets/shaders/backdrop_parallax.gdshader");
        var tex    = GD.Load<Texture2D>("res://assets/sprites/tileset/backdrop_tile.png");
        if (shader == null || tex == null) return;
        _backdropMat = new ShaderMaterial { Shader = shader };
        _backdropMat.SetShaderParameter("tex", tex);
        // Pas plus grossier (96 vs 64) : une fenêtre vitrée (64-96px) ne voit alors qu'une fraction
        // du motif plutôt qu'un motif répété plusieurs fois — plus facile à repérer en mouvement
        // (un fin quadrillage périodique se lit comme statique même quand il bouge réellement).
        _backdropMat.SetShaderParameter("tile_size", new Vector2(96f, 96f));
        _backdropMat.SetShaderParameter("tint", accent);
        _backdropMat.SetShaderParameter("parallax", 0.30f);
        _backdropMat.SetShaderParameter("alpha_mult", 0.85f);
        AddChild(VoidOverlay(_backdropMat, zIndex: -11));
    }

    /// <summary>Polygon2D couvrant la marge caméra au-delà des murs (voir <see cref="BackdropMargin"/>).</summary>
    private static Polygon2D VoidOverlay(ShaderMaterial mat, int zIndex)
    {
        const float hw = Constants.ArenaWidth / 2f + BackdropMargin;
        const float hh = Constants.ArenaHeight / 2f + BackdropMargin;
        return new Polygon2D
        {
            Polygon  = new Vector2[] { new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh) },
            Color    = Colors.White,
            Material = mat,
            ZIndex   = zIndex,
        };
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
        _backdropMat?.SetShaderParameter("cam_offset", cam);

        // Parallaxe de la poussière : décalage = cam × (1 - facteur). facteur<1 => lag (lointain),
        // facteur>1 => devance (premier plan).
        if (_dustFar     != null) _dustFar.Position     = cam * (1f - DustFarParallax);
        if (_dustNear    != null) _dustNear.Position    = cam * (1f - DustNearParallax);
        if (_motifLayer  != null) _motifLayer.Position  = cam * (1f - MotifParallax);
    }
}
