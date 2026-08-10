using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ennemi de base — port de <c>EnemyBase</c> (Lot 2, docs/UNITY_MIGRATION_PLAN.md).
///
/// <para><b>Deux propriétés du jeu d'origine gouvernent tout ce fichier</b>, établies en lisant le
/// code Godot (§4.4) :</para>
/// <list type="number">
///   <item>Les <b>dégâts de contact se calculent par distance</b>, jamais par collision physique —
///         d'où l'absence totale de <c>Collider2D</c> ici ;</item>
///   <item>les ennemis <b>ne collisionnent ni entre eux ni avec le joueur</b> (<c>mask = 2</c>,
///         obstacles statiques uniquement). Il n'y a donc aucune physique à n corps à reproduire,
///         ce qui rend 300 entités bon marché (0,168 ms/pas mesuré au Lot 1).</item>
/// </list>
///
/// <para>La liste <see cref="Active"/> remplace le groupe <c>enemies</c> de l'arbre Godot
/// (<c>GetNodesInGroup</c>) : même service, sans parcours d'arbre à chaque frame.</para>
/// </summary>
public class EnemyBase : MonoBehaviour
{
    /// <summary>
    /// Tous les ennemis vivants — remplace le groupe <c>enemies</c>. Maintenue à l'activation et à
    /// la destruction, jamais reconstruite : elle est parcourue à chaque frame par le joueur et par
    /// les armes.
    /// </summary>
    public static readonly List<EnemyBase> Active = new();

    [Header("Statistiques")]
    public float MaxHp = 20f;
    public float Speed = 120f;
    public float Damage = 5f;
    public int   XpValue = 1;

    /// <summary>
    /// Identité de l'entrée de bestiaire qui a produit cet ennemi. Posée par le spawner ; c'est elle
    /// qui permet de compter les exemplaires vivants d'un champion.
    /// </summary>
    public string DefId { get; set; } = "";

    /// <summary>Exemplaires vivants portant cet id — plafond simultané des champions.</summary>
    public static int CountOf(string defId)
    {
        int n = 0;
        foreach (var e in Active)
            if (e != null && !e.IsDead && e.DefId == defId) n++;
        return n;
    }

    /// <summary>Rayon des dégâts de contact. Surchargeable — les champions frappent plus large.</summary>
    protected virtual float ContactRadius => 24f;

    /// <summary>Rayon utilisé par le joueur pour repousser l'ennemi hors de son corps.</summary>
    public float PushRadius => ContactRadius;

    /// <summary>Émis à la mort, avec la valeur d'XP à créditer.</summary>
    public event Action<int>? Died;

    private float _currentHp;
    private bool  _isDead;
    private FrameAnimator? _animator;

    /// <summary>PV courants — lus par l'UI et les armes.</summary>
    public float CurrentHp => _currentHp;

    /// <summary>L'ennemi est-il déjà mort ? Une arme ne doit pas le frapper deux fois.</summary>
    public bool IsDead => _isDead;

    protected virtual void Awake()
    {
        _currentHp = MaxHp;
        _animator = GetComponentInChildren<FrameAnimator>();
    }

    protected virtual void OnEnable() => Active.Add(this);

    protected virtual void OnDisable() => Active.Remove(this);

    protected virtual void Update()
    {
        if (_isDead) return;

        float dt = Time.deltaTime;
        UpdateStatusEffects(dt);
        if (_isDead) return;          // la brûlure a pu tuer entre-temps

        var player = Player.Instance;
        UpdateEliteEffects(dt, player);

        if (player == null || player.IsDead) return;

        UpdateMovement(player, dt);
        HandleContactDamage(player, dt);
    }

    // ─── Effets de statut ─────────────────────────────────────────────────────

    private float _slowMult = 1f;
    private float _slowLeft;
    private float _burnDps;
    private float _burnLeft;

    /// <summary>Multiplicateur de vitesse courant (1 = intact). Lu par les déplacements.</summary>
    public float SlowMultiplier => _slowLeft > 0f ? _slowMult : 1f;

    /// <summary>L'ennemi brûle-t-il ?</summary>
    public bool IsBurning => _burnLeft > 0f;

    /// <summary>L'ennemi est-il ralenti à l'instant ?</summary>
    public bool IsSlowed => _slowLeft > 0f;

