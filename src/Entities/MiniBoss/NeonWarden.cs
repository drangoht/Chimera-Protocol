using Godot;

/// <summary>
/// Gardien Néon — mid-boss du Secteur Néon (~8 min). Cf. docs/GDD.md §32.
///
/// Le seul champion du jeu que le DPS brut ne suffit pas à abattre : un <b>bouclier orbital</b>
/// couvre en permanence les deux tiers de sa circonférence et <b>absorbe 80 % des dégâts</b> qui le
/// traversent. Il tourne lentement, donc l'ouverture se déplace : le joueur doit <b>tourner autour</b>
/// du Gardien pour rester dans l'angle mort, au lieu de tenir une position et d'arroser.
///
/// C'est une menace <i>défensive</i>, là où le boss de fin du même biome (<c>RotatingBeams</c>, §29.2)
/// est purement offensif — les deux se jouent en tournant, mais l'un pour tirer, l'autre pour esquiver.
///
/// Le bouclier est dessiné <b>en code</b> (<see cref="_Draw"/>) et non dans le sprite : il doit rester
/// exactement synchrone avec l'angle qui décide des dégâts, et le faire tourner via le sprite ferait
/// pivoter l'éclairage pseudo-3D avec lui (lumière fixe haut-gauche, docs/ART_BRIEF_PSEUDO3D.md).
/// </summary>
public partial class NeonWarden : EnemyBase
{
    private AnimatedSprite2D? _sprite;

    private const float BaseSpeed       = 88f;
    private const float ShieldRadius    = 34f;
    private const float ShieldHalfSpan  = 2.00f;   // ~115° de part et d'autre → 230° couverts
    private const float ShieldSpinSpeed = 0.75f;   // rad/s : lent, l'ouverture se suit à l'œil
    private const float ShieldAbsorb    = 0.20f;   // fraction des dégâts qui PASSE le bouclier

    private const float SummonEvery     = 9f;
    private const int   SummonCount     = 3;

    private float _shieldAngle;
    private float _summonTimer = SummonEvery;
    private float _absorbFlash;                    // retour visuel d'un tir absorbé
    private ChampionOverlay? _shield;              // rendu hors arbre (cf. ChampionOverlay)

    public override void _Ready()
    {
        MaxHp   = 800f;
        Speed   = BaseSpeed;
        Damage  = 20f;
        XpValue = 220;
        AddToGroup("neon_warden");
        base._Ready();

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            MidBossVisuals.ApplyTo(_sprite);
            PlayAnim(_sprite, "idle");
        }

        _shieldAngle = GD.Randf() * Mathf.Tau;     // l'ouverture ne démarre pas toujours au même endroit
        AddAura();
        ZIndex = 1;

        _shield = ChampionOverlay.Attach(this, ChampionOverlayKind.OrbitalShield);
        _shield.Radius   = ShieldRadius;
        _shield.HalfSpan = ShieldHalfSpan;
    }

    private void AddAura()
    {
        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.35f, 0.9f),
            Energy       = 0.85f,
            Texture      = Player.MakeRadialLightTexture(32),
            TextureScale = 5.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var t = CreateTween().SetLoops();
        t.TweenProperty(light, "energy", 1.5f, 0.65f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(light, "energy", 0.85f, 0.65f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 32f;
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 0.40f;

    protected override void UpdateMovement(Player player, double delta)
    {
        float dt = (float)delta;

        _shieldAngle = Mathf.Wrap(_shieldAngle + ShieldSpinSpeed * dt, 0f, Mathf.Tau);
        if (_absorbFlash > 0f) _absorbFlash -= dt;
        if (_shield != null && IsInstanceValid(_shield))
        {
            _shield.Angle = _shieldAngle;
            _shield.Flash = Mathf.Max(0f, _absorbFlash / 0.18f);
        }

        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = toPlayer * Speed;
        MoveAndSlide();

        _summonTimer -= dt;
        if (_summonTimer <= 0f)
        {
            _summonTimer = SummonEvery;
            SummonEscort();
        }

        if (_sprite != null && !_isDead)
        {
            _sprite.FlipH = toPlayer.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                PlayAnim(_sprite, "move");
        }
    }

    /// <summary>
    /// Appelle la faune locale en renfort. Passe par <c>EnemySpawner.SummonAdds</c> — donc soumis au
    /// cap simultané global : un mid-boss ne peut pas faire exploser le budget de performance.
    /// </summary>
    private void SummonEscort()
    {
        if (GetTree().GetFirstNodeInGroup(Constants.GroupEnemySpawner) is not EnemySpawner spawner) return;
        float tMin = (RunStatsTracker.Instance?.ElapsedSeconds ?? 480f) / 60f;
        if (spawner.SummonAdds(SummonCount, tMin) > 0)
        {
            PlayAnim(_sprite, "attack");
            ScreenShake.Instance?.Shake(3f, 0.15f);
        }
    }

    /// <summary>Le bouclier couvre-t-il la direction d'où vient <paramref name="from"/> ?</summary>
    private bool IsShielded(Vector2 from)
    {
        var incoming = from - GlobalPosition;
        if (incoming.LengthSquared() < 0.001f) return false;
        float delta = Mathf.Wrap(incoming.Angle() - _shieldAngle, -Mathf.Pi, Mathf.Pi);
        return Mathf.Abs(delta) <= ShieldHalfSpan;
    }

    /// <summary>
    /// Réduit les dégâts venus d'un angle couvert par le bouclier.
    ///
    /// La source réelle du coup n'est pas transmise (<c>TakeDamage(float)</c> ne porte pas de
    /// position) : on prend celle du <b>joueur</b>. C'est volontairement l'approximation la plus
    /// lisible — la règle affichée au joueur devient « place-toi face à l'ouverture », vraie quelle
    /// que soit l'arme, plutôt qu'une règle par projectile qu'il ne pourrait pas déduire.
    /// </summary>
    public override void TakeDamage(float amount)
    {
        var player = GameManager.Instance?.PlayerInstance;
        if (!_isDead && player != null && IsInstanceValid(player) && IsShielded(player.GlobalPosition))
        {
            _absorbFlash = 0.18f;
            base.TakeDamage(amount * ShieldAbsorb);
            return;
        }
        base.TakeDamage(amount);
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
        ScreenShake.Instance?.Shake(7f, 0.3f);

        // Le bouclier vit hors de l'arbre du Gardien : il ne disparaît pas avec lui tout seul, et
        // l'animation de mort dure encore ~1 s pendant laquelle un bouclier orphelin mentirait.
        if (_shield != null && IsInstanceValid(_shield)) _shield.QueueFree();
        _shield = null;

        if (!PlayAnim(_sprite, "death"))
        {
            LevelUpSystem.Instance?.ShowWeaponDrop(3);
            QueueFree();
        }
    }

    protected override void PlayDeathSfx()
        => AudioSystem.Instance?.PlaySfx("sfx_enemy_sentinel_die");
}
