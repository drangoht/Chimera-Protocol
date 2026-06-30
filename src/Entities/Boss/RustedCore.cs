using Godot;

/// <summary>
/// Le Noyau Rouillé — BOSS DE FIN (~13 min). Sprite dédié rouge-or, agrandi ×2.4 (imposant).
/// Avance lentement, tire des salves radiales de 16 projectiles toutes les 2.0 s et émet des
/// ondes de choc. Très résistant (HP base 18000 → ~32000 effectif à 13 min en Normal).
/// Le vaincre = VICTOIRE de la run : à la mort, 3 Noyaux d'Aether + explosion massive,
/// puis écran de fin "extraction réussie" (~1,4 s plus tard). Pas d'orbe XP ni de choix
/// d'arme — la run se termine, ce serait sans effet et risquerait un LevelUpScreen parasite.
/// </summary>
public partial class RustedCore : EnemyBase
{
    private AnimatedSprite2D? _sprite;
    private static PackedScene? _bulletScene;
    private static PackedScene? _aetherCoreScene;
    private static PackedScene? _shockwaveScene;

    private const float BurstEvery   = 2.0f;   // 2.5→2.0 : salves plus rapprochées (boss plus pressant)
    private const int   BulletsRing  = 16;     // 12→16 : rideau radial plus dense
    private float _burstTimer = BurstEvery;

    private const float ShockEvery = 3.5f;
    private float _shockTimer = ShockEvery;

    private Node?    _cachedParent;
    private Vector2  _cachedDeathPos;

    public override void _Ready()
    {
        MaxHp   = 1600f;
        Speed   = 46f;
        Damage  = 28f;
        XpValue = 500;
        AddToGroup("rusted_core");
        base._Ready();

        _bulletScene     ??= GD.Load<PackedScene>("res://scenes/entities/EnemyBullet.tscn");
        _aetherCoreScene ??= GD.Load<PackedScene>("res://scenes/entities/AetherCore.tscn");
        _shockwaveScene  ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_shockwave_ring.tscn");

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Scale = new Vector2(2.4f, 2.4f);  // boss massif et imposant ; sprite dédié déjà rouge-or
            _sprite.Play("idle");
        }

        AddBossAura();

        // Entrée fracassante
        ScreenShake.Instance?.Shake(14f, 0.5f);
        SpawnShockwave();
    }

    private void AddBossAura()
    {
        var tex = Player.MakeRadialLightTexture(64);
        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.45f, 0.2f),   // orange-rouille ardent
            Energy       = 1.0f,
            Texture      = tex,
            TextureScale = 9.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
        var t = CreateTween().SetLoops();
        t.TweenProperty(light, "energy", 1.8f, 0.9f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(light, "energy", 1.0f, 0.9f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 56f;  // boss agrandi (scale 2.4) → portée de contact proportionnée
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 1.0f;

    protected override void UpdateMovement(Player player, double delta)
    {
        if (_isDead) { Velocity = Vector2.Zero; return; }

        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = toPlayer * Speed;
        MoveAndSlide();

        if (_sprite != null)
        {
            _sprite.FlipH = toPlayer.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                _sprite.Play("move");
        }

        _burstTimer -= (float)delta;
        if (_burstTimer <= 0f)
        {
            FireRadialBurst();
            _burstTimer = BurstEvery;
        }

        _shockTimer -= (float)delta;
        if (_shockTimer <= 0f)
        {
            SpawnShockwave();
            _shockTimer = ShockEvery;
        }
    }

    private void FireRadialBurst()
    {
        if (_bulletScene == null) return;
        _sprite?.Play("attack");
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");
        ScreenShake.Instance?.Shake(2.5f, 0.1f);

        var parent = GetParent();
        for (int i = 0; i < BulletsRing; i++)
        {
            float angle = 2f * Mathf.Pi * i / BulletsRing;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var bullet = _bulletScene.Instantiate<EnemyBullet>();
            bullet.Direction = dir;
            bullet.Speed     = 210f;
            bullet.Damage    = Damage;
            parent?.CallDeferred(Node.MethodName.AddChild, bullet);
            bullet.SetDeferred("global_position", GlobalPosition);
        }
    }

    private void SpawnShockwave()
    {
        if (_shockwaveScene == null) return;
        var ring = _shockwaveScene.Instantiate<Node2D>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
        ring.SetDeferred("global_position", GlobalPosition);
    }

    protected override void HandleContactDamage(Player player, double delta)
    {
        // Contact lourd en plus des salves
        if (GlobalPosition.DistanceTo(player.GlobalPosition) < ContactRadius)
        {
            var stats = player.Stats;
            player.TakeDamage(Damage * (1f - stats.DamageReduction));
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;
        if (_sprite.Animation == "attack" && !_isDead)
            _sprite.Play("move");
        else if (_sprite.Animation == "death")
            FinishDeath();
    }

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;
        _cachedParent   = GetParent();
        _cachedDeathPos = GlobalPosition;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        // Pas de SpawnXpOrb() : vaincre le boss termine la run. 500 XP juste avant l'écran
        // de victoire pourrait ouvrir un LevelUpScreen parasite (cf. OBS-2 du game-tester).

        if (_sprite != null)
            _sprite.Play("death");
        else
            FinishDeath();
    }

    /// <summary>Explosion massive : 3 ondes de choc, gros flash, 3 Noyaux, choix d'arme.</summary>
    private void FinishDeath()
    {
        ScreenShake.Instance?.Shake(18f, 0.6f);
        ScreenShake.Instance?.HitStop(0.1f);

        // 3 ondes de choc concentriques
        for (int i = 0; i < 3; i++)
            SpawnShockwaveAt(_cachedDeathPos);

        // Burst de mort tier 3
        SpawnDeathBurst();

        // 3 Noyaux d'Aether répartis autour
        if (_aetherCoreScene != null && _cachedParent != null)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = 2f * Mathf.Pi * i / 3f;
                var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 48f;
                var core = _aetherCoreScene.Instantiate<AetherCore>();
                _cachedParent.CallDeferred(Node.MethodName.AddChild, core);
                core.SetDeferred("global_position", _cachedDeathPos + offset);
            }
        }

        // Battre le boss de fin de niveau = NIVEAU TERMINÉ (débloque le suivant + bannière) mais
        // la run NE s'arrête PAS : l'escalade overtime continue (survie sans fin). La run se
        // termine à la mort du joueur (high score = temps survécu).
        RunStatsTracker.Instance?.OnLevelBossDefeated();
        QueueFree();
    }

    private void SpawnShockwaveAt(Vector2 pos)
    {
        if (_shockwaveScene == null) return;
        var ring = _shockwaveScene.Instantiate<Node2D>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
        ring.SetDeferred("global_position", pos);
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
    }
}
