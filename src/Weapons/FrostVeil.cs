using Godot;

/// <summary>
/// Voile de Givre — fusion de la Lance Cryo + Plaque Renforcée. Le rayon glacé perçant devient un
/// VOILE de givre CONTINU autour du joueur : à chaque tick, tout ennemi dans le rayon subit des
/// dégâts ET un ralentissement fort (plafonné par CrowdControlCaps via <see cref="EnemyBase.ApplySlow"/>),
/// réappliqué en permanence → la nuée reste engluée au ralenti à portée. Fantasme défensif du blindage :
/// on ne fuit plus, on gèle tout ce qui approche. Aura radiale façon Lame à Fusion, sans cooldown
/// discret (le timer interne vaut TickInterval). Stats en dur (les fusions n'ont pas de niveaux JSON).
/// </summary>
public partial class FrostVeil : WeaponBase
{
    private const float Radius       = 150f;
    private const float Dps          = 42f;
    private const float TickInterval = 0.2f;
    private const float SlowMult     = 0.55f;   // clampé à -40 % par CrowdControlCaps
    private const float SlowDuration = 0.5f;    // réappliqué chaque tick → slow permanent en zone

    private static readonly Color IceColor = new(0.6f, 0.85f, 1f, 1f);

    private Line2D?         _ring;
    private PointLight2D?   _auraLight;
    private CpuParticles2D? _frost;
    private Tween?          _flashTween;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage   = Dps * TickInterval;   // dégâts par tick
        Cooldown = TickInterval;

        BuildRing();
        BuildAura();
        BuildFrost();

        base._Ready();
    }

    /// <summary>Anneau de givre au sol (sous le joueur, au-dessus des ennemis).</summary>
    private void BuildRing()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.5f, 0.8f, 1f, 0.3f));
        grad.SetColor(1, new Color(0.9f, 0.98f, 1f, 0.9f));
        _ring = new Line2D
        {
            Points   = BuildCircle(Radius, 48),
            Closed   = true,
            Width    = 3f,
            Gradient = grad,
            ZIndex   = -1,   // relatif : passe sous le sprite du joueur (ZIndex 5), au-dessus des ennemis
        };
        AddChild(_ring);
    }

    /// <summary>Halo glacé doux pulsant à chaque tick.</summary>
    private void BuildAura()
    {
        _lightTex ??= Player.MakeRadialLightTexture(64);
        _auraLight = new PointLight2D
        {
            Color        = IceColor,
            Energy       = 0.20f,
            Texture      = _lightTex,
            TextureScale = Radius / 24f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            ZIndex       = -1,
        };
        AddChild(_auraLight);
    }

    /// <summary>Particules de givre dérivant lentement dans la zone.</summary>
    private void BuildFrost()
    {
        _frost = new CpuParticles2D
        {
            Amount               = 24,
            Lifetime             = 1.6,
            Emitting             = true,
            EmissionShape        = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = Radius * 0.9f,
            Direction            = new Vector2(0, -1),
            Spread               = 180f,
            Gravity              = Vector2.Zero,
            InitialVelocityMin   = 4f,
            InitialVelocityMax   = 16f,
            ScaleAmountMin       = 1.0f,
            ScaleAmountMax       = 2.2f,
            Color                = new Color(0.8f, 0.95f, 1f, 0.7f),
            Material             = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            ZIndex               = -1,
        };
        AddChild(_frost);
    }

    protected override void Attack()
    {
        var player = GameManager.Instance?.PlayerInstance;
        if (player == null) return;

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            if (e.GlobalPosition.DistanceTo(player.GlobalPosition) > Radius) continue;
            e.TakeDamage(Damage);
            e.ApplySlow(SlowMult, SlowDuration);
        }

        FlashPulse();
    }

    /// <summary>Bref éclat de l'aura à chaque pulse (modéré : additif réamorcé toutes les 0.2 s).</summary>
    private void FlashPulse()
    {
        if (_auraLight == null) return;
        _flashTween?.Kill();
        _flashTween = CreateTween();
        _auraLight.Energy = 0.34f;
        _flashTween.TweenProperty(_auraLight, "energy", 0.20f, TickInterval);
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
