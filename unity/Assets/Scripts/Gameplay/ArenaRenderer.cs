using UnityEngine;

/// <summary>
/// Sol et limites de l'arène (lot de parité visuelle).
///
/// <para><b>Le défaut que ce composant corrige</b> : la zone de jeu était un <b>vide plat</b>. Sans
/// sol, rien ne donne l'échelle ni le sens du déplacement — le joueur bouge sans que le monde ne
/// défile, et le jeu paraît se dérouler dans le noir. Sans bordure visible, on découvre la limite en
/// s'y cognant.</para>
///
/// <para>Le rendu reste volontairement sobre : une tuile de sol répétée, teintée par biome, et un
/// cadre lumineux sur les quatre bords. Les décors, obstacles et reliefs du jeu d'origine
/// (<c>BiomeObstacles</c>, <c>FloorFeatures</c>) restent à porter — mais un sol uni vaut infiniment
/// mieux qu'aucun sol.</para>
/// </summary>
public sealed class ArenaRenderer : MonoBehaviour
{
    /// <summary>Épaisseur du liseré qui matérialise la limite, en pixels.</summary>
    private const float BorderThickness = 6f;

    private void Start() => Build(GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId);

    /// <summary>Construit le sol et les bords pour un biome.</summary>
    public void Build(string? biomeId)
    {
        var (floor, border) = PaletteFor(biomeId);

        BuildFloor(floor);

        // Quatre bandes plutôt qu'un cadre dessiné : elles restent nettes à toute résolution et ne
        // demandent aucun shader.
        float w = Arena.HalfWidth, h = Arena.HalfHeight;
        BuildBar("BordHaut",   new Vector2(0f,  h), new Vector2(Arena.Width, BorderThickness), border);
        BuildBar("BordBas",    new Vector2(0f, -h), new Vector2(Arena.Width, BorderThickness), border);
        BuildBar("BordGauche", new Vector2(-w, 0f), new Vector2(BorderThickness, Arena.Height), border);
        BuildBar("BordDroite", new Vector2( w, 0f), new Vector2(BorderThickness, Arena.Height), border);

        BuildObstacles(biomeId, border);

        // L'atmosphère vient EN DERNIER : ses couches se placent par rapport à la caméra, pas au
        // sol, et elle doit exister même si le décor manque.
        var atmosphere = gameObject.GetComponent<BiomeAtmosphere>()
                      ?? gameObject.AddComponent<BiomeAtmosphere>();

        atmosphere.Configure(biomeId);
    }

    /// <summary>
    /// Obstacles : quelques masses lisibles qui créent des couloirs et des angles morts. Leur
    /// disposition vient de <see cref="ArenaLayout"/> (logique pure, testée) et ne dépend que de la
    /// graine — deux runs de même graine donnent la même arène.
    /// </summary>
    private void BuildObstacles(string? biomeId, Color accent)
    {
        var sprite = Resources.Load<Sprite>(DecorFor(biomeId));
        var centers = new System.Collections.Generic.List<Vector2>();

        foreach (var spot in ArenaLayout.Positions(Gd.Randf, Arena.HalfWidth, Arena.HalfHeight))
        {
            var position = new Vector2(spot.X, spot.Y);
            centers.Add(position);

            var go = new GameObject("Obstacle", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);

            // ⚠ AUCUNE mise à l'échelle. Les sprites du monde sont importés à 1 px = 1 unité : le
            // pilier fait déjà 32 × 64 px, la taille de sa silhouette d'origine. Le portage lui
            // appliquait un facteur 2 « pour des masses lisibles », ce qui donnait 64 × 128 px — le
            // double du joueur, et surtout <b>deux fois et demie</b> la zone qu'il bloque.
            //
            // Ancrage à la BASE, comme sous Godot : le point bloquant est au pied de l'obstacle, et
            // la silhouette monte au-dessus. Centrer le sprite sur ce point le faisait déborder
            // autant vers le bas que vers le haut, si bien que le joueur — arrêté à 26 px du
            // centre — se retrouvait recouvert par le décor de tous les côtés. C'est le « le joueur
            // passe dessous » : le corps était bien bloqué, c'est le dessin qui l'avalait.
            float half = sprite != null ? sprite.rect.height * 0.5f : 0f;
            go.transform.position = position + new Vector2(0f, half - FootOffset);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : UiPrimitives.White;
            sr.color = sprite != null ? Color.white : accent;

            if (sprite == null) go.transform.localScale = new Vector3(2f * ArenaLayout.BlockRadius,
                                                                     2f * ArenaLayout.BlockRadius, 1f);

            // Au-dessus des entités : un obstacle « infranchissable » doit OCCULTER ce qui passe
            // derrière, sinon il se lit comme un décor au sol qu'on peut survoler. C'est le
            // ZIndex 6 de Godot, contre 5 pour le joueur.
            sr.sortingOrder = 20;
        }

        ArenaObstacles.Set(centers);
    }

