using UnityEngine;

/// <summary>
/// Rend <b>visibles</b> les deux états qu'un ennemi peut subir : le <b>gel</b> et la <b>brûlure</b>.
///
/// <para><b>Pourquoi ce composant existe.</b> Les deux effets étaient parfaitement fonctionnels et
/// parfaitement invisibles. Un ralentissement de 45 % ne se voit pas dans une nuée qui avance déjà
/// lentement, et une brûlure qui grignote 8 PV/s ne se distingue en rien d'un ennemi intact. Le
/// joueur n'avait donc aucun moyen de savoir laquelle de ses armes avait touché, ni sur quelles
/// cibles ses effets couraient encore — ce qui rend les deux archétypes indécidables au moment de
/// choisir une carte.</para>
///
/// <para>Chaque état porte <b>plusieurs</b> signaux, parce qu'un seul se perd dans la masse : le gel
/// recolore la silhouette, <i>porte des cristaux</i>, <i>exhale une vapeur froide</i>, <i>ralentit sa
/// propre animation</i> et sème des éclats derrière lui ; la brûlure porte de petites langues de feu
/// <i>et</i> laisse une <b>traînée de fumée</b> tant que la chaleur court. Le mouvement est ce qui
/// accroche l'œil dans une mêlée à 300 entités — une teinte fixe, non.</para>
///
/// <para><b>Les deux états ne bougent pas de la même façon, et c'est le fond du sujet.</b> Le feu
/// monte, ondule et se renouvelle ; la glace <i>tient</i>. Les cristaux sont donc immobiles et ne
/// font que scintiller, là où les langues de feu courent le long du corps — sans ce contraste, deux
/// nuages de particules de couleurs différentes se liraient comme le même effet.</para>
///
/// <para><b>Le gel dit sa force.</b> Une Lance Cryogénique (−20 %) et un Voile de Givre (−45 %)
/// produisaient exactement la même image : la teinte était binaire. L'intensité du givre suit
/// désormais le multiplicateur de vitesse, et la <b>cadence d'animation de la victime</b> le suit
/// aussi — c'est ce dernier signal, et non la couleur, qui fait lire « il est ralenti » plutôt que
/// « il est bleu » : un sprite qui s'agite à pleine vitesse en avançant au ralenti se lit comme un
/// personnage qui glisse.</para>
///
/// <para><b>La brûlure se porte à la taille de sa victime.</b> La première version posait trois
/// lueurs de côté fixe sur tout le bestiaire — et ce côté était en réalité un <i>facteur
/// d'échelle</i> appliqué à un sprite de 16 px, si bien que chaque langue de feu couvrait près de
/// 290 px : un essaim de 16 px disparaissait sous ce que le joueur lisait comme une explosion
/// permanente. Toutes les mesures ci-dessous sont donc des <b>fractions du corps</b>, jamais des
/// pixels absolus. (Deuxième fois qu'un sprite tracé au vol, en PPU 1, se voit traiter comme si
/// <c>localScale</c> était une taille — cf. les drones.)</para>
/// </summary>
public sealed class EnemyStatusFx : MonoBehaviour
{
    /// <summary>Secondes entre deux éclats de givre semés derrière un ennemi gelé.</summary>
    private const float FrostTrailInterval = 0.22f;

    /// <summary>Déplacement minimal, en pixels, sous lequel aucune traînée n'est semée.</summary>
    private const float FrostTrailMinDistance = 6f;

    /// <summary>
    /// Ralentissement au-delà duquel le givre est déjà à son maximum. Calé sur l'arme la plus froide
    /// du jeu (Voile de Givre, ×0,55) : au-dessous, l'échelle se tasserait sur une plage que rien
    /// n'atteint jamais, et les deux armes cryo se ressembleraient de nouveau.
    /// </summary>
    private const float FullFrostSlow = 0.5f;

    /// <summary>
    /// Part du givre acquise dès le plus faible ralentissement. <b>Ce n'est pas un réglage de
    /// confort</b> : un −20 % qui ne teinterait qu'à 20 % ne se verrait pas du tout, et l'effet
    /// redeviendrait invisible pour l'arme qui en a le plus besoin (la Lance touche peu de cibles à
    /// la fois). Le dosage sert à <i>distinguer</i> deux forces, pas à en effacer une.
    /// </summary>
    private const float FrostFloor = 0.5f;

    /// <summary>
    /// Vitesse de fonte du givre, en unités d'intensité par seconde (1 → 0 en ~0,35 s).
    /// </summary>
    /// <remarks>
    /// Le givre <b>prend d'un coup</b> et <b>fond lentement</b> : c'est asymétrique à dessein. La
    /// prise instantanée est le seul retour qui dise « le tir a porté sur celle-ci » ; une extinction
    /// tout aussi sèche, elle, ferait <i>clignoter</i> la nuée entière au rythme des recharges.
    /// </remarks>
    private const float FrostMeltSpeed = 2.8f;

