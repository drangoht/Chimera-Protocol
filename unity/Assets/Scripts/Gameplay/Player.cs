using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Joueur — port du cœur de <c>Player</c> (Lot 2, docs/UNITY_MIGRATION_PLAN.md).
///
/// <para><b>Mouvement par transform, sans physique dynamique</b> (§4.4, point P1 tranché) : sous
/// Godot, <c>EnemyBase.CollisionMask = 2</c> — les ennemis ne collisionnent ni entre eux ni avec le
/// joueur, seulement avec les obstacles statiques. Reproduire ici un <c>Rigidbody2D</c> ajouterait
/// une physique que le jeu d'origine n'a jamais eue.</para>
///
/// <para><b>Les i-frames sont critiques</b> : 0,45 s, valeur que le projet documente comme telle
/// pour la survie en nuée. Un joueur touché par 30 ennemis dans la même frame ne doit encaisser
/// qu'un seul coup.</para>
/// </summary>
public sealed class Player : MonoBehaviour
{
    /// <summary>Fenêtre d'invulnérabilité après un coup. Constante de gameplay, pas un réglage.</summary>
    public const float InvulnWindow = 0.45f;

    /// <summary>Rayon du corps, utilisé pour repousser les ennemis qui le chevauchent.</summary>
    private const float PlayerBodyRadius = 13f;

    public static Player? Instance { get; private set; }

    public PlayerStats Stats { get; } = new();

    /// <summary>Multiplicateur de vitesse temporaire (effets, ralentissements).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Direction de visée : souris ou stick droit.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    /// <summary>Vrai quand le sprite regarde à gauche — lu par les accessoires de silhouette.</summary>
    public bool FacingLeft { get; private set; }

    /// <summary>Vitesse courante, en unités par seconde.</summary>
    public Vector2 Velocity { get; private set; }

    /// <summary>
    /// Direction imposée de l'extérieur, court-circuitant le clavier. Sert au <b>banc</b>
    /// (<c>--auto-play</c>) : le pilote automatique doit traverser exactement le même chemin de
    /// mouvement qu'un joueur humain, sinon la mesure porte sur autre chose que le jeu.
    /// </summary>
    public Vector2? ExternalMoveOverride { get; set; }

    /// <summary>
    /// Visée imposée de l'extérieur, court-circuitant souris et stick. Même rôle que
    /// <see cref="ExternalMoveOverride"/> : sans elle, une arme dirigée serait invérifiable au banc,
    /// qui n'a ni curseur ni manette.
    /// </summary>
    public Vector2? ExternalAimOverride { get; set; }

    /// <summary>Impose la visée — raccourci lisible pour les bancs et le pilote automatique.</summary>
    public void ForceAim(Vector2 direction) => ExternalAimOverride = direction;

    /// <summary>Émis à chaque changement de PV : <c>(courant, max)</c>.</summary>
    public event Action<float, float>? HealthChanged;

    /// <summary>Émis quand les PV atteignent zéro.</summary>
    public event Action? Died;

    private float _invulnTimer;
    private bool  _dead;

    private FrameAnimator? _animator;

    private void Awake()
    {
        Instance = this;
        Stats.ResetForRun();

        _animator = GetComponentInChildren<FrameAnimator>();
        if (_animator != null)
        {
            var frames = SpriteFramesLibrary.Get("player");
            if (frames != null) _animator.SetSpriteFrames(frames);
            _animator.Play("idle");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_dead) return;

        float dt = Time.deltaTime;
        if (_invulnTimer > 0f) _invulnTimer -= dt;

        UpdateMovement(dt);
        UpdateRegen(dt);
        PushEnemiesAside();
    }

    // ─── Déplacement ──────────────────────────────────────────────────────────

