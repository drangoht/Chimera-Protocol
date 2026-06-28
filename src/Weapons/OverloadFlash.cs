using Godot;

/// <summary>Node temporaire (0.18 s) affichant le flash visuel de surcharge.</summary>
public partial class OverloadFlash : Node2D
{
    public float Radius { get; set; } = 100f;
    private float _lifetime = 0.18f;
    private Polygon2D? _poly;
    private PointLight2D? _flashLight;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        _lightTex ??= Player.MakeRadialLightTexture(64);

        // Cercle plein flash (opaque → transparent)
        _poly = new Polygon2D
        {
            Color   = new Color(0.667f, 0.267f, 1f, 0.45f),
            Polygon = BuildCircle(Radius, 32),
            ZIndex  = 2,
        };
        AddChild(_poly);

        // Lumière flash forte
        _flashLight = new PointLight2D
        {
            Color        = new Color(0.667f, 0.267f, 1f, 1f),
            Energy       = 3.5f,
            Texture      = _lightTex,
            TextureScale = Radius / 18f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            ZIndex       = 3,
        };
        AddChild(_flashLight);

        // Tween : fade-out rapide en 0.18 s
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_poly,        "color:a",  0f, 0.18);
        tween.TweenProperty(_flashLight,  "energy",   0f, 0.14);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    public override void _Process(double delta)
    {
        _lifetime -= (float)delta;
        if (_lifetime <= 0f) QueueFree();
    }

    private static Vector2[] BuildCircle(float r, int segments)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.Pi * i / segments;
            pts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }
        return pts;
    }
}