    /// <summary>Cadence d'animation plancher d'une victime gelée.</summary>
    /// <remarks>
    /// Le multiplicateur de vitesse descend jusqu'à 0,05 (<c>ApplySlow</c> le borne là) : recopié tel
    /// quel, il donnerait un sprite <b>immobile</b>, que le joueur lit « l'animation est cassée » et
    /// non « il est gelé ». Le plancher garde un reste de mouvement, donc un signe de vie.
    /// </remarks>
    private const float MinCadence = 0.35f;

    /// <summary>Directions où poussent les cristaux, en degrés autour du centre du corps.</summary>
    /// <remarks>
    /// Fixes, et volontairement <b>asymétriques</b> : réparties à intervalle régulier, quatre pointes
    /// dessinent une étoile — une forme d'interface, pas du givre. Elles sont aussi identiques d'un
    /// ennemi à l'autre, ce qui est sans importance : on ne voit jamais deux fois la même silhouette
    /// assez longtemps pour le remarquer, et un tirage aléatoire coûterait un générateur de plus.
    /// </remarks>
    /// <remarks>
    /// ⚠ Aucune n'est <b>verticale</b> : à 96°, la pointe du haut se lisait comme une antenne plantée
    /// sur la tête de l'ennemi, ce qui change sa silhouette au lieu de la qualifier.
    /// </remarks>
    private static readonly float[] ShardAnglesDeg = { 208f, 334f, 118f, 22f };

    /// <summary>Longueur d'un cristal, en fraction de la <b>hauteur</b> du corps.</summary>
    private const float ShardLengthRatio = 0.27f;

    /// <summary>Largeur d'un cristal, en fraction de la <b>largeur</b> du corps.</summary>
    private const float ShardWidthRatio = 0.085f;

    /// <summary>
    /// Distance du centre du corps au <b>milieu</b> d'un cristal, en fraction de sa plus petite
    /// dimension.
    /// </summary>
    /// <remarks>
    /// ⚠ Le cristal est centré là, il n'y <b>part</b> pas. Une première version posait sa base à cette
    /// distance et le laissait pousser vers l'extérieur : les éclats se retrouvaient entièrement
    /// <i>hors</i> de la silhouette, et la capture montrait des planches blanches flottant à côté
    /// d'ennemis intacts. Du givre pousse <b>sur</b> un corps — il doit donc le chevaucher.
    /// </remarks>
    private const float ShardOrbitRatio = 0.20f;

    /// <summary>
    /// Opacité maximale d'un cristal — même raison que <see cref="FlameAlpha"/> : en mélange additif,
    /// quatre éclats clairs posés sur un corps de 32 px saturent au blanc bien avant 1.
    /// </summary>
    /// <remarks>
    /// ⚠ Baissé de 0,5 à 0,32 <b>sur capture</b>. Le sprite employé est un blanc uni : à 0,5, quatre
    /// rectangles francs sur fond sombre se lisaient comme des débris, pas comme du givre.
    /// </remarks>
    private const float ShardAlpha = 0.32f;

    /// <summary>Secondes entre deux bouffées de vapeur froide.</summary>
    /// <remarks>
    /// Plus espacée que la fumée (0,45 s) parce qu'elle <b>vit plus longtemps</b> : c'est le cumul
    /// qui décide de la lisibilité, jamais l'exemplaire isolé — la fumée l'a appris à ses dépens.
    /// </remarks>
    private const float VaporInterval = 0.6f;

    /// <summary>Plafond d'effets simultanés au-delà duquel la vapeur s'abstient.</summary>
    private const int VaporBudget = 90;

    /// <summary>Nombre de langues de feu portées par un ennemi qui brûle.</summary>
    private const int FlameCount = 3;

    /// <summary>Diamètre d'une langue de feu, en fraction de la <b>largeur</b> du corps.</summary>
    private const float FlameWidthRatio = 0.38f;

    /// <summary>Écart entre deux langues, en fraction de la largeur du corps.</summary>
    private const float FlameSpreadRatio = 0.24f;

    /// <summary>Course d'une langue, du bas du corps vers le haut, en fraction de sa hauteur.</summary>
    private const float FlameRiseRatio = 0.85f;

    /// <summary>
    /// Opacité maximale d'une langue. <b>C'est la valeur de la subtilité</b> : en mélange additif,
    /// trois lueurs superposées sur un corps de 32 px saturent au blanc bien avant 1 — l'ennemi
    /// disparaît alors derrière son propre état.
    /// </summary>
    private const float FlameAlpha = 0.5f;

    /// <summary>Taille de référence du grain de particule, en pixels (cf. <c>VfxPrimitives.Spark</c>).</summary>
    private const float SparkSize = 16f;

    /// <summary>Dimensions par défaut d'un corps dont le sprite ne dit encore rien, en pixels.</summary>
    private const float FallbackBodySize = 32f;