    /// <summary>
    /// De combien la base du sprite descend sous le point bloquant, en pixels. Reprend le décalage
    /// du collider de Godot (<c>CollisionShape2D.Position.Y = 14</c>) : le pied de l'obstacle est
    /// posé un peu plus bas que son centre de blocage, sinon la silhouette paraît flotter.
    /// </summary>
    private const float FootOffset = 14f;

    /// <summary>Décor d'obstacle propre au biome, avec repli sur le pilier de pierre.</summary>
    private static string DecorFor(string? biomeId) => biomeId switch
    {
        "fournaise" => "Environment/tile_wreck_machine",
        "neon"      => "Environment/tile_terminal_corrupt_01",
        "aether"    => "Environment/decor_column",
        _           => "Environment/tile_pillar_stone",
    };

    /// <summary>
    /// Sol : la tuile de pierre répétée sur toute l'arène. <c>SpriteDrawMode.Tiled</c> évite d'avoir à
    /// instancier des milliers de tuiles — 1920 × 1216 en pas de 32 en demanderait plus de 2 200.
    /// </summary>
    private void BuildFloor(Color tint)
    {
        var sprite = Resources.Load<Sprite>("Environment/tile_floor_stone");

        var go = new GameObject("SolArene", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.color = tint;

        // Sous absolument tout le reste : orbes (5), faune (10), champions (12), joueur (15).
        sr.sortingOrder = -100;

        if (sprite != null)
        {
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(Arena.Width, Arena.Height);
        }
        else
        {
            // Repli : un aplat. Une tuile manquante ne doit pas ramener le vide noir de départ.
            Debug.LogWarning("[ArenaRenderer] tuile de sol introuvable — aplat uni.");
            sr.sprite = UiPrimitives.White;
            go.transform.localScale = new Vector3(Arena.Width, Arena.Height, 1f);
        }
    }

    private void BuildBar(string name, Vector2 center, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = UiPrimitives.White;
        sr.color = color;
        sr.sortingOrder = -90;   // au-dessus du sol, sous les entités
    }

    /// <summary>
    /// Teintes par biome — le Sanctuaire froid et neutre, la Fournaise chaude, le Givre bleuté, le
    /// Néon violet. La couleur du sol <b>dit où l'on joue</b> avant même que la faune n'apparaisse.
    ///
    /// <para>⚠ Ces valeurs <b>multiplient</b> la tuile, qui est déjà sombre et texturée. Les choisir
    /// sombres (premier essai à 0,15) écrase le motif jusqu'au noir : le sol redevient alors le vide
    /// plat qu'on cherchait à supprimer. Elles restent donc proches de 1.</para>
    /// </summary>
    private static (Color Floor, Color Border) PaletteFor(string? biomeId) => biomeId switch
    {
        "fournaise" => (new Color(1.00f, 0.72f, 0.58f), new Color(1f, 0.45f, 0.20f, 0.75f)),
        "givre"     => (new Color(0.72f, 0.88f, 1.00f), new Color(0.55f, 0.85f, 1f, 0.75f)),
        "neon"      => (new Color(0.86f, 0.70f, 1.00f), new Color(0.75f, 0.35f, 1f, 0.75f)),
        "aether"    => (new Color(0.72f, 1.00f, 0.94f), new Color(0.30f, 1f, 0.85f, 0.75f)),
        _           => (new Color(0.88f, 0.90f, 1.00f), new Color(0.35f, 0.85f, 0.85f, 0.75f)),
    };
}
