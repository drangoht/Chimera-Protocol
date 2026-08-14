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

    // ─── Filets de survie achetés au Hub (cran IV « Sans filet ») ─────────────

    private int _extraLivesLeft;
    private int _absorbChargesLeft;

    /// <summary>Noyaux de Secours restants — une charge annule une mort.</summary>
    public int ExtraLivesLeft => _extraLivesLeft;

    /// <summary>Plaques Adaptatives restantes — une charge annule un coup entier.</summary>
    public int AbsorbChargesLeft => _absorbChargesLeft;

    /// <summary>Noyaux de Secours au départ de la run. Figé : le HUD dessine aussi les charges dépensées.</summary>
    public int ExtraLivesMax { get; private set; }

    /// <summary>Plaques Adaptatives au départ de la run. Figé, même raison.</summary>
    public int AbsorbChargesMax { get; private set; }

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

    /// <summary>
    /// Branche le soin de passage de niveau. <b>En <c>Start</c> et non en <c>Awake</c></b> : l'ordre
    /// entre deux <c>Awake</c> n'est pas garanti par Unity, et <see cref="XpSystem.Instance"/> serait
    /// tantôt là, tantôt nul — donc le filet fonctionnerait une frame sur deux.
    /// </summary>
    private void Start()
    {
        if (XpSystem.Instance != null) XpSystem.Instance.LevelUp += OnLevelUp;
        else Debug.LogError("[Player] pas de XpSystem : le soin de passage de niveau ne se declenchera jamais.");
    }

    private void OnDestroy()
    {
        if (XpSystem.Instance != null) XpSystem.Instance.LevelUp -= OnLevelUp;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Passage de niveau : secousse, et surtout le <b>soin</b> qui va avec.
    /// </summary>
    /// <remarks>
    /// <para>Ce soin est le filet <b>universel</b> du jeu — gratuit, automatique, indexé sur les PV
    /// max (donc sans plafond, les cartes de surcharge faisant monter ces PV sans fin) et déclenché
    /// en rafale en overtime. C'est la plus grosse source de soin du jeu, très loin devant les orbes
    /// et le vol de vie.</para>
    ///
    /// <para>⚠ <b>Il manquait entièrement au portage jusqu'au 2026-08-09</b> : rien, côté Unity,
    /// n'écoutait <see cref="XpSystem.LevelUp"/> sur le joueur. Le jeu ne plantait pas et ne signalait
    /// rien — il était simplement plus dur que le jeu d'origine, d'un facteur que personne ne pouvait
    /// deviner en lisant le code, puisque la règle qui le gouverne
    /// (<see cref="SaturationTable.LevelUpHealsEnabled"/>) existait bel et bien.</para>
    ///
    /// <para>Le cran I « Hémorragie » le coupe. La secousse, elle, reste à tous les crans : le niveau
    /// doit continuer de se voir — seul le rattrapage disparaît.</para>
    /// </remarks>
    private void OnLevelUp(int newLevel)
    {
        ScreenShake.Shake(6f, 0.20f);

        if (RunConfig.LevelUpHealsEnabled) HealFraction(LevelUpHealFraction);
    }

    /// <summary>Part des PV max rendue à chaque passage de niveau. Constante de gameplay.</summary>
    public const float LevelUpHealFraction = 0.25f;

    /// <summary>Part des PV max rendue par un Noyau de Secours quand il annule une mort.</summary>
    public const float ExtraLifeHpFraction = 0.30f;

    /// <summary>
    /// Recharge les deux consommables méta pour la run à venir. <b>Appelé par
    /// <see cref="RunBootstrap"/></b>, jamais depuis un <c>Start</c>.
    /// </summary>
    /// <remarks>
    /// <para>Même raison que pour les charges de Renouveler / Passer : les niveaux d'amélioration
    /// peuvent être imposés en ligne de commande par <c>RunBootstrap</c>, et l'ordre entre deux
    /// <c>Start</c> n'est pas garanti. Lues trop tôt, les charges vaudraient tantôt celles de la
    /// sauvegarde, tantôt celles du drapeau, <b>selon la frame</b>.</para>
    ///
    /// <para>Les maxima sont figés ici pour que le HUD puisse dessiner les charges <b>dépensées</b> en
    /// plus des restantes : une pastille qui s'éteint se lit, une pastille qui disparaît ne se lit
    /// pas. C'est le correctif d'un retour joué — « on ne distingue pas quand une vie est utilisée ».</para>
    /// </remarks>
    public void InitSafetyNets()
    {
        // Cran IV « Sans filet » : ces deux achats profitent à TOUTES les runs suivantes, si bien
        // qu'une partie ne commence jamais vraiment à zéro. Le cran les met à zéro.
        bool enabled = RunConfig.SafetyNetsEnabled;

        _extraLivesLeft    = enabled ? MetaProgression.LevelOf("extra_life")    : 0;
        _absorbChargesLeft = enabled ? MetaProgression.LevelOf("damage_absorb") : 0;

        ExtraLivesMax    = _extraLivesLeft;
        AbsorbChargesMax = _absorbChargesLeft;

        Debug.Log($"[Player] filets de survie : {_extraLivesLeft} Noyau(x) de Secours, " +
                  $"{_absorbChargesLeft} Plaque(s) Adaptative(s)" +
                  (enabled ? "." : " — coupes par le cran IV « Sans filet »."));
    }

    private void Update()
    {
        if (_dead) return;

        float dt = Time.deltaTime;
        if (_invulnTimer > 0f) _invulnTimer -= dt;

        UpdateChill(dt);
        UpdateMovement(dt);
        UpdateRegen(dt);
        PushEnemiesAside();
    }

    // ─── Ralentissement environnemental (gel du Givre) ────────────────────────

    private float _chillMult = 1f;
    private float _chillTime;

    /// <summary>Ralentissement de gel en cours (1 = aucun) — lu par le déplacement.</summary>
    public float ChillMultiplier => _chillMult;

    /// <summary>
    /// Vitesse de déplacement <b>réellement appliquée</b> à l'instant, en pixels par seconde :
    /// statistique, Célérité, plafond et gel compris.
    /// </summary>
    /// <remarks>
    /// La vitesse est plafonnée par <see cref="StatCaps"/> — la même source que côté Godot. Les deux
    /// sources se <b>multiplient</b> : un gel ne doit ni effacer une Célérité, ni être effacé par
    /// elle. Le plafond ne borne que le haut — un ralentissement passe dessous.
    ///
    /// <para>⚠ Exposée parce que d'autres systèmes doivent <b>courir après le joueur</b> et n'ont
    /// aucun moyen de deviner cette composition. L'aimantation des orbes lisait <c>Stats.Speed</c>,
    /// qui ignore et la Célérité et le plafond : une seule copie de cette formule suffit, et c'est
    /// celle-ci. La ruade n'y figure pas — elle impose sa vitesse, ne dure qu'un instant, et un orbe
    /// n'a pas à suivre une esquive.</para>
    /// </remarks>
    public float CurrentSpeed =>
        Mathf.Min(Stats.Speed * SpeedMultiplier, StatCaps.MaxSpeed) * _chillMult;

    /// <summary>
    /// Ralentit le joueur pendant <paramref name="duration"/> secondes (nova et cône du Givre).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Canal séparé de <see cref="SpeedMultiplier"/></b>, et ce n'est pas un détail : le
    /// portage écrivait le gel directement dans le multiplicateur de vitesse, <b>sans durée</b>. Un
    /// joueur touché par une Sentinelle Cryo restait donc à moitié vitesse <b>jusqu'à la fin de la
    /// run</b> — signalé en jouant le 2026-08-09, « en tuant un mid-boss le joueur perd beaucoup de
    /// vitesse et reste à vitesse faible ». Tuer le champion n'y changeait rien : le ralentissement
    /// ne lui appartenait plus.</para>
    ///
    /// <para>Et les deux canaux ne peuvent pas être confondus : le power-up Célérité écrit lui aussi
    /// <c>SpeedMultiplier</c>, si bien qu'un gel effaçait la Célérité — et qu'une Célérité qui
    /// s'achève aurait effacé le gel. Deux sources qui s'écrasent au lieu de se multiplier.</para>
    ///
    /// <para>Deux gels qui se chevauchent ne s'additionnent pas : on garde le plus <b>fort</b> et on
    /// rafraîchit la durée. Rester dans une nappe de plaques de givre ne doit pas clouer sur place —
    /// d'où le plancher à 0,35.</para>
    /// </remarks>
    public void ApplyChill(float mult, float duration)
    {
        mult = Mathf.Clamp(mult, 0.35f, 1f);

        if (mult < _chillMult || _chillTime <= 0f) _chillMult = mult;
        _chillTime = Mathf.Max(_chillTime, duration);
    }

    private void UpdateChill(float dt)
    {
        if (_chillTime <= 0f) return;

        _chillTime -= dt;
        if (_chillTime <= 0f) { _chillTime = 0f; _chillMult = 1f; }
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

        Velocity = input * CurrentSpeed;

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

        // Doigts : il n'y a ni curseur ni stick droit à lire. La visée se prend alors sur l'ennemi le
        // plus proche — voir AutoAim().
        if (TouchInput.Active)
        {
            AimAutomatically();
            UpdateAimIndicator();
            return;
        }

        // ⚠ Ces deux lectures passaient par des axes de l'Input Manager — dont "RightStickX" et
        // "RightStickY" qui n'ont JAMAIS été déclarés dans InputManager.asset. Unity lève alors une
        // ArgumentException à chaque frame, ce qui interrompait cette méthode ici même : la branche
        // souris ci-dessous n'était donc jamais atteinte et le réticule jamais posé. Le stick se lit
        // maintenant sur le périphérique directement, sans table d'axes à tenir à jour.
        //
        // Le Y n'est plus inversé : les axes joystick de l'ancienne API pointaient vers le bas,
        // rightStick pointe vers le haut, comme le monde.
        Vector2 stick = RawInput.RightStick();
        if (stick.magnitude < AimStickDeadzone) stick = Vector2.zero;

        Vector2? pointer = RawInput.PointerPosition();
        if (pointer is { } mouse)
        {
            if (((Vector3)mouse - _lastMousePosition).sqrMagnitude > 1f)
            {
                _gamepadAim = false;
                _lastMousePosition = mouse;
            }
        }

        if (stick != Vector2.zero) _gamepadAim = true;

        if (_gamepadAim)
        {
            if (stick != Vector2.zero) AimDirection = stick.normalized;   // sinon : garde la dernière
        }
        else if (pointer is { } cursor)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                Vector2 world = camera.ScreenToWorldPoint(cursor);
                Vector2 toMouse = world - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 1f) AimDirection = toMouse.normalized;
            }
        }

        UpdateAimIndicator();
    }

    /// <summary>Portée dans laquelle la visée automatique cherche une cible, en pixels.</summary>
    /// <remarks>
    /// Un peu plus que la demi-diagonale de l'écran de référence : la visée ne doit désigner que ce
    /// que le joueur <b>voit</b>. Viser plus loin ferait pointer le réticule vers un vide apparent et
    /// donnerait l'impression d'une arme cassée — exactement le symptôme que la visée dirigée existe
    /// pour éviter.
    /// </remarks>
    private const float AutoAimRange = 1100f;

    /// <summary>
    /// Visée sans dispositif de pointage : la direction de l'ennemi le plus proche.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Ceci est le comportement que <c>VectorLance</c> s'interdit</b>, et pour de bonnes
    /// raisons : viser automatiquement transforme la seule arme d'adresse du jeu en canon
    /// automatique. La différence tient entièrement à ce qui déclenche cette branche — <b>l'absence
    /// de tout moyen de pointer</b>. Sur un téléphone, il n'y a ni curseur ni stick droit ; le choix
    /// n'est pas entre viser à la main et viser tout seul, il est entre viser tout seul et une arme
    /// qui tire dans une direction que le joueur ne contrôle pas du tout. La règle du fichier reste
    /// donc intacte : tant qu'un moyen de pointer existe, la Lance ne cible jamais toute seule.</para>
    ///
    /// <para>Sans cible en vue, on <b>garde la dernière direction</b> plutôt que de repartir vers la
    /// droite : un réticule qui se recale brutalement à chaque accalmie attire l'œil pour rien, au
    /// milieu d'un écran déjà chargé.</para>
    /// </remarks>
    private void AimAutomatically()
    {
        var target = EnemyBase.Nearest(transform.position, AutoAimRange);
        if (target == null) return;

        Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
        if (toTarget.sqrMagnitude > 1f) AimDirection = toTarget.normalized;
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
        // Suspension sous le feu : on coupe la SOURCE, la réserve déjà constituée continue
        // d'absorber (règle du cran de saturation, GDD §33.7).
        //
        // ⚠ Le décompte et le test appartiennent à `RegenReserve`, ils ne se réécrivent pas ici.
        // Le test posé en dur était `> 0f` là où la règle impose un epsilon, et le HUD, lui,
        // appelait déjà `IsSuppressed` : deux définitions du même état, donc une fenêtre où l'icône
        // annonce une régénération qui ne coule pas. Le décompte, non borné, laissait de surcroît
        // filer une durée négative — que ce même HUD affiche.
        if (RegenReserve.IsSuppressed(Stats.RegenSuppressLeft))
        {
            Stats.RegenSuppressLeft = RegenReserve.TickSuppression(Stats.RegenSuppressLeft, dt);
            return;
        }

        Stats.RegenSuppressLeft = 0f;

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

        // Plaque Adaptative (achat méta `damage_absorb`) : les premiers coups de la run sont
        // totalement absorbés. Placée APRÈS les i-frames — donc au plus une charge par fenêtre de
        // 0,45 s — et non avant comme sous Godot : au contact d'une nuée, TakeDamage est appelé à
        // chaque frame par chaque ennemi, si bien que trois charges partaient en trois frames sans
        // que le joueur ait encaissé trois coups. Divergence assumée, elle sert l'intention écrite
        // sur la carte (« les premiers COUPS reçus »).
        if (_absorbChargesLeft > 0)
        {
            _absorbChargesLeft--;

            // Le retour visuel est allongé à dessein : à 0,1 s il se confondait avec le clignotement
            // d'i-frames. Pas de bannière ici, contrairement au Noyau de Secours — trois charges par
            // run, une interruption à chaque fois deviendrait du bruit ; les pastilles du HUD portent
            // l'information.
            Vfx.Shockwave(transform.position, 52f, 0.25f, new Color(0.45f, 0.72f, 1f));
            HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
            return;
        }

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
        Stats.RegenSuppressLeft = RegenReserve.Suppress();

        // Épines : la greffe rend une fraction du coup à ce qui l'a porté. Calculée sur le montant
        // ENTRANT et non sur le net — c'est le coup reçu qui est renvoyé, pas ce qu'il en reste après
        // les protections du porteur.
        ReflectThorns(amount);

        if (net <= 0f) { HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp); return; }

        Stats.CurrentHp = Mathf.Max(0f, Stats.CurrentHp - net);
        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
        AudioSystem.PlaySfx("sfx_player_hit");

        if (Stats.CurrentHp <= 0f) HandleDeath();
    }

    /// <summary>
    /// Le joueur vient de tomber à zéro. Un <b>Noyau de Secours</b> (achat méta <c>extra_life</c>)
    /// peut encore annuler cette mort.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Cet événement est <b>le plus lourd de conséquence de toute la run</b>, et il ne se
    /// signalait sous Godot que par un flash de 0,3 s et le son de ramassage d'un Noyau d'Aether —
    /// celui qu'on entend des dizaines de fois par partie. Retour joué : « on ne distingue pas très
    /// bien quand une vie est utilisée ». Il s'annonce donc comme une <i>mort évitée</i> et non comme
    /// un ramassage : bannière, secousse, et un son qui n'existe nulle part ailleurs en run.</para>
    /// </remarks>
    private void HandleDeath()
    {
        if (_dead) return;

        if (_extraLivesLeft > 0)
        {
            _extraLivesLeft--;
            Stats.CurrentHp = Stats.MaxHp * ExtraLifeHpFraction;
            _invulnTimer = InvulnWindow;
            HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);

            AudioSystem.PlaySfx("sfx_ui_death");
            ScreenShake.Shake(16f, 0.45f);
            Vfx.Shockwave(transform.position, 140f, 0.5f, new Color(0.55f, 1f, 0.65f));
            HUD.Instance?.Announce(string.Format(Loc.T("BANNER_EXTRA_LIFE"), _extraLivesLeft), 3f);

            Debug.Log($"[Player] Noyau de Secours consomme. Charges restantes : {_extraLivesLeft}.");
            return;
        }

        _dead = true;
        AudioSystem.PlaySfx("sfx_player_die");
        Died?.Invoke();
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

        // Cran I « Hémorragie » : les soins PONCTUELS seulement. La régénération continue est un
        // autre canal, avec ses propres leviers (réserve, suspension sous le feu) — les confondre
        // ici appliquerait deux fois la même coupe à un joueur qui n'a pris qu'une carte.
        if (!fromRegen) amount *= RunConfig.HealingMult;
        if (amount <= 0f) return;

        float before = Stats.CurrentHp;
        Stats.CurrentHp = Mathf.Min(Stats.MaxHp, Stats.CurrentHp + amount);

        float applied = Stats.CurrentHp - before;

        if (fromRegen) PowerTelemetry.NotifyRegen(applied);
        else PowerTelemetry.NotifyHealed(applied, amount);

        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
    }

    /// <summary>Soigne d'une <b>fraction</b> des PV max (0,25 = un quart de la barre).</summary>
    /// <remarks>
    /// ⚠ Le nom est explicite parce que le portage a failli reproduire un piège classique : une
    /// méthode <c>Heal</c> qui prend un pourcentage sous Godot et un montant absolu ici. Un
    /// <c>Heal(0,25f)</c> recopié tel quel aurait rendu <b>un quart de point de vie</b> au lieu d'un
    /// quart de la barre — sans erreur, sans avertissement, et invisible à la lecture.
    /// </remarks>
    public void HealFraction(float fraction)
    {
        if (fraction <= 0f) return;
        HealFlat(Stats.MaxHp * fraction);
    }

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