    /// <summary>
    /// Secondes entre deux bouffées de fumée.
    /// </summary>
    /// <remarks>
    /// ⚠ Réglé <b>sur capture</b>, et à la hausse : à 0,3 s, une nuée entière en flammes couvrait le
    /// sol d'un voile laiteux continu — chaque bouffée est discrète, leur <i>cumul</i> ne l'est pas.
    /// C'est la même leçon que les lueurs d'armes portées telles quelles depuis Godot : en mélange
    /// additif, un effet ne se juge jamais sur un exemplaire isolé.
    /// </remarks>
    private const float SmokeInterval = 0.45f;

    /// <summary>
    /// Plafond d'effets simultanés au-delà duquel la fumée s'abstient — plus serré que celui de la
    /// traînée de givre. Une bouffée vit trois fois plus longtemps qu'un éclat, et la brûlure peut
    /// courir sur des dizaines de cibles : sans cette marge, la fumée mangerait à elle seule le
    /// vivier partagé et ferait disparaître les traces d'<b>armes</b>.
    /// </summary>
    private const int SmokeBudget = 90;

    /// <summary>
    /// Aléatoire des bouffées. <b>Jamais <c>UnityEngine.Random</c></b> : il partage son état avec le
    /// jeu, et une campagne de banc à graine fixe verrait ses tirages se décaler selon le nombre
    /// d'ennemis ayant brûlé. Un effet décoratif ne change jamais le déroulé d'une run.
    /// </summary>
    private static readonly System.Random SmokeJitter = new(20260806);

    /// <summary>
    /// Plafond d'effets simultanés au-delà duquel la traînée s'abstient. Les états peuvent toucher
    /// des centaines d'entités à la fois : sans ce garde-fou, ils videraient le pool partagé et
    /// feraient disparaître les effets d'<b>armes</b>, autrement dit le retour dont le joueur a le
    /// plus besoin.
    /// </summary>
    private const int TrailBudget = 150;

    /// <summary>
    /// Aléatoire de la vapeur. <b>Jamais <c>UnityEngine.Random</c></b>, pour la raison donnée sur
    /// <see cref="SmokeJitter"/> : un décor ne doit pas décaler les tirages d'une run.
    /// </summary>
    private static readonly System.Random VaporJitter = new(20260807);

    private static Shader? _frostShader;

    private SpriteRenderer? _sprite;
    private FrameAnimator? _animator;
    private Material? _frostMaterial;

    /// <summary>Intensité de givre affichée à l'instant (0 = intact, 1 = pris).</summary>
    private float _frostLevel;

    /// <summary>Dernière valeur réellement poussée dans le matériau — évite d'écrire pour rien.</summary>
    private float _frostPushed = -1f;

    /// <summary>Cadence d'animation déjà appliquée, pour ne l'écrire qu'aux changements.</summary>
    private float _cadenceApplied = 1f;

    private bool _wasFrozen;

    private Transform? _shardRoot;
    private SpriteRenderer[]? _shards;
    private float _shardPhase;

    /// <summary>Taille du sprite de cristal, en unités — voir la remarque de <see cref="BuildShards"/>.</summary>
    private float _shardUnits = 1f;

    private float _vaporTimer;

    private float _trailTimer;
    private Vector2 _lastTrailPosition;

    private Transform? _flameRoot;
    private SpriteRenderer[]? _flames;
    private float _flamePhase;
    private float _smokeTimer;

    /// <summary>Largeur et hauteur du corps rendu, en pixels du monde — mesurées, jamais supposées.</summary>
    private float _bodyWidth = FallbackBodySize;
    private float _bodyHeight = FallbackBodySize;

    /// <summary>Conversion pixel du monde → unité locale des flammes (inverse de l'échelle portée).</summary>
    private float _localPerWorld = 1f;

    /// <summary>Le corps a-t-il pu être mesuré sur le sprite, ou tient-on encore un repli ?</summary>
    private bool _bodyMeasured;

    /// <summary>Éclats de givre semés — observable pour les vérifications.</summary>
    public int FrostShardsDropped { get; private set; }

    /// <summary>Bouffées de fumée émises — observable pour les vérifications.</summary>
    public int SmokePuffsEmitted { get; private set; }

    /// <summary>Bouffées de vapeur froide émises — observable pour les vérifications.</summary>
    public int FrostVaporEmitted { get; private set; }

    /// <summary>Prises de gel signalées par une gerbe d'éclats — observable pour les vérifications.</summary>
    public int FreezeSnaps { get; private set; }

    /// <summary>Intensité de givre affichée, de 0 à 1. Suit la <b>force</b> du ralentissement.</summary>
    public float FrostLevel => _frostLevel;

    /// <summary>
    /// Cadence d'animation imposée à la victime (1 = intacte). C'est le seul signal du ralentissement
    /// qu'un banc puisse constater : les autres sont des pixels.
    /// </summary>
    public float CadenceScale => _cadenceApplied;

