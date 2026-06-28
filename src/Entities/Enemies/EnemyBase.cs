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

    private Tween? _hitTween;
    private static PackedScene? _xpOrbScene;
    private static PackedScene? _hpOrbScene;
    private static PackedScene? _magnetScene;

    [Signal] public delegate void DiedEventHandler(int xpValue);

    /// <summary>Probabilité de dropper un orbe HP à la mort. Surchargeable (mini-boss : 0.25f).</summary>
    protected virtual float HpDropChance => 0.08f;

    /// <summary>Probabilité de dropper un Aimant à la mort (aspire toutes les orbes d'XP). Drop rare.</summary>
    protected virtual float MagnetDropChance => 0.0025f;

    public override void _Ready()
    {
        AddToGroup(Constants.GroupEnemies);
        _currentHp = MaxHp;
        // Les ennemis ignorent les murs physiquement — le joueur seul est contenu par les murs
        CollisionMask = 0;

        _xpOrbScene ??= GD.Load<PackedScene>("res://scenes/entities/XpOrb.tscn");
        _hpOrbScene ??= GD.Load<PackedScene>("res://scenes/entities/HpOrb.tscn");
        _magnetScene ??= GD.Load<PackedScene>("res://scenes/entities/Magnet.tscn");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        UpdateMovement(player, delta);
        HandleContactDamage(player, delta);
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
        TrySpawnMagnet();
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

    protected void TrySpawnMagnet()
    {
        if (_magnetScene == null) return;
        if (GD.Randf() > MagnetDropChance) return;

        var parent = GetParent();
        if (parent == null) return;

        var magnet = _magnetScene.Instantiate<MagnetPickup>();
        var spawnPos = GlobalPosition + new Vector2(GD.Randf() * 16f - 8f, GD.Randf() * 16f - 8f);
        parent.CallDeferred(Node.MethodName.AddChild, magnet);
        magnet.SetDeferred("global_position", spawnPos);
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
