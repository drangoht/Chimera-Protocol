using Godot;

/// <summary>
/// VFX du coup de Lame Plasma : un croissant d'énergie cyan tracé dans l'arc d'attaque,
/// qui s'illumine, gonfle légèrement, balaie l'arc puis s'efface (~0.22 s).
/// Remplace l'ancien nuage de particules carrées (perçu comme un « rectangle clignotant »).
/// La rotation du Node2D = direction d'attaque ; le tracé est centré sur +X en local.
/// <see cref="ArcRadiusPx"/> / <see cref="ArcAngleDeg"/> sont posés par PlasmaBlade avant l'ajout
/// à l'arbre (sinon valeurs par défaut).
/// </summary>
public partial class PlasmaArcFlash : Node2D
{
    public float ArcRadiusPx { get; set; } = 80f;
    public float ArcAngleDeg { get; set; } = 180f;

    private static readonly Color Cyan  = new(0.267f, 1f, 0.933f);
    private static readonly Color White = new(0.85f, 1f, 1f);
    private static Texture2D? _lightTex;

    private float        _t;            // 0 → 1 sur la durée du flash
    private PointLight2D? _light;

    public override void _Ready()
    {
        ZIndex = 4;

        // ── Lueur brève au centre de l'arc (devant le joueur) ──────────────
        // Flash discret au bord de l'arc — volontairement modéré pour ne pas noyer le tracé du
        // croissant (sinon on ne voit qu'un gros halo cyan, cf. itération précédente).
        _lightTex ??= Player.MakeRadialLightTexture(64);
        _light = new PointLight2D
        {
            Color        = Cyan,
            Energy       = 1.3f,
            Texture      = _lightTex,
            TextureScale = ArcRadiusPx / 26f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            Position     = new Vector2(ArcRadiusPx * 0.85f, 0f),
        };
        AddChild(_light);

        // ── Animation : énergie lumière → 0, progression _t → 1, puis libère ─
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_light, "energy", 0f, 0.18);
        tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, 0.22);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private void SetProgress(float v)
    {
        _t = v;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float half = Mathf.DegToRad(ArcAngleDeg / 2f);
        float grow = 1f + 0.18f * _t;            // le croissant gonfle un peu en s'effaçant
        float r1   = ArcRadiusPx * grow;         // rayon extérieur
        float r0   = ArcRadiusPx * 0.5f * grow;  // rayon intérieur
        float rMid = (r0 + r1) * 0.5f;
        float band = r1 - r0;
        float a    = 1f - _t;                    // fondu sortant
        const int N = 24;

        // ── Corps du croissant (band cyan saturé) ──────────────────────────
        DrawArc(Vector2.Zero, rMid, -half, half, N, new Color(Cyan, 0.45f * a), band, true);
        // ── Bords lumineux : halo cyan large + cœur blanc franc (le « fil » de la lame) ──
        DrawArc(Vector2.Zero, r1, -half, half, N, new Color(Cyan,  0.7f * a), 10f, true);
        DrawArc(Vector2.Zero, r1, -half, half, N, new Color(White, 1.0f * a),  4f, true);
        DrawArc(Vector2.Zero, r0, -half, half, N, new Color(Cyan,  0.5f * a),  3f, true);

        // ── Tranche lumineuse qui balaie l'arc (effet de coup de lame) ─────
        float sweep = -half + 2f * half * _t;
        var sdir = new Vector2(Mathf.Cos(sweep), Mathf.Sin(sweep));
        DrawLine(sdir * r0, sdir * (r1 + band * 0.3f), new Color(White, 0.9f * a), 4f, true);
    }
}