    /// <summary>Largeur retenue pour le corps, en pixels — et si elle vient du sprite ou d'un repli.</summary>
    public float BodyWidthPx => _bodyWidth;

    /// <summary>Le corps a-t-il pu être mesuré sur le sprite ?</summary>
    public bool BodyMeasured => _bodyMeasured;

    /// <summary>
    /// Largeur totale occupée par les flammes, en pixels. Le banc s'en sert pour vérifier que
    /// l'effet <b>tient dans la silhouette</b> — la seule chose qu'il puisse constater d'un réglage
    /// dont le reste (« est-ce subtil ? ») ne se juge qu'à l'œil.
    /// </summary>
    public float FlameSpanPx => _bodyWidth * (FlameSpreadRatio * (FlameCount - 1) + FlameWidthRatio);

    /// <summary>Le givre est-il appliqué à l'instant ?</summary>
    public bool FrostVisible => _frostMaterial != null && _frostLevel > 0.05f;

    /// <summary>Les cristaux sont-ils portés à l'instant ?</summary>
    public bool ShardsVisible => _shardRoot != null && _shardRoot.gameObject.activeSelf;

    /// <summary>
    /// Largeur totale occupée par les cristaux, en pixels — le pendant de <see cref="FlameSpanPx"/>,
    /// et pour la même raison : c'est la seule chose qu'un banc puisse dire d'un effet porté.
    /// </summary>
    public float ShardSpanPx
        => 2f * (Mathf.Min(_bodyWidth, _bodyHeight) * ShardOrbitRatio
                 + _bodyHeight * ShardLengthRatio * 0.5f);

