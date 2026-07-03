using Godot;

/// <summary>
/// Lame à Fusion — anneau continu 360°, 55 dps, rayon 130 px, pulse toutes les 0.15 s.
/// Pas de cooldown discret : le timer interne est fixé à damageInterval.
///
/// VFX :
///   - Anneau tournant Line2D doré matérialisant le rayon de frappe (spinning blade)
///   - Aura permanente PointLight2D dorée, flash rapide à chaque pulse de dégâts
/// </summary>
public partial class FusionBlade : WeaponBase
{
    private const float DamageInterval = 0.15f;
    private const float RingRadius     = 130f;
    private const float Dps            = 55f;
    private const float SpinSpeed      = 2.4f;   // rad/s de rotation de l'anneau

    private static readonly Color GoldColor = new(1f, 0.8f, 0.267f, 1f);   // #FFCC44

    private Line2D?      _ring;
    private PointLight2D? _auraLight;
    private Tween?        _flashTween;

    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        // DPS = Damage / DamageInterval → Damage par pulse = 55 * 0.15 = 8.25
        Damage   = Dps * DamageInterval;
        Cooldown = DamageInterval;

        // ── Anneau tournant matérialisant le rayon ────────────────────────
        // Dégradé le long de l'anneau : un arc brillant balaie le cercle quand il tourne
        // (sinon un cercle parfait qui pivote paraît statique par symétrie de rotation).
        var ringGradient = new Gradient();
        ringGradient.SetColor(0, new Color(GoldColor, 0.15f));
        ringGradient.SetColor(1, new Color(1f, 0.95f, 0.7f, 0.95f));
        _ring = new Line2D
        {
            Name        = "FusionRing",
            Points      = BuildCircle(RingRadius, 48),
            Closed      = true,
            Width       = 3f,
            Gradient    = ringGradient,
            ZIndex      = -1,
        };
        AddChild(_ring);

        // ── Aura lumineuse dorée ──────────────────────────────────────────
        _lightTex ??= Player.MakeRadialLightTexture(64);
        _auraLight = new PointLight2D
        {
            Name         = "FusionAura",
            Color        = GoldColor,
            Energy       = 0.35f,
            Texture      = _lightTex,
            TextureScale = 6.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_auraLight);

        base._Ready();
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
        _flashTween?.Kill();
        _flashTween = CreateTween();
        _auraLight.Energy = 0.9f;
        _flashTween.TweenProperty(_auraLight, "energy", 0.35f, DamageInterval);
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
