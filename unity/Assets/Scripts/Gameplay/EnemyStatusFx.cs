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
/// <para>Chaque état porte <b>deux</b> signaux, parce qu'un seul se perd dans la masse : le gel
/// recolore la silhouette <i>et</i> sème des éclats derrière elle ; la brûlure porte de petites
/// langues de feu <i>et</i> laisse une <b>traînée de fumée</b> tant que la chaleur court. Le
/// mouvement est ce qui accroche l'œil dans une mêlée à 300 entités — une teinte fixe, non.</para>
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

    private static Shader? _frostShader;

    private SpriteRenderer? _sprite;
    private Material? _frostMaterial;
    private float _frostShown = -1f;

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
    public bool FrostVisible => _frostMaterial != null && _frostShown > 0.5f;

    /// <summary>Les flammes sont-elles visibles à l'instant ?</summary>
    public bool FlamesVisible => _flameRoot != null && _flameRoot.gameObject.activeSelf;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _lastTrailPosition = transform.position;
    }

    /// <summary>
    /// Met à jour les deux états. Appelé par <see cref="EnemyBase"/> à chaque image, avec la vérité
    /// du modèle — ce composant ne décide de rien, il ne fait que le montrer.
    /// </summary>
    public void Render(bool frozen, bool burning, float dt)
    {
        RenderFrost(frozen, dt);
        RenderBurn(burning, dt);
    }

    // ─── Gel ──────────────────────────────────────────────────────────────────

    private void RenderFrost(bool frozen, float dt)
    {
        float target = frozen ? 1f : 0f;

        if (!Mathf.Approximately(_frostShown, target))
        {
            EnsureFrostMaterial();

            // Le paramètre n'est poussé qu'au CHANGEMENT : l'écrire à chaque image sur 300 entités
            // coûterait cher pour une valeur qui ne bouge que deux fois par gel.
            _frostMaterial?.SetFloat("_Frost", target);
            _frostShown = target;
        }

        if (!frozen)
        {
            _trailTimer = 0f;
            _lastTrailPosition = transform.position;
            return;
        }

        _trailTimer += dt;
        if (_trailTimer < FrostTrailInterval) return;

        _trailTimer = 0f;

        // Une traînée derrière un ennemi immobile serait un tas de givre, pas un sillage : elle ne
        // se sème que si la cible a réellement avancé depuis le dernier éclat.
        Vector2 here = transform.position;
        if (Vector2.Distance(here, _lastTrailPosition) < FrostTrailMinDistance) return;

        if (Vfx.ActiveEffects < TrailBudget)
        {
            Vfx.Dot(_lastTrailPosition, new Color(0.62f, 0.86f, 1f, 0.9f), size: 11f, life: 0.55f);
            FrostShardsDropped++;
        }

        _lastTrailPosition = here;
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

        // ⚠ Sur le CORPS, pas sur l'origine. Les sprites d'ennemis ne sont pas centrés sur le
        // transform de leur entité — les flammes se posaient sous les pieds, ce qui se lit « le sol
        // brûle » et non « l'ennemi brûle ».
        if (_sprite != null)
        {
            Vector3 center = transform.InverseTransformPoint(_sprite.bounds.center);
            root.transform.localPosition = new Vector3(center.x, center.y, 0f);
        }

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