    /// <summary>
    /// Combien d'ennemis vivants portent chaque état, à l'instant.
    /// </summary>
    /// <remarks>
    /// ⚠ Ces deux compteurs existent parce que <b>vérifier un état en regardant l'écran ne marche
    /// pas</b> : dans la première minute, la faune meurt avant que sa brûlure n'ait le temps de se
    /// voir, et le couloir de la Lance Cryo est trop étroit pour qu'une capture le surprenne. Sans
    /// relevé, on ne peut pas distinguer « l'effet ne s'affiche pas » de « l'effet ne se produit
    /// jamais » — deux causes opposées qui se ressemblent parfaitement.
    /// </remarks>
    /// <summary>Ralentissements et brûlures appliqués depuis le début, cumulés.</summary>
    /// <remarks>
    /// ⚠ Ce sont ces cumuls qui tranchent, pas les comptes instantanés : dans la première minute
    /// une cible touchée meurt du même coup, si bien qu'un relevé ponctuel affiche <b>zéro</b> même
    /// quand l'effet part à chaque tir. La leçon est la même que pour la pression ressentie sous
    /// Godot — un événement rare et bref ne se mesure jamais par échantillonnage.
    /// </remarks>
    public static int SlowsApplied { get; private set; }
    public static int BurnsApplied { get; private set; }

    /// <summary>Remet les cumuls à zéro — début de run, ou banc.</summary>
    public static void ResetStatusCounters() { SlowsApplied = 0; BurnsApplied = 0; }

    public static (int Slowed, int Burning) StatusCounts()
    {
        int slowed = 0, burning = 0;
        foreach (var e in Active)
        {
            if (e == null || e.IsDead) continue;
            if (e.IsSlowed) slowed++;
            if (e.IsBurning) burning++;
        }
        return (slowed, burning);
    }

    /// <summary>
    /// Ralentit l'ennemi. Un ralentissement plus fort <b>remplace</b> un plus faible ; à force
    /// égale, seule la durée est prolongée — sinon deux sources de gel empileraient leurs
    /// multiplicateurs jusqu'à l'immobilité totale.
    /// </summary>
    public void ApplySlow(float mult, float duration)
    {
        // ⚠ Le plafond vient de CrowdControlCaps, pas d'un clamp local. Le portage bornait à 0,05
        // — soit un ralentissement de 95 % là où le jeu en autorise 40 — ce qui transforme une arme
        // de givre en bouton « pause » sur toute la nuée. Ces garde-fous existent précisément pour
        // que slow et DoT ne trivialisent jamais le jeu ; les recopier de travers les annule.
        mult = CrowdControlCaps.CapSlowMult(mult);

        if (_slowLeft <= 0f || mult < _slowMult) _slowMult = mult;
        _slowLeft = Mathf.Max(_slowLeft, duration);
        SlowsApplied++;
    }

    /// <summary>
    /// Applique une brûlure. Les dégâts par seconde sont <b>continus</b> : ils ne passent donc
    /// jamais par le chemin des coups discrets, et surtout jamais par un plancher exprimé en
    /// pourcentage des PV max — appliqué à chaque frame, il tuerait en quelques images.
    /// </summary>
    public void ApplyBurn(float dps, float duration)
    {
        // La source la plus forte l'emporte, mais elle reste bornée : sans ce plafond, empiler des
        // sources de brûlure remplace le combat par une attente.
        _burnDps = Mathf.Max(_burnDps, CrowdControlCaps.CapBurnDps(dps));
        _burnLeft = Mathf.Max(_burnLeft, duration);
        BurnsApplied++;
    }

    private void UpdateStatusEffects(float dt)
    {
        if (_slowLeft > 0f)
        {
            _slowLeft -= dt;
            if (_slowLeft <= 0f) _slowMult = 1f;
        }

        if (_burnLeft > 0f)
        {
            _burnLeft -= dt;
            TakeDamage(_burnDps * dt);
            if (_burnLeft <= 0f) _burnDps = 0f;
        }

        RenderStatus(dt);
    }

    /// <summary>
    /// Confie l'<b>apparence</b> des états à <see cref="EnemyStatusFx"/>, créé au premier état subi.
    /// </summary>
    /// <remarks>
    /// ⚠ Rien n'est posé sur un ennemi qui n'a jamais été ni gelé ni brûlé : la faune de base atteint
    /// 300 entités, et un composant de plus sur chacune se paierait à chaque image pour des états que
    /// la plupart ne connaîtront jamais.
    /// </remarks>
    private void RenderStatus(float dt)
    {
        // ⚠ La FORCE du ralentissement, pas un booléen : c'est elle qui décide de l'intensité du
        // givre et de la cadence d'animation de la victime. Réduite à « gelé ou non », la Lance
        // Cryogénique (−20 %) et le Voile de Givre (−45 %) donnaient la même image.
        float slow = SlowMultiplier;
        bool frozen = slow < 1f;
        bool burning = _burnLeft > 0f;

        if (_statusFx == null)
        {
            if (!frozen && !burning) return;
            _statusFx = gameObject.AddComponent<EnemyStatusFx>();
        }

        // Une fois posé, il continue d'être appelé même sans état : le givre FOND au lieu de
        // s'éteindre, et la cadence d'animation doit être rendue à sa victime.
        _statusFx.Render(slow, burning, dt);
    }

