using Godot;

/// <summary>
/// Revenant d'Aether — mini-boss de mi-temps (~7 min). Réutilise le sprite du Rôdeur
/// agrandi et teinté violet. IA « lunging_chaser » : poursuite rapide ponctuée de ruades
/// (dash) toutes les ~2.2 s. Aura violette intense.
/// À la mort : gros orbe XP (180) + écran choix d'arme (3 cartes).
/// </summary>
public partial class AetherRevenant : EnemyBase
{
    private AnimatedSprite2D? _sprite;

    private const float BaseSpeed   = 130f;
    private const float DashSpeed   = 420f;
    private const float DashEvery   = 2.2f;
    private const float DashTime    = 0.32f;
    private float _dashCooldown = DashEvery;
    private float _dashTimer    = 0f;
    private Vector2 _dashDir     = Vector2.Zero;

    public override void _Ready()
    {
        MaxHp   = 550f;
        Speed   = BaseSpeed;
        Damage  = 18f;
        XpValue = 180;
        AddToGroup("aether_revenant");
        base._Ready();

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("idle");  // sprite dédié déjà violet — pas de teinte
        }

        AddAura();
    }

    private void AddAura()
    {
        var tex = Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Color        = new Color(0.667f, 0.267f, 1f),
            Energy       = 0.8f,
            Texture      = tex,
            TextureScale = 5.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var t = CreateTween().SetLoops();
        t.TweenProperty(light, "energy", 1.5f, 0.6f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(light, "energy", 0.8f, 0.6f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 34f;
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 0.35f;

    protected override void UpdateMovement(Player player, double delta)
    {
        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();

        if (_dashTimer > 0f)
        {
            // En pleine ruade
            _dashTimer -= (float)delta;
            Velocity = _dashDir * DashSpeed;
        }
        else
        {
            _dashCooldown -= (float)delta;
            if (_dashCooldown <= 0f)
            {
                // Déclenche une ruade vers le joueur
                _dashDir      = toPlayer;
                _dashTimer    = DashTime;
                _dashCooldown = DashEvery;
                ScreenShake.Instance?.Shake(1.5f, 0.06f);
            }
            Velocity = toPlayer * BaseSpeed;
        }

        MoveAndSlide();

        if (_sprite != null && !_isDead)
        {
            _sprite.FlipH = toPlayer.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                _sprite.Play("move");
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;
        if (_sprite.Animation == "attack" && !_isDead)
            _sprite.Play("move");
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
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        SpawnXpOrb();
        TrySpawnHpOrb();
        SpawnDeathBurst();
        ScreenShake.Instance?.Shake(7f, 0.3f);
        // Pas de hitstop à la mort : le ralenti casse le flow de jeu

        if (_sprite != null)
            _sprite.Play("death");
        else
        {
            LevelUpSystem.Instance?.ShowWeaponDrop(3);
            QueueFree();
        }
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
    }
}
