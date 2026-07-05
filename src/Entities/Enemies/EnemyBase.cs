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

    // ── Rendu « gelé » : tant que l'ennemi est ralenti (toute source de slow : Lance Cryo, Voile de
    //    Givre…), un shader recolore sa texture vers un bleu glacé (paramètre `frost` 0→1). Un multiply
    //    sur SelfModulate ne peut qu'assombrir, jamais AJOUTER du bleu à un sprite chaud (orange) → le
    //    shader lerpe la couleur du pixel. Il ne touche pas MODULATE, donc HitFlash (modulate du nœud)
    //    et la teinte d'élite (self_modulate) restent composés par-dessus. ──
    private AnimatedSprite2D? _sprite;
    private bool  _frostActive = false;
    private ShaderMaterial? _frostMaterial;   // posé au 1er gel (lazy → batching préservé hors Givre)
    private static Shader?  _frostShader;

    /// <summary>Pose le shader de gel sur le sprite au premier besoin (lazy : les ennemis jamais gelés
    /// gardent leur matériau par défaut et restent batchables).</summary>
    private void EnsureFrostMaterial()
    {
        if (_frostMaterial != null || _sprite == null) return;
        _frostShader ??= GD.Load<Shader>("res://assets/shaders/enemy_frost.gdshader");
        _frostMaterial = new ShaderMaterial { Shader = _frostShader };
        _sprite.Material = _frostMaterial;
    }

    // ── Affixe d'élite (voir EliteAffixTable) : None pour un ennemi normal ──────
    protected EliteAffix _eliteAffix       = EliteAffix.None;
    private float _damageTakenMult         = 1f;   // <1 = blindé
    private float _regenFractionPerSecond  = 0f;   // >0 = régénérant
    private float _lifestealFraction       = 0f;   // >0 = vampirique
    private float _explodeDamageMult       = 0f;   // >0 = explosif (AoE à la mort)
    private float _timeSinceHit            = 999f; // pour le gating de la régénération
    private float _hpDropChance            = 0.08f;
    private static PackedScene? _shockwaveScene;

    public bool IsElite => _eliteAffix != EliteAffix.None;

    [Signal] public delegate void DiedEventHandler(int xpValue);

    /// <summary>Probabilité de dropper un orbe HP à la mort. Surchargeable (mini-boss : 0.25f).</summary>
    protected virtual float HpDropChance => _hpDropChance;

    public override void _Ready()
    {
        AddToGroup(Constants.GroupEnemies);
        _currentHp = MaxHp;
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
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

        UpdateEliteRegen((float)delta);

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

        // Rendu « gelé » : bascule le paramètre `frost` du shader uniquement au changement d'état
        // (évite d'écrire le uniform à chaque frame pour 200-300 ennemis).
        bool frozen = _slowTime > 0f;
        if (frozen != _frostActive && _sprite != null)
        {
            _frostActive = frozen;
            EnsureFrostMaterial();
            _frostMaterial?.SetShaderParameter("frost", frozen ? 1f : 0f);
        }

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

    /// <summary>
    /// Affixe Régénérant : soigne une fraction du MaxHp par seconde, tant que l'ennemi n'a pas été
    /// frappé depuis <see cref="EliteAffixTable.RegenDelaySeconds"/> (récompense le DPS soutenu).
    /// No-op pour un ennemi normal (_regenFractionPerSecond = 0).
    /// </summary>
    private void UpdateEliteRegen(float dt)
    {
        _timeSinceHit += dt;
        if (_regenFractionPerSecond <= 0f || _isDead) return;
        if (_timeSinceHit < EliteAffixTable.RegenDelaySeconds) return;
        if (_currentHp >= MaxHp) return;
        _currentHp = Mathf.Min(MaxHp, _currentHp + _regenFractionPerSecond * MaxHp * dt);
    }

    /// <summary>Affixe Vampirique : soigne l'ennemi d'une part des dégâts qu'il vient d'infliger.
    /// Appelé par les chemins de dégâts de contact. No-op si _lifestealFraction = 0.</summary>
    protected void ApplyLifesteal(float dealtDamage)
    {
        if (_lifestealFraction <= 0f || _isDead) return;
        _currentHp = Mathf.Min(MaxHp, _currentHp + dealtDamage * _lifestealFraction);
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
                ApplyLifesteal(reduced);
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
    /// Promeut cet ennemi en élite : applique les multiplicateurs de l'affixe (PV/vitesse/dégâts/XP),
    /// active son comportement (blindage, régén, vampirisme, explosion) et le distingue visuellement
    /// (teinte + agrandissement + halo). Appelé par EnemySpawner APRÈS ApplyScaling. Doit être appelé
    /// avant la 1re frame physique (la vitesse de base est capturée à ce moment-là dans
    /// UpdateStatusEffects). No-op si affix = None.
    /// </summary>
    public void ApplyElite(EliteAffix affix)
    {
        if (affix == EliteAffix.None) return;
        _eliteAffix = affix;
        var m = EliteAffixTable.Modifiers(affix);

        MaxHp      *= m.HpMult;
        _currentHp  = MaxHp;
        Speed      *= m.SpeedMult;
        Damage     *= m.DamageMult;
        XpValue     = Mathf.Max(1, Mathf.RoundToInt(XpValue * m.XpMult));

        _damageTakenMult        = m.DamageTakenMult;
        _regenFractionPerSecond = m.RegenFractionPerSecond;
        _lifestealFraction      = m.LifestealFraction;
        _explodeDamageMult      = m.ExplodeDamageMult;
        _hpDropChance           = m.HpDropChance;

        var tint = new Color(m.TintR, m.TintG, m.TintB, 1f);
        _sprite ??= GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            // SelfModulate teinte le sprite sans écraser le Modulate du corps (HitFlash reste net).
            // Le gel est géré indépendamment par le shader (paramètre `frost`), pas par SelfModulate.
            _sprite.SelfModulate = tint;
            _sprite.Scale *= EliteAffixTable.VisualScale;
        }

        // Halo pulsant derrière l'ennemi (couleur de l'affixe, semi-transparent).
        var aura = new EliteAura();
        AddChild(aura);
        aura.Configure(new Color(m.TintR, m.TintG, m.TintB, 0.28f), 20f * EliteAffixTable.VisualScale);
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
        _timeSinceHit = 0f;                    // suspend la régénération (affixe Régénérant)
        _currentHp -= amount * _damageTakenMult; // <1 pour un affixe Blindé
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
        TriggerEliteExplosion();
        QueueFree();
    }

    /// <summary>
    /// Affixe Explosif : à la mort, inflige une AoE au joueur s'il est dans le rayon (respecte ses
    /// i-frames via Player.TakeDamage) et joue un anneau de choc rouge. No-op sans l'affixe.
    /// Appelable depuis les Die() surchargés (ex. GraftedColossus) pour que l'affixe reste universel.
    /// </summary>
    protected void TriggerEliteExplosion()
    {
        if (_explodeDamageMult <= 0f) return;

        var player = GameManager.Instance?.PlayerInstance;
        if (player != null &&
            GlobalPosition.DistanceTo(player.GlobalPosition) < EliteAffixTable.ExplosionRadius)
        {
            float dmg = Damage * _explodeDamageMult * (1f - player.Stats.DamageReduction);
            player.TakeDamage(dmg);
        }

        _shockwaveScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_shockwave_ring.tscn");
        if (_shockwaveScene != null)
        {
            var ring = _shockwaveScene.Instantiate<Node2D>();
            ring.Modulate = new Color(1.6f, 0.5f, 0.3f, 1f); // teinte incandescente
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
            ring.SetDeferred("global_position", GlobalPosition);
        }
        ScreenShake.Instance?.Shake(8f, 0.25f);
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
