using Godot;

/// <summary>
/// Colosse en Fusion — mid-boss de la Fournaise (~8 min). Cf. docs/GDD.md §32.
///
/// Lent et massif, il alterne marche pesante et <b>charges télégraphiées</b> qui laissent derrière
/// elles un <b>sillage de magma</b> persistant. La menace n'est pas la charge elle-même — elle est
/// évitable — mais le terrain qu'elle referme peu à peu : au bout de quelques charges, l'arène est
/// traversée de couloirs brûlants et le joueur doit choisir ses lignes de fuite.
///
/// Distinct de la signature <c>MagmaPools</c> du Noyau Rouillé (§29.2), qui <i>projette</i> des
/// flaques à distance : ici les flaques naissent sous les pieds du colosse, donc leur emplacement
/// est décidé par le déplacement du joueur autant que par le boss.
/// </summary>
public partial class MoltenColossus : EnemyBase
{
    private AnimatedSprite2D? _sprite;

    private const float BaseSpeed      = 68f;    // lourd : le joueur peut toujours le distancer
    private const float ChargeSpeed    = 330f;
    private const float ChargeEvery    = 6.5f;
    private const float TelegraphTime  = 0.8f;   // immobile, cœur qui s'allume : la charge se lit
    private const float ChargeTime     = 1.25f;
    private const float TrailInterval  = 0.16f;  // une flaque tous les ~53 px de charge

    private const float TrailRadius    = 30f;
    private const float TrailLifetime  = 5.0f;
    private const float TrailArmDelay  = 0.35f;  // le sillage ne brûle pas à l'instant où il naît

    /// <summary>Étapes du cycle d'attaque. Le colosse ne fait qu'une chose à la fois.</summary>
    private enum Stance { Walk, Telegraph, Charge }

    private Stance  _stance       = Stance.Walk;
    private float   _stanceTimer  = ChargeEvery;
    private float   _trailTimer;
    private Vector2 _chargeDir    = Vector2.Right;

    public override void _Ready()
    {
        MaxHp   = 700f;
        Speed   = BaseSpeed;
        Damage  = 22f;
        XpValue = 200;
        AddToGroup("molten_colossus");
        base._Ready();

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            MidBossVisuals.ApplyTo(_sprite);
            PlayAnim(_sprite, "idle");
        }

        AddAura();
    }

    /// <summary>Halo de chaleur : signale un champion avant même de lire la silhouette.</summary>
    private void AddAura()
    {
        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.42f, 0.12f),
            Energy       = 0.9f,
            Texture      = Player.MakeRadialLightTexture(32),
            TextureScale = 6f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var t = CreateTween().SetLoops();
        t.TweenProperty(light, "energy", 1.6f, 0.7f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(light, "energy", 0.9f, 0.7f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 36f;
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 0.40f;

    protected override void UpdateMovement(Player player, double delta)
    {
        float dt = (float)delta;
        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        _stanceTimer -= dt;

        switch (_stance)
        {
            case Stance.Walk:
                Velocity = toPlayer * BaseSpeed;
                if (_stanceTimer <= 0f)
                {
                    // Verrouille la direction MAINTENANT : la charge est esquivable justement
                    // parce qu'elle ne suit plus le joueur une fois lancée.
                    _chargeDir   = toPlayer;
                    _stance      = Stance.Telegraph;
                    _stanceTimer = TelegraphTime;
                    PlayAnim(_sprite, "attack");
                    ScreenShake.Instance?.Shake(2f, 0.15f);
                }
                break;

            case Stance.Telegraph:
                Velocity = Vector2.Zero;             // il s'arrête net : le joueur a le temps de lire
                if (_stanceTimer <= 0f)
                {
                    _stance      = Stance.Charge;
                    _stanceTimer = ChargeTime;
                    _trailTimer  = 0f;
                    AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
                }
                break;

            case Stance.Charge:
                Velocity = _chargeDir * ChargeSpeed;
                _trailTimer -= dt;
                if (_trailTimer <= 0f)
                {
                    DropMagma();
                    _trailTimer = TrailInterval;
                }
                if (_stanceTimer <= 0f)
                {
                    _stance      = Stance.Walk;
                    _stanceTimer = ChargeEvery;
                    PlayAnim(_sprite, "move");
                    ScreenShake.Instance?.Shake(4f, 0.2f);
                }
                break;
        }

        MoveAndSlide();

        if (_sprite != null && !_isDead)
        {
            _sprite.FlipH = toPlayer.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                PlayAnim(_sprite, "move");
        }
    }

    /// <summary>Pose une flaque de magma sous le colosse (réutilise la zone du boss de fin).</summary>
    private void DropMagma()
        => BossHazard.Spawn(GetTree(), GlobalPosition, BossHazardKind.Magma,
                            TrailRadius, TrailLifetime, TrailArmDelay);

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
        TriggerEliteExplosion();     // l'affixe doit rester universel malgré ce Die() surchargé
        ScreenShake.Instance?.Shake(8f, 0.35f);

        // La récompense dépend de l'animation de mort : sans elle (SpriteFrames incomplet), il faut
        // la donner tout de suite, sinon le mini-boss meurt sans rien lâcher.
        if (!PlayAnim(_sprite, "death"))
        {
            LevelUpSystem.Instance?.ShowWeaponDrop(3);
            QueueFree();
        }
    }

    protected override void PlayDeathSfx()
        => AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
}