    private EnemyStatusFx? _statusFx;


    /// <summary>Comportement de déplacement, issu des données (<c>ai.type</c>).</summary>
    public EnemyTable.AiType Ai { get; set; } = EnemyTable.AiType.StraightChase;

    /// <summary>
    /// Phase de déplacement propre à cette entité. Sans état <b>par ennemi</b>, une nuée entière
    /// zigzaguerait à l'unisson — un défaut immédiatement visible à l'écran.
    /// </summary>
    private float _aiPhase;

    /// <summary>Déplacement délégué à <see cref="EnemyAi"/> selon le comportement des données.</summary>
    protected virtual void UpdateMovement(Player player, float dt)
    {
        Vector2 self = transform.position;
        Vector2 target = player.transform.position;

        Vector2 next = EnemyAi.Step(Ai, self, target, Speed * SlowMultiplier, dt, ref _aiPhase);

        // La faune contourne les mêmes masses que le joueur : sous Godot, un seul calque de collision
        // bloquait les deux. Des ennemis qui traversent un pilier retireraient tout intérêt à s'y
        // abriter.
        next = ArenaObstacles.Resolve(next, ContactRadius * 0.5f);
        transform.position = next;

        if (_animator != null)
        {
            Vector2 moved = next - self;
            if (Mathf.Abs(moved.x) > 0.001f) _animator.FlipX = moved.x < 0f;
            _animator.Play(moved.sqrMagnitude > 0.0001f ? "move" : "idle");
        }
    }

    /// <summary>Secondes entre deux tirs d'un archétype à distance. Valeur du jeu d'origine.</summary>
    private const float RangedShootCooldown = 2.5f;

    /// <summary>Portée au-delà de laquelle un tireur ne gaspille pas ses projectiles.</summary>
    private const float RangedMaxRange = 520f;

    private float _shootTimer = RangedShootCooldown;

    /// <summary>
    /// Tir des archétypes à distance (<c>ranged_kiter</c>, <c>cone_kiter</c>).
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Aucun ennemi ne tirait dans le portage.</b> Ces deux archétypes gardaient
    /// scrupuleusement leur distance — approche, recul, orbite — et ne faisaient jamais feu. Le
    /// défaut est parfaitement silencieux : un ennemi qui kite sans tirer ressemble à un ennemi
    /// prudent, pas à un ennemi cassé, et il rendait surtout la moitié de sa raison d'être au
    /// kiting du joueur. La Sentinelle Corrompue est <i>censée</i> punir la ligne droite.</para>
    ///
    /// <para>Sous Godot, ce comportement vit dans une classe dédiée (<c>CorruptedSentinel</c>) ;
    /// ici la faune est <b>pilotée par les données</b>, donc il appartient au socle et se déclenche
    /// sur le type d'IA. Un ennemi de plus dans <c>enemies.json</c> avec <c>ranged_kiter</c> tire
    /// désormais sans une ligne de code — c'est l'intérêt du portage data-driven, à condition que
    /// le socle porte le comportement.</para>
    /// </remarks>
    private void UpdateRangedFire(Player player, float dt)
    {
        // ⚠ `ranged_kiter` SEULEMENT, jamais `cone_kiter`. Le second ne désigne qu'un ennemi dans
        // tout le bestiaire — la Sentinelle Cryo — et c'est un champion dont la signature est un
        // CÔNE dirigé, pas un projectile. Sous Godot la distinction passe par la scène : tous les
        // `ranged_kiter` héritent de CorruptedSentinel et tirent, la Cryo a la sienne.
        if (Ai != EnemyTable.AiType.RangedKiter) return;

        _shootTimer -= dt;
        if (_shootTimer > 0f) return;

        Vector2 self = transform.position;
        Vector2 toPlayer = (Vector2)player.transform.position - self;
        if (toPlayer.sqrMagnitude > RangedMaxRange * RangedMaxRange) return;

        _shootTimer = RangedShootCooldown;

        // Pas d'animation « attack » ici : le déplacement en repose une à chaque image, si bien
        // qu'elle serait écrasée avant d'avoir affiché une seule frame. Une pose qui ne se voit
        // jamais est un appel mort de plus, pas un retour visuel.
        EnemyBullet.Fire(self, toPlayer.normalized, EnemyBullet.SentinelSpeed, Damage,
                         fromChampion: IsChampion, new Color(1f, 0.45f, 0.15f));

        AudioSystem.PlaySfx("sfx_enemy_sentinel_projectile");
    }

