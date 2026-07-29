using Godot;

/// <summary>
/// Sentinelle Cryo — mid-boss du Givre (~8 min). Cf. docs/GDD.md §32.
///
/// Tourelle flottante qui <b>garde ses distances</b> et balaie le joueur d'un <b>cône de gel
/// dirigé</b> : dégâts modérés, mais un ralentissement sévère qui l'empêche de fuir la nuée pendant
/// quelques secondes. Le cône est télégraphié avant de frapper, et il ne suit pas le joueur une fois
/// verrouillé — la parade est de <b>sortir de l'axe</b>, pas de reculer.
///
/// C'est précisément ce qui la distingue de la signature <c>FrostNova</c> du Noyau Rouillé (§29.2),
/// qui est <i>radiale</i> et se fuit en s'éloignant : les deux menaces de givre demandent au joueur
/// deux réflexes opposés.
/// </summary>
public partial class CryoSentinel : EnemyBase
{
    private AnimatedSprite2D? _sprite;

    private const float BaseSpeed     = 58f;
    private const float PreferredDist = 250f;   // distance de confort : elle recule si on l'approche
    private const float DistTolerance = 55f;

    private const float ConeEvery     = 4.5f;
    private const float TelegraphTime = 0.7f;
    private const float ConeReach     = 330f;
    private const float ConeHalfAngle = 0.42f;  // ~24° de demi-ouverture
    private const float ConeDamage    = 14f;
    private const float ChillMult     = 0.45f;
    private const float ChillDuration = 2.2f;

    private const int   PatchCount    = 3;      // plaques de givre laissées dans l'axe du tir
    private const float PatchRadius   = 34f;
    private const float PatchLifetime = 6f;

    private enum Stance { Hold, Telegraph }

    private Stance  _stance      = Stance.Hold;
    private float   _stanceTimer = ConeEvery;
    private Vector2 _aimDir      = Vector2.Down;
    private float   _flashTime;                 // rémanence visuelle du cône après le tir
    private ChampionOverlay? _cone;             // rendu hors arbre (cf. ChampionOverlay)

    public override void _Ready()
    {
        MaxHp   = 620f;
        Speed   = BaseSpeed;
        Damage  = 16f;                          // contact faible : la menace est le cône
        XpValue = 200;
        AddToGroup("cryo_sentinel");
        base._Ready();

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            MidBossVisuals.ApplyTo(_sprite);
            PlayAnim(_sprite, "idle");
        }

        AddAura();
        ZIndex = 1;

        _cone = ChampionOverlay.Attach(this, ChampionOverlayKind.FrostCone, zIndex: 0);
        _cone.Radius   = ConeReach;
        _cone.HalfSpan = ConeHalfAngle;
        _cone.Visible  = false;                 // n'existe qu'à la visée et juste après le tir
    }

    private void AddAura()
    {
        var light = new PointLight2D
        {
            Color        = new Color(0.42f, 0.82f, 1f),
            Energy       = 0.8f,
            Texture      = Player.MakeRadialLightTexture(32),
            TextureScale = 5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var t = CreateTween().SetLoops();
        t.TweenProperty(light, "energy", 1.4f, 0.8f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(light, "energy", 0.8f, 0.8f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 30f;
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 0.40f;

    protected override void UpdateMovement(Player player, double delta)
    {
        float dt = (float)delta;
        var toPlayer = player.GlobalPosition - GlobalPosition;
        float dist   = toPlayer.Length();
        var dir      = dist > 0.001f ? toPlayer / dist : Vector2.Down;

        _stanceTimer -= dt;
        if (_flashTime > 0f) _flashTime -= dt;

        if (_cone != null && IsInstanceValid(_cone))
        {
            _cone.Visible = _stance == Stance.Telegraph || _flashTime > 0f;
            _cone.Angle   = _aimDir.Angle();
            _cone.Flash   = Mathf.Max(0f, _flashTime / 0.25f);
        }

        if (_stance == Stance.Telegraph)
        {
            Velocity = Vector2.Zero;            // elle se fige pour viser : le télégraphe est lisible
            if (_stanceTimer <= 0f)
            {
                FireCone(player);
                _stance      = Stance.Hold;
                _stanceTimer = ConeEvery;
            }
        }
        else
        {
            // Kiting : garde PreferredDist. Trop près → recule ; trop loin → se rapproche.
            if (dist < PreferredDist - DistTolerance)      Velocity = -dir * BaseSpeed;
            else if (dist > PreferredDist + DistTolerance) Velocity =  dir * BaseSpeed;
            else                                           Velocity = dir.Orthogonal() * BaseSpeed * 0.6f;

            if (_stanceTimer <= 0f && dist < ConeReach)
            {
                _aimDir      = dir;             // verrouillé ici : le cône ne suivra plus le joueur
                _stance      = Stance.Telegraph;
                _stanceTimer = TelegraphTime;
                PlayAnim(_sprite, "attack");
            }
        }

        MoveAndSlide();

        if (_sprite != null && !_isDead)
        {
            _sprite.FlipH = dir.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                PlayAnim(_sprite, "move");
        }
    }

    /// <summary>
    /// Applique le cône : dégâts + ralentissement si le joueur est dans le secteur, puis dépose des
    /// plaques de givre le long de l'axe (la zone reste dangereuse après le tir).
    /// </summary>
    private void FireCone(Player player)
    {
        _flashTime = 0.25f;
        AudioSystem.Instance?.PlaySfx("sfx_enemy_sentinel_projectile");
        ScreenShake.Instance?.Shake(2.5f, 0.12f);

        var toPlayer = player.GlobalPosition - GlobalPosition;
        if (toPlayer.Length() <= ConeReach &&
            Mathf.Abs(_aimDir.AngleTo(toPlayer.Normalized())) <= ConeHalfAngle)
        {
            player.TakeDamage(ConeDamage * (1f - player.Stats.DamageReduction));
            player.ApplyChill(ChillMult, ChillDuration);
        }

        for (int i = 1; i <= PatchCount; i++)
        {
            var pos = GlobalPosition + _aimDir * (ConeReach * i / (PatchCount + 1f));
            BossHazard.Spawn(GetTree(), pos, BossHazardKind.Frost,
                             PatchRadius, PatchLifetime, 0.3f);
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;
        if (_sprite.Animation == "attack" && !_isDead)
            PlayAnim(_sprite, "move");
        else if (_sprite.Animation == "death")
        {
            Callable.From(() => LevelUpSystem.Instance?.ShowWeaponDrop(3)).CallDeferred();
            QueueFree();
        }
    }

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled(this);
        PlayDeathSfx();
        SpawnXpOrb();
        TrySpawnHpOrb();
        SpawnDeathBurst();
        TriggerEliteExplosion();
        ScreenShake.Instance?.Shake(6f, 0.28f);

        // Le cône vit hors de l'arbre de la sentinelle : sans ça, un télégraphe orphelin resterait
        // affiché pendant toute l'animation de mort.
        if (_cone != null && IsInstanceValid(_cone)) _cone.QueueFree();
        _cone = null;

        if (!PlayAnim(_sprite, "death"))
        {
            LevelUpSystem.Instance?.ShowWeaponDrop(3);
            QueueFree();
        }
    }

    protected override void PlayDeathSfx()
        => AudioSystem.Instance?.PlaySfx("sfx_enemy_sentinel_die");
}
