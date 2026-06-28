using Godot;

/// <summary>
/// Sentinelle Corrompue — IA ranged_kiter.
/// Se maintient à 200–350 px. Tire un projectile ennemi toutes les 2.5 s.
/// </summary>
public partial class CorruptedSentinel : EnemyBase
{
    private const float RetreatRange  = 200f;
    private const float AdvanceRange  = 350f;
    private const float ShootCooldown = 2.5f;

    private float _shootTimer = ShootCooldown;

    private static PackedScene? _bulletScene;
    private AnimatedSprite2D? _sprite;

    public override void _Ready()
    {
        MaxHp   = 45f;
        Speed   = 70f;
        Damage  = 12f;  // par projectile
        XpValue = 8;
        base._Ready();

        _bulletScene ??= GD.Load<PackedScene>("res://scenes/entities/EnemyBullet.tscn");

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("idle");
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;
        if (_sprite.Animation == "attack" && !_isDead)
            _sprite.Play("idle");
        else if (_sprite.Animation == "death")
            QueueFree();
    }

    protected override void UpdateMovement(Player player, double delta)
    {
        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();

        if (dist < RetreatRange)
        {
            // Recul en diagonale (perpendiculaire + opposé)
            var perp = new Vector2(-toPlayer.Y, toPlayer.X);
            Velocity = (-toPlayer + perp).Normalized() * Speed;

            if (_sprite != null && _sprite.Animation != "attack")
                _sprite.Play("move");
        }
        else if (dist > AdvanceRange)
        {
            Velocity = toPlayer * Speed;

            if (_sprite != null && _sprite.Animation != "attack")
                _sprite.Play("move");
        }
        else
        {
            // Zone de confort : stationnaire
            Velocity = Vector2.Zero;

            if (_sprite != null && _sprite.Animation != "attack")
                _sprite.Play("idle");
        }

        MoveAndSlide();

        // Orientation du sprite selon direction joueur
        if (_sprite != null)
        {
            if (toPlayer.X < -0.1f)
                _sprite.FlipH = true;
            else if (toPlayer.X > 0.1f)
                _sprite.FlipH = false;
        }

        // Timer de tir
        _shootTimer -= (float)delta;
        if (_shootTimer <= 0f)
        {
            Shoot(player);
            _shootTimer = ShootCooldown;
        }
    }

    private void Shoot(Player player)
    {
        if (_bulletScene == null) return;

        // Lancer l'animation attack avant de spawner le projectile
        _sprite?.Play("attack");

        var bullet = _bulletScene.Instantiate<EnemyBullet>();
        bullet.Direction = (player.GlobalPosition - GlobalPosition).Normalized();
        bullet.Speed     = 180f;
        bullet.Damage    = Damage;

        var parent   = GetParent();
        var spawnPos = GlobalPosition;
        parent?.CallDeferred(Node.MethodName.AddChild, bullet);
        bullet.SetDeferred("global_position", spawnPos);

        // SFX de tir de la Sentinelle
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");
    }

    // Pas de dégâts de contact standard — la sentinelle tire
    protected override void HandleContactDamage(Player player, double delta) { }

    protected override float ContactRadius => 24f;

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        SpawnXpOrb();
        SpawnDeathBurst();
        ScreenShake.Instance?.Shake(3f, 0.12f);

        if (_sprite != null)
            _sprite.Play("death");  // QueueFree déclenché par OnAnimationFinished
        else
            QueueFree();
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_sentinel_die");
    }
}
