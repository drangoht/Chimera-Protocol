using Godot;

/// <summary>
/// Rayon Vecteur — fusion de la Lance Vectorielle + Servo-Moteurs. Le trait unique devient un
/// RAYON perforant CONTINU orienté par la direction de déplacement du joueur (<see cref="Player.AimDirection"/>).
/// Plus de projectile ni de cooldown : le faisceau est toujours actif, pivote avec le joueur et
/// inflige des dégâts par tick à tout ennemi le long de la ligne (perforation totale). Amplifie le
/// skill de visée introduit par la Lance Vectorielle : on « peint » l'écran avec le rayon.
/// Stats en dur (les fusions n'ont pas de niveaux dans weapons.json ; cf. SolarColumn/IonicStorm).
/// </summary>
public partial class VectorBeam : WeaponBase
{
    private const float BeamLength = 520f;  // portée du rayon (px)
    private const float HitRadius  = 34f;    // demi-largeur de collision (rayon ennemi inclus)
    private const float TickDamage = 11f;    // dégâts par tick
    private const float TickInterval = 0.13f;

    private Vector2 _aim = Vector2.Down;      // direction courante du rayon (lissée)
    private Line2D? _core;
    private Line2D? _glow;
    private PointLight2D? _muzzle;
    private static Texture2D? _lightTex;
    private int _sfxThrottle;

    public override void _Ready()
    {
        Damage   = TickDamage;
        Cooldown = TickInterval;

        // ── VFX du faisceau : dessiné en coords locales de l'arme (enfant du joueur), pivoté
        //    chaque frame selon la direction de visée. Cyan→or (identité fusion). ──
        // ZIndex -1 (relatif) : le rayon part de SOUS le sprite du joueur (ZIndex 5) tout en restant
        // au-dessus des ennemis (ZIndex 0) — cohérent avec la priorité de lisibilité du joueur.
        _glow = new Line2D
        {
            Width = 18f, DefaultColor = new Color(1f, 0.85f, 0.35f, 0.22f),
            BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = true, ZIndex = -1,
        };
        _glow.AddPoint(Vector2.Zero);
        _glow.AddPoint(new Vector2(BeamLength, 0f));
        AddChild(_glow);

        _core = new Line2D
        {
            Width = 6f, DefaultColor = new Color(0.85f, 1f, 1f, 0.95f),
            BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
            Antialiased = true, ZIndex = -1,
        };
        _core.AddPoint(Vector2.Zero);
        _core.AddPoint(new Vector2(BeamLength, 0f));
        AddChild(_core);

        _lightTex ??= Player.MakeRadialLightTexture(48);
        _muzzle = new PointLight2D
        {
            Color = new Color(1f, 0.9f, 0.5f), Energy = 1.2f, Texture = _lightTex,
            TextureScale = 2.2f, BlendMode = PointLight2D.BlendModeEnum.Add, ZIndex = -1,
        };
        AddChild(_muzzle);

        base._Ready();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);   // gère le timer de tick → Attack()

        var player = GameManager.Instance?.PlayerInstance;
        if (player != null)
        {
            var target = player.AimDirection == Vector2.Zero ? _aim : player.AimDirection;
            // Lissage : le rayon pivote sans à-coups quand on change de direction.
            _aim = _aim.Slerp(target, Mathf.Min(1f, (float)delta * 18f)).Normalized();
        }
        Rotation = _aim.Angle();

        // Léger battement lumineux pour un faisceau « vivant ».
        if (_muzzle != null)
            _muzzle.Energy = 1.0f + 0.3f * Mathf.Sin(Time.GetTicksMsec() / 90f);
    }

    protected override void Attack()
    {
        var a = GlobalPosition;
        var b = a + _aim * BeamLength;

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            if (DistanceToSegment(e.GlobalPosition, a, b) <= HitRadius)
                e.TakeDamage(Damage);   // perforation totale : tous les ennemis de la ligne
        }

        // SFX throttlé (~1 tir sur 3) pour un feedback sans spam audio.
        if (++_sfxThrottle % 3 == 0)
            AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");
    }

    /// <summary>Distance d'un point au segment [a,b] (projection clampée).</summary>
    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        float t = lenSq > 0.0001f ? Mathf.Clamp((p - a).Dot(ab) / lenSq, 0f, 1f) : 0f;
        return p.DistanceTo(a + ab * t);
    }
}
