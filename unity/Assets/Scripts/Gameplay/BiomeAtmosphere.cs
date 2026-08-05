using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atmosphère de l'arène : <b>brume</b> animée et couches en <b>parallaxe</b>.
///
/// <para><b>La parallaxe ne se voit qu'à travers les TROUS du sol</b>, et c'est tout le mécanisme.
/// Sous Godot, le sol est une grille de tuiles dont trois ou quatre amas sont remplacés par une
/// tuile « vitre » : le fond défile derrière, et l'œil ne le lit comme une profondeur que parce
/// qu'il l'aperçoit <i>par une fenêtre</i>. Un premier portage posait les mêmes motifs
/// <b>par-dessus</b> le sol : ils se lisaient alors comme des dalles peintes sur le terrain — la
/// même donnée, exactement l'effet inverse.</para>
///
/// <para>D'où l'empilement : sol opaque → fond de puits (par fenêtre) → motifs masqués → reflet de
/// vitre → poussière → brume → entités. Le trou n'est pas une découpe du sol — Unity ne sait pas
/// percer un sprite — mais un aplat très sombre <b>redessiné par-dessus</b> aux dimensions de
/// l'amas ; les motifs, confinés par un <c>SpriteMask</c> à cette même forme, continuent de dériver
/// en parallaxe et ne se voient qu'à l'intérieur.</para>
/// </summary>
public sealed class BiomeAtmosphere : MonoBehaviour
{
    /// <summary>Une couche et son facteur de suivi de caméra.</summary>
    private readonly struct Layer
    {
        public readonly Transform Root;
        public readonly float Parallax;

        public Layer(Transform root, float parallax) { Root = root; Parallax = parallax; }
    }

    private const float MotifParallax    = 0.06f;   // presque immobile : le fond du monde
    private const float DustFarParallax  = 0.55f;   // < 1 : lointain, suit moins la caméra
    private const float DustNearParallax = 1.35f;   // > 1 : premier plan, devance la caméra

    /// <summary>Ordres de rendu, du plus profond au plus proche.</summary>
    /// ⚠ Les motifs sont AU-DESSUS du sol (-100) dans l'ordre de tri, et pourtant ils se lisent
    /// comme un lointain : c'est le MASQUE qui les confine aux fenêtres, et le fond de puits opaque
    /// posé sous eux qui fait le trou. Un premier essai les plaçait réellement sous le sol —
    /// invisibles, puisque le sol est une surface pleine.
    private const int OrderMotif  = -98;    // motifs profonds, confinés aux fenêtres par le masque
    private const int OrderGlass  = -95;    // reflet de la vitre, par-dessus le sol (-100)
    private const int OrderDustFar = -60;
    private const int OrderFog     = -50;
    private const int OrderShafts  = -49;   // les rais passent par-dessus la brume
    private const int OrderDustNear = 18;   // devant les entités

    private readonly List<Layer> _layers = new();
    private Camera? _camera;
    private Material? _fogMaterial;
    private Material? _shaftMaterial;

    /// <summary>Particules posées — observable pour les vérifications.</summary>
    public int MoteCount { get; private set; }

    /// <summary>Fenêtres vitrées ouvertes dans le sol — observable pour les vérifications.</summary>
    public int WindowCount { get; private set; }

    /// <summary>La brume est-elle active ? Faux si le shader manque.</summary>
    public bool HasFog => _fogMaterial != null;

    /// <summary>Les rais de lumière sont-ils actifs ? Faux si le shader manque.</summary>
    public bool HasShafts => _shaftMaterial != null;

    /// <summary>
    /// Construit les couches pour un biome.
    /// </summary>
    /// <param name="windows">
    /// Centres des amas vitrés, fournis par le rendu du sol. <b>Un motif est posé derrière chacun</b>
    /// plutôt que tirés au hasard : un tirage indépendant du placement des fenêtres en rate trop, et
    /// le joueur doit voir la profondeur par <i>chaque</i> fenêtre, pas « parfois ».
    /// </param>
    public void Configure(string? biomeId, IReadOnlyList<Vector2> windows)
    {
        _camera = Camera.main;

        foreach (var layer in _layers)
            if (layer.Root != null) Destroy(layer.Root.gameObject);

        _layers.Clear();
        MoteCount = 0;
        WindowCount = windows.Count;

        var tint = TintOf(biomeId);

        BuildMotifs(tint, windows);
        BuildFog(biomeId, tint);
        BuildShafts(biomeId, tint);

        AddDust("PoussiereLointaine", DustFarParallax, 34, 7f,
                new Color(tint.r, tint.g, tint.b, 0.34f), spread: 1.2f, order: OrderDustFar);

        AddDust("PoussiereProche", DustNearParallax, 16, 11f,
                new Color(tint.r, tint.g, tint.b, 0.22f), spread: 1.0f, order: OrderDustNear);
    }

