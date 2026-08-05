using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Couches d'atmosphère de l'arène — <b>parallaxe</b> de poussière et de motifs profonds.
///
/// <para><b>Ce qu'elle apporte</b> : sans elle, le sol est une texture qui glisse d'un bloc sous le
/// joueur, et l'arène paraît plate. La profondeur ne vient pas d'un dégradé mais du <b>décalage
/// relatif</b> entre des couches qui ne suivent pas la caméra au même rythme — c'est ce décalage,
/// et lui seul, que l'œil lit comme de l'espace.</para>
///
/// <para>Trois couches, aux facteurs du jeu publié (<c>BiomeAtmosphere</c>) : un motif très lointain
/// (0,06 — presque immobile), une poussière lointaine (0,55) et une poussière de premier plan
/// (1,35, qui <b>devance</b> la caméra). Un facteur supérieur à 1 n'est pas une erreur : c'est ce
/// qui place une couche <i>devant</i> le plan de jeu.</para>
///
/// <para>⚠ <b>Ce qui n'est PAS porté</b>, et qui demanderait des shaders : la brume animée et les
/// rais de lumière. Ils tiennent à un échantillonnage par fragment, sans équivalent gratuit ici. La
/// parallaxe, elle, ne demande qu'un déplacement par frame — c'est la part qui produit la
/// profondeur.</para>
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

    private const float MotifParallax    = 0.06f;   // presque immobile : le fond du décor
    private const float DustFarParallax  = 0.55f;   // < 1 : lointain, suit moins la caméra
    private const float DustNearParallax = 1.35f;   // > 1 : premier plan, devance la caméra

    private readonly List<Layer> _layers = new();
    private Camera? _camera;

    /// <summary>Nombre de particules posées — observable pour les vérifications.</summary>
    public int MoteCount { get; private set; }

    /// <summary>Construit les couches pour un biome.</summary>
    public void Configure(string? biomeId)
    {
        _camera = Camera.main;

        foreach (var layer in _layers)
            if (layer.Root != null) Destroy(layer.Root.gameObject);

        _layers.Clear();
        MoteCount = 0;

        var tint = TintOf(biomeId);

        // Le motif profond est plus large que l'arène : à 0,06 il bouge à peine, mais la caméra le
        // parcourt tout de même — s'il s'arrêtait aux murs, on verrait son bord.
        //
        // ⚠ Volontairement DISCRET (7 % d'opacité). Plus appuyé, ses grandes formes se lisent comme
        // des dalles posées sur le sol et non comme une profondeur derrière lui — l'œil les prend
        // alors pour des éléments de terrain, et cherche à les contourner.
        AddLayer("MotifProfond", MotifParallax, 26, 36f, new Color(tint.r, tint.g, tint.b, 0.07f),
                 spread: 1.6f, order: -70);

        AddLayer("PoussiereLointaine", DustFarParallax, 34, 7f,
                 new Color(tint.r, tint.g, tint.b, 0.34f), spread: 1.2f, order: -60);

        AddLayer("PoussiereProche", DustNearParallax, 18, 11f,
                 new Color(tint.r, tint.g, tint.b, 0.24f), spread: 1.0f, order: 18);
    }

    private void AddLayer(string name, float parallax, int count, float size, Color color,
                          float spread, int order)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);

        var sprite = Resources.Load<Sprite>("Vfx/vfx_particle_noyau") ?? UiPrimitives.White;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mote", typeof(SpriteRenderer));
            go.transform.SetParent(root, false);

            go.transform.localPosition = new Vector3(
                ((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfWidth * spread,
                ((float)_rng.NextDouble() * 2f - 1f) * Arena.HalfHeight * spread, 0f);

            float scale = size * (0.6f + 0.8f * (float)_rng.NextDouble());
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;

            MoteCount++;
        }

        _layers.Add(new Layer(root, parallax));
    }

    /// <summary>
    /// Décale chaque couche. <c>LateUpdate</c> et non <c>Update</c> : la caméra suit le joueur dans
    /// son propre <c>LateUpdate</c>, et lire sa position trop tôt ferait <b>trembler</b> les couches
    /// d'une frame de retard — le défaut classique de la parallaxe, et il ne se voit qu'en mouvement.
    /// </summary>
    private void LateUpdate()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Vector3 camera = _camera.transform.position;

        foreach (var layer in _layers)
        {
            if (layer.Root == null) continue;

            // À parallax = 1 la couche est fixe dans le monde ; en deçà elle « traîne », au-delà
            // elle devance. C'est l'écart entre les trois qui donne la profondeur.
            layer.Root.position = new Vector3(camera.x * (1f - layer.Parallax),
                                              camera.y * (1f - layer.Parallax), 0f);
        }
    }

    /// <summary>Teinte de l'atmosphère — celle du biome, comme le sol et les bords.</summary>
    private static Color TintOf(string? biomeId) => biomeId switch
    {
        "fournaise" => new Color(1.00f, 0.55f, 0.30f),
        "givre"     => new Color(0.60f, 0.85f, 1.00f),
        "neon"      => new Color(0.75f, 0.40f, 1.00f),
        "aether"    => new Color(0.55f, 0.90f, 0.85f),
        _           => new Color(0.45f, 0.60f, 0.80f),
    };

    /// <summary>
    /// ⚠ Générateur PRIVÉ : l'atmosphère ne doit pas décaler les tirages de gameplay d'une campagne
    /// à graine fixe.
    /// </summary>
    private readonly System.Random _rng = new(0xA7305);
}
