using Godot;

/// <summary>
/// Power-up temporaire ramassable. Apparaît pendant la run (via PowerUpSpawner) ; au contact du
/// joueur, applique son buff à durée limitée puis disparaît. Visuel : orbe hexagonal coloré (couleur
/// du buff) + anneau tournant + halo pulsant. Le <see cref="Type"/> est posé avant l'ajout à l'arbre.
/// </summary>
public partial class PowerUpPickup : Area2D
{
    public PowerUpType Type { get; set; } = PowerUpType.Overclock;

    private static Texture2D? _lightTex;
    private Node2D? _ring;
    private float _spin;

    public override void _Ready()
    {
        var def = PowerUps.Get(Type);

        CollisionLayer = 4; CollisionMask = 1;   // détecte le joueur (couche 1), comme l'aimant
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 18f } });

        // Orbe hexagonal coloré + cœur clair
        AddChild(new Polygon2D { Polygon = Hexagon(11f), Color = new Color(def.Color, 0.92f) });
        AddChild(new Polygon2D { Polygon = Hexagon(5f),  Color = new Color(1f, 1f, 1f, 0.9f) });

        // Anneau tournant
        _ring = new Node2D();
        AddChild(_ring);
        _ring.AddChild(new Line2D
        {
            Points = HexagonClosed(15f), Width = 2f,
            DefaultColor = def.Color.Lerp(Colors.White, 0.3f), Closed = true,
        });

        // Halo pulsant
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Color = def.Color, Energy = 0.7f, Texture = _lightTex,
            TextureScale = 2.4f, BlendMode = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var tw = CreateTween().SetLoops();
        tw.TweenProperty(light, "energy", 1.5f, 0.5).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(light, "energy", 0.7f, 0.5).SetEase(Tween.EaseType.InOut);

        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        _spin += 1.2f * (float)delta;
        if (_ring != null) _ring.Rotation = _spin;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        player.ApplyPowerUp(Type, PowerUps.Get(Type).Duration);
        QueueFree();
    }

    private static Vector2[] Hexagon(float r)
    {
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Tau * i / 6f - Mathf.Pi / 2f;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    private static Vector2[] HexagonClosed(float r)
    {
        var h = Hexagon(r);
        var pts = new Vector2[7];
        for (int i = 0; i < 6; i++) pts[i] = h[i];
        pts[6] = h[0];
        return pts;
    }
}
