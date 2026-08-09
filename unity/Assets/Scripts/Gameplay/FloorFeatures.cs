using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rendu des grandes structures de sol par biome — chemin pavé, rivière d'énergie, coulée de lave,
/// rivière gelée, conduits de données.
///
/// <para><b>Ce qui manquait sans elles.</b> Les cinq arènes ne se distinguaient que par la
/// <b>teinte</b> d'une même tuile répétée. Or c'est le sol qui dit où l'on joue, avant la faune et
/// avant la musique : une coulée de lave qui traverse l'écran et une rivière gelée ne se confondent
/// pas, deux nappes de gris colorées si.</para>
///
/// <para>Le tracé vient de <see cref="FloorFeatureLayout"/> (logique pure, testée) ; cette classe ne
/// fait que colorier des cellules, border des rives et poser des lueurs. Elle rend l'ensemble des
/// cellules occupées : <b>les obstacles et les fenêtres vitrées s'en écartent</b>, comme sous Godot —
/// un pilier planté au milieu d'une rivière, ou un puits de parallaxe ouvert dans de la lave, se
/// lisent tous deux comme un défaut d'assemblage.</para>
///
/// <para>⚠ Les <c>Polygon2D</c> et <c>Line2D</c> de Godot n'ont pas d'équivalent : tout est ici un
/// <c>SpriteRenderer</c> blanc mis à l'échelle. Une cellule = un sprite, une rive = un sprite fin.
/// C'est plus d'objets qu'un maillage, mais ils sont <b>statiques</b> — posés une fois au début de la
/// run et jamais retouchés.</para>
/// </summary>
public static class FloorFeatures
{
    /// <summary>Au-dessus du sol (−100) et de la vitre des fenêtres (−95), sous les bords (−90).</summary>
    private const int CellOrder = -93;

    /// <summary>Rives, flux, fissures : par-dessus les cellules qu'ils bordent.</summary>
    private const int DetailOrder = -92;

    /// <summary>Lueurs additives, en dernier — elles éclairent ce qui est déjà posé.</summary>
    private const int GlowOrder = -91;

    private static readonly int Cols = Arena.Width / FloorFeatureLayout.TileSize;
    private static readonly int Rows = Arena.Height / FloorFeatureLayout.TileSize;

    /// <summary>
    /// Construit la structure du biome sous <paramref name="parent"/> et renvoie les cellules
    /// qu'elle occupe.
    /// </summary>
    public static HashSet<FloorFeatureLayout.Cell> Build(Transform parent, string? biomeId, Color accent,
                                                        Func<float> rand)
    {
        var root = new GameObject("MotifsDeSol");
        root.transform.SetParent(parent, false);

        var layout = FloorFeatureLayout.Build(biomeId, Cols, Rows, rand);

        switch (biomeId)
        {
            case "aether":    PaintAetherRiver(root.transform, layout, accent, rand); break;
            case "fournaise": PaintLavaFlow(root.transform, layout, accent, rand); break;
            case "givre":     PaintFrozenRiver(root.transform, layout, accent, rand); break;
            case "neon":      PaintConduits(root.transform, layout, accent, rand); break;
            default:          PaintPavedPath(root.transform, layout, accent, rand); break;
        }

        return layout.Cells;
    }

    // ─── Sanctuaire : dalles et rune ──────────────────────────────────────────

    private static void PaintPavedPath(Transform root, FloorFeatureLayout.Layout layout, Color accent,
                                       Func<float> rand)
    {
        // ⚠ Les dalles sont posées EN RETRAIT de 2 px. Ce n'est pas un détail d'esthétique : c'est le
        // sol sombre resté visible entre elles qui dessine les joints. Des cellules jointives
        // donneraient une nappe unie, et le pavage disparaîtrait.
        var slab = new Color(0.21f, 0.20f, 0.27f);

        foreach (var cell in layout.Cells)
        {
            Cell(root, cell, Jitter(slab, rand, 0.12f), CellOrder, inset: 2f);

            if (rand() < 0.10f)
            {
                var p = Center(cell);
                Streak(root, p + new Vector2(-4f, -3f), p + new Vector2(3f, 5f),
                       new Color(0.09f, 0.08f, 0.12f), 1.5f);
            }
        }

        if (layout.Hub == null) return;

        // La rune : un anneau gravé au centre du parvis. Un disque plein en ferait une tache ; c'est
        // le contour seul qui se lit comme une gravure.
        var hub = Center(layout.Hub.Value);
        Ring(root, hub, 56f, new Color(accent.r, accent.g, accent.b, 0.35f), 2f);
        Glow(root, hub, accent, 90f, 0.5f);
    }

