using Godot;

/// <summary>
/// Voile de Givre — fusion de la Lance Cryo + Plaque Renforcée. Le rayon glacé perçant devient un
/// VOILE de givre CONTINU autour du joueur : à chaque tick, tout ennemi dans le rayon subit des dégâts
/// ET un ralentissement fort (plafonné par CrowdControlCaps via <see cref="EnemyBase.ApplySlow"/>),
/// réappliqué en permanence → la nuée reste engluée au ralenti à portée, et les ennemis touchés
/// virent au bleu glacé (rendu « gelé », cf. EnemyBase). Fantasme défensif du blindage. Stats en dur
/// (les fusions n'ont pas de niveaux JSON).
///
/// VFX « vraie brume de froid » : deux nappes de brume douce (sprites radiaux translucides bleutés)
/// qui dérivent et pulsent en sens opposés pour un effet volumétrique tourbillonnant, des particules
/// de givre qui flottent lentement dans la zone, un liseré glacé discret marquant la portée, et une
/// lueur froide additive légère. Sans shader (robuste, léger).
/// </summary>
public partial class FrostVeil : WeaponBase
{
    private const float Radius       = 150f;
    private const float Dps          = 42f;
    private const float TickInterval = 0.2f;
    private const float SlowMult     = 0.55f;   // clampé à -40 % par CrowdControlCaps
    private const float SlowDuration = 0.5f;    // réappliqué chaque tick → slow permanent en zone

    private static readonly Color MistColor = new(0.72f, 0.86f, 1f);   // brume bleu pâle

    private Sprite2D?       _fogA, _fogB;
    private Line2D?         _ring;
    private PointLight2D?   _coldLight;
    private CpuParticles2D? _frost;
    private static Texture2D? _softTex;

    private float _t;

    public override void _Ready()
    {
        Damage   = Dps * TickInterval;   // dégâts par tick
        Cooldown = TickInterval;

        BuildMist();
        BuildRing();
        BuildColdLight();
        BuildFrostMotes();

        base._Ready();
    }

    /// <summary>Deux nappes de brume douce (sprites radiaux) qui donnent le volume du voile.</summary>
    private void BuildMist()
    {
        _softTex ??= Player.MakeRadialLightTexture(96);
        float scale = (2f * Radius) / 96f;

        _fogA = new Sprite2D
        {
            Texture  = _softTex,
            Scale    = new Vector2(scale, scale),
            Modulate = new Color(MistColor.R, MistColor.G, MistColor.B, 0.22f),
            ZIndex   = -1,   // sous le sprite du joueur, au-dessus des ennemis
        };
        AddChild(_fogA);

        _fogB = new Sprite2D
        {
            Texture  = _softTex,
            Scale    = new Vector2(scale * 0.82f, scale * 0.82f),
            Modulate = new Color(MistColor.R, MistColor.G, MistColor.B, 0.18f),
            ZIndex   = -1,
        };
        AddChild(_fogB);
    }

    /// <summary>Liseré glacé discret : marque la portée du voile (lisibilité de la zone).</summary>
    private void BuildRing()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.6f, 0.85f, 1f, 0.12f));
        grad.SetColor(1, new Color(0.9f, 0.98f, 1f, 0.4f));
        _ring = new Line2D
        {
            Points   = BuildCircle(Radius, 56),
            Closed   = true,
            Width    = 2f,
            Gradient = grad,
            ZIndex   = -1,
        };
        AddChild(_ring);
    }

    /// <summary>Lueur froide additive légère (juste un souffle de lumière, pas un blob).</summary>
    private void BuildColdLight()
    {
        _coldLight = new PointLight2D
        {
            Color        = new Color(0.55f, 0.8f, 1f),
            Energy       = 0.16f,
            Texture      = _softTex,
            TextureScale = Radius / 24f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
            ZIndex       = -1,
        };
        AddChild(_coldLight);
    }

    /// <summary>Particules de givre flottant lentement dans la zone.</summary>
    private void BuildFrostMotes()
    {
        _frost = new CpuParticles2D
        {
            Amount               = 32,
            Lifetime             = 2.2,
            Emitting             = true,
            EmissionShape        = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = Radius * 0.85f,
            Direction            = new Vector2(0, -1),
            Spread               = 180f,
            Gravity              = new Vector2(0, -6f),   // légère montée, comme un froid qui s'élève
            InitialVelocityMin   = 3f,
            InitialVelocityMax   = 12f,
            ScaleAmountMin       = 1.4f,
            ScaleAmountMax       = 3.2f,
            Color                = new Color(0.85f, 0.95f, 1f, 0.55f),
            ZIndex               = -1,
        };
        AddChild(_frost);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);   // gère le tick de dégâts → Attack()

        _t += (float)delta;
        // Dérive circulaire lente et pulsation d'opacité en sens opposés → la brume tourbillonne.
        if (_fogA != null)
        {
            _fogA.Position = new Vector2(Mathf.Cos(_t * 0.6f), Mathf.Sin(_t * 0.6f)) * 9f;
            _fogA.Modulate = new Color(MistColor.R, MistColor.G, MistColor.B, 0.20f + 0.05f * Mathf.Sin(_t * 1.3f));
        }
        if (_fogB != null)
        {
            _fogB.Position = new Vector2(Mathf.Cos(-_t * 0.45f + 3.14f), Mathf.Sin(-_t * 0.45f + 3.14f)) * 12f;
            _fogB.Modulate = new Color(MistColor.R, MistColor.G, MistColor.B, 0.16f + 0.05f * Mathf.Sin(_t * 1.1f + 1.5f));
        }
        if (_coldLight != null)
            _coldLight.Energy = 0.14f + 0.05f * Mathf.Sin(_t * 1.7f);
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
            e.ApplySlow(SlowMult, SlowDuration);   // → rendu « gelé » côté EnemyBase
        }
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
