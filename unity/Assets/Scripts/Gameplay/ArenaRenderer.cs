using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sol et limites de l'arène (lot de parité visuelle).
///
/// <para><b>Le défaut que ce composant corrige</b> : la zone de jeu était un <b>vide plat</b>. Sans
/// sol, rien ne donne l'échelle ni le sens du déplacement — le joueur bouge sans que le monde ne
/// défile, et le jeu paraît se dérouler dans le noir. Sans bordure visible, on découvre la limite en
/// s'y cognant.</para>
///
/// <para>Le sol se construit en trois couches, et <b>l'ordre compte</b> : la tuile répétée teintée
/// par biome, puis la structure qui traverse l'arène (<see cref="FloorFeatures"/> — coulée de lave,
/// rivière gelée, conduits de données), puis seulement ce qui doit s'en écarter, obstacles et
/// fenêtres vitrées. Une teinte seule ne distingue pas cinq lieux ; une coulée de lave, si.</para>
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

        // Les motifs de sol EN PREMIER : ce sont eux qui décident où le reste n'a pas le droit
        // d'aller. Un pilier planté au milieu d'une coulée de lave, ou un puits de parallaxe ouvert
        // dans une rivière, se lisent l'un comme l'autre comme un défaut d'assemblage — et c'est
        // l'ordre du jeu publié, où le sol interroge `_featureCells` avant de poser quoi que ce soit.
        _featureCells = FloorFeatures.Build(transform, biomeId, border, Gd.Randf);

        BuildObstacles(biomeId, border);

        // Les fenêtres AVANT l'atmosphère : leurs centres décident où poser les motifs profonds.
        var windows = BuildGlassWindows(floor);

        // L'atmosphère vient en dernier : ses couches se placent par rapport à la caméra, pas au
        // sol, et elle doit exister même si le décor manque.
        var atmosphere = gameObject.GetComponent<BiomeAtmosphere>()
                      ?? gameObject.AddComponent<BiomeAtmosphere>();

        atmosphere.Configure(biomeId, windows);

        // La Marée de Rouille s'installe ici, par le même motif que l'atmosphère : elle appartient au
        // décor de l'arène, et l'accrocher au composant qui construit ce décor garantit qu'elle existe
        // dans TOUTE run — jeu, banc, capture — sans qu'un objet de plus soit à poser dans la scène.
        // Elle reste invisible et inerte tant que l'overtime n'a pas commencé.
        if (gameObject.GetComponent<RustTideZone>() == null) gameObject.AddComponent<RustTideZone>();
    }

    /// <summary>
    /// Ouvre trois à quatre <b>fenêtres vitrées</b> dans le sol, et renvoie leurs centres.
    ///
    /// <para><b>C'est par elles que la profondeur se voit.</b> Sous Godot, le sol est une grille de
    /// tuiles dont quelques amas sont remplacés par une tuile « vitre » ; le fond en parallaxe
    /// défile derrière, et l'œil ne le lit comme un lointain que parce qu'il l'aperçoit par une
    /// ouverture. Sans fenêtre, une couche parallaxée posée sur le sol se lit comme du terrain.</para>
    ///
    /// <para>Chaque fenêtre est un <c>SpriteMask</c> — qui découpe la couche profonde — surmonté
    /// d'un reflet de vitre. Le sol lui-même reste une seule surface tuilée : le découper en 2 200
    /// sprites pour ouvrir douze trous coûterait bien plus que ces quatre masques.</para>
    /// </summary>
    private List<Vector2> BuildGlassWindows(Color tint)
    {
        var centers = new List<Vector2>();

        var glass = Resources.Load<Sprite>("Environment/tile_floor_glass");

        // ⚠ Le masque est un CARRÉ PLEIN, jamais la tuile « vitre ». Un `SpriteMask` ne retient que
        // les pixels dont l'alpha dépasse son seuil, et cette tuile est un reflet posé sur du vide :
        // elle est transparente presque partout. S'en servir comme forme découpait la fenêtre à la
        // taille du seul trait de reflet — le motif profond ne se voyait donc nulle part, et le
        // défaut se lisait « il n'y a rien au fond ».
        var mask = UiPrimitives.White;

        int count = 3 + (int)(Gd.Randf() * 2f);

        for (int i = 0; i < count; i++)
        {
            // Loin des bords : une fenêtre collée au mur se découvre en s'y acculant, c'est-à-dire
            // au pire moment — et la caméra, bornée par l'arène, ne la centre jamais.
            //
            // Et loin des motifs de sol : vingt essais, comme sous Godot. Un puits ouvert au milieu
            // d'une rivière la percerait, et la structure la plus visible du biome se retrouverait
            // trouée sans raison lisible.
            Vector2 center = Vector2.zero;
            bool placed = false;

            for (int attempt = 0; attempt < 20 && !placed; attempt++)
            {
                center = new Vector2(
                    (Gd.Randf() * 2f - 1f) * (Arena.HalfWidth - WindowMargin),
                    (Gd.Randf() * 2f - 1f) * (Arena.HalfHeight - WindowMargin));

                // Et à l'écart des fenêtres déjà posées. Trois puits superposés ne font pas trois
                // ouvertures : ils font une tache d'hexagones enchevêtrés, où la profondeur qu'ils
                // servent à montrer devient illisible. Le défaut s'est aggravé en écartant les
                // fenêtres des motifs — il reste moins de place, donc plus de collisions.
                placed = !AreaOnFeature(center, WindowSize * 1.5f) && FarFromAll(center, centers);
            }

            // Après vingt refus, on pose quand même : mieux vaut une fenêtre mal placée que trois
            // fenêtres au lieu de quatre — c'est par elles que la parallaxe se voit.
            centers.Add(center);

            // Un amas de 2 à 3 tuiles de côté, comme sous Godot.
            float size = WindowSize * (1f + 0.5f * Gd.Randf());

            var maskGo = new GameObject("FenetreMasque", typeof(SpriteMask));
            maskGo.transform.SetParent(transform, false);
            maskGo.transform.position = center;

            // ⚠ Le masque se dimensionne, jamais à main levée. Un `localScale = size` posé sur la
            // tuile « vitre » (64 px, PPU 1) donnait un masque de 6 144 unités — plus large que
            // l'arène — et les motifs devenaient visibles PARTOUT : le défaut se lisait « le masque
            // ne marche pas » alors qu'il marchait trop.
            maskGo.transform.localScale = ScaleToPixels(mask, size);

            var spriteMask = maskGo.GetComponent<SpriteMask>();
            spriteMask.sprite = mask;

            // ⚠ Sans ces bornes, le masque s'applique à TOUS les ordres de tri — il découperait
            // aussi le sol, les entités et les effets. Elles le limitent à la couche profonde.
            spriteMask.isCustomRangeActive = true;
            spriteMask.frontSortingOrder = -96;
            spriteMask.backSortingOrder = -99;

            // ⚠ LE FOND DU PUITS, et c'est lui qui fait le trou. Le sol est une surface pleine :
            // poser les motifs « dessous » les rendrait simplement invisibles. On perce donc en
            // dessinant PAR-DESSUS le sol un aplat très sombre, aux dimensions de la fenêtre — le
            // motif s'y détache, et le masque l'empêche de déborder ailleurs.
            var wellGo = new GameObject("FenetrePuits", typeof(SpriteRenderer));
            wellGo.transform.SetParent(transform, false);
            wellGo.transform.position = center;
            wellGo.transform.localScale = ScaleToPixels(UiPrimitives.White, size);

            var wellRenderer = wellGo.GetComponent<SpriteRenderer>();
            wellRenderer.sprite = UiPrimitives.White;
            // Assez sombre pour se lire comme un trou, assez clair pour que le glyphe s'y détache :
            // un fond quasi noir avalait le motif au lieu de le révéler.
            wellRenderer.color = new Color(tint.r * 0.10f, tint.g * 0.11f, tint.b * 0.22f, 1f);
            wellRenderer.sortingOrder = -99;

            if (glass == null) continue;

            // Reflet de la vitre, par-dessus le sol : sans lui, la fenêtre n'a pas de bord et le
            // motif profond paraît flotter au milieu du terrain.
            var glassGo = new GameObject("FenetreVitre", typeof(SpriteRenderer));
            glassGo.transform.SetParent(transform, false);
            glassGo.transform.position = center;
            glassGo.transform.localScale = ScaleToPixels(glass, size);

            var renderer = glassGo.GetComponent<SpriteRenderer>();
            renderer.sprite = glass;
            renderer.color = new Color(tint.r, tint.g, tint.b, 0.55f);
            renderer.sortingOrder = -95;
        }

        return centers;
    }

    /// <summary>
    /// Échelle donnant à <paramref name="sprite"/> un côté de <paramref name="pixels"/> px.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Diviser par <c>rect.width</c> seul est faux</b>, et le faux est silencieux. La taille
    /// monde d'un sprite vaut <c>rect.width / pixelsPerUnit</c> : sur les tuiles du jeu (PPU 1) les
    /// deux coïncident, sur <c>UiPrimitives.White</c> (4 px, PPU 4) le rapport est de 4. Un masque
    /// calculé ainsi mesurait <b>48 px pour 190 demandés</b> et rognait le glyphe qu'il devait
    /// révéler — on ne voyait plus qu'un fragment de trait, et le défaut se lisait « le motif est mal
    /// dessiné » alors qu'il était juste découpé trop court.
    /// </remarks>
    private static Vector3 ScaleToPixels(Sprite sprite, float pixels)
    {
        float unitsX = sprite.rect.width  / sprite.pixelsPerUnit;
        float unitsY = sprite.rect.height / sprite.pixelsPerUnit;

        return new Vector3(pixels / unitsX, pixels / unitsY, 1f);
    }

    /// <summary>Côté d'une fenêtre vitrée, en pixels — deux à trois tuiles, comme le jeu publié.</summary>
    /// ⚠ Un amas de 2 tuiles de 64 px, pas une tuile isolée. À 96 px la fenêtre était PLUS PETITE
    /// que le glyphe qu'elle est censée révéler (175-250 px) : on n'en apercevait qu'un fragment de
    /// trait au milieu du vide, ce qui se lit « la fenêtre est vide » et non « il y a une structure
    /// au fond ». Ce que la fenêtre montre se calibre sur ce qu'il y a derrière.
    private const float WindowSize = 128f;

    /// <summary>Distance minimale d'une fenêtre au bord de l'arène.</summary>
    private const float WindowMargin = 260f;

    /// <summary>Écart minimal entre deux fenêtres — deux fois leur côté, pour qu'on les compte.</summary>
    private const float WindowSpacing = WindowSize * 2f;

    private static bool FarFromAll(Vector2 candidate, List<Vector2> placed)
    {
        foreach (var other in placed)
            if (Vector2.Distance(candidate, other) < WindowSpacing) return false;

        return true;
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
            var position = NudgeOffFeature(new Vector2(spot.X, spot.Y));
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

            // ⚠ EN DERNIER, une fois la position posée : le composant capture sa pose de repos dès
            // son activation, et l'ajouter plus haut figerait l'origine de la scène comme place
            // d'origine du pilier — qui y retournerait à la première déformation.
            //
            // Le blocage ne passe pas par ce transform (il vit dans ArenaObstacles, plus bas) : un
            // pilier penché par une singularité bloque donc exactement là où il bloquait.
            go.AddComponent<SpaceDistortion>();
        }

        ArenaObstacles.Set(centers);
    }

    /// <summary>Cellules couvertes par les motifs de sol — obstacles et fenêtres s'en écartent.</summary>
    private HashSet<FloorFeatureLayout.Cell> _featureCells = new();

    /// <summary>Emprise au sol d'un obstacle, en pixels — la largeur de son sprite, pas son rayon.</summary>
    private const float ObstacleFootprint = 32f;

    /// <summary>Ce point tombe-t-il sur une structure de sol ?</summary>
    private bool OnFeature(Vector2 position) => _featureCells.Contains(FloorFeatures.CellAt(position));

    /// <summary>
    /// Un carré de <paramref name="size"/> pixels centré ici touche-t-il une structure de sol ?
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Tester le seul centre ne suffit pas</b>, et la capture l'a montré tout de suite : une
    /// fenêtre vitrée de 128 px dont le centre tombe à côté d'une rivière la chevauche quand même sur
    /// la moitié de sa surface. Le point passe le contrôle, la forme non — c'est la même erreur que
    /// juger une silhouette sur sa position.
    /// </remarks>
    private bool AreaOnFeature(Vector2 center, float size)
    {
        float h = size * 0.5f;

        for (float dx = -h; dx <= h; dx += FloorFeatureLayout.TileSize)
        for (float dy = -h; dy <= h; dy += FloorFeatureLayout.TileSize)
            if (OnFeature(center + new Vector2(dx, dy))) return true;

        return OnFeature(center + new Vector2(h, h)) || OnFeature(center + new Vector2(-h, -h));
    }

    /// <summary>
    /// Décale un obstacle verticalement jusqu'à sortir des motifs de sol.
    /// </summary>
    /// <remarks>
    /// <para>Verticalement seulement, et par rangées entières : les gabarits d'obstacles sont
    /// <b>symétriques</b> (<see cref="ArenaLayout"/>) et un décalage horizontal casserait la
    /// composition qu'ils dessinent. Rivières et conduits étant essentiellement horizontaux, quelques
    /// rangées suffisent presque toujours à en sortir.</para>
    ///
    /// <para>Si rien ne convient, l'obstacle reste où il était : sa position d'origine a été choisie
    /// pour ouvrir un couloir, et ce rôle-là prime sur la propreté du décor.</para>
    /// </remarks>
    private Vector2 NudgeOffFeature(Vector2 position)
    {
        if (!AreaOnFeature(position, ObstacleFootprint)) return position;

        // La liste va plus loin que les ±192 px du jeu publié. Une rivière large de trois tuiles
        // flanquée d'une poche de glace couvre près de 250 px : sous Godot, un pilier sur trois
        // restait donc planté dans le courant, et la première capture du portage l'a montré tout de
        // suite. Deux rangées de plus suffisent à sortir du cas le plus large.
        foreach (float dy in new[] { 64f, -64f, 128f, -128f, 192f, -192f, 256f, -256f, 320f, -320f })
        {
            float y = Mathf.Clamp(position.y + dy, -Arena.HalfHeight + 64f, Arena.HalfHeight - 64f);
            var moved = new Vector2(position.x, y);

            if (!AreaOnFeature(moved, ObstacleFootprint)) return moved;
        }

        return position;
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