    /// <summary>
    /// Motifs profonds — un derrière chaque fenêtre, plus quelques-uns dispersés. Ils sont
    /// <b>masqués</b> : visibles seulement là où une fenêtre est ouverte.
    /// </summary>
    private void BuildMotifs(Color tint, IReadOnlyList<Vector2> windows)
    {
        var root = new GameObject("MotifsProfonds").transform;
        root.SetParent(transform, false);

        var sprite = DeepMotifSprite.Get();

        // Décalage faible : le glyphe doit tenir ENTIER dans sa fenêtre. À ±24 px sur une ouverture
        // de 128-192, il en sortait par un bord et on n'apercevait plus que son noyau.
        foreach (var window in windows) AddMotif(root, sprite, tint, window, jitter: 10f);

        // 22 motifs dispersés au-delà des fenêtres, comme le jeu publié. Ils ne sont pas décoratifs :
        // la couche profonde dérive presque avec la caméra (parallaxe 0,06), si bien qu'un motif posé
        // au centre d'une fenêtre en SORT dès que le joueur traverse l'arène. Sans ce fond dispersé,
        // les fenêtres se videraient définitivement au premier déplacement.
        for (int i = 0; i < 22; i++)
        {
            var at = new Vector2(((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfWidth * 1.4f,
                                 ((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfHeight * 1.4f);

            AddMotif(root, sprite, tint, at, jitter: 0f);
        }

        _layers.Add(new Layer(root, MotifParallax));
    }

    private void AddMotif(Transform root, Sprite sprite, Color tint, Vector2 at, float jitter)
    {
        var go = new GameObject("Motif", typeof(SpriteRenderer));
        go.transform.SetParent(root, false);

        go.transform.localPosition = at + new Vector2(
            ((float)_rng.NextDouble() * 2f - 1f) * jitter,
            ((float)_rng.NextDouble() * 2f - 1f) * jitter);

        go.transform.localRotation = Quaternion.Euler(0f, 0f, (float)_rng.NextDouble() * 360f);
        // ⚠ Un FACTEUR, pas une taille : le glyphe mesure déjà ~100 px. Le premier portage écrivait
        // `Vector3.one * 46..72` en croyant régler des pixels — sur un sprite de 3 px c'était
        // fortuitement du bon ordre de grandeur, sur celui-ci ce serait un motif de 7 000 px.
        //
        // Plus petit que sous Godot (1,9-2,7) : là-bas le motif dépasse volontiers de l'amas vitré
        // parce que la grille de tuiles en ouvre plusieurs voisins ; ici la fenêtre est unique, et un
        // glyphe qui la déborde ne montre plus que son noyau — donc rien de reconnaissable.
        go.transform.localScale = Vector3.one * (1.0f + 0.45f * (float)_rng.NextDouble());

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(tint.r, tint.g, tint.b, 0.62f);
        renderer.sortingOrder = OrderMotif;

        // ⚠ C'EST ce réglage qui produit l'effet « vu par une fenêtre ». Sans lui, le motif se
        // dessine partout et redevient une dalle posée sur le terrain.
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        MoteCount++;
    }

    /// <summary>
    /// Brume : un quad couvrant l'arène, portant le shader de bruit animé. Son décalage de caméra
    /// est poussé chaque frame — c'est lui qui donne à la brume sa propre vitesse.
    /// </summary>
    private void BuildFog(string? biomeId, Color tint)
    {
        var shader = Resources.Load<Shader>("Shaders/AtmosphereFog");
        if (shader == null)
        {
            Debug.LogWarning("[BiomeAtmosphere] shader de brume introuvable — arène sans brume.");
            return;
        }

        _fogMaterial = new Material(shader);
        _fogMaterial.SetColor("_FogColor", FogColorOf(biomeId, tint));
        _fogMaterial.SetFloat("_Strength", FogStrengthOf(biomeId));
        _fogMaterial.SetFloat("_Parallax", 0.35f);

        var go = new GameObject("Brume", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        // Large marge : la brume doit couvrir la caméra à son excursion maximale, sinon son bord
        // apparaît quand le joueur longe un mur.
        go.transform.localScale = new Vector3(Arena.Width + 1400f, Arena.Height + 1400f, 1f);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = UiPrimitives.White;
        renderer.sharedMaterial = _fogMaterial;
        renderer.sortingOrder = OrderFog;
    }

    /// <summary>
    /// Rais de lumière — même quad plein écran que la brume, mais additif et bien plus lent en
    /// parallaxe (0,15 contre 0,35), pour se lire comme une source lointaine.
    /// </summary>
    private void BuildShafts(string? biomeId, Color tint)
    {
        var shader = Resources.Load<Shader>("Shaders/AtmosphereShafts");
        if (shader == null)
        {
            Debug.LogWarning("[BiomeAtmosphere] shader de rais introuvable — arène sans god-rays.");
            return;
        }

        var (strength, angle) = ShaftsOf(biomeId);

        _shaftMaterial = new Material(shader);
        _shaftMaterial.SetColor("_ShaftColor", tint);
        _shaftMaterial.SetFloat("_Strength", strength);
        _shaftMaterial.SetFloat("_Angle", angle);
        _shaftMaterial.SetFloat("_Parallax", 0.15f);

        var go = new GameObject("Rais", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(Arena.Width + 1400f, Arena.Height + 1400f, 1f);

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = UiPrimitives.White;
        renderer.sharedMaterial = _shaftMaterial;
        renderer.sortingOrder = OrderShafts;
    }

    private void AddDust(string name, float parallax, int count, float size, Color color,
                         float spread, int order)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);

        // ⚠ Une pastille DOUCE. Le premier portage prenait `vfx_particle_noyau` — 3 × 3 px, quasi
        // opaque — agrandi ×7 : de petits carrés nets, qui se lisaient comme des débris posés sur le
        // sol et non comme de la poussière en suspension. Un grain d'atmosphère n'a pas de bord.
        var sprite = SoftDotSprite.Get();

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mote", typeof(SpriteRenderer));
            go.transform.SetParent(root, false);

            go.transform.localPosition = new Vector3(
                ((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfWidth * spread,
                ((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfHeight * spread, 0f);

            go.transform.localScale = Vector3.one * (size * (0.6f + 0.8f * (float)_rng.NextDouble()));

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;

            MoteCount++;
        }

        _layers.Add(new Layer(root, parallax));
    }

    /// <summary>
    /// Décale les couches et pousse la position de caméra à la brume.
    ///
    /// <para><c>LateUpdate</c> et non <c>Update</c> : la caméra suit le joueur dans son propre
    /// <c>LateUpdate</c>, et lire sa position trop tôt fait <b>trembler</b> les couches d'une frame
    /// de retard — un défaut qui ne se voit qu'en mouvement.</para>
    /// </summary>
    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Vector3 camera = _camera.transform.position;

        foreach (var layer in _layers)
        {
            if (layer.Root == null) continue;

            // À parallax = 1 la couche est fixe dans le monde ; en deçà elle traîne, au-delà elle
            // devance. C'est l'écart entre les trois qui donne la profondeur.
            layer.Root.position = new Vector3(camera.x * (1f - layer.Parallax),
                                              camera.y * (1f - layer.Parallax), 0f);
        }

        // La brume suit la caméra (elle est posée dessus) mais son BRUIT est échantillonné en
        // coordonnées écran décalées : sans ce transfert, le motif serait collé à l'objectif.
        // ⚠ Ne JAMAIS déplacer `transform` ici : les couches sont ses enfants et on vient de leur
        // fixer une position MONDE. Bouger le parent après coup les décalerait toutes d'autant.
        var offset = new Vector4(camera.x, camera.y, 0f, 0f);

        if (_fogMaterial   != null) _fogMaterial.SetVector("_CamOffset", offset);
        if (_shaftMaterial != null) _shaftMaterial.SetVector("_CamOffset", offset);
    }

    /// <summary>Teinte des couches — celle du biome, comme le sol et les bords.</summary>
    private static Color TintOf(string? biomeId) => biomeId switch
    {
        "fournaise" => new Color(1.00f, 0.55f, 0.30f),
        "givre"     => new Color(0.60f, 0.85f, 1.00f),
        "neon"      => new Color(0.75f, 0.40f, 1.00f),
        "aether"    => new Color(0.55f, 0.90f, 0.85f),
        _           => new Color(0.45f, 0.60f, 0.80f),
    };

    /// <summary>Couleur et force de la brume par biome — les valeurs du jeu publié.</summary>
    private static Color FogColorOf(string? biomeId, Color fallback) => biomeId switch
    {
        "sanctuaire" => new Color(0.40f, 0.55f, 0.70f),
        "aether"     => new Color(0.55f, 0.42f, 0.85f),
        "fournaise"  => new Color(0.80f, 0.45f, 0.28f),
        "givre"      => new Color(0.60f, 0.80f, 0.92f),
        "neon"       => new Color(0.70f, 0.35f, 0.85f),
        _            => Color.Lerp(fallback, Color.white, 0.3f),
    };

    private static float FogStrengthOf(string? biomeId) => biomeId switch
    {
        "sanctuaire" => 0.20f,
        "aether"     => 0.30f,
        "fournaise"  => 0.32f,
        "givre"      => 0.36f,
        "neon"       => 0.26f,
        _            => 0.20f,
    };

    /// <summary>Force et inclinaison des rais par biome — les valeurs du jeu publié.</summary>
    private static (float Strength, float Angle) ShaftsOf(string? biomeId) => biomeId switch
    {
        "sanctuaire" => (0.16f, 0.55f),
        "aether"     => (0.28f, 0.70f),
        "fournaise"  => (0.32f, 0.85f),
        "givre"      => (0.18f, 0.45f),
        "neon"       => (0.48f, 0.62f),
        _            => (0.14f, 0.60f),
    };

    /// <summary>
    /// ⚠ Générateur PRIVÉ : l'atmosphère ne doit pas décaler les tirages de gameplay d'une campagne
    /// à graine fixe.
    /// </summary>
    private readonly System.Random _rng = new(0xA7305);
}
