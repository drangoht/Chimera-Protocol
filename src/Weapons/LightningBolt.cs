using Godot;

/// <summary>
/// Éclair dentelé éphémère entre deux points. Line2D blanc-cyan additif + flash
/// lumineux aux extrémités, fade en ~0.16 s.
/// </summary>
public partial class LightningBolt : Node2D
{
    public Vector2 From { get; set; }
    public Vector2 To   { get; set; }

    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        ZIndex = 6;

        var rng    = new RandomNumberGenerator();
        var dir    = To - From;
        float len  = dir.Length();
        var perp   = dir.Normalized().Orthogonal();
        int segs   = Mathf.Clamp((int)(len / 22f) + 2, 3, 14);

        var pts = new Vector2[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float jitter = (i == 0 || i == segs) ? 0f : rng.RandfRange(-10f, 10f);
            pts[i] = From.Lerp(To, t) + perp * jitter;
        }

        // Halo large (cyan translucide)
        var glow = new Line2D
        {
            Points     = pts,
            Width      = 7f,
            DefaultColor = new Color(0.3f, 0.8f, 1f, 0.35f),
            JointMode  = Line2D.LineJointMode.Round,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        AddChild(glow);

        // Coeur fin (blanc sur-exposé)
        var core = new Line2D
        {
            Points       = pts,
            Width        = 2.5f,
            DefaultColor = new Color(0.85f, 0.97f, 1f, 1f),
            JointMode    = Line2D.LineJointMode.Round,
        };
        AddChild(core);

        // Flash à l'arrivée
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var flash = new PointLight2D
        {
            Position     = To - GlobalPosition,
            Color        = new Color(0.5f, 0.9f, 1f),
            Energy       = 2.6f,
            Texture      = _lightTex,
            TextureScale = 2.2f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(flash);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(glow,  "modulate:a", 0f, 0.16f);
        tw.TweenProperty(core,  "modulate:a", 0f, 0.16f);
        tw.TweenProperty(flash, "energy",     0f, 0.16f);
        tw.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
