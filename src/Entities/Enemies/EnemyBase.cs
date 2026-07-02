using Godot;

public partial class EnemyBase : CharacterBody2D
{
    [Export] public float MaxHp  { get; set; } = 20f;
    [Export] public float Speed  { get; set; } = 120f;
    [Export] public float Damage { get; set; } = 5f;
    [Export] public int   XpValue { get; set; } = 1;

    protected float _currentHp;
    protected bool  _isDead = false;

    private float _damageCooldown = 0f;
    private const float DamageInterval = 1f;

    // ── Effets de statut (slow + brûlure DoT), plafonnés par CrowdControlCaps ──
    private float _baseSpeed = 0f;       // vitesse réelle capturée (post-scaling/biome), 0 = pas encore
    private float _slowMult  = 1f;       // multiplicateur de vitesse courant (≤ 1)
    private float _slowTime  = 0f;       // temps restant de ralentissement
    private float _burnDps   = 0f;       // dégâts/seconde de brûlure courante
    private float _burnTime  = 0f;       // temps restant de brûlure
    private float _burnTick  = 0f;       // accumulateur de tick (feedback visuel)
    private const float BurnTickInterval = 0.33f;

    private Tween? _hitTween;
    private static PackedScene? _xpOrbScene;
    private static PackedScene? _hpOrbScene;

    [Signal] public delegate void DiedEventHandler(int xpValue);

    /// <summary>Probabilité de dropper un orbe HP à la mort. Surchargeable (mini-boss : 0.25f).</summary>
    protected virtual float HpDropChance => 0.08f;

    public override void _Ready()
    {
        AddToGroup(Constants.GroupEnemies);
        _currentHp = MaxHp;
        // Les ennemis traversent les murs (layer 1) mais sont BLOQUÉS par les obstacles
        // infranchissables (sur le bit 2). mask = 2 → collision avec les obstacles uniquement.
        CollisionMask = 2;

        _xpOrbScene ??= GD.Load<PackedScene>("res://scenes/entities/XpOrb.tscn");
        _hpOrbScene ??= GD.Load<PackedScene>("res://scenes/entities/HpOrb.tscn");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        UpdateStatusEffects((float)delta);
        if (_isDead) return;   // la brûlure a pu tuer l'ennemi

        UpdateMovement(player, delta);
        HandleContactDamage(player, delta);
    }

    /// <summary>
    /// Applique slow (sur la vitesse effective) et brûlure (DoT) chaque frame. Capture la vitesse
    /// de base à la 1re frame (après que EnemySpawner a posé le scaling + le multiplicateur de biome).
    /// </summary>
    private void UpdateStatusEffects(float dt)
    {
        if (_baseSpeed <= 0f) _baseSpeed = Speed;

        if (_slowTime > 0f)
        {
            _slowTime -= dt;
            if (_slowTime <= 0f) _slowMult = 1f;
        }
        Speed = _baseSpeed * _slowMult;   // les sous-classes lisent Speed → ralenties automatiquement

        if (_burnTime > 0f)
        {
            _burnTime -= dt;
            _burnTick += dt;
            if (_burnTick >= BurnTickInterval)
            {
                _burnTick -= BurnTickInterval;
                TakeDamage(_burnDps * BurnTickInterval);   // tick → HitFlash = feedback visuel
            }
            if (_burnTime <= 0f) { _burnDps = 0f; _burnTick = 0f; }
        }
    }

    /// <summary>Ralentit l'ennemi : <paramref name="mult"/> ∈ (0,1]. On garde le slow le plus fort,
    /// avec la durée la plus longue. Plafonné à -40 % (CrowdControlCaps).</summary>
    public void ApplySlow(float mult, float duration)
    {
        mult = CrowdControlCaps.CapSlowMult(mult);
        if (mult < _slowMult || _slowTime <= 0f) _slowMult = mult;
        _slowTime = Mathf.Max(_slowTime, duration);
    }

    /// <summary>Applique une brûlure (DoT) non-stackable : garde le dps le plus fort et la durée la
    /// plus longue. dps plafonné par CrowdControlCaps.</summary>
    public void ApplyBurn(float dps, float duration)
    {
        _burnDps  = Mathf.Max(_burnDps, CrowdControlCaps.CapBurnDps(dps));
        _burnTime = Mathf.Max(_burnTime, duration);
    }

