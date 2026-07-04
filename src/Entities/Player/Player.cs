using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public PlayerStats Stats { get; set; } = new PlayerStats();

    [Signal] public delegate void HpChangedEventHandler(float current, float max);

    private bool _isDead = false;
    private AnimatedSprite2D? _sprite;
    private PointLight2D?     _playerLight;
    private Camera2D?         _camera;
    private GpuParticles2D?   _trailParticles;

    // Clignotement implants sous 25% HP
    private float _blinkTimer = 0f;
    private bool  _blinkOn    = true;

    // Fenêtre d'invulnérabilité après un coup (façon Vampire Survivors) : empêche
    // qu'une nuée superposée vide la barre de vie en une frame. Cap le DPS reçu.
    private float       _invulnTimer = 0f;
    private const float InvulnWindow = 0.45f;

    private Tween? _hitTween;

    // ── Consommables meta (rechargés à chaque run, cf. MetaProgressionSystem) ──
    private int _extraLivesLeft   = 0;
    private int _absorbChargesLeft = 0;

    private static Texture2D? _playerLightTex;

    // ── Power-ups temporaires (buffs à durée limitée, aucun power-creep permanent) ──
    public  float SpeedMultiplier { get; private set; } = 1f;   // Célérité
    public  bool  Shielded        { get; private set; }          // Égide (invulnérabilité)

    /// <summary>Direction de visée = dernière direction de déplacement non nulle (twin-stick
    /// clavier/manette). Utilisée par les armes DIRIGÉES (ex. Lance Vectorielle) pour laisser
    /// place au skill de placement, là où les autres armes auto-visent l'ennemi le plus proche.</summary>
    public  Vector2 AimDirection { get; private set; } = Vector2.Down;
    private readonly System.Collections.Generic.Dictionary<PowerUpType, float> _buffTime = new();
    private BuffBar? _buffBar;

    public override void _Ready()
    {
        // Le joueur passe AU-DESSUS des VFX d'armes proches (flammes du Jet de Pyre ZIndex=2,
        // muzzle flash 4, cônes/champs) pour rester lisible « dans le feu de l'action » — sans
        // ce ZIndex il était occulté par ses propres projectiles. Reste sous les flashs d'impact
        // très ponctuels (foudre ZIndex=6) et sous l'UI (CanvasLayer).
        ZIndex = 5;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite?.Play("idle");

        _camera         = GetNodeOrNull<Camera2D>("Camera2D");
        _trailParticles = GetNodeOrNull<GpuParticles2D>("Trail");

        AddToGroup(Constants.GroupPlayer);
        GameManager.Instance.RegisterPlayer(this);
        AddPlayerLight();

        // Enregistre la caméra dans ScreenShake
        if (_camera != null)
            ScreenShake.Instance?.SetCamera(_camera);

        // Feedback level-up : shake + flash or
        if (XpSystem.Instance != null)
            XpSystem.Instance.LevelUp += OnLevelUp;

        // Power-ups : reset du multiplicateur de cadence (statique) + barre de buffs HUD.
        WeaponBase.FireRateMultiplier = 1f;
        _buffBar = new BuffBar();
        AddChild(_buffBar);

        // Consommables meta rechargés à chaque run.
        _extraLivesLeft    = MetaProgressionSystem.Instance?.GetUpgradeLevel("extra_life")    ?? 0;
        _absorbChargesLeft = MetaProgressionSystem.Instance?.GetUpgradeLevel("damage_absorb")  ?? 0;
    }

    public override void _ExitTree()
    {
        if (XpSystem.Instance != null)
            XpSystem.Instance.LevelUp -= OnLevelUp;
    }

    private void OnLevelUp(int newLevel)
    {
        ScreenShake.Instance?.Shake(6f, 0.20f);
        HitFlash(0.15f, new Color(1f, 0.8f, 0.267f, 1f));
        Heal(0.25f);
    }

    // ─── Power-ups temporaires ────────────────────────────────────────────────

    /// <summary>Applique (ou rafraîchit) un power-up temporaire et son effet immédiat.</summary>
    public void ApplyPowerUp(PowerUpType type, float duration)
    {
        bool wasActive = _buffTime.ContainsKey(type);
        _buffTime[type] = Mathf.Max(_buffTime.GetValueOrDefault(type), duration);
        if (!wasActive) StartBuff(type);

        var def = PowerUps.Get(type);
        AudioSystem.Instance?.PlaySfx("sfx_core_collect");
        HitFlash(0.18f, new Color(def.Color.R + 1f, def.Color.G + 1f, def.Color.B + 1f, 1f));
        ScreenShake.Instance?.Shake(2f, 0.12f);
    }

    private void StartBuff(PowerUpType type)
    {
        float m = PowerUps.Get(type).Magnitude;
        switch (type)
        {
            case PowerUpType.Overclock: WeaponBase.FireRateMultiplier = m; break;
            case PowerUpType.Berserk:   Stats.DamageMultiplier += m; InventorySystem.Instance?.RefreshWeaponDamages(); break;
            case PowerUpType.Celerity:  SpeedMultiplier = m; break;
            case PowerUpType.Aegis:     Shielded = true; break;
        }
    }

    private void EndBuff(PowerUpType type)
    {
        float m = PowerUps.Get(type).Magnitude;
        switch (type)
        {
            case PowerUpType.Overclock: WeaponBase.FireRateMultiplier = 1f; break;
            case PowerUpType.Berserk:   Stats.DamageMultiplier -= m; InventorySystem.Instance?.RefreshWeaponDamages(); break;
            case PowerUpType.Celerity:  SpeedMultiplier = 1f; break;
            case PowerUpType.Aegis:     Shielded = false; break;
        }
    }

    private void UpdateBuffs(float dt)
    {
        if (_buffTime.Count > 0)
        {
            foreach (var type in new System.Collections.Generic.List<PowerUpType>(_buffTime.Keys))
            {
                float t = _buffTime[type] - dt;
                if (t <= 0f) { _buffTime.Remove(type); EndBuff(type); }
                else _buffTime[type] = t;
            }
        }
        _buffBar?.UpdateBuffs(_buffTime);
    }

    /// <summary>Restaure un pourcentage des HP max. Flash vert si HP < max avant soin.</summary>
    public void Heal(float percent)
    {
        float amount = Stats.MaxHp * percent;
        float before = Stats.CurrentHp;
        Stats.CurrentHp = Mathf.Min(Stats.MaxHp, Stats.CurrentHp + amount);
        EmitSignal(SignalName.HpChanged, Stats.CurrentHp, Stats.MaxHp);
        if (Stats.CurrentHp > before)
            HitFlash(0.2f, new Color(0.2f, 1f, 0.4f, 1f));
    }

    // Teinte d'identité du personnage sélectionné (posée par GameManager via ApplyCharacterVisual).
    private Color _characterTint = new(0.267f, 1f, 0.933f); // aura cyan par défaut

    /// <summary>
    /// Applique la teinte d'identité du personnage : aura colorée + léger SelfModulate du sprite
    /// (atténué vers le blanc pour préserver l'art). Appelée par GameManager.RegisterPlayer()
    /// avant AddPlayerLight() — d'où le stockage dans un champ relu par AddPlayerLight().
    /// </summary>
    public void ApplyCharacterVisual(Color tint)
    {
        _characterTint = tint;
        // Les sprites de perso sont dédiés (couleurs propres) → pas de teinte du sprite,
        // seule l'aura prend la couleur d'identité.
        if (_playerLight != null)
            _playerLight.Color = tint;
    }

    /// <summary>Échange le SpriteFrames du joueur pour celui du personnage sélectionné.</summary>
    public void SetCharacterFrames(string framesPath)
    {
        if (_sprite == null || string.IsNullOrEmpty(framesPath)) return;
        var frames = GD.Load<SpriteFrames>(framesPath);
        if (frames == null) return;
        _sprite.SpriteFrames = frames;
        _sprite.Play("idle");
    }

    private void AddPlayerLight()
    {
        _playerLightTex ??= MakeRadialLightTexture(128);
        _playerLight = new PointLight2D
        {
            Name         = "PlayerLight",
            Color        = _characterTint,
            Energy       = 0.45f,
            Texture      = _playerLightTex,
            TextureScale = 4.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_playerLight);
    }

    internal static Texture2D MakeRadialLightTexture(int size)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, Colors.White);
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width    = size,
            Height   = size,
            Fill     = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo   = new Vector2(1.0f, 0.5f),
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        if (_invulnTimer > 0f) _invulnTimer -= (float)delta;
        UpdateBuffs((float)delta);
        UpdateHpRegen(delta);

        var direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (direction != Vector2.Zero)
            AimDirection = direction.Normalized();   // mémorise la dernière direction (visée des armes dirigées)
        Velocity = direction.Normalized() * Stats.Speed * SpeedMultiplier;
        MoveAndSlide();
        ClampToArena();

        UpdateAnimation(direction);
        UpdateHpBlink(delta);
        UpdateTrail();
        UpdateAura();
    }

    /// <summary>Auto-Réparation (upgrade meta hp_regen) : régénération continue clampée à MaxHp.</summary>
    private void UpdateHpRegen(double delta)
    {
        if (Stats.HpRegenPerSecond <= 0f || Stats.CurrentHp <= 0f) return;
        float before = Stats.CurrentHp;
        Stats.CurrentHp = Mathf.Min(Stats.MaxHp, Stats.CurrentHp + Stats.HpRegenPerSecond * (float)delta);
        if (Stats.CurrentHp != before)
            EmitSignal(SignalName.HpChanged, Stats.CurrentHp, Stats.MaxHp);
    }

    private void UpdateTrail()
    {
        if (_trailParticles == null) return;
        bool isMoving = Velocity.LengthSquared() > 100f;
        if (_trailParticles.Emitting != isMoving)
            _trailParticles.Emitting = isMoving;
    }

    // Aura du joueur qui s'intensifie avec la puissance du build (« ça brille »).
    private void UpdateAura()
    {
        if (_playerLight == null) return;
        int power = InventorySystem.Instance?.TotalWeaponPower ?? 1;
        float target = Mathf.Min(0.45f + power * 0.07f, 1.6f);
        _playerLight.Energy        = Mathf.Lerp(_playerLight.Energy, target, 0.05f);
        _playerLight.TextureScale  = Mathf.Min(4.0f + power * 0.18f, 7.5f);
    }

    // ─── Logique d'animation ─────────────────────────────────────────────────

    private void UpdateAnimation(Vector2 direction)
    {
        if (_sprite == null) return;

        if (direction == Vector2.Zero)
        {
            if (_sprite.Animation != "idle")
                _sprite.Play("idle");
            return;
        }

        // Deplacement : choisir run_right ou run_down selon direction dominante
        // flip_h gere les directions miroir (gauche = droite inversee)
        float absX = Mathf.Abs(direction.X);
        float absY = Mathf.Abs(direction.Y);

        if (absX >= absY)
        {
            // Horizontal dominant → run_right (flip_h si gauche)
            if (_sprite.Animation != "run_right")
                _sprite.Play("run_right");
            _sprite.FlipH = direction.X < 0f;
        }
        else
        {
            // Vertical dominant
            if (direction.Y > 0f)
            {
                // Vers le bas → run_down (pas de flip)
                if (_sprite.Animation != "run_down")
                    _sprite.Play("run_down");
                _sprite.FlipH = false;
            }
            else
            {
                // Vers le haut → run_down flippe verticalement
                // On reutilise run_down avec FlipV pour eviter un sprite supplementaire
                if (_sprite.Animation != "run_down")
                    _sprite.Play("run_down");
                _sprite.FlipH = false;
                // Note : flip vertical non supporte directement ici, approximation :
                // on utilise run_right en le faisant pivoter via la rotation si besoin.
                // Pour le MVP, run_down vers le haut est acceptable.
            }
        }
    }

    private void UpdateHpBlink(double delta)
    {
        if (_sprite == null) return;
        if (Stats.CurrentHp > Stats.MaxHp * 0.25f)
        {
            // HP > 25% : implants toujours allumes
            _sprite.Modulate = Colors.White;
            _blinkTimer = 0f;
            _blinkOn = true;
            return;
        }

        // Clignotement : 0,4 s ON / 0,2 s OFF
        _blinkTimer += (float)delta;
        float period = _blinkOn ? 0.4f : 0.2f;
        if (_blinkTimer >= period)
        {
            _blinkOn = !_blinkOn;
            _blinkTimer = 0f;
        }
        // Modulate : pleine couleur quand ON, desature quand OFF
        _sprite.Modulate = _blinkOn
            ? Colors.White
            : new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    // ─── Arena clamp ─────────────────────────────────────────────────────────

    private void ClampToArena()
    {
        float halfW = Constants.ArenaWidth  / 2f - Constants.WallThickness;
        float halfH = Constants.ArenaHeight / 2f - Constants.WallThickness;
        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, -halfW, halfW),
            Mathf.Clamp(GlobalPosition.Y, -halfH, halfH)
        );
    }

    // ─── Degats et mort ──────────────────────────────────────────────────────

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        // Plaque Adaptative (consommable meta damage_absorb) : absorbe totalement les premiers coups.
        if (_absorbChargesLeft > 0)
        {
            _absorbChargesLeft--;
            HitFlash(0.1f, new Color(1.2f, 1.6f, 2f, 1f));
            return;
        }
        // Égide (power-up) : invulnérabilité totale le temps du buff — absorbe le coup (flash doré).
        if (Shielded) { HitFlash(0.1f, new Color(2f, 1.6f, 0.6f, 1f)); return; }
        // Invulnérabilité : un seul coup encaissé par fenêtre, peu importe le nombre
        // d'ennemis collés. C'est le levier qui rend les grosses nuées jouables.
        if (_invulnTimer > 0f) return;
        _invulnTimer = InvulnWindow;

        Stats.CurrentHp = Mathf.Max(0f, Stats.CurrentHp - amount);
        EmitSignal(SignalName.HpChanged, Stats.CurrentHp, Stats.MaxHp);

        if (Stats.CurrentHp > 0f)
        {
            AudioSystem.Instance?.PlaySfx("sfx_player_hit");
            HitFlash(0.1f);
        }

        if (Stats.CurrentHp <= 0f)
            HandleDeath();
    }

    /// <summary>
    /// Flash de dégâts blanc sur-exposé (défaut) ou couleur personnalisée (ex. or pour level-up).
    /// </summary>
    public void HitFlash(float duration, Color? flashColor = null)
    {
        var color = flashColor ?? new Color(5f, 5f, 5f, 1f);
        _hitTween?.Kill();
        _hitTween = CreateTween();
        _hitTween.TweenProperty(this, "modulate", Colors.White, duration)
                 .From(color);
    }

    private void HandleDeath()
    {
        if (_isDead) return;

        // Noyau de Secours (consommable meta extra_life) : ramène à 30% HP au lieu de mourir.
        if (_extraLivesLeft > 0)
        {
            _extraLivesLeft--;
            Stats.CurrentHp = Stats.MaxHp * 0.3f;
            _invulnTimer = InvulnWindow;
            EmitSignal(SignalName.HpChanged, Stats.CurrentHp, Stats.MaxHp);
            HitFlash(0.3f, new Color(1.6f, 2f, 1.2f, 1f));
            AudioSystem.Instance?.PlaySfx("sfx_core_collect");
            GD.Print($"[Player] Noyau de Secours consommé. Charges restantes : {_extraLivesLeft}");
            return;
        }

        _isDead = true;
        GD.Print("Player died.");

        // Screen shake mort joueur
        ScreenShake.Instance?.Shake(20f, 0.50f);

        // Animation death
        if (_sprite != null)
        {
            _sprite.Play("death");
            _sprite.Modulate = Colors.White;
        }

        // SFX et arret musique a la mort
        AudioSystem.Instance?.PlaySfx("sfx_player_die");
        AudioSystem.Instance?.StopMusic(fadeOutSec: 1.0f);

        RunStatsTracker.Instance?.EndRun("death");
    }
}