    /// <summary>
    /// Dégâts de contact, par <b>distance</b> et non par collision. Le joueur porte des i-frames de
    /// 0,45 s, ce qui borne naturellement les dégâts d'une nuée : appeler ceci à chaque frame pour
    /// 300 ennemis reste correct.
    /// </summary>
    protected virtual void HandleContactDamage(Player player, float dt)
    {
        // Un tireur ne blesse pas AUSSI au contact : sa menace est son projectile, et cumuler les
        // deux ferait d'un archétype qui garde ses distances le plus dangereux au corps-à-corps.
        UpdateRangedFire(player, dt);
        if (Ai == EnemyTable.AiType.RangedKiter) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist < ContactRadius) DealDiscreteDamage(player, Damage);
    }

    /// <summary>
    /// Cet ennemi est-il un <b>champion</b> — mini-boss ou boss de fin ? Seuls eux portent le
    /// plancher de dégâts du cran VI.
    /// </summary>
    public virtual bool IsChampion => false;

    /// <summary>
    /// Chemin <b>unique</b> des coups discrets. Le projet a introduit cette centralisation côté
    /// Godot parce que huit appelants recopiaient la réduction de dégâts, et parce qu'un plancher
    /// exprimé en pourcentage des PV max ne doit <b>jamais</b> toucher un dégât continu — appliqué
    /// à chaque tick, il tuerait en quelques frames.
    /// </summary>
    /// <remarks>
    /// <para><b>Cran VI « Purificateur »</b> : un coup de champion inflige au minimum une fraction
    /// des PV max du joueur. C'est le seul levier de l'échelle qui suive la pente de la défense — le
    /// joueur gagne ~277 PV max par minute d'overtime via la carte Blindage, sans plafond, pendant
    /// que les dégâts ennemis restent des valeurs absolues issues d'une courbe fixe. Il ne donne
    /// aucun PV au boss : celui-ci devient plus <i>dangereux</i>, jamais plus <i>long</i>.</para>
    ///
    /// <para>⚠ Le plancher s'applique <b>avant</b> la réduction de dégâts — appliquée par
    /// <see cref="Player.TakeDamage"/> — donc DR, i-frames et réserve de régénération continuent de
    /// jouer. Un cran retire <b>une</b> certitude, pas trois.</para>
    ///
    /// <para>⚠ Ce plancher était <b>porté et jamais appelé</b> jusqu'au 2026-08-09 : cette méthode
    /// existait, documentait la règle dans son propre commentaire, et ne l'appliquait pas — le cran VI
    /// était donc totalement inopérant dans le portage. Les chemins <b>continus</b> (traînée de magma
    /// du Colosse, exprimée en PV/s × delta) n'appellent pas cette méthode, et c'est ce qui rend le
    /// piège impossible à reproduire par distraction.</para>
    /// </remarks>
    protected void DealDiscreteDamage(Player player, float amount)
    {
        float raw = IsChampion
            ? SaturationTable.ChampionDamage(amount, player.Stats.MaxHp, RunConfig.Saturation)
            : amount;

        player.TakeDamage(raw);
        OnDealtDamage(raw);
    }

    // ─── Affixes d'élite ──────────────────────────────────────────────────────

    private EliteAffix _affix = EliteAffix.None;
    private EliteModifiers _mods;
    private float _regenAccumulator;
    private float _timeSinceHit;

    /// <summary>Affixe porté, <see cref="EliteAffix.None"/> pour un ennemi ordinaire.</summary>
    public EliteAffix Affix => _affix;

    /// <summary>Cet ennemi est-il une élite ?</summary>
    public bool IsElite => _affix != EliteAffix.None;

    /// <summary>
    /// Promeut l'ennemi en élite. Les multiplicateurs viennent d'<see cref="EliteAffixTable"/> —
    /// logique pure partagée avec Godot, donc mêmes chiffres par construction.
    /// </summary>
    /// <remarks>
    /// ⚠ À appeler <b>avant</b> <see cref="ApplyScaling"/> ou juste après, mais jamais deux fois :
    /// les multiplicateurs s'appliqueraient en cascade.
    /// </remarks>
    public void ApplyElite(EliteAffix affix)
    {
        if (affix == EliteAffix.None || IsElite) return;

        _affix = affix;
        _mods = EliteAffixTable.Modifiers(affix);

        MaxHp *= _mods.HpMult;
        _currentHp = MaxHp;
        Speed *= _mods.SpeedMult;
        Damage *= _mods.DamageMult;
        XpValue = Mathf.Max(1, Mathf.RoundToInt(XpValue * _mods.XpMult));

        transform.localScale *= EliteAffixTable.VisualScale;

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(_mods.TintR, _mods.TintG, _mods.TintB);
    }

    /// <summary>Régénération et vol de vie propres aux affixes.</summary>
    private void UpdateEliteEffects(float dt, Player? player)
    {
        if (!IsElite) return;

        _timeSinceHit += dt;

        // Régénérant : ne se soigne que s'il n'a pas été frappé récemment. C'est ce délai qui
        // impose un pic de dégâts plutôt qu'une attrition — sans lui, l'affixe serait un simple
        // sac de PV.
        if (_mods.RegenFractionPerSecond > 0f && _timeSinceHit > 1.5f && _currentHp < MaxHp)
        {
            _regenAccumulator += MaxHp * _mods.RegenFractionPerSecond * dt;
            if (_regenAccumulator >= 1f)
            {
                _currentHp = Mathf.Min(MaxHp, _currentHp + _regenAccumulator);
                _regenAccumulator = 0f;
            }
        }
    }

    /// <summary>Vol de vie : appelé quand cet ennemi blesse le joueur.</summary>
    private void OnDealtDamage(float amount)
    {
        if (!IsElite || _mods.LifestealFraction <= 0f) return;
        _currentHp = Mathf.Min(MaxHp, _currentHp + amount * _mods.LifestealFraction);
    }

    /// <summary>Explosion de mort de l'affixe Explosif. Sans effet sans l'affixe.</summary>
    private void TriggerEliteExplosion()
    {
        if (!IsElite || _mods.ExplodeDamageMult <= 0f) return;

        var player = Player.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist > EliteAffixTable.ExplosionRadius) return;

        // Passe par TakeDamage du joueur, donc respecte ses i-frames : une explosion ne doit pas
        // contourner la seule protection qui rend les nuées survivables.
        player.TakeDamage(Damage * _mods.ExplodeDamageMult);
    }

    /// <summary>Applique le scaling de vague — voir <c>EnemyScaling</c> (logique pure partagée).</summary>
    public void ApplyScaling(float scaledMaxHp, float scaledDamage)
    {
        MaxHp = scaledMaxHp;
        Damage = scaledDamage;
        _currentHp = MaxHp;
    }

    /// <summary>Installe le jeu d'animations, comme <c>SetSpriteFrames</c> sous Godot.</summary>
    public void SetSpriteFrames(SpriteFramesAsset frames)
    {
        _animator ??= GetComponentInChildren<FrameAnimator>();
        if (_animator == null) return;

        _animator.SetSpriteFrames(frames);
        _animator.Play("idle");
    }

    /// <summary>Encaisse des dégâts. Sans effet si l'ennemi est déjà mort.</summary>
    public virtual void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f) return;

        // Affixe Blindé : la réduction s'applique ici, en un seul point, pour qu'aucune source de
        // dégâts ne puisse l'oublier.
        if (IsElite && _mods.DamageTakenMult != 1f) amount *= _mods.DamageTakenMult;

        _timeSinceHit = 0f;   // toute frappe suspend la régénération de l'affixe Régénérant

        // Journalisé APRÈS la réduction de l'affixe : la courbe de puissance doit compter ce qui est
        // réellement retiré à l'ennemi, pas ce que l'arme a annoncé. Un blindé encaissant 40 % de
        // moins gonflerait sinon le DPS mesuré sans qu'aucun ennemi ne meure plus vite.
        PowerTelemetry.NotifyDamageDealt(amount);

        _currentHp -= amount;
        if (_currentHp <= 0f) Die();
    }

    /// <summary>
    /// Mort : fait tomber un orbe d'XP, puis retire l'ennemi.
    /// </summary>
    /// <remarks>
    /// L'XP n'est <b>pas</b> créditée directement : elle passe par un orbe à ramasser. C'est une
    /// boucle de gameplay, pas un détail de présentation — elle oblige le joueur à entrer dans la
    /// zone qu'il vient de nettoyer. Court-circuiter l'orbe rendrait le jeu plus sûr et changerait
    /// son rythme.
    /// </remarks>
    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;

        Died?.Invoke(XpValue);
        PlayDeathSfx();
        SpawnDeathBurst();
        TriggerEliteExplosion();
        SpawnXpOrb();
        AetherCoreDrops.OnEnemyDied(DefId, transform.position);
        GameManager.Instance?.RegisterKill();

        // Assimilation : chaque élimination alimente la jauge de son archétype. C'est ici, et nulle
        // part ailleurs, que le bestiaire rencontre les greffes — un champion et un boss versent
        // dans la jauge des champions, pas dans celle de leur comportement.
        Assimilation.OnEnemyKilled(
            EnemyTable.AiKey(Ai), IsElite,
            isMiniBoss: this is MiniBoss,
            isBoss: this is RustedCore);

        Destroy(gameObject);
    }

    /// <summary>
    /// Gerbe de mort — portage d'<c>EnemyDeathBurst</c>. Le <b>calibre</b> suit le rôle de l'ennemi,
    /// pas ses PV : c'est le seul retour qui distingue « j'ai nettoyé de la piétaille » de « je viens
    /// d'abattre un champion », dans une mêlée où les sprites sont trop petits pour être suivis.
    /// </summary>
    private void SpawnDeathBurst()
    {
        int tier = this switch
        {
            RustedCore => 3,
            MiniBoss => 2,
            _ => IsElite ? 1 : 0,
        };

        // Teinte reprise du sprite : un ennemi de givre n'explose pas en orange. Le blanc pur d'un
        // sprite non teinté retomberait sur l'orange chaud d'origine, qui reste le défaut lisible.
        var sr = GetComponentInChildren<SpriteRenderer>();
        var tint = sr != null && sr.color != Color.white
            ? new Color(sr.color.r, sr.color.g, sr.color.b)
            : new Color(1f, 0.55f, 0.3f);

        Vfx.Death(transform.position, tier, tint);

        if (tier >= 2) ScreenShake.Shake(8f, 0.25f);
    }

    /// <summary>
    /// Son de mort, choisi selon l'archétype. Il porte une information de jeu : un colosse qui tombe
    /// hors du champ ne se voit pas, mais s'entend — et ne sonne pas comme une nuée qui s'effondre.
    /// </summary>
    private void PlayDeathSfx()
    {
        string sfx = Ai switch
        {
            EnemyTable.AiType.RangedKiter     => "sfx_enemy_sentinel_die",
            EnemyTable.AiType.ConeKiter       => "sfx_enemy_sentinel_die",
            EnemyTable.AiType.SlowHunter      => "sfx_enemy_colossus_die",
            EnemyTable.AiType.ChargingBruiser => "sfx_enemy_colossus_die",
            EnemyTable.AiType.ErraticChase    => "sfx_enemy_drone_die",
            _                                 => "sfx_enemy_swarm_die",
        };

        // Variation de hauteur large, et non les 6 % par défaut : une mort n'arrive jamais seule. À la
        // 3ᵉ minute, une nuée en produit des dizaines par seconde, et c'est la répétition à
        // l'identique du même échantillon — plus encore que son volume — qui s'entend comme une
        // bouillie plutôt que comme des morts distinctes. Les orbes d'XP, autre événement de masse,
        // font le même choix.
        AudioSystem.PlaySfx(sfx, pitchVariation: 0.18f);
    }

    /// <summary>Prefab d'orbe d'XP, injecté par le spawner à la création.</summary>
    public GameObject? XpOrbPrefab { get; set; }

    private void SpawnXpOrb()
    {
        if (XpOrbPrefab == null) return;

        var go = Instantiate(XpOrbPrefab, transform.position, Quaternion.identity);
        go.SetActive(true);   // sémantique Godot : un nœud instancié est toujours actif

        var orb = go.GetComponent<XpOrb>();
        if (orb != null) orb.Configure(XpValue, GetOrbTier());
    }

    /// <summary>Palier visuel de l'orbe, dérivé de la valeur — comme sous Godot.</summary>
    protected virtual int GetOrbTier() => XpValue switch
    {
        >= 50 => 3,
        >= 20 => 2,
        >= 5  => 1,
        _     => 0,
    };
}
