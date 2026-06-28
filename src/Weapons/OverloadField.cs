using Godot;

/// <summary>
/// Champ de Surcharge — pulse de zone, knockback + dégâts à tous les ennemis dans le rayon.
/// Flash visuel (Polygon2D) apparaît 0.1 s puis disparaît.
///
/// VFX :
///   - PointLight2D violet-électrique pulsant rapidement (0.3→1.2, boucle 0.3 s)
///   - Indicateur de rayon Polygon2D circulaire très transparent (alpha 0.06) en violet
///   - Flash visuel plein-rayon à chaque pulse
/// </summary>
public partial class OverloadField : WeaponBase
{
    public float Radius    { get; set; } = 100f;
    public float Knockback { get; set; } = 40f;

    private PointLight2D? _fieldLight;
    private Polygon2D?    _radiusIndicator;
    private Tween?        _pulseTween;

    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage    = 8f;
        Cooldown  = 2.5f;
        Radius    = 100f;
        Knockback = 40f;

        // ── Texture lumière ───────────────────────────────────────────────
        _lightTex ??= Player.MakeRadialLightTexture(64);

        // ── PointLight2D violet-électrique pulsant ────────────────────────
        _fieldLight = new PointLight2D
        {
            Name         = "FieldLight",
            Color        = new Color(0.667f, 0.267f, 1f, 1f),   // violet #AA44FF
            Energy       = 0.3f,
            Texture      = _lightTex,
            TextureScale = 5.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_fieldLight);
        StartFieldPulse();

        // ── Indicateur de rayon (cercle très transparent) ─────────────────
        _radiusIndicator = new Polygon2D
        {
            Name    = "RadiusIndicator",
            Color   = new Color(0.667f, 0.267f, 1f, 0.06f),
            Polygon = BuildCircle(Radius, 48),
            ZIndex  = -2,
        };
        AddChild(_radiusIndicator);

        base._Ready();
    }

    /// <summary>Pulsation rapide 0.3 → 1.2 → 0.3, boucle 0.3 s.</summary>
    private void StartFieldPulse()
    {
        if (_fieldLight == null) return;
        _pulseTween?.Kill();
        _pulseTween = CreateTween().SetLoops();
        _pulseTween.TweenProperty(_fieldLight, "energy", 1.2f, 0.15);
        _pulseTween.TweenProperty(_fieldLight, "energy", 0.3f, 0.15);
    }

    protected override void Attack()
    {
        var player  = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        // SFX pulse de zone
        AudioSystem.Instance?.PlaySfx("sfx_weapon_overload_pulse");

        // Screen shake électrique
        ScreenShake.Instance?.Shake(3f, 0.12f);

        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            float dist = enemy.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist > Radius) continue;

            enemy.TakeDamage(Damage);

            // Knockback : impulsion opposée au joueur
            if (dist > 0.1f)
            {
                var knockDir = (enemy.GlobalPosition - player.GlobalPosition).Normalized();
                enemy.Velocity += knockDir * Knockback;
            }
        }

        // Flash visuel temporaire
        SpawnFlash(player.GlobalPosition);
    }

    private void SpawnFlash(Vector2 center)
    {
        var flash = new OverloadFlash();
        flash.Radius = Radius;
        GetTree().Root.AddChild(flash);
        flash.GlobalPosition = center;
    }

    // ── Mise à jour du rayon si upgradé ──────────────────────────────────
    public override void _Process(double delta)
    {
        // Synchronise le rayon de l'indicateur si Radius a changé (upgrade)
        if (_radiusIndicator != null)
        {
            var expectedPoly = BuildCircle(Radius, 48);
            // Ne reconstruire que si le rayon a changé (comparaison approximative)
            if (_radiusIndicator.Polygon.Length > 0 &&
                !Mathf.IsEqualApprox(_radiusIndicator.Polygon[0].X, expectedPoly[0].X, 0.5f))
            {
                _radiusIndicator.Polygon = expectedPoly;
                if (_fieldLight != null)
                    _fieldLight.TextureScale = 5.0f * (Radius / 100f);
            }
        }
        base._Process(delta);
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