    // ─── Aether : rivière d'énergie ───────────────────────────────────────────

    private static void PaintAetherRiver(Transform root, FloorFeatureLayout.Layout layout, Color accent,
                                         Func<float> rand)
    {
        Fill(root, layout.Cells, new Color(0.24f, 0.11f, 0.42f), rand, jitter: 0.22f);
        Flow(root, layout.Center, new Color(accent.r, accent.g, accent.b, 0.45f), 6f);
        Banks(root, layout.Cells, new Color(accent.r, accent.g, accent.b, 0.50f), 2f);

        Sparkle(root, layout.Cells, rand, 0.10f, 2f, new Color(0.85f, 0.75f, 1f, 0.7f));
        Glows(root, layout.Center, accent, 0.45f, every: 9);
    }

    // ─── Fournaise : coulée de lave ───────────────────────────────────────────

    private static void PaintLavaFlow(Transform root, FloorFeatureLayout.Layout layout, Color accent,
                                      Func<float> rand)
    {
        Fill(root, layout.Cells, new Color(0.60f, 0.16f, 0.03f), rand, jitter: 0.30f);
        Flow(root, layout.Center, new Color(1f, 0.50f, 0.14f, 0.70f), 8f);

        // La rive doit BRÛLER et non s'éteindre : sur un sol brun, un contour sombre disparaît et la
        // coulée n'a plus de bord — elle se lit comme une tache de terre.
        Banks(root, layout.Cells, new Color(1f, 0.45f, 0.12f, 0.65f), 2.5f);

        Sparkle(root, layout.Cells, rand, 0.14f, 2.5f, Color.Lerp(accent, Color.white, 0.3f));
        Glows(root, layout.Center, new Color(1f, 0.50f, 0.18f), 0.55f, every: 8);
    }

    // ─── Givre : rivière gelée ────────────────────────────────────────────────

    private static void PaintFrozenRiver(Transform root, FloorFeatureLayout.Layout layout, Color accent,
                                         Func<float> rand)
    {
        Fill(root, layout.Cells, new Color(0.24f, 0.36f, 0.44f), rand, jitter: 0.15f);
        Banks(root, layout.Cells, new Color(accent.r, accent.g, accent.b, 0.55f), 2f);

        // Craquelures : ce qui distingue une plaque de glace d'une flaque d'eau. Pas de flux central
        // — une rivière gelée ne coule plus, et c'est exactement ce qu'il faut montrer.
        foreach (var cell in layout.Cells)
        {
            if (rand() >= 0.16f) continue;

            var p = Center(cell) + Offset(rand, 6f);
            Streak(root, p + new Vector2(-10f, -4f), p + new Vector2(4f, -3f),
                   new Color(0.85f, 0.95f, 1f, 0.30f), 1.5f);
            Streak(root, p + new Vector2(4f, -3f), p + new Vector2(11f, 5f),
                   new Color(0.85f, 0.95f, 1f, 0.30f), 1.5f);
        }

        Glows(root, layout.Center, accent, 0.25f, every: 12);
    }

    // ─── Néon : conduits de données ───────────────────────────────────────────

    private static void PaintConduits(Transform root, FloorFeatureLayout.Layout layout, Color accent,
                                      Func<float> rand)
    {
        // Le conduit se CREUSE dans le sol : son lit est plus sombre que tout le reste de l'arène.
        // C'est ce contraste, et non la trace lumineuse, qui donne la profondeur du canal.
        Fill(root, layout.Cells, new Color(0.04f, 0.03f, 0.08f), rand, jitter: 0.10f);

        Flow(root, layout.Center, new Color(accent.r, accent.g, accent.b, 0.85f), 4f);
        Flow(root, layout.Center2, new Color(accent.r, accent.g, accent.b, 0.85f), 4f);

        int lights = 0;
        foreach (var node in layout.Nodes)
        {
            var p = Center(node);

            // ⚠ Le nœud est un CARRÉ, pas un losange. Un anneau à quatre segments part de l'angle
            // zéro : ses sommets tombent sur les axes, et le contour pivote de 45° par rapport à la
            // boîte sombre qu'il est censé border. Les deux formes se contredisaient à l'image.
            Box(root, p, 16f, new Color(0.02f, 0.01f, 0.05f), CellOrder);
            Outline(root, p, 16f, accent, 2f);

            // Budget de lueurs : huit, comme sous Godot. Un nœud par coude sur deux routes en
            // produirait une vingtaine, et l'arène entière virerait à la couleur d'accent.
            if (lights++ < 8) Glow(root, p, accent, 80f, 0.5f);
        }
    }

