using Godot;

/// <summary>
/// Sentinelle Maîtresse — mini-boss tireur d'élite, 64×64 px.
/// HP 450, Speed 50, 2 projectiles ±12° / 1.5s, 18 dégâts chacun.
/// Spawn dès 16 min, max 1 simultané.
/// À la mort : orbe XP or (120 XP) + écran choix d'arme (3 cartes).
/// </summary>
public partial class MasterSentinel : EnemyBase
{
    private const float RetreatRange  = 250f;
    private const float AdvanceRange  = 400f;
    private const float ShootCooldown = 1.5f;

    private float _shootTimer = ShootCooldown;
    private AnimatedSprite2D? _sprite;
    private static PackedScene? _bulletScene;

    public override void _Ready()
    {
        MaxHp   = 450f;
        Speed   = 50f;
        Damage  = 18f;
        XpValue = 120;
        AddToGroup("master_sentinel");
        base._Ready();

        _bulletScene ??= GD.Load<PackedScene>("res://scenes/entities/EnemyBullet.tscn");

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
            Color        = new Color(0.267f, 0.667f, 1f),  // #44AAFF cyan froid
            Energy       = 0.5f,
            Texture      = tex,
            TextureScale = 4.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        var tween = CreateTween().SetLoops();
        tween.TweenProperty(light, "energy", 1.1f, 1.0f).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(light, "energy", 0.5f, 1.0f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 36f;
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
        float dist     = GlobalPosition.DistanceTo(player.GlobalPosition);
        var   toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();

        if (dist < RetreatRange)
        {
            var perp = new Vector2(-toPlayer.Y, toPlayer.X);
            Velocity = (-toPlayer + perp).Normalized() * Speed;
            if (_sprite != null && _sprite.Animation != "attack") _sprite.Play("move");
        }
        else if (dist > AdvanceRange)
        {
            Velocity = toPlayer * Speed;
            if (_sprite != null && _sprite.Animation != "attack") _sprite.Play("move");
        }
        else
        {
            Velocity = Vector2.Zero;
            if (_sprite != null && _sprite.Animation != "attack") _sprite.Play("idle");
        }

        MoveAndSlide();

        if (_sprite != null)
            _sprite.FlipH = toPlayer.X < -0.1f;

        _shootTimer -= (float)delta;
        if (_shootTimer <= 0f && !_isDead)
        {
            Shoot(player);
            _shootTimer = ShootCooldown;
        }
    }

    private void Shoot(Player player)
    {
        if (_bulletScene == null) return;

        _sprite?.Play("attack");

        var baseDir = (player.GlobalPosition - GlobalPosition).Normalized();
        SpawnBullet(baseDir.Rotated( Mathf.DegToRad(12f)));
        SpawnBullet(baseDir.Rotated(-Mathf.DegToRad(12f)));

        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");
    }

    private void SpawnBullet(Vector2 direction)
    {
        if (_bulletScene == null) return;

        var bullet = _bulletScene.Instantiate<EnemyBullet>();
        bullet.Direction = direction;
        bullet.Speed     = 200f;
        bullet.Damage    = Damage;

        var parent   = GetParent();
        var spawnPos = GlobalPosition;
        parent?.CallDeferred(Node.MethodName.AddChild, bullet);
        bullet.SetDeferred("global_position", spawnPos);
    }

    protected override void HandleContactDamage(Player player, double delta) { }

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        SpawnXpOrb();
        TrySpawnHpOrb();
        TrySpawnMagnet();
        SpawnDeathBurst();
        // Pas de hitstop à la mort : le ralenti casse le flow de jeu
        ScreenShake.Instance?.Shake(6f, 0.3f);

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
        AudioSystem.Instance?.PlaySfx("sfx_enemy_sentinel_die");
    }
}