    private void UpdateMovement(float dt)
    {
        // Les touches passent par InputRemap : elles sont rebindables depuis les Options, et le
        // libellé affiché au joueur doit venir de la même source (cf. InputRemap).
        Vector2 input = ExternalMoveOverride ?? InputRemap.MoveVector();

        UpdateDashTimers(dt);

        // Déclenchement de l'esquive : ruade brève et invulnérable, disponible seulement si une
        // greffe l'a accordée.
        if (_dashEnabled && _dashActiveLeft <= 0f && _dashCooldownLeft <= 0f &&
            InputRemap.WasPressedThisFrame(GameAction.Dash))
            StartDash(input);

        // La vitesse est plafonnée par StatCaps — la même source que côté Godot.
        float speed = Mathf.Min(Stats.Speed * SpeedMultiplier, StatCaps.MaxSpeed);
        Velocity = input * speed;

        // Pendant la ruade, la vitesse est IMPOSÉE : elle ne passe pas par le plafond, sans quoi une
        // esquive ne serait qu'un déplacement ordinaire et ne sortirait jamais d'un encerclement.
        if (_dashActiveLeft > 0f) Velocity = _dashVelocity;

        Vector3 next = transform.position + (Vector3)(Velocity * dt);
        next.x = Mathf.Clamp(next.x, -Arena.HalfWidth, Arena.HalfWidth);
        next.y = Mathf.Clamp(next.y, -Arena.HalfHeight, Arena.HalfHeight);

        // Les obstacles écartent au lieu d'arrêter net : un mur qui bloque brutalement transforme
        // chaque angle en piège, alors que le jeu demande de circuler entre les masses en permanence.
        next = ArenaObstacles.Resolve(next, PlayerBodyRadius);
        transform.position = next;

        if (Mathf.Abs(input.x) > 0.01f) FacingLeft = input.x < 0f;

        UpdateAim();

        // La charge blesse pendant toute la ruade — un ennemi une seule fois par charge.
        if (_dashActiveLeft > 0f && _chargeDamage > 0f) ApplyChargeDamage();

        if (_animator != null)
        {
            _animator.FlipX = FacingLeft;
            _animator.Play(Velocity.sqrMagnitude > 1f ? "move" : "idle");
        }
    }

    // ─── Visée dirigée ────────────────────────────────────────────────────────

    /// <summary>Seuil sous lequel le stick droit est considéré au repos.</summary>
    private const float AimStickDeadzone = 0.25f;

    /// <summary>Distance du réticule au joueur, en pixels.</summary>
    private const float AimIndicatorRadius = 46f;

    private bool _gamepadAim;
    private Vector3 _lastMousePosition;
    private Transform? _aimIndicator;

    /// <summary>
    /// Met à jour <see cref="AimDirection"/> et le réticule.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Le portage visait dans la direction de DÉPLACEMENT</b> (<c>Velocity.normalized</c>).
    /// C'était une arme dirigée sans direction : la Lance Vectorielle tirait là où l'on courait, on
    /// ne pouvait pas viser un ennemi sans lui foncer dessus, et rien à l'écran n'indiquait où le
    /// trait partirait. La seule arme d'adresse du jeu devenait ainsi <i>moins</i> maniable qu'un
    /// canon automatique — ce qui se signale « la Lance Vectorielle ne fonctionne pas », et c'est
    /// exact du point de vue du joueur, bien qu'aucun tir ne manque.
    ///
    /// <para>La règle d'origine : stick droit prioritaire dès qu'il sort de sa zone morte, sinon
    /// curseur souris ; bouger l'un rebascule sur lui. Sans cette mémoire du dernier périphérique,
    /// une souris immobile ramènerait sans cesse la visée manette vers le curseur.</para>
    /// </remarks>
    private void UpdateAim()
    {
        // Visée imposée : le banc n'a ni souris ni manette, et une arme dirigée qu'on ne peut pas
        // pointer n'est pas vérifiable autrement.
        if (ExternalAimOverride.HasValue)
        {
            var forced = ExternalAimOverride.Value;
            if (forced.sqrMagnitude > 0.0001f) AimDirection = forced.normalized;

            UpdateAimIndicator();
            return;
        }

        Vector2 stick = new(Input.GetAxisRaw("RightStickX"), -Input.GetAxisRaw("RightStickY"));
        if (stick.magnitude < AimStickDeadzone) stick = Vector2.zero;

        var mouse = Input.mousePosition;
        if ((mouse - _lastMousePosition).sqrMagnitude > 1f) { _gamepadAim = false; _lastMousePosition = mouse; }
        if (stick != Vector2.zero) _gamepadAim = true;

        if (_gamepadAim)
        {
            if (stick != Vector2.zero) AimDirection = stick.normalized;   // sinon : garde la dernière
        }
        else
        {
            var camera = Camera.main;
            if (camera != null)
            {
                Vector2 world = camera.ScreenToWorldPoint(mouse);
                Vector2 toMouse = world - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 1f) AimDirection = toMouse.normalized;
            }
        }

