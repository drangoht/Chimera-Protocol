using Godot;

/// <summary>
/// Le Noyau Rouillé — BOSS DE FIN (~13 min), condition de victoire des CINQ niveaux.
///
/// Socle commun : avance lentement, salves radiales, ondes de choc, contact lourd, sprite agrandi
/// ×2,4. Le vaincre débloque le niveau suivant (<c>RunStatsTracker.OnLevelBossDefeated</c>) sans
/// arrêter la run — l'escalade d'overtime continue.
///
/// Deux couches s'ajoutent au socle (cf. docs/GDD.md §29) :
/// - **trois phases** (100→66→33→0 % de PV) qui resserrent la cadence de tout ce qu'il fait, la
///   dernière invoquant des adds ; chaque bascule ouvre 1 s de surcharge invulnérable et inoffensive,
///   fenêtre de repositionnement pour le joueur ;
/// - **une incarnation par biome** (<see cref="BossIncarnations"/>) : même entité, mais elle a
///   assimilé la matière locale et gagne UNE mécanique signature (éventail dirigé, translocation,
///   nova de givre, flaques de magma, faisceaux rotatifs).
///
/// Les PV et le TTK ne changent pas (12000 base, ~30 s pour un build de référence, GDD §20) : les
/// phases redistribuent l'intensité, elles ne rallongent pas le combat.
/// </summary>
public partial class RustedCore : EnemyBase
{
    private AnimatedSprite2D? _sprite;
    private static PackedScene? _bulletScene;
    private static PackedScene? _aetherCoreScene;
    private static PackedScene? _shockwaveScene;

    private const int   BulletsRing  = 16;     // rideau radial
    private const float BulletSpeed  = 210f;

    private float _burstTimer;
    private float _shockTimer;
    private float _signatureTimer;

    private Node?    _cachedParent;
    private Vector2  _cachedDeathPos;

    private PointLight2D? _aura;
    private float _baseSpeed;

    // ── Phases (GDD §29.3) ────────────────────────────────────────────────────────────────────
    private int   _phase;
    private float _transitionLeft;
    private float _addsTimer;

    // ── Incarnation de biome (GDD §29.2) ──────────────────────────────────────────────────────
    private BossIncarnation _inc = BossIncarnations.Root;

    // Faisceaux rotatifs (Néon) : un rig tournant, dont les rayons blessent par distance au segment.
    private Node2D? _beamRig;
    private float   _beamActiveLeft;
    private const float BeamLength    = 260f;
    private const float BeamHalfWidth = 11f;
    private const float BeamDps       = 18f;
    private const float BeamBurstSec  = 3f;
    private const float BeamSpinSpeed = 0.9f;   // rad/s
    private static readonly Color BeamColor = new(1f, 0.25f, 0.95f, 0.85f);

    // ── Lecture par le HUD (barre de boss) ────────────────────────────────────────────────────

    /// <summary>Nom localisé de l'incarnation, affiché au-dessus de la barre de boss.</summary>
    public string DisplayName => Loc.T(_inc.NameKey);

    /// <summary>Ratio de PV dans [0,1] — source de la barre de boss.</summary>
    public float HpRatio => MaxHp <= 0f ? 0f : Mathf.Clamp(_currentHp / MaxHp, 0f, 1f);

    /// <summary>Phase courante (0..2).</summary>
    public int Phase => _phase;

    /// <summary>Le boss est-il en surcharge de bascule (invulnérable, immobile, inoffensif) ?</summary>
    public bool IsSurcharging => _transitionLeft > 0f;

    /// <summary>Id de l'incarnation résolue depuis le biome (journal de TTK, cf. BossTelemetry).</summary>
    public string IncarnationId => _inc.Id;