    /// <summary>
    /// Mouvement par défaut : ligne droite vers le joueur.
    /// Surchargé par les sous-classes pour des comportements IA différents.
    /// </summary>
    protected virtual void UpdateMovement(Player player, double delta)
    {
        var direction = (player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = direction * Speed;
        MoveAndSlide();
    }

    /// <summary>Dégâts de contact à portée contactRadius (défini dans les sous-classes ou 24 px par défaut).</summary>
    protected virtual void HandleContactDamage(Player player, double delta)
    {
        if (GlobalPosition.DistanceTo(player.GlobalPosition) < ContactRadius)
        {
            _damageCooldown -= (float)delta;
            if (_damageCooldown <= 0f)
            {
                var stats = player.Stats;
                float reduced = Damage * (1f - stats.DamageReduction);
                player.TakeDamage(reduced);
                _damageCooldown = DamageInterval;
            }
        }
    }

    /// <summary>Distance de contact (px). Les sous-classes peuvent la surcharger.</summary>
    protected virtual float ContactRadius => 24f;

    /// <summary>
    /// Appelé par EnemySpawner après AddChild pour appliquer le scaling temporel.
    /// Synchronise _currentHp avec le MaxHp scalé.
    /// </summary>
    public void ApplyScaling(float scaledMaxHp, float scaledDamage)
    {
        MaxHp      = scaledMaxHp;
        _currentHp = scaledMaxHp;
        Damage     = scaledDamage;
    }

    /// <summary>
    /// Échange dynamiquement le SpriteFrames de l'AnimatedSprite2D enfant (même principe que
    /// Player.SetCharacterFrames) — permet à plusieurs ids d'ennemis de réutiliser une même scène
    /// archétype (RustSwarm/CorruptedDrone/CorruptedSentinel/GraftedColossus) avec un sprite dédié
    /// par id, sans dupliquer la scène ni le script (cf. EnemySpawnData.FramesPath).
    /// Appelé par EnemySpawner.SpawnEnemy juste après AddChild ; ne fait rien si le chemin est vide,
    /// si le nœud n'a pas d'AnimatedSprite2D, ou si la ressource est introuvable — dans ces cas le
    /// SpriteFrames posé en dur dans le .tscn reste actif (rétro-compatible avec les 8 ennemis
    /// existants qui n'ont pas de FramesPath).
    /// </summary>
    public void SetSpriteFrames(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null) return;

        var frames = GD.Load<SpriteFrames>(path);
        if (frames == null) return;

        string previousAnim = sprite.Animation;
        sprite.SpriteFrames = frames;

        // Reprend la même animation si elle existe dans le nouveau jeu de frames (convention :
        // toutes les scènes archétype exposent idle/move/attack/death), sinon repli sur "move".
        if (!string.IsNullOrEmpty(previousAnim) && frames.HasAnimation(previousAnim))
            sprite.Play(previousAnim);
        else if (frames.HasAnimation("move"))
            sprite.Play("move");
    }

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        _currentHp -= amount;
        HitFlash(0.05f);
        if (_currentHp <= 0f)
            Die();
    }

    private void HitFlash(float duration)
    {
        _hitTween?.Kill();
        _hitTween = CreateTween();
        _hitTween.TweenProperty(this, "modulate", Colors.White, duration)
                 .From(new Color(5f, 5f, 5f, 1f));
    }

    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        SpawnXpOrb();
        TrySpawnHpOrb();
        SpawnDeathBurst();
        QueueFree();
    }

    private static PackedScene? _deathBurstScene;

    protected void SpawnDeathBurst()
    {
        _deathBurstScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_enemy_death_burst.tscn");
        if (_deathBurstScene == null) return;

        var instance = _deathBurstScene.Instantiate<EnemyDeathBurst>();
        instance.Tier = GetOrbTier(); // fourrage 0 → mini-boss 3 : calibre l'explosion
        // AddChild est différé — GlobalPosition doit l'être aussi, sinon le nœud hors-arbre
        // ignore l'assignation et le burst apparaît à (0,0). Pattern identique à SpawnXpOrb().
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
        instance.SetDeferred("global_position", GlobalPosition);
    }

    /// <summary>
    /// Joue le SFX de mort adapte au type d'ennemi.
    /// Les sous-classes peuvent surcharger pour specifier un id different.
    /// </summary>
    protected virtual void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_swarm_die");
    }

    /// <summary>
    /// Tier de l'orbe XP droppée. Surchargeable par les sous-classes (ex. mini-boss → T3/T4).
    /// T0 ≤5 XP, T1 ≤10 XP, T2 ≤30 XP, T3 >30 XP.
    /// </summary>
    protected virtual int GetOrbTier() => XpValue switch
    {
        <= 5  => 0,
        <= 10 => 1,
        <= 30 => 2,
        _     => 3,
    };

    protected void TrySpawnHpOrb()
    {
        if (_hpOrbScene == null) return;
        if (GD.Randf() > HpDropChance) return;

        var parent = GetParent();
        if (parent == null) return;

        var orb = _hpOrbScene.Instantiate<HpOrb>();
        var spawnPos = GlobalPosition + new Vector2(GD.Randf() * 16f - 8f, GD.Randf() * 16f - 8f);
        parent.CallDeferred(Node.MethodName.AddChild, orb);
        orb.SetDeferred("global_position", spawnPos);
    }

    protected void SpawnXpOrb()
    {
        if (_xpOrbScene == null) return;
        var parent = GetParent();
        if (parent == null) return;

        var orb = _xpOrbScene.Instantiate<XpOrb>();
        orb.Value   = XpValue;
        orb.OrbTier = GetOrbTier();
        var spawnPos = GlobalPosition;

        // AddChild et GlobalPosition sont différés car on est dans un callback physique
        // (Bullet.OnBodyEntered → TakeDamage → Die). Godot interdit de modifier le scene
        // tree pendant le flush de queries.
        parent.CallDeferred(Node.MethodName.AddChild, orb);
        orb.SetDeferred("global_position", spawnPos);
    }
}
