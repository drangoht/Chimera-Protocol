using Godot;

/// <summary>
/// Voile de Givre — fusion de la Lance Cryo + Plaque Renforcée. Le rayon glacé perçant devient un
/// VOILE de givre CONTINU autour du joueur : à chaque tick, tout ennemi dans le rayon subit des dégâts
/// ET un ralentissement fort (plafonné par CrowdControlCaps via <see cref="EnemyBase.ApplySlow"/>),
/// réappliqué en permanence → la nuée reste engluée au ralenti à portée, et les ennemis touchés
/// virent au bleu glacé (rendu « gelé », cf. EnemyBase). Fantasme défensif du blindage. Stats en dur
/// (les fusions n'ont pas de niveaux JSON).
///
/// VFX « vraie brume de froid » : un amas de nappes douces (sprites radiaux translucides bleutés) qui
/// se recouvrent — une grande nappe centrale + des puffs plus petits répartis sur un anneau — chacune
/// dérivant/pulsant sur une phase propre pour un effet volumétrique tourbillonnant lisible même à
/// l'arrêt, des particules de givre densifiées qui flottent dans la zone, un liseré glacé discret
/// marquant la portée, et une lueur froide additive légère. Sans shader (robuste, léger).
/// </summary>
public partial class FrostVeil : WeaponBase
{
    private const float Radius       = 150f;
    private const float Dps          = 42f;
    private const float TickInterval = 0.2f;
    private const float SlowMult     = 0.55f;   // clampé à -40 % par CrowdControlCaps
    private const float SlowDuration = 0.5f;    // réappliqué chaque tick → slow permanent en zone

    private static readonly Color MistColor = new(0.72f, 0.86f, 1f);   // brume bleu pâle

    // Amas de nappes de brume : plusieurs petits sprites radiaux décalés se recouvrent → densité interne
    // variable qui lit comme un nuage même en frame fixe (au lieu d'un simple halo radial concentrique).
    private const int PuffCount = 6;
    private readonly Sprite2D[] _puffs      = new Sprite2D[PuffCount];
    private readonly Vector2[]  _puffOrigin = new Vector2[PuffCount];   // ancre de l'orbite de dérive
    private readonly float[]    _puffScale  = new float[PuffCount];     // échelle de base (relatif au rayon)
    private readonly float[]    _puffPhase  = new float[PuffCount];     // déphasage de la dérive/pulsation

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

    /// <summary>Amas de nappes de brume douce (sprites radiaux décalés) qui donnent le volume du voile.
    /// Une nappe large centrale + des puffs plus petits répartis sur l'anneau : leur recouvrement crée
    /// une brume texturée lisible même à l'arrêt. Chaque puff a une échelle/opacité/phase distincte.</summary>
    private void BuildMist()
    {
        _softTex ??= Player.MakeRadialLightTexture(96);
        float full = (2f * Radius) / 96f;   // échelle pour couvrir tout le diamètre

        for (int i = 0; i < PuffCount; i++)
        {
            // i == 0 : grande nappe centrale ; i >= 1 : puffs plus petits sur un anneau intermédiaire.
            bool  center = i == 0;
            float ring   = center ? 0f : Radius * 0.5f;
            float angle  = 2f * Mathf.Pi * (i - 1) / (PuffCount - 1) + 0.6f;
            _puffOrigin[i] = center ? Vector2.Zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
            _puffScale[i]  = center ? 0.95f : 0.5f + 0.12f * (i % 3);
            _puffPhase[i]  = i * 1.7f;

            float alpha = center ? 0.18f : 0.15f;
            var puff = new Sprite2D
            {
                Texture  = _softTex,
                Position = _puffOrigin[i],
                Scale    = new Vector2(full * _puffScale[i], full * _puffScale[i]),
                Modulate = new Color(MistColor.R, MistColor.G, MistColor.B, alpha),
                ZIndex   = -1,   // sous le sprite du joueur, au-dessus des ennemis
            };
            _puffs[i] = puff;
            AddChild(puff);
        }
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
            Amount               = 56,   // densifié : les motes portent le volume en frame statique
            Lifetime             = 2.6,
            Emitting             = true,
            EmissionShape        = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = Radius * 0.85f,
            Direction            = new Vector2(0, -1),
            Spread               = 180f,
            Gravity              = new Vector2(0, -6f),   // légère montée, comme un froid qui s'élève
            InitialVelocityMin   = 3f,
            InitialVelocityMax   = 12f,
            ScaleAmountMin       = 1.6f,
            ScaleAmountMax       = 3.6f,
            Color                = new Color(0.85f, 0.95f, 1f, 0.7f),
            ZIndex               = -1,
        };
        AddChild(_frost);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);   // gère le tick de dégâts → Attack()

        _t += (float)delta;
        // Chaque puff dérive sur une petite orbite et pulse en opacité, avec une phase propre → l'amas
        // ondule et tourbillonne sans jamais s'aligner, ce qui entretient la texture de brume.
        for (int i = 0; i < PuffCount; i++)
        {
            var puff = _puffs[i];
            if (puff == null) continue;
            float ph  = _puffPhase[i];
            float dir = (i % 2 == 0) ? 1f : -1f;   // sens de dérive alterné
            puff.Position = _puffOrigin[i]
                + new Vector2(Mathf.Cos(_t * 0.5f * dir + ph), Mathf.Sin(_t * 0.5f * dir + ph)) * 10f;
            float baseA = i == 0 ? 0.18f : 0.15f;
            puff.Modulate = new Color(MistColor.R, MistColor.G, MistColor.B,
                baseA + 0.045f * Mathf.Sin(_t * 1.2f + ph));
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