    public override void _Ready()
    {
        // NB : ces valeurs sont écrasées par EnemySpawner.ApplyScaling() qui lit data/enemies.json
        // (source de vérité du tuning) juste après _Ready. On les garde alignées sur le JSON pour
        // documenter l'intention et éviter la dérive.
        MaxHp   = 8000f;
        Speed   = 46f;
        Damage  = 34f;
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
            _sprite.Scale = new Vector2(2.4f, 2.4f);  // boss massif et imposant
            _sprite.Play("idle");
        }

        ResolveIncarnation();

        _baseSpeed      = Speed;
        _burstTimer     = BossPhases.BurstInterval(0);
        _shockTimer     = BossPhases.ShockInterval(0);
        _signatureTimer = _inc.BaseIntervalSec;
        _addsTimer      = BossPhases.AddsIntervalSeconds;

        AddBossAura();

        // Journal de TTK (user://boss_ttk.log) : DIFFÉRÉ d'une frame, car EnemySpawner.ApplyScaling
        // écrase MaxHp juste après ce _Ready — ouvrir le relevé ici journaliserait les 12000 PV de
        // base au lieu des PV effectifs (palier de menace + scaling temporel).
        Callable.From(() => BossTelemetry.Begin(this)).CallDeferred();

        // Entrée fracassante
        ScreenShake.Instance?.Shake(14f, 0.5f);
        SpawnShockwave();
    }

    /// <summary>
    /// Résout l'incarnation depuis le biome joué et l'applique au sprite. Le jeu de frames dédié
    /// est chargé s'il existe : une variante dont l'asset n'a pas encore été généré retombe sur le
    /// sprite de la souche + teinte, plutôt que de faire disparaître le boss.
    /// </summary>
    private void ResolveIncarnation()
    {
        _inc = BossIncarnations.For(GameManager.Instance?.CurrentBiomeId);

        if (_sprite == null) return;

        if (!string.IsNullOrEmpty(_inc.FramesPath) && ResourceLoader.Exists(_inc.FramesPath))
            SetSpriteFrames(_inc.FramesPath);

        _sprite.SelfModulate = new Color(_inc.TintR, _inc.TintG, _inc.TintB);
    }

    private void AddBossAura()
    {
        var tex = Player.MakeRadialLightTexture(64);
        _aura = new PointLight2D
        {
            // L'aura reprend la teinte de l'incarnation : le halo est ce qu'on voit en premier
            // quand le boss entre dans le champ, avant même son sprite.
            Color        = new Color(Mathf.Min(1f, _inc.TintR), Mathf.Min(1f, _inc.TintG * 0.55f),
                                     Mathf.Min(1f, _inc.TintB * 0.3f + 0.2f)),
            Energy       = 1.0f,
            Texture      = tex,
            TextureScale = 9.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_aura);
        var t = CreateTween().SetLoops();
        t.TweenProperty(_aura, "energy", 1.8f, 0.9f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(_aura, "energy", 1.0f, 0.9f).SetEase(Tween.EaseType.InOut);
    }

    protected override float ContactRadius => 56f;  // boss agrandi (scale 2.4)
    protected override int   GetOrbTier()  => 3;
    protected override float HpDropChance  => 1.0f;

    // -------------------------------------------------------------------------
    // Phases
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pendant la surcharge, le boss encaisse mais ne perd PLUS de PV : le retour visuel du coup
    /// est conservé (sans quoi le joueur croirait ses armes cassées), la barre reste gelée.
    /// </summary>
    public override void TakeDamage(float amount)
    {
        if (_isDead) return;
        if (_transitionLeft > 0f) { HitFlash(0.05f); return; }
        base.TakeDamage(amount);

        // Le chrono du TTK part au PREMIER coup encaissé, pas à l'apparition : le boss arrive à
        // distance et le temps d'approche n'appartient pas au temps de mise à mort.
        BossTelemetry.NotifyFirstDamage();
        BossTelemetry.NotifyHpRatio(HpRatio);
    }

    /// <summary>Entre en surcharge et bascule de phase à la fin du télégraphe.</summary>
    private void EnterSurcharge(int nextPhase)
    {
        _phase          = nextPhase;
        _transitionLeft = BossPhases.TransitionSeconds;

        // Trace de mesure pour le game-tester (--debug-boss) : horodate chaque bascule, ce que la
        // capture d'écran seule ne donne pas.
        if (DebugHooks.BossDebug)
            GD.Print($"[RustedCore] {_inc.Id} → phase {BossPhases.RomanNumeral(_phase)} " +
                     $"à t={RunStatsTracker.Instance?.ElapsedSeconds ?? 0f:0.0}s (PV {HpRatio:P0})");

        BossTelemetry.NotifyPhase(_phase, HpRatio);

        // Télégraphe : le boss blanchit par pulsations et son aura sature. Lisible même quand
        // l'écran est plein d'ennemis (c'est le seul moment où le boss cesse de tirer).
        if (_sprite != null)
        {
            var t = CreateTween().SetLoops(3);
            t.TweenProperty(_sprite, "modulate", new Color(3.2f, 3f, 2.2f), 0.16f);
            t.TweenProperty(_sprite, "modulate", Colors.White, 0.16f);
        }
        if (_aura != null) _aura.Energy = 3.2f;

        ScreenShake.Instance?.Shake(6f, 0.35f);
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");

        // Cadences de la NOUVELLE phase, appliquées dès la reprise.
        _burstTimer     = BossPhases.BurstInterval(_phase);
        _shockTimer     = BossPhases.ShockInterval(_phase);
        _signatureTimer = BossPhases.SignatureInterval(_phase, _inc.BaseIntervalSec);

        // Phase III : la 1re vague d'adds part à la reprise, pas 12 s plus tard.
        if (BossPhases.SummonsAdds(_phase)) _addsTimer = 0f;
    }

    private void ExitSurcharge()
    {
        if (_sprite != null) _sprite.Modulate = Colors.White;
        SpawnShockwave();                      // la reprise repousse le joueur collé au boss
        ScreenShake.Instance?.Shake(9f, 0.3f);
    }

    // -------------------------------------------------------------------------
    // Boucle
    // -------------------------------------------------------------------------

    protected override void UpdateMovement(Player player, double delta)
    {
        if (_isDead) { Velocity = Vector2.Zero; return; }

        float dt = (float)delta;

        // Surcharge : immobile, muet, invulnérable. Les faisceaux éventuels sont coupés.
        if (_transitionLeft > 0f)
        {
            Velocity = Vector2.Zero;
            _transitionLeft -= dt;
            if (_transitionLeft <= 0f) ExitSurcharge();
            UpdateBeams(player, dt, allowDamage: false);
            return;
        }

        int next = BossPhases.Advance(_phase, HpRatio);
        if (next != _phase) { EnterSurcharge(next); return; }

        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        Speed = _baseSpeed * BossPhases.SpeedMult(_phase);
        Velocity = toPlayer * Speed;
        MoveAndSlide();

        if (_sprite != null)
        {
            _sprite.FlipH = toPlayer.X < 0f;
            if (_sprite.Animation == "idle" && Velocity.LengthSquared() > 1f)
                _sprite.Play("move");
        }

        _burstTimer -= dt;
        if (_burstTimer <= 0f)
        {
            FireRadialBurst();
            _burstTimer = BossPhases.BurstInterval(_phase);
        }

        _shockTimer -= dt;
        if (_shockTimer <= 0f)
        {
            SpawnShockwave();
            _shockTimer = BossPhases.ShockInterval(_phase);
        }

        _signatureTimer -= dt;
        if (_signatureTimer <= 0f)
        {
            FireSignature(player);
            _signatureTimer = BossPhases.SignatureInterval(_phase, _inc.BaseIntervalSec);
        }

        UpdateBeams(player, dt, allowDamage: true);
        UpdateAdds(dt);
    }

    /// <summary>Phase III : renfort de la faune LOCALE, plafonné par le cap global du spawner.</summary>
    private void UpdateAdds(float dt)
    {
        if (!BossPhases.SummonsAdds(_phase)) return;

        _addsTimer -= dt;
        if (_addsTimer > 0f) return;
        _addsTimer = BossPhases.AddsIntervalSeconds;

        if (GetTree().GetFirstNodeInGroup(Constants.GroupEnemySpawner) is not EnemySpawner spawner) return;
        float tMin = (RunStatsTracker.Instance?.ElapsedSeconds ?? 780f) / 60f;
        if (spawner.SummonAdds(BossPhases.AddsPerWave, tMin) > 0)
            ScreenShake.Instance?.Shake(4f, 0.2f);
    }

    protected override void HandleContactDamage(Player player, double delta)
    {
        // Inoffensif pendant la surcharge : punir un joueur au corps-à-corps pendant la seule
        // fenêtre où le boss ne peut pas riposter serait un coup gratuit.
        if (_transitionLeft > 0f) return;

        if (GlobalPosition.DistanceTo(player.GlobalPosition) < ContactRadius)
        {
            // Sans cooldown propre : la cadence est celle des i-frames du joueur (0,45 s). Le coup
            // reste donc DISCRET, ce qui autorise le plancher du cran VI — contrairement au faisceau
            // et aux flaques, qui sont des PV/seconde (cf. SaturationTable.ChampionDamage).
            DealDiscreteDamage(player, Damage);
        }
    }

    // -------------------------------------------------------------------------
    // Attaques du socle commun
    // -------------------------------------------------------------------------

    private void FireRadialBurst()
    {
        if (_bulletScene == null) return;
        PlayAnim(_sprite, "attack");
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");
        ScreenShake.Instance?.Shake(2.5f, 0.1f);

        for (int i = 0; i < BulletsRing; i++)
        {
            float angle = 2f * Mathf.Pi * i / BulletsRing;
            SpawnBullet(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), BulletSpeed);
        }
    }

    private void SpawnBullet(Vector2 dir, float speed, Vector2 originOffset = default)
    {
        if (_bulletScene == null) return;
        var parent = GetParent();
        var bullet = _bulletScene.Instantiate<EnemyBullet>();
        bullet.Direction = dir;
        bullet.Speed     = speed;
        bullet.Damage    = Damage;
        bullet.FromChampion = true;   // plancher du cran VI (le projectile survit à son tireur)
        parent?.CallDeferred(Node.MethodName.AddChild, bullet);
        bullet.SetDeferred("global_position", GlobalPosition + originOffset);
    }

    private void SpawnShockwave() => SpawnShockwaveAt(GlobalPosition);

    private void SpawnShockwaveAt(Vector2 pos)
    {
        if (_shockwaveScene == null) return;
        var ring = _shockwaveScene.Instantiate<Node2D>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
        ring.SetDeferred("global_position", pos);
    }

    // -------------------------------------------------------------------------
    // Mécaniques signature (une par biome, cf. GDD §29.2)
    // -------------------------------------------------------------------------

    private void FireSignature(Player player)
    {
        switch (_inc.Signature)
        {
            case BossSignature.DirectedFan:   SignatureDirectedFan(player);  break;
            case BossSignature.Blink:         SignatureBlink(player);        break;
            case BossSignature.FrostNova:     SignatureFrostNova(player);    break;
            case BossSignature.MagmaPools:    SignatureMagmaPools(player);   break;
            case BossSignature.RotatingBeams: SignatureRotatingBeams();      break;
        }
    }

    /// <summary>Sanctuaire — éventail resserré vers le joueur : punit la ligne droite.</summary>
    private void SignatureDirectedFan(Player player)
    {
        PlayAnim(_sprite, "attack");
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");

        var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
        const int shots = 5;
        const float spread = Mathf.Pi / 8f;   // ±22,5°
        for (int i = 0; i < shots; i++)
        {
            float a = -spread + 2f * spread * i / (shots - 1);
            SpawnBullet(toPlayer.Rotated(a), BulletSpeed * 1.25f);
        }
    }

    /// <summary>Aether — translocation près du joueur puis salve spiralée : casse le kiting.</summary>
    private void SignatureBlink(Player player)
    {
        const float blinkDistance = 190f;

        // Rémanence à l'ancienne position : sans elle, le boss « saute » sans qu'on comprenne d'où.
        SpawnShockwaveAt(GlobalPosition);

        float angle = GD.Randf() * Mathf.Tau;
        var target  = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * blinkDistance;
        GlobalPosition = target;
        SpawnShockwave();
        ScreenShake.Instance?.Shake(7f, 0.25f);
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");

        // Salve spiralée : chaque projectile décalé, la nappe tourne au lieu de former un anneau net.
        const int shots = 10;
        for (int i = 0; i < shots; i++)
        {
            float a = Mathf.Tau * i / shots + GD.Randf() * 0.12f;
            SpawnBullet(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), BulletSpeed * (0.85f + 0.05f * i));
        }
    }

    /// <summary>Givre — nova qui ralentit, et plaques persistantes autour : punit l'immobilité.</summary>
    private void SignatureFrostNova(Player player)
    {
        const float novaRadius = 240f;

        SpawnShockwave();
        ScreenShake.Instance?.Shake(8f, 0.3f);
        AudioSystem.Instance?.PlaySfx("sfx_weapon_rail_shoot");   // même SFX que la Lance Cryogénique

        if (GlobalPosition.DistanceTo(player.GlobalPosition) <= novaRadius)
            player.ApplyChill(0.55f, 2f);

        // Couronne de plaques : elles restent après la nova et redessinent l'espace jouable.
        int plaques = 4 + _phase;                       // 4 → 6 selon la phase
        float baseAngle = GD.Randf() * Mathf.Tau;
        for (int i = 0; i < plaques; i++)
        {
            float a = baseAngle + Mathf.Tau * i / plaques;
            var pos = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 130f;
            BossHazard.Spawn(GetTree(), pos, BossHazardKind.Frost, radius: 58f, lifetime: 7f);
        }
    }

    /// <summary>Fournaise — flaques de magma télégraphiées : réduit l'espace sûr.</summary>
    private void SignatureMagmaPools(Player player)
    {
        PlayAnim(_sprite, "attack");
        AudioSystem.Instance?.PlaySfx("sfx_weapon_sentinel_shoot");
        ScreenShake.Instance?.Shake(5f, 0.25f);

        int pools = 2 + _phase;                          // 2 → 4 selon la phase
        for (int i = 0; i < pools; i++)
        {
            // Autour du joueur, jamais exactement dessous : on lui laisse toujours une sortie.
            float a = GD.Randf() * Mathf.Tau;
            float d = 85f + GD.Randf() * 90f;
            var pos = player.GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            BossHazard.Spawn(GetTree(), pos, BossHazardKind.Magma,
                             radius: 46f, lifetime: 6.5f, armDelay: 0.7f);
        }
    }

    /// <summary>Néon — salve de faisceaux rotatifs : impose une rotation constante autour du boss.</summary>
    private void SignatureRotatingBeams()
    {
        int beams = 2 + _phase;                          // 2 → 4 selon la phase
        BuildBeamRig(beams);
        _beamActiveLeft = BeamBurstSec;
        AudioSystem.Instance?.PlaySfx("sfx_weapon_rail_shoot");
        ScreenShake.Instance?.Shake(5f, 0.2f);
    }

    private void BuildBeamRig(int beams)
    {
        _beamRig?.QueueFree();
        _beamRig = new Node2D { ZIndex = 2 };
        AddChild(_beamRig);

        for (int i = 0; i < beams; i++)
        {
            var dir  = Vector2.Right.Rotated(Mathf.Tau * i / beams);
            var line = new Line2D
            {
                Width        = BeamHalfWidth * 2f,
                DefaultColor = BeamColor,
                Points       = new[] { Vector2.Zero, dir * BeamLength },
            };
            _beamRig.AddChild(line);

            // Cœur clair plus fin : sans lui, le faisceau lit comme une barre grise translucide
            // sur les fonds sombres du Néon (constaté au playtest du 2026-07-28).
            var core = new Line2D
            {
                Width        = BeamHalfWidth * 0.7f,
                DefaultColor = new Color(1f, 0.9f, 1f, 0.95f),
                Points       = new[] { Vector2.Zero, dir * BeamLength },
            };
            line.AddChild(core);
        }
    }

    /// <summary>
    /// Fait tourner les faisceaux et applique leurs dégâts par distance point-segment — pas de
    /// corps physique : un rayon fin en rotation rapide raterait le joueur entre deux frames.
    /// </summary>
    private void UpdateBeams(Player player, float dt, bool allowDamage)
    {
        if (_beamRig == null) return;

        if (_beamActiveLeft <= 0f)
        {
            _beamRig.QueueFree();
            _beamRig = null;
            return;
        }

        _beamRig.Rotation += BeamSpinSpeed * dt * BossPhases.SignatureRate(_phase);

        // Extinction progressive sur la dernière demi-seconde (le rayon ne disparaît pas d'un coup).
        _beamActiveLeft -= dt;
        float fade = Mathf.Clamp(_beamActiveLeft / 0.5f, 0f, 1f);
        float dim = fade * (allowDamage ? 1f : 0.35f);
        foreach (var child in _beamRig.GetChildren())
            if (child is Line2D l)
            {
                l.DefaultColor = new Color(BeamColor.R, BeamColor.G, BeamColor.B, BeamColor.A * dim);
                if (l.GetChildCount() > 0 && l.GetChild(0) is Line2D core)
                    core.DefaultColor = new Color(1f, 0.9f, 1f, 0.95f * dim);
            }

        if (!allowDamage || _beamActiveLeft <= 0f) return;

        var local = player.GlobalPosition - GlobalPosition;
        int beams = _beamRig.GetChildCount();
        for (int i = 0; i < beams; i++)
        {
            var dir = Vector2.Right.Rotated(_beamRig.Rotation + Mathf.Tau * i / beams);
            float along = local.Dot(dir);
            if (along < 0f || along > BeamLength) continue;
            if (Mathf.Abs(local.Dot(new Vector2(-dir.Y, dir.X))) > BeamHalfWidth) continue;

            player.TakeDamage(BeamDps * dt * (1f - player.Stats.DamageReduction));
            return;   // un seul faisceau blesse à la fois : les croisements ne doublent pas les dégâts
        }
    }

    // -------------------------------------------------------------------------
    // Mort
    // -------------------------------------------------------------------------

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

        _beamRig?.QueueFree();
        _beamRig = null;

        BossTelemetry.NotifyKill();

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled(this);
        PlayDeathSfx();
        // Pas de SpawnXpOrb() : 500 XP à l'instant de la victoire ouvrirait un LevelUpScreen
        // parasite par-dessus l'écran de fin (cf. OBS-2 du game-tester).

        if (_sprite != null)
            _sprite.Play("death");
        else
            FinishDeath();
    }

    /// <summary>Explosion massive : 3 ondes de choc, gros flash, 3 Noyaux d'Aether.</summary>
    private void FinishDeath()
    {
        ScreenShake.Instance?.Shake(18f, 0.6f);
        ScreenShake.Instance?.HitStop(0.1f);

        for (int i = 0; i < 3; i++)
            SpawnShockwaveAt(_cachedDeathPos);

        SpawnDeathBurst();

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

        // Battre le boss de fin = NIVEAU TERMINÉ (débloque le suivant + bannière) mais la run NE
        // s'arrête PAS : l'escalade overtime continue (survie sans fin). La run se termine à la
        // mort du joueur (high score = temps survécu).
        RunStatsTracker.Instance?.OnLevelBossDefeated();
        QueueFree();
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
    }
}
