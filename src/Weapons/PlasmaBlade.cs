using Godot;

/// <summary>
/// Lame Plasma — arc de mêlée centré sur la direction de déplacement du joueur.
/// À chaque cooldown, crée une zone temporaire et inflige des dégâts à tous les ennemis dans l'arc.
///
/// VFX :
///   - Arc flash cyan GPUParticles2D one-shot à chaque swing (PlasmaArcFlash)
///   - Aura permanente PointLight2D sur l'arme quand équipée
///   - Screen shake léger à chaque swing
/// </summary>
public partial class PlasmaBlade : WeaponBase
{
    public float ArcAngleDeg { get; set; } = 180f;
    public float ArcRadius   { get; set; } = 80f;

    // Direction d'attaque : suit le dernier déplacement joueur
    private Vector2 _attackDir = Vector2.Right;

    // VFX aura permanente
    private PointLight2D? _auraLight;
    private Tween?         _auraTween;

    private static PackedScene? _arcFlashScene;
    private static Texture2D?   _auraLightTex;

    public override void _Ready()
    {
        Damage      = 18f;
        Cooldown    = 1.2f;
        ArcAngleDeg = 180f;
        ArcRadius   = 80f;

        // ── Scène arc flash ───────────────────────────────────────────────
        _arcFlashScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_plasma_arc_flash.tscn");

        // ── Aura permanente ───────────────────────────────────────────────
        _auraLightTex ??= Player.MakeRadialLightTexture(32);
        _auraLight = new PointLight2D
        {
            Name         = "PlasmaAura",
            Color        = new Color(0.267f, 1f, 0.933f, 1f),
            Energy       = 0.6f,
            Texture      = _auraLightTex,
            TextureScale = 3.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_auraLight);
        StartAuraPulse();

        base._Ready();
    }

    /// <summary>Pulsation douce 0.6 → 0.9 boucle 1.2 s.</summary>
    private void StartAuraPulse()
    {
        if (_auraLight == null) return;
        _auraTween?.Kill();
        _auraTween = CreateTween().SetLoops();
        _auraTween.TweenProperty(_auraLight, "energy", 0.9f, 0.6);
        _auraTween.TweenProperty(_auraLight, "energy", 0.6f, 0.6);
    }

    public override void _Process(double delta)
    {
        // Met à jour la direction d'attaque selon le déplacement du joueur
        var player = GameManager.Instance.PlayerInstance;
        if (player != null && player.Velocity.Length() > 1f)
            _attackDir = player.Velocity.Normalized();

        base._Process(delta);
    }

    protected override void Attack()
    {
        var enemies = GetTree().GetNodesInGroup(Constants.GroupEnemies);
        float halfArcRad = Mathf.DegToRad(ArcAngleDeg / 2f);

        bool hitAny = false;
        foreach (var node in enemies)
        {
            if (node is not EnemyBase enemy) continue;

            var toEnemy = enemy.GlobalPosition - GlobalPosition;
            float dist  = toEnemy.Length();
            if (dist > ArcRadius) continue;

            float angle = _attackDir.AngleTo(toEnemy.Normalized());
            if (Mathf.Abs(angle) <= halfArcRad)
            {
                enemy.TakeDamage(Damage);
                hitAny = true;
            }
        }

        // SFX de swing (toujours, même sans ennemi touché)
        AudioSystem.Instance?.PlaySfx("sfx_weapon_plasma_swing");
        _ = hitAny;

        // ── VFX arc flash ─────────────────────────────────────────────────
        SpawnArcFlash();

        // ── Screen shake léger ────────────────────────────────────────────
        ScreenShake.Instance?.Shake(1.5f, 0.06f);
    }

    private void SpawnArcFlash()
    {
        if (_arcFlashScene == null) return;
        var flash = _arcFlashScene.Instantiate<PlasmaArcFlash>();
        // Rotation : angle de _attackDir en radians
        flash.Rotation = _attackDir.Angle();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flash);
        flash.SetDeferred("global_position", GlobalPosition);
    }
}