    /// <summary>Les flammes sont-elles visibles à l'instant ?</summary>
    public bool FlamesVisible => _flameRoot != null && _flameRoot.gameObject.activeSelf;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponentInChildren<FrameAnimator>();
        _lastTrailPosition = transform.position;
    }

    /// <summary>
    /// Met à jour les deux états. Appelé par <see cref="EnemyBase"/> à chaque image, avec la vérité
    /// du modèle — ce composant ne décide de rien, il ne fait que le montrer.
    /// </summary>
    /// <param name="slowMultiplier">
    /// Multiplicateur de vitesse courant (1 = intact). C'est une <b>force</b>, pas un booléen : le
    /// composant en tire l'intensité du givre et la cadence de l'animation, qui sont précisément ce
    /// qui distinguait mal la Lance Cryogénique du Voile de Givre.
    /// </param>
    public void Render(float slowMultiplier, bool burning, float dt)
    {
        RenderFrost(slowMultiplier, dt);
        RenderBurn(burning, dt);
    }

    // ─── Gel ──────────────────────────────────────────────────────────────────

    private void RenderFrost(float slowMultiplier, float dt)
    {
        bool frozen = slowMultiplier < 0.999f;
        float target = frozen ? FrostLevelFor(slowMultiplier) : 0f;

        // Prise sèche, fonte lente (cf. FrostMeltSpeed). Un gel plus fort qui s'ajoute par-dessus un
        // gel faible reprend donc lui aussi d'un coup : c'est le même événement.
        _frostLevel = target >= _frostLevel
            ? target
            : Mathf.Max(target, _frostLevel - FrostMeltSpeed * dt);

        PushFrost();

        // ⚠ La cadence suit le MODÈLE, jamais la fonte : elle dit ce que l'ennemi fait, pas ce qu'il
        // porte. La laisser redescendre avec le givre montrerait une cible encore ralentie alors
        // qu'elle a repris sa vitesse — un mensonge, et sur la seule information tactique du lot.
        ApplyCadence(frozen ? Mathf.Max(slowMultiplier, MinCadence) : 1f);

        RenderShards(dt);

        // La gerbe de prise n'est émise qu'au FRONT MONTANT. Les deux armes cryo réappliquent leur
        // gel à chaque recharge — posée à chaque application, elle ferait crépiter en permanence
        // tout ce qui est à portée, et « celle-ci vient d'être prise » cesserait d'être une
        // information.
        if (frozen && !_wasFrozen) EmitFreezeSnap();
        _wasFrozen = frozen;

        if (!frozen)
        {
            _trailTimer = 0f;
            _vaporTimer = 0f;
            _lastTrailPosition = transform.position;
            return;
        }

        EmitVapor(dt);

        _trailTimer += dt;
        if (_trailTimer < FrostTrailInterval) return;

        _trailTimer = 0f;

        // Une traînée derrière un ennemi immobile serait un tas de givre, pas un sillage : elle ne
        // se sème que si la cible a réellement avancé depuis le dernier éclat. La vapeur, elle, ne
        // pose pas cette condition — c'est la répartition des rôles héritée de la brûlure.
        Vector2 here = transform.position;
        if (Vector2.Distance(here, _lastTrailPosition) < FrostTrailMinDistance) return;

        if (Vfx.ActiveEffects < TrailBudget)
        {
            // ⚠ Nettement assombrie (alpha 0,9 → 0,32) et rétrécie (11 → 7 px) : elle avait été
            // calibrée quand elle était le SEUL signal de mouvement du gel. Avec les cristaux et la
            // vapeur, une nuée gelée devenait un tapis de taches blanches où l'on ne distinguait
            // plus les ennemis — le même piège de cumul que la fumée, une couche plus loin.
            Vfx.Dot(_lastTrailPosition, new Color(0.62f, 0.86f, 1f, 0.32f), size: 7f, life: 0.45f);
            FrostShardsDropped++;
        }

        _lastTrailPosition = here;
    }

    /// <summary>
    /// Traduit un multiplicateur de vitesse en intensité de givre : ×0,80 (Lance) → 0,70,
    /// ×0,55 (Voile) → 0,95. Les deux se voient, et pas de la même façon.
    /// </summary>
    private static float FrostLevelFor(float slowMultiplier)
    {
        float t = Mathf.InverseLerp(1f, FullFrostSlow, slowMultiplier);
        return FrostFloor + (1f - FrostFloor) * t;
    }

    /// <summary>Pousse l'intensité dans le matériau, seulement quand elle a bougé.</summary>
    /// <remarks>
    /// L'écrire à chaque image sur 300 entités coûterait cher pour une valeur qui, hors fonte, ne
    /// bouge que deux fois par gel. Le seuil laisse passer la fonte (quelques images) sans laisser
    /// passer le bruit d'arrondi.
    /// </remarks>
    private void PushFrost()
    {
        if (Mathf.Abs(_frostLevel - _frostPushed) < 0.02f
            && !(_frostLevel <= 0f && _frostPushed > 0f)) return;

        // Rien n'est instancié tant qu'aucun gel n'a été subi : voir EnsureFrostMaterial.
        if (_frostLevel <= 0f && _frostMaterial == null) { _frostPushed = 0f; return; }

        EnsureFrostMaterial();
        _frostMaterial?.SetFloat("_Frost", _frostLevel);
        _frostPushed = _frostLevel;
    }

    /// <summary>
    /// Ralentit l'animation de la victime à la mesure de son ralentissement.
    /// </summary>
    /// <remarks>
    /// <para>C'est le signal le moins coûteux et le plus parlant du lot : il ne dessine rien, ne prend
    /// aucun objet au vivier partagé, et se lit même sur un ennemi de 16 px au fond d'une nuée. Sans
    /// lui, un sprite qui s'agite à pleine cadence tout en avançant deux fois moins vite se lit
    /// « il glisse », pas « il est gelé ».</para>
    ///
    /// <para>⚠ Elle se remet à 1 dès la fin du ralentissement — pas à la fin de la fonte. Et il n'y a
    /// rien à restaurer à la mort : <c>EnemyBase.Die</c> détruit l'objet sur-le-champ, sans jouer
    /// d'animation de mort.</para>
    /// </remarks>
    private void ApplyCadence(float scale)
    {
        if (Mathf.Approximately(scale, _cadenceApplied)) return;

        _cadenceApplied = scale;
        if (_animator != null) _animator.SpeedScale = scale;
    }

    /// <summary>Gerbe brève au moment où la cible est prise — « ce tir a porté sur celle-ci ».</summary>
    private void EmitFreezeSnap()
    {
        if (Vfx.ActiveEffects >= TrailBudget) return;

        var ice = new Color(0.84f, 0.96f, 1f, 0.9f);
        Vector2 at = _shardRoot != null ? (Vector2)_shardRoot.position : (Vector2)transform.position;

        Vfx.Burst(at, ice, new Color(ice.r, ice.g, ice.b, 0f), 5, 14f, 48f, 4f, 0.26f);
        FreezeSnaps++;
    }

    /// <summary>
    /// Vapeur froide — le pendant de la traînée de fumée, et pour la même raison : un état qui dure a
    /// besoin d'un signal qui dure.
    /// </summary>
    /// <remarks>
    /// <para>Elle <b>tombe</b> au lieu de monter, et s'étale bien moins que la fumée : c'est ce qui la
    /// distingue du feu à teinte égale de luminosité. De l'air froid descend et se dépose — de la
    /// chaleur monte et se disperse.</para>
    ///
    /// <para>Comme la fumée, elle ne demande pas que la cible avance : les seules cibles qui portent
    /// un état assez longtemps pour être lues sont les plus lentes, donc les plus grosses.</para>
    /// </remarks>
    private void EmitVapor(float dt)
    {
        _vaporTimer += dt;
        if (_vaporTimer < VaporInterval) return;

        _vaporTimer = 0f;
        if (Vfx.ActiveEffects >= VaporBudget) return;

        Vector2 from = _shardRoot != null ? (Vector2)_shardRoot.position : (Vector2)transform.position;
        float jitter = (float)(VaporJitter.NextDouble() * 2.0 - 1.0);

        Vfx.Puff(from + new Vector2(jitter * _bodyWidth * 0.25f, -_bodyHeight * 0.2f),
                 // Bleu très pâle et très faible : une seule bouffée doit être à la limite du
                 // visible pour que dix superposées restent une buée et non une nappe blanche.
                 new Color(0.58f, 0.84f, 1f, 0.16f),
                 radiusPx: _bodyWidth * 0.2f,
                 life: 1.1f,
                 riseSpeed: -(10f + _bodyHeight * 0.25f),
                 drift: jitter * 7f,
                 growth: 1.35f);

        FrostVaporEmitted++;
    }

    /// <summary>
    /// Les cristaux portés par la victime — le pendant des langues de feu, en <b>immobile</b>.
    /// </summary>
    private void RenderShards(float dt)
    {
        if (_frostLevel <= 0.01f)
        {
            if (_shardRoot != null) _shardRoot.gameObject.SetActive(false);
            return;
        }

        if (_shardRoot == null) BuildShards();
        if (_shardRoot == null || _shards == null) return;

        if (!_bodyMeasured) MeasureBody();

        _shardRoot.gameObject.SetActive(true);
        _shardPhase += dt * 2.4f;

        // Mêmes règles que les flammes : tout dérive du corps mesuré, et tout repasse par l'inverse
        // de l'échelle portée puisque ces objets sont des enfants.
        float k = _localPerWorld;
        float orbit = Mathf.Min(_bodyWidth, _bodyHeight) * ShardOrbitRatio * k;
        float length = _bodyHeight * ShardLengthRatio * k / _shardUnits;
        float width = _bodyWidth * ShardWidthRatio * k / _shardUnits;

        for (int i = 0; i < _shards.Length; i++)
        {
            var shard = _shards[i];
            if (shard == null) continue;

            float deg = ShardAnglesDeg[i];
            float rad = deg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // Centré sur l'orbite, donc à cheval sur le bord du corps (cf. ShardOrbitRatio).
            shard.transform.localPosition = dir * orbit;
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, deg - 90f);

            // Scintillement : la glace ne bouge pas, elle accroche la lumière. Chaque cristal a sa
            // phase — à l'unisson, quatre éclats se liraient comme un seul clignotant.
            float glint = 0.82f + 0.18f * Mathf.Sin(_shardPhase + i * 1.9f);

            // La taille suit l'intensité : un −20 % porte de fines aiguilles, un −45 % une gangue.
            float grow = _frostLevel * glint;
            shard.transform.localScale = new Vector3(width * grow, length * grow, 1f);

            // Bleu franc plutôt que blanc cassé : en additif sur un fond sombre, un blanc à peine
            // teinté ressort GRIS, et quatre esquilles grises sur un corps bleu se lisent comme des
            // débris posés dessus plutôt que comme du givre qui en sort.
            shard.color = new Color(0.62f, 0.88f, 1f, ShardAlpha * _frostLevel * glint);
        }
    }

    /// <summary>
    /// Quatre cristaux <b>persistants</b>, pour la raison qui vaut pour les flammes : un gel court
    /// sur des dizaines de cibles à la fois et les emprunter au vivier partagé le viderait.
    /// </summary>
    /// <remarks>
    /// ⚠ Le sprite employé n'est <b>pas</b> le grain rond des flammes mais le blanc uni, étiré en
    /// écharde : une pointe anguleuse est ce qui fait lire « cristal » plutôt que « lueur ». Sa taille
    /// est <b>relevée sur le sprite</b> (<c>bounds</c>) et non supposée — <c>Flat</c> est en PPU 4,
    /// donc il mesure 1 unité et non 4 px, et c'est exactement le piège qui a produit les langues de
    /// feu de 288 px.
    /// </remarks>
    private void BuildShards()
    {
        var root = new GameObject("Givre");
        root.transform.SetParent(transform, false);
        PlaceOnBody(root.transform);

        MeasureBody();

        var sprite = VfxPrimitives.Flat;
        _shardUnits = Mathf.Max(0.001f, sprite.bounds.size.x);
        _shards = new SpriteRenderer[ShardAnglesDeg.Length];

        for (int i = 0; i < _shards.Length; i++)
        {
            var go = new GameObject($"Cristal{i}", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = VfxPrimitives.AdditiveFlat;
            sr.sortingOrder = 24;   // devant le porteur, comme les flammes

            _shards[i] = sr;
        }

        _shardRoot = root.transform;
    }

    /// <summary>
    /// Pose le matériau de givre au <b>premier</b> gel seulement.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Deux approches plus simples ont été essayées et <b>ne peuvent pas marcher</b>, pour
    /// la même raison : la faune est majoritairement rouge. Teinter <c>SpriteRenderer.color</c>
    /// multiplie, donc ne peut qu'assombrir — un ennemi rouge gelé reste rouge, en plus sombre, ce
    /// qui se lit « il est dans l'ombre ». Superposer un calque additif bleu ajoute du bleu au rouge
    /// et donne du <b>rose délavé</b>. Il faut <i>remplacer</i> la couleur, donc un shader — c'est
    /// exactement pourquoi le jeu d'origine en utilise un.</para>
    ///
    /// <para>Un matériau posé d'emblée sur chaque ennemi supprimerait le regroupement de rendu de
    /// toute la faune, alors que la plupart des entités ne sont jamais gelées. Il est donc créé à la
    /// demande — mais <b>par instance</b> : partager un matériau ferait givrer la nuée entière dès
    /// qu'un seul ennemi l'est.</para>
    /// </remarks>
    private void EnsureFrostMaterial()
    {
        if (_frostMaterial != null || _sprite == null) return;

        _frostShader ??= Resources.Load<Shader>("Shaders/EnemyFrost");
        if (_frostShader == null)
        {
            Debug.LogWarning("[EnemyStatusFx] shader de givre introuvable — ennemis gelés non teintés.");
            return;
        }

        _frostMaterial = new Material(_frostShader);
        _sprite.material = _frostMaterial;
    }

    // ─── Brûlure ──────────────────────────────────────────────────────────────

    private void RenderBurn(bool burning, float dt)
    {
        if (!burning)
        {
            if (_flameRoot != null) _flameRoot.gameObject.SetActive(false);
            _smokeTimer = 0f;
            return;
        }

        if (_flameRoot == null) BuildFlames();
        if (_flameRoot == null || _flames == null) return;

        // Tant que le sprite n'a rien dit de sa taille, on redemande : une brûlure commence souvent
        // avant la première image d'animation, et un repli figé habillerait un boss comme un essaim.
        if (!_bodyMeasured) MeasureBody();

        _flameRoot.gameObject.SetActive(true);
        _flamePhase += dt * 6.5f;

        // Toutes les mesures dérivent du corps : le même code habille un essaim de 16 px et un
        // colosse de 72 sans qu'aucune valeur ne soit à reprendre.
        //
        // ⚠ Le corps se mesure en pixels du MONDE, mais les flammes sont des enfants : ce qu'on leur
        // donne passe par l'échelle de l'ennemi. Sans ce facteur, une entité rendue à 1,5 porterait
        // des flammes une fois et demie trop grandes — la même confusion entre taille et facteur qui
        // a produit les « explosions ».
        float k = _localPerWorld;
        float flameScale = _bodyWidth * FlameWidthRatio / SparkSize * k;
        float spread = _bodyWidth * FlameSpreadRatio * k;
        float rise = _bodyHeight * FlameRiseRatio;

        for (int i = 0; i < _flames.Length; i++)
        {
            var flame = _flames[i];
            if (flame == null) continue;

            // Chaque langue a sa propre phase : à l'unisson, trois flammes se lisent comme un seul
            // bloc qui clignote, et le feu perd exactement ce qui le rend reconnaissable.
            float phase = _flamePhase + i * 2.1f;
            float t = Mathf.Repeat(phase * 0.34f, 1f);

            flame.transform.localPosition = new Vector3(
                (i - (FlameCount - 1) * 0.5f) * spread + Mathf.Sin(phase * 1.7f) * spread * 0.35f,
                (-0.3f + t) * rise * k, 0f);

            // La langue naît large et s'affine en montant, comme une vraie flamme — l'inverse (une
            // lueur qui grossit en s'élevant) se lit comme une explosion qui part.
            float fade = 1f - t;
            flame.transform.localScale = Vector3.one * (0.45f + 0.55f * fade) * flameScale;

            flame.color = Color.Lerp(new Color(1f, 0.34f, 0.08f, 0f),
                                     new Color(1f, 0.72f, 0.26f, FlameAlpha), fade);
        }

        EmitSmoke(dt, rise);
    }

    /// <summary>
    /// Sème la traînée de fumée qui <b>dure le temps du poison de chaleur</b>.
    /// </summary>
    /// <remarks>
    /// <para>À la différence de la traînée de givre, elle ne demande <b>pas</b> que la cible avance.
    /// Un ennemi arrêté qui brûle doit fumer : c'est justement là que le joueur a besoin de lire
    /// « celui-ci est déjà en train de mourir, inutile de le retirer », et une traînée conditionnée
    /// au déplacement laisserait muettes les cibles les plus lentes — c'est-à-dire les grosses, les
    /// seules qui survivent assez longtemps pour porter un état
    /// (relevé <c>brulent 0/9</c>).</para>
    ///
    /// <para>Les bouffées sont posées en <b>espace monde</b> et non attachées au corps : accrochées
    /// à l'ennemi, elles le suivraient et formeraient un nuage collé — l'exact contraire d'un
    /// sillage.</para>
    /// </remarks>
    private void EmitSmoke(float dt, float rise)
    {
        _smokeTimer += dt;
        if (_smokeTimer < SmokeInterval) return;

        _smokeTimer = 0f;
        if (Vfx.ActiveEffects >= SmokeBudget) return;

        Vector2 from = _flameRoot != null ? _flameRoot.position : (Vector2)transform.position;
        float jitter = (float)(SmokeJitter.NextDouble() * 2.0 - 1.0);

        Vfx.Puff(from + new Vector2(jitter * _bodyWidth * 0.2f, rise * 0.45f),
                 // Gris chaud très faible : c'est ce qui reste du feu une fois la lumière partie.
                 // L'alpha est bas à dessein — une seule bouffée doit être à la limite du visible
                 // pour que dix superposées restent de la fumée et non une nappe blanche.
                 new Color(0.44f, 0.37f, 0.33f, 0.2f),
                 radiusPx: _bodyWidth * 0.18f,
                 life: 0.9f,
                 riseSpeed: 22f + _bodyHeight * 0.35f,
                 drift: jitter * 10f,
                 growth: 1.9f);

        SmokePuffsEmitted++;
    }

    private void OnDestroy()
    {
        if (_frostMaterial != null) Destroy(_frostMaterial);
    }

    /// <summary>
    /// Centre un porte-effet sur le <b>corps</b>, et non sur l'origine de l'entité.
    /// </summary>
    /// <remarks>
    /// ⚠ Les sprites d'ennemis ne sont pas centrés sur le transform de leur entité : posés sur
    /// l'origine, les effets d'état atterrissaient sous les pieds, ce qui se lit « le sol brûle » et
    /// non « l'ennemi brûle ». Vrai pour les flammes comme pour les cristaux.
    /// </remarks>
    private void PlaceOnBody(Transform root)
    {
        if (_sprite == null) return;

        Vector3 center = transform.InverseTransformPoint(_sprite.bounds.center);
        root.localPosition = new Vector3(center.x, center.y, 0f);
    }

    /// <summary>
    /// Relève les dimensions réellement rendues du corps.
    /// </summary>
    /// <remarks>
    /// <para><c>bounds</c> est en espace monde et tient déjà compte de l'échelle, du sprite courant
    /// et de la hiérarchie — c'est la seule mesure qui reste juste pour un essaim à 16 px, un
    /// champion à 72 et un boss à 154.</para>
    ///
    /// <para>⚠ Elle peut être <b>muette</b> : tant qu'aucune image d'animation n'est posée, le
    /// renderer annonce des dimensions nulles. Le repli n'est alors pas une constante devinée mais le
    /// <b>rayon de contact</b> de l'entité — la mesure que le jeu emploie déjà pour dire jusqu'où
    /// s'étend son corps, et sur laquelle la taille des champions avait justement été recalée. Et
    /// comme la mesure peut réussir plus tard, on la retente tant qu'elle a échoué : figer un repli
    /// au premier appel donnerait la même taille de flammes à tout le bestiaire.</para>
    /// </remarks>
    private void MeasureBody()
    {
        float scale = Mathf.Abs(transform.lossyScale.x);
        _localPerWorld = scale > 0.001f ? 1f / scale : 1f;

        var size = _sprite != null ? _sprite.bounds.size : Vector3.zero;

        if (size.x > 1f && size.y > 1f)
        {
            _bodyWidth = size.x;
            _bodyHeight = size.y;
            _bodyMeasured = true;
            return;
        }

        var enemy = GetComponent<EnemyBase>();
        float diameter = enemy != null && enemy.PushRadius > 1f
            ? enemy.PushRadius * 2f
            : FallbackBodySize;

        _bodyWidth = _bodyHeight = diameter;
    }

    /// <summary>
    /// Trois petites langues de feu portées par l'ennemi, dimensionnées sur son corps.
    /// </summary>
    /// <remarks>
    /// Des enfants <b>persistants</b>, et non des effets empruntés au vivier partagé : une brûlure
    /// dure plusieurs secondes et peut courir sur des dizaines de cibles à la fois. Les tirer du
    /// vivier le viderait en une seconde, au détriment des effets d'armes — c'est la fumée seule,
    /// bien plus clairsemée, qui s'y sert.
    /// </remarks>
    private void BuildFlames()
    {
        var root = new GameObject("Flammes");
        root.transform.SetParent(transform, false);
        PlaceOnBody(root.transform);

        MeasureBody();

        _flames = new SpriteRenderer[FlameCount];

        for (int i = 0; i < FlameCount; i++)
        {
            var go = new GameObject($"Flamme{i}", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = VfxPrimitives.Spark;
            sr.sharedMaterial = VfxPrimitives.AdditiveSpark;

            // Devant l'ennemi qui les porte, sinon un colosse mange ses propres flammes.
            sr.sortingOrder = 24;

            _flames[i] = sr;
        }

        _flameRoot = root.transform;
    }

}