    // ─── Briques de rendu ─────────────────────────────────────────────────────

    private static void Fill(Transform root, HashSet<FloorFeatureLayout.Cell> cells, Color baseColor,
                             Func<float> rand, float jitter)
    {
        foreach (var cell in cells)
            Cell(root, cell, Jitter(baseColor, rand, jitter), CellOrder, inset: 0f);
    }

    /// <summary>
    /// Trace la rive : un trait sur chaque bord de cellule qui donne sur l'extérieur.
    /// </summary>
    /// <remarks>
    /// C'est ce contour qui sépare la structure du sol. Sans lui, une nappe teintée se lit comme une
    /// variation d'éclairage — le même défaut que les nuages d'aura sans dégradé, à l'envers : ici la
    /// frontière est justement ce qu'il faut affirmer, une rivière ayant des berges.
    /// </remarks>
    private static void Banks(Transform root, HashSet<FloorFeatureLayout.Cell> cells, Color color, float width)
    {
        const int t = FloorFeatureLayout.TileSize;

        foreach (var (cell, dr, dc) in FloorFeatureLayout.Banks(cells))
        {
            var c = Center(cell);

            // dr et dc désignent la cellule ABSENTE : le trait se pose sur ce bord-là.
            var offset = new Vector2(dc * t * 0.5f, -dr * t * 0.5f);
            var size = dc != 0 ? new Vector2(width, t) : new Vector2(t, width);

            Quad(root, c + offset, size, color, DetailOrder);
        }
    }

    /// <summary>Flux central : le cœur lumineux d'une rivière ou d'un conduit.</summary>
    private static void Flow(Transform root, List<FloorFeatureLayout.Cell> path, Color color, float width)
    {
        // ⚠ Sous-échantillonné. Un segment par cellule donnerait une polyligne en escalier de 32 px
        // de marche : le « cours d'eau » se lirait comme un tracé de tableur.
        var thinned = FloorFeatureLayout.Thin(path, 2);

        for (int i = 1; i < thinned.Count; i++)
            Streak(root, Center(thinned[i - 1]), Center(thinned[i]), color, width);
    }

    private static void Sparkle(Transform root, HashSet<FloorFeatureLayout.Cell> cells, Func<float> rand,
                                float chance, float size, Color color)
    {
        foreach (var cell in cells)
            if (rand() < chance)
                Quad(root, Center(cell) + Offset(rand, 10f), new Vector2(size * 2f, size * 2f),
                     color, DetailOrder);
    }

