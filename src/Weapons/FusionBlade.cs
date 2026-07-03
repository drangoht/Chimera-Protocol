using Godot;

/// <summary>
/// Lame à Fusion — anneau continu 360°, 55 dps, rayon 130 px, pulse toutes les 0.15 s.
/// Pas de cooldown discret : le timer interne est fixé à damageInterval.
///
/// VFX « matière en fusion » :
///   - Distorsion de chaleur en espace écran (heat haze) : un shader rééchantillonne l'écran
///     avec un décalage piloté par du bruit animé montant, masqué au disque de frappe → le sol
///     et les ennemis dans le rayon ondulent comme au-dessus d'une source de chaleur. Nécessite
///     un BackBufferCopy pour capturer l'écran déjà rendu (z absolus élevés pour passer après
///     le monde dans l'ordre de rendu).
///   - Braises incandescentes montantes (CpuParticles2D additif).
///   - Anneau Line2D tournant à bord incandescent + aura PointLight2D dorée, flash à chaque pulse.
/// </summary>
public partial class FusionBlade : WeaponBase
{
    private const float DamageInterval = 0.15f;
    private const float RingRadius     = 130f;
    private const float Dps            = 55f;
    private const float SpinSpeed      = 2.4f;   // rad/s de rotation de l'anneau

    private static readonly Color GoldColor = new(1f, 0.8f, 0.267f, 1f);   // #FFCC44

    private Line2D?         _ring;
    private PointLight2D?   _auraLight;
    private Sprite2D?       _heatQuad;
    private CpuParticles2D? _embers;
    private Tween?          _flashTween;

    private static Texture2D?     _lightTex;
    private static Shader?        _heatShader;
    private static NoiseTexture2D? _heatNoise;

    // Shader de distorsion de chaleur (embarqué → aucun asset externe à importer).
    private const string HeatShaderCode = @"
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
uniform sampler2D noise_tex  : repeat_enable, filter_linear;
uniform float strength    = 7.0;   // amplitude de distorsion en pixels
uniform float rise_speed  = 0.7;   // vitesse de montée du bruit
uniform vec3  molten_color : source_color = vec3(1.0, 0.45, 0.12);

void fragment() {
    float d    = distance(UV, vec2(0.5));
    float mask = 1.0 - smoothstep(0.40, 0.5, d);
    if (mask <= 0.001) {
        COLOR = vec4(0.0);
    } else {
        vec2 nuv = UV * 2.2 + vec2(0.0, -TIME * rise_speed);
        float n1 = texture(noise_tex, nuv).r;
        float n2 = texture(noise_tex, nuv * 1.9 + vec2(0.37, 0.11)).r;
        float n  = (n1 + n2) - 1.0;                       // ~ -1..1
        vec2 disp = vec2(n * 0.6, n) * strength * SCREEN_PIXEL_SIZE * mask;
        vec3 scol = texture(screen_tex, SCREEN_UV + disp).rgb;
        float edge = smoothstep(0.26, 0.5, d) * mask;      // liseré incandescent
        vec3 col   = scol + molten_color * edge * 0.20 * (0.6 + 0.4 * n);
        COLOR = vec4(col, mask);
    }
}";

    public override void _Ready()
    {
        // DPS = Damage / DamageInterval → Damage par pulse = 55 * 0.15 = 8.25
        Damage   = Dps * DamageInterval;
        Cooldown = DamageInterval;

        BuildHeatHaze();
        BuildEmbers();
        BuildRing();
        BuildAura();

        base._Ready();
    }