        UpdateAimIndicator();
    }

    /// <summary>
    /// Réticule : un repère posé à distance fixe dans la direction visée, <b>affiché seulement</b>
    /// quand une arme dirigée est équipée. Le montrer en permanence ajouterait un élément mobile
    /// dans une nuée pour une information dont 17 armes sur 19 n'ont aucun usage.
    /// </summary>
    private void UpdateAimIndicator()
    {
        var inv = InventorySystem.Instance;
        bool directed = inv != null &&
            (inv.WeaponLevels.ContainsKey("vector_lance") || inv.WeaponLevels.ContainsKey("vector_beam"));

        if (!directed)
        {
            if (_aimIndicator != null) _aimIndicator.gameObject.SetActive(false);
            return;
        }

        if (_aimIndicator == null) _aimIndicator = BuildAimIndicator();

        _aimIndicator.gameObject.SetActive(true);
        _aimIndicator.localPosition = AimDirection * AimIndicatorRadius;
        _aimIndicator.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg);
    }

    private Transform BuildAimIndicator()
    {
        var go = new GameObject("Reticule", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = AimReticleSprite.Get();
        sr.color = new Color(0.27f, 1f, 0.93f, 0.85f);

        // Au-dessus du joueur : sous lui, une nuée serrée le masquerait exactement quand viser
        // compte le plus.
        sr.sortingOrder = 30;

        return go.transform;
    }

    // ─── Esquive (greffe « Servos Erratiques ») ───────────────────────────────

    private bool  _dashEnabled;
    private float _dashDistance, _dashDuration, _dashCooldown, _dashCooldownFloor, _dashIframes;
    private bool  _dashFollowsCooldownReduction;
    private float _dashCooldownLeft, _dashActiveLeft, _dashCooldownDuration = 1f;
    private Vector2 _dashVelocity;

    private float _chargeDamage, _chargeWidth, _chargeKnockback;
    private readonly HashSet<EnemyBase> _chargeHit = new();

    /// <summary>L'esquive est-elle accordée par une greffe ? (jauge du HUD)</summary>
    public bool DashEnabled => _dashEnabled;

    /// <summary>La ruade est-elle en cours ? Le front descendant déclenche la nova de la fusion.</summary>
    public bool IsDashing => _dashActiveLeft > 0f;

    /// <summary>Recharge dans [0,1] : 0 juste après usage, 1 = prête.</summary>
    public float DashReadyRatio => _dashCooldownDuration <= 0f
        ? 1f
        : Mathf.Clamp01(1f - _dashCooldownLeft / _dashCooldownDuration);

    /// <summary>
    /// Accorde l'esquive. Les trois derniers paramètres la transforment en <b>charge</b> : un couloir
    /// de dégâts avec recul.
    /// </summary>
    public void EnableDash(float distance, float duration, float cooldown, float cooldownFloor,
                           float iframes, bool followsCooldownReduction,
                           float chargeDamage = 0f, float chargeWidth = 0f, float chargeKnockback = 0f)
    {
        _dashEnabled = true;
        _dashDistance = distance;
        _dashDuration = Mathf.Max(0.01f, duration);
        _dashCooldown = cooldown;
        _dashCooldownFloor = cooldownFloor;
        _dashIframes = iframes;
        _dashFollowsCooldownReduction = followsCooldownReduction;
        _dashCooldownLeft = 0f;   // disponible immédiatement

        _chargeDamage = chargeDamage;
        _chargeWidth = chargeWidth;
        _chargeKnockback = chargeKnockback;
    }

    /// <summary>
    /// Déclenche une ruade sans passer par le clavier — réservé aux bancs, qui n'ont pas d'entrée.
    /// Sans ce point d'entrée, l'esquive resterait invérifiable autrement qu'en jouant.
    /// </summary>
    public void TriggerDashForBench()
    {
        if (!_dashEnabled || _dashActiveLeft > 0f || _dashCooldownLeft > 0f) return;
        StartDash(ExternalMoveOverride ?? AimDirection);
    }

    private void UpdateDashTimers(float dt)
    {
        if (_dashCooldownLeft > 0f) _dashCooldownLeft -= dt;
        if (_dashActiveLeft   > 0f) _dashActiveLeft   -= dt;
    }

    private void StartDash(Vector2 moveDirection)
    {
        Vector2 dir = moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : AimDirection;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;

        _dashVelocity = dir * (_dashDistance / _dashDuration);
        _dashActiveLeft = _dashDuration;
        _chargeHit.Clear();   // un ennemi n'est touché qu'une fois par charge

        // Les i-frames de l'esquive passent par la MÊME fenêtre que celles des dégâts subis : deux
        // compteurs concurrents laisseraient passer un coup au moment précis où le joueur esquive.
        _invulnTimer = Mathf.Max(_invulnTimer, _dashIframes);

        float reduced = _dashFollowsCooldownReduction
            ? _dashCooldown * (1f - Stats.CooldownReduction)
            : _dashCooldown;

        _dashCooldownLeft = Mathf.Max(_dashCooldownFloor, reduced);
        _dashCooldownDuration = _dashCooldownLeft;

        Vfx.Shockwave(transform.position, 44f, 0.16f, new Color(0.6f, 0.9f, 1f));
    }

    /// <summary>Couloir de dégâts de la charge : chaque ennemi une seule fois par ruade.</summary>
    private void ApplyChargeDamage()
    {
        Vector2 center = transform.position;

        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null || enemy.IsDead || _chargeHit.Contains(enemy)) continue;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude > _chargeWidth * _chargeWidth) continue;

            enemy.TakeDamage(_chargeDamage);

            Vector2 push = (Vector2)enemy.transform.position - center;
            push = push.sqrMagnitude > 0.01f ? push.normalized : _dashVelocity.normalized;
            enemy.transform.position = (Vector2)enemy.transform.position + push * _chargeKnockback;

            _chargeHit.Add(enemy);
        }
    }

    /// <summary>
    /// Repousse les ennemis qui chevauchent le corps, sans les bloquer — port fidèle de
    /// <c>PushEnemiesAside</c>. La séparation reste <b>sous</b> le rayon de contact de l'ennemi,
    /// pour que les dégâts continuent de s'appliquer : c'est ce qui donne la sensation de
    /// « labourer la foule » plutôt que de la pousser devant soi.
    /// </summary>
    private void PushEnemiesAside()
    {
        Vector2 me = transform.position;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null) continue;

            float sep = Mathf.Max(PlayerBodyRadius, enemy.PushRadius - 6f);
            Vector2 offset = (Vector2)enemy.transform.position - me;
            float dist = offset.magnitude;
            if (dist >= sep) continue;

            Vector2 dir = dist > 0.01f
                ? offset / dist
                : (Velocity.sqrMagnitude > 1f ? Velocity.normalized : Vector2.right);

            enemy.transform.position = me + dir * sep;
        }
    }

    // ─── Régénération ─────────────────────────────────────────────────────────

    private void UpdateRegen(float dt)
    {
        if (Stats.RegenSuppressLeft > 0f)
        {
            // Suspension sous le feu : on coupe la SOURCE, la réserve déjà constituée continue
            // d'absorber (règle du cran de saturation, GDD §33.7).
            Stats.RegenSuppressLeft -= dt;
            return;
        }

        if (Stats.HpRegenPerSecond <= 0f) return;

        float tick = Stats.HpRegenPerSecond * dt;
        float missing = Stats.MaxHp - Stats.CurrentHp;
        float applied = Mathf.Min(tick, missing);

        // Canal RÉGÉNÉRATION, jamais le soin ponctuel : les deux se règlent avec des leviers
        // différents, et les confondre dans le journal a déjà produit un faux diagnostic.
        if (applied > 0f) HealInternal(applied, fromRegen: true);

        // Le surplus qui serait perdu à PV pleins alimente la réserve anti-pic.
        float surplus = tick - applied;
        if (surplus > 0f)
        {
            float cap = RegenReserve.Capacity(Stats.HpRegenPerSecond, Stats.MaxHp);
            Stats.RegenReserveCharge = Mathf.Min(Stats.RegenReserveCharge + surplus, cap);
        }
    }

    // ─── Dégâts et soins ──────────────────────────────────────────────────────

    /// <summary>
    /// Encaisse un coup. Sans effet pendant les i-frames — c'est ce qui rend une nuée survivable.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (_dead || _invulnTimer > 0f || amount <= 0f) return;

        // --invuln : observer un combat long jusqu'au bout (les trois phases du boss) sans mourir
        // avant. Sorti AVANT les i-frames et la télémétrie — sous ce drapeau, la colonne des dégâts
        // subis doit rester vide plutôt que fausse.
        if (DebugHooks.Invulnerable) return;

        _invulnTimer = InvulnWindow;

        // La réduction est bornée par StatCaps : une seule source de vérité avec Godot.
        float dr = Mathf.Min(Stats.DamageReduction, StatCaps.MaxDamageReduction);
        float net = amount * (1f - dr);

        // Journalisé ICI : le coup qui passe réellement, i-frames et égide déjà écartées, mais AVANT
        // que la réserve n'en absorbe une part. C'est la pression exercée par le contenu, distincte de
        // ce que la défense du joueur en retient — les deux se lisent en face l'une de l'autre.
        PowerTelemetry.NotifyDamageTaken(net);

        // La réserve de régénération absorbe en premier, après les i-frames.
        if (Stats.RegenReserveCharge > 0f)
        {
            float absorbed = Mathf.Min(Stats.RegenReserveCharge, net);
            Stats.RegenReserveCharge -= absorbed;
            net -= absorbed;

            // Les PV épargnés par la réserve sont de la régénération ENFIN rendue : elle se compte
            // ici, et non à la mise en réserve. Compter les deux la doublerait dans le journal.
            PowerTelemetry.NotifyRegen(absorbed);
        }

        // Tout coup encaissé suspend la régénération, même entièrement absorbé.
        Stats.RegenSuppressLeft = RegenReserve.SuppressionSeconds;

        // Épines : la greffe rend une fraction du coup à ce qui l'a porté. Calculée sur le montant
        // ENTRANT et non sur le net — c'est le coup reçu qui est renvoyé, pas ce qu'il en reste après
        // les protections du porteur.
        ReflectThorns(amount);

        if (net <= 0f) { HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp); return; }

        Stats.CurrentHp = Mathf.Max(0f, Stats.CurrentHp - net);
        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
        AudioSystem.PlaySfx("sfx_player_hit");

        if (Stats.CurrentHp <= 0f)
        {
            _dead = true;
            AudioSystem.PlaySfx("sfx_player_die");
            Died?.Invoke();
        }
    }

    private GraftManager? _grafts;

    /// <summary>
    /// Renvoie une part du coup encaissé aux ennemis au contact (greffe « Carapace Greffée »).
    /// Sans greffe d'épines, ne fait rien et ne coûte rien.
    /// </summary>
    private void ReflectThorns(float incoming)
    {
        _grafts ??= GetComponent<GraftManager>();
        if (_grafts == null || !_grafts.HasThorns) return;

        float reflected = _grafts.ThornsDamageFor(incoming);
        if (reflected <= 0f) return;

        Vector2 me = transform.position;
        const float reach = 48f;

        // Copie de sécurité : le renvoi peut tuer, donc modifier la liste pendant qu'on la parcourt.
        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null || enemy.IsDead) continue;
            if (((Vector2)enemy.transform.position - me).sqrMagnitude > reach * reach) continue;

            enemy.TakeDamage(reflected);
        }
    }

    /// <summary>
    /// Soigne d'un montant absolu. <b>Chemin unique</b> pour tout soin : le projet a déjà connu un
    /// bug majeur parce que des soins écrivaient <c>CurrentHp</c> en direct, échappant ainsi aux
    /// crans de saturation et à l'instrumentation. Rien ne doit contourner cette méthode.
    /// </summary>
    public void HealFlat(float amount) => HealInternal(amount, fromRegen: false);

    /// <summary>
    /// Applique un soin et le journalise dans le bon canal.
    /// </summary>
    /// <param name="fromRegen">
    /// Vrai pour la régénération continue, faux pour un soin ponctuel (orbe, vol de vie, carte).
    /// </param>
    /// <remarks>
    /// ⚠ <b>Les deux canaux ne se pilotent pas avec les mêmes leviers et ne doivent jamais être
    /// confondus dans le journal.</b> Le relevé qui les sépare a montré que le canal dominant était
    /// le soin ponctuel — <b>×9,5</b> la régénération — donc que régler la régénération ne pouvait
    /// rien changer, quelle que soit sa valeur.
    ///
    /// <para>Et pour le soin ponctuel, deux montants sont journalisés : ce qui est <b>rendu</b> et ce
    /// qui est <b>offert</b>. À PV pleins un soin vaut zéro ; ne mesurer que le rendu fait passer « le
    /// joueur reçoit plus de soins » et « le joueur a plus de PV à remplir » pour la même chose.</para>
    /// </remarks>
    private void HealInternal(float amount, bool fromRegen)
    {
        if (_dead || amount <= 0f) return;

        float before = Stats.CurrentHp;
        Stats.CurrentHp = Mathf.Min(Stats.MaxHp, Stats.CurrentHp + amount);

        float applied = Stats.CurrentHp - before;

        if (fromRegen) PowerTelemetry.NotifyRegen(applied);
        else PowerTelemetry.NotifyHealed(applied, amount);

        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
    }

    /// <summary>Soigne d'une fraction des PV max.</summary>
    public void Heal(float amount) => HealFlat(amount);

    /// <summary>Le joueur est-il invulnérable en ce moment ?</summary>
    public bool IsInvulnerable => _invulnTimer > 0f;

    /// <summary>Le joueur est-il mort ?</summary>
    public bool IsDead => _dead;
}

/// <summary>Dimensions de l'arène — reprises de <c>Constants</c> côté Godot.</summary>
public static class Arena
{
    public const int Width  = 1920;
    public const int Height = 1216;

    public const float HalfWidth  = Width / 2f;
    public const float HalfHeight = Height / 2f;
}