    /// <summary>Segment épais entre deux points — le <c>Line2D</c> de Godot, en un seul quad orienté.</summary>
    private static void Streak(Transform root, Vector2 a, Vector2 b, Color color, float width)
    {
        var delta = b - a;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        var go = Quad(root, (a + b) * 0.5f, new Vector2(length, width), color, DetailOrder);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    /// <summary>Anneau ouvert, tracé segment par segment.</summary>
    private static void Ring(Transform root, Vector2 center, float radius, Color color, float width,
                             int segments = 24)
    {
        var previous = center + new Vector2(radius, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.PI * 2f * i / segments;
            var point = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;

            Streak(root, previous, point, color, width);
            previous = point;
        }
    }

    private static void Box(Transform root, Vector2 center, float size, Color color, int order)
        => Quad(root, center, new Vector2(size, size), color, order);

    /// <summary>Contour d'un carré aligné sur les axes — les quatre côtés, pas un anneau.</summary>
    private static void Outline(Transform root, Vector2 center, float size, Color color, float width)
    {
        float h = size * 0.5f;

        var a = center + new Vector2(-h, -h);
        var b = center + new Vector2(h, -h);
        var c = center + new Vector2(h, h);
        var d = center + new Vector2(-h, h);

        Streak(root, a, b, color, width);
        Streak(root, b, c, color, width);
        Streak(root, c, d, color, width);
        Streak(root, d, a, color, width);
    }

    private static GameObject Cell(Transform root, FloorFeatureLayout.Cell cell, Color color, int order,
                                   float inset)
    {
        float side = FloorFeatureLayout.TileSize - 2f * inset;
        return Quad(root, Center(cell), new Vector2(side, side), color, order);
    }

    /// <summary>
    /// Rectangle plein de <paramref name="size"/> <b>pixels</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ L'échelle passe par la taille réelle du sprite (<c>rect / pixelsPerUnit</c>) et jamais par
    /// <c>rect.width</c> seul : <c>UiPrimitives.White</c> fait 4 px pour un PPU de 4, soit 1 unité —
    /// un facteur 4 d'écart, silencieux, et c'est la quatrième fois que ce portage s'y reprend.
    /// </remarks>
    private static GameObject Quad(Transform root, Vector2 center, Vector2 size, Color color, int order)
    {
        var go = new GameObject("Motif", typeof(SpriteRenderer));
        go.transform.SetParent(root, false);
        go.transform.position = center;

        var sprite = UiPrimitives.White;
        float unitsX = sprite.rect.width / sprite.pixelsPerUnit;
        float unitsY = sprite.rect.height / sprite.pixelsPerUnit;
        go.transform.localScale = new Vector3(size.x / unitsX, size.y / unitsY, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;

        return go;
    }

    private static void Glows(Transform root, List<FloorFeatureLayout.Cell> path, Color color, float energy,
                              int every)
    {
        for (int i = every / 2; i < path.Count; i += every)
            Glow(root, Center(path[i]), color, 72f, energy);
    }

    /// <summary>
    /// Lueur <b>permanente</b> posée sur le sol — le <c>PointLight2D</c> additif de Godot.
    /// </summary>
    /// <remarks>
    /// Un halo additif et non une vraie lumière : ces lumières n'éclairaient rien sous Godot non plus
    /// (aucun sprite du jeu n'est en matériau éclairé), elles ajoutaient de la clarté. Et un halo
    /// statique ne coûte rien, là où <c>VfxGlow</c> est prévu pour s'éteindre.
    /// </remarks>
    private static void Glow(Transform root, Vector2 center, Color color, float radius, float energy)
    {
        var go = new GameObject("Lueur", typeof(SpriteRenderer));
        go.transform.SetParent(root, false);
        go.transform.position = center;

        var sprite = VfxPrimitives.Glow;
        float units = sprite.rect.width / sprite.pixelsPerUnit;
        float scale = radius * 2f / units;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = VfxPrimitives.Additive;
        sr.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(energy));
        sr.sortingOrder = GlowOrder;
    }

    // ─── Repères ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Centre d'une cellule, en coordonnées du monde.
    /// </summary>
    /// <remarks>
    /// ⚠ La ligne 0 est en <b>haut</b>. L'axe Y de Godot descend, celui d'Unity monte : reprendre le
    /// calcul tel quel retournerait toutes les structures — sans que cela se voie, une rivière
    /// symétrique restant une rivière, jusqu'au jour où un tracé asymétrique le trahirait.
    /// </remarks>
    public static Vector2 Center(FloorFeatureLayout.Cell cell)
    {
        const float t = FloorFeatureLayout.TileSize;

        return new Vector2(
            -Arena.HalfWidth + cell.Col * t + t * 0.5f,
            Arena.HalfHeight - cell.Row * t - t * 0.5f);
    }

    /// <summary>Cellule contenant un point du monde — sert à écarter obstacles et fenêtres.</summary>
    public static FloorFeatureLayout.Cell CellAt(Vector2 position)
    {
        const float t = FloorFeatureLayout.TileSize;

        return new FloorFeatureLayout.Cell(
            Mathf.FloorToInt((Arena.HalfHeight - position.y) / t),
            Mathf.FloorToInt((position.x + Arena.HalfWidth) / t));
    }

    private static Color Jitter(Color color, Func<float> rand, float amount)
    {
        float m = 1f + (rand() * 2f - 1f) * amount;
        return new Color(color.r * m, color.g * m, color.b * m, color.a);
    }

    private static Vector2 Offset(Func<float> rand, float amplitude)
        => new((rand() * 2f - 1f) * amplitude, (rand() * 2f - 1f) * amplitude);
}