    /// <summary>Distorsion de chaleur : BackBufferCopy (capture écran) + quad shadé (relecture).</summary>
    private void BuildHeatHaze()
    {
        _heatShader ??= new Shader { Code = HeatShaderCode };

        // Bruit de chaleur seamless partagé (généré une fois, réutilisé entre runs).
        if (_heatNoise == null)
        {
            _heatNoise = new NoiseTexture2D
            {
                Width    = 128,
                Height   = 128,
                Seamless = true,
                Noise    = new FastNoiseLite
                {
                    NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                    Frequency = 0.045f,
                },
            };
        }

        // BackBufferCopy : capture l'écran déjà rendu (monde + ennemis) juste avant le quad.
        // z absolu élevé → passe après le monde dans l'ordre de rendu.
        var backBuffer = new BackBufferCopy
        {
            Name        = "HeatBackBuffer",
            CopyMode    = BackBufferCopy.CopyModeEnum.Viewport,
            ZAsRelative = false,
            ZIndex      = 400,
        };
        AddChild(backBuffer);

        var mat = new ShaderMaterial { Shader = _heatShader };
        mat.SetShaderParameter("noise_tex", _heatNoise);

        // Quad blanc couvrant le disque ; le shader masque au cercle et lit l'écran capturé.
        var whiteImg = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        whiteImg.Fill(Colors.White);
        var whiteTex = ImageTexture.CreateFromImage(whiteImg);

        _heatQuad = new Sprite2D
        {
            Name        = "HeatHaze",
            Texture     = whiteTex,
            Centered    = true,
            Scale       = new Vector2(2f * RingRadius / 8f, 2f * RingRadius / 8f),
            Material    = mat,
            ZAsRelative = false,
            ZIndex      = 401,
        };
        AddChild(_heatQuad);
    }

    /// <summary>Braises incandescentes montant du disque de fusion.</summary>
    private void BuildEmbers()
    {
        _embers = new CpuParticles2D
        {
            Name                 = "FusionEmbers",
            Amount               = 28,
            Lifetime             = 1.1,
            Emitting             = true,
            EmissionShape        = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = RingRadius * 0.85f,
            Direction            = new Vector2(0, -1),
            Spread               = 25f,
            Gravity              = new Vector2(0, -40f),
            InitialVelocityMin   = 12f,
            InitialVelocityMax   = 34f,
            ScaleAmountMin       = 1.2f,
            ScaleAmountMax       = 2.4f,
            Color                = new Color(1f, 0.55f, 0.18f, 1f),
            Material             = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            ZAsRelative          = false,
            ZIndex               = 402,
        };
        AddChild(_embers);
    }

    /// <summary>Anneau tournant à bord incandescent matérialisant le rayon.</summary>
    private void BuildRing()
    {
        // Dégradé le long de l'anneau : un arc brillant balaie le cercle quand il tourne
        // (sinon un cercle parfait qui pivote paraît statique par symétrie de rotation).
        var ringGradient = new Gradient();
        ringGradient.SetColor(0, new Color(1f, 0.35f, 0.1f, 0.25f));   // orange sombre
        ringGradient.SetColor(1, new Color(1f, 0.95f, 0.7f, 0.95f));   // blanc incandescent
        _ring = new Line2D
        {
            Name         = "FusionRing",
            Points       = BuildCircle(RingRadius, 48),
            Closed       = true,
            Width        = 3f,
            Gradient     = ringGradient,
            ZAsRelative  = false,
            ZIndex       = 403,
        };
        AddChild(_ring);
    }

    /// <summary>Aura lumineuse dorée pulsant à chaque frappe.</summary>
    private void BuildAura()
    {
        _lightTex ??= Player.MakeRadialLightTexture(64);
        _auraLight = new PointLight2D
        {
            Name         = "FusionAura",
            Color        = GoldColor,
            Energy       = 0.22f,
            Texture      = _lightTex,
            TextureScale = 4.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_auraLight);
    }

    public override void _Process(double delta)
    {
        // Rotation continue de l'anneau (spinning blade)
        if (_ring != null)
            _ring.Rotation += SpinSpeed * (float)delta;

        base._Process(delta);
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;
            if (enemy.GlobalPosition.DistanceTo(player.GlobalPosition) <= RingRadius)
                enemy.TakeDamage(Damage);
        }

        FlashPulse();
    }

    /// <summary>Bref éclat de l'aura + de l'anneau à chaque pulse de dégâts.</summary>
    private void FlashPulse()
    {
        if (_auraLight == null) return;
        // L'attaque tire toutes les 0.15 s → le flash se réamorce en continu : garder un pic
        // modéré, sinon l'aura additive reste saturée en permanence et noie le sprite joueur
        // (amplifié par le bloom). Cf. BUG-701.
        _flashTween?.Kill();
        _flashTween = CreateTween();
        _auraLight.Energy = 0.4f;
        _flashTween.TweenProperty(_auraLight, "energy", 0.22f, DamageInterval);
        if (_ring != null)
        {
            _ring.Width = 5f;
            _flashTween.Parallel().TweenProperty(_ring, "width", 3f, DamageInterval);
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
