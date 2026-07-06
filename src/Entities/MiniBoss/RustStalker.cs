using Godot;

/// <summary>
/// Rôdeur de Rouille — mini-boss araignée mécanique, 64×64 px.
/// HP 300, Speed 85, Contact 15/s. Spawn dès 12 min, max 1 simultané.
/// À la mort : orbe XP or (80 XP) + écran choix d'arme (3 cartes).
/// </summary>
public partial class RustStalker : EnemyBase
{
    private AnimatedSprite2D? _sprite;
    private Node? _cachedParent;
    private Vector2 _cachedDeathPos;

    public override void _Ready()
    {
        MaxHp   = 300f;
        Speed   = 85f;
        Damage  = 15f;
        XpValue = 80;
        AddToGroup("rust_stalker");
        base._Ready();

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("idle");
        }

        AddMiniBossLight();
    }

    private void AddMiniBossLight()
    {
        var tex = Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.4f, 0.133f),  // #FF6622 or-rouille
            Energy       = 0.4f,
            Texture      = tex,
            TextureScale = 4.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        var tween = CreateTween().SetLoops();
        tween.TweenProperty(light, "energy", 0.9f, 0.8f).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(light, "energy", 0.4f, 0.8f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius  => 32f;
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 0.25f;

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;
        if (_sprite.Animation == "attack" && !_isDead)
            _sprite.Play("idle");
        else if (_sprite.Animation == "death")
        {
            // CallDeferred évite que GetTree().Paused soit appelé depuis le callback
            // AnimationFinished (deadlock Godot si synchrone).
            Callable.From(() => LevelUpSystem.Instance?.ShowWeaponDrop(3)).CallDeferred();
            QueueFree();
        }
    }

    protected override void UpdateMovement(Player player, double delta)
    {
        var dir = (player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = dir * Speed;
        MoveAndSlide();

        if (_sprite != null && !_isDead)
        {
            _sprite.FlipH = dir.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                _sprite.Play("move");
            else if (_sprite.Animation == "move" && Velocity.LengthSquared() < 1f)
                _sprite.Play("idle");
        }
    }

    protected override void Die()
    {
        if (_isDead) return;
        _cachedParent   = GetParent();
        _cachedDeathPos = GlobalPosition;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled(this);
        PlayDeathSfx();
        SpawnXpOrb();
        TrySpawnHpOrb();
        SpawnDeathBurst();
        ScreenShake.Instance?.Shake(5f, 0.25f);

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
        AudioSystem.Instance?.PlaySfx("sfx_enemy_swarm_die");
    }
}
