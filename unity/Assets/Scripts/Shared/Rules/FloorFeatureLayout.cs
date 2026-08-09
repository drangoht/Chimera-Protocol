using System;
using System.Collections.Generic;

/// <summary>
/// Tracé des grandes structures de sol par biome — <b>géométrie seule</b>, sans moteur.
///
/// <para>Chemin pavé du Sanctuaire, rivière d'Aether, coulée de lave, rivière gelée, conduits de
/// données : ce sont ces formes qui donnent au terrain la lecture immédiate du lieu où l'on joue.
/// Sans elles, les cinq biomes ne se distinguent que par la <b>teinte</b> d'une même tuile répétée —
/// c'est-à-dire à peine.</para>
///
/// <para><b>Pourquoi la géométrie vit ici et le dessin ailleurs.</b> Sous Godot, tracé et rendu
/// étaient mêlés dans une seule classe de nœuds : impossible de vérifier qu'une rivière traverse
/// bien l'arène, qu'elle reste dans la zone visible ou qu'un chemin ne sort pas de la grille — il
/// fallait regarder. Séparés, ces invariants deviennent des tests, et le dessin n'a plus qu'à
/// colorier des cellules.</para>
///
/// <para>L'aléatoire est <b>injecté</b> (<c>Func&lt;float&gt;</c> rendant [0,1[), comme partout dans
/// <c>Rules</c> : la couche pure ne connaît ni <c>UnityEngine.Random</c> ni <c>GD</c>, et une graine
/// fixée redonne la même arène.</para>
/// </summary>
public static class FloorFeatureLayout
{
    /// <summary>Côté d'une cellule de la grille de sol, en pixels.</summary>
    public const int TileSize = 32;

    /// <summary>Une cellule de la grille, repérée par sa ligne et sa colonne.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int Row;
        public readonly int Col;

        public Cell(int row, int col) { Row = row; Col = col; }

        public bool Equals(Cell other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object? obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => Row * 397 ^ Col;
        public override string ToString() => $"({Row},{Col})";
    }

    /// <summary>Ce que le rendu a besoin de savoir : où peindre, où tracer, où poser les lueurs.</summary>
    public sealed class Layout
    {
        /// <summary>Cellules couvertes par la structure — c'est aussi ce qui <b>interdit</b> le reste.</summary>
        public readonly HashSet<Cell> Cells = new();

        /// <summary>Axe de la structure, cellule par cellule le long du parcours.</summary>
        public readonly List<Cell> Center = new();

        /// <summary>Second axe, pour les biomes qui en ont deux (les conduits du Néon se croisent).</summary>
        public readonly List<Cell> Center2 = new();

        /// <summary>Points remarquables à illuminer : coudes, nœuds, jonctions.</summary>
        public readonly List<Cell> Nodes = new();

        /// <summary>Place centrale, s'il y en a une (le parvis du Sanctuaire et sa rune).</summary>
        public Cell? Hub;
    }

    /// <summary>
    /// Trace la structure d'un biome sur une grille de <paramref name="cols"/> × <paramref name="rows"/>.
    /// </summary>
    /// <param name="rand">Source d'aléa rendant [0,1[.</param>
    public static Layout Build(string? biomeId, int cols, int rows, Func<float> rand) => biomeId switch
    {
        "aether"    => River(cols, rows, rand, blobs: 1, blobRadius: 2.4f),
        "fournaise" => River(cols, rows, rand, blobs: 1, blobRadius: 2.6f),
        "givre"     => River(cols, rows, rand, blobs: 2, blobRadius: 2.2f),
        "neon"      => Conduits(cols, rows, rand),
        _           => PavedPath(cols, rows, rand),
    };

    // ─── Sanctuaire : un chemin pavé et son parvis ────────────────────────────

    private static Layout PavedPath(int cols, int rows, Func<float> rand)
    {
        var layout = OrthoPath(cols, rows, rand, width: 2, horizontal: true);

        // La place se pose sur un COUDE, pas au hasard : un élargissement au milieu d'une ligne
        // droite se lit comme un défaut de tracé, sur un virage comme un carrefour.
        var hub = layout.Nodes.Count > 0 ? layout.Nodes[layout.Nodes.Count / 2] : new Cell(rows / 2, cols / 2);
        AddBlob(layout.Cells, hub, 3.2f, cols, rows);
        layout.Hub = hub;

        return layout;
    }

    // ─── Aether, Fournaise, Givre : une rivière ───────────────────────────────

    /// <summary>
    /// Rivière sinueuse traversant l'arène de gauche à droite.
    /// </summary>
    /// <remarks>
    /// ⚠ La dérive est <b>biaisée vers le centre vertical</b>. Sans ce rappel, une marche aléatoire
    /// part sur un bord et y reste : la rivière longe alors le mur, hors du champ de la caméra qui
    /// suit le joueur, et le biome perd la seule chose qui le distingue à l'œil.
    /// </remarks>
    private static Layout River(int cols, int rows, Func<float> rand, int blobs, float blobRadius)
    {
        var layout = new Layout();

        float row = Range(rand, 9f, rows - 9f);
        float drift = Range(rand, -0.5f, 0.5f);
        float width = Range(rand, 1.6f, 2.4f);

        for (int col = 0; col < cols; col++)
        {
            float bias = (rows / 2f - row) * 0.014f;
            drift = Clamp(drift + Range(rand, -0.30f, 0.30f) + bias, -1.1f, 1.1f);
            row = Clamp(row + drift, 4f, rows - 5f);
            width = Clamp(width + Range(rand, -0.20f, 0.20f), 1.4f, 3.0f);

            int r0 = (int)MathF.Floor(row - width / 2f);
            int r1 = (int)MathF.Ceiling(row + width / 2f);

            for (int r = r0; r <= r1; r++)
                if (r >= 1 && r < rows - 1)
                    layout.Cells.Add(new Cell(r, col));

            layout.Center.Add(new Cell((int)MathF.Round(row), col));
        }

        // Poches annexes : plaques de glace, mares de lave, bassins d'énergie. Elles cassent la
        // lecture « un ruban traverse l'écran » et donnent au biome une seconde échelle.
        for (int i = 0; i < blobs; i++)
        {
            var center = new Cell((int)Range(rand, 6f, rows - 7f), (int)Range(rand, 10f, cols - 11f));
            AddBlob(layout.Cells, center, blobRadius, cols, rows);
        }

        return layout;
    }

    // ─── Néon : deux conduits qui se croisent ─────────────────────────────────

    private static Layout Conduits(int cols, int rows, Func<float> rand)
    {
        var horizontal = OrthoPath(cols, rows, rand, width: 1, horizontal: true);
        var vertical = OrthoPath(cols, rows, rand, width: 1, horizontal: false);

        var layout = new Layout();
        layout.Cells.UnionWith(horizontal.Cells);
        layout.Cells.UnionWith(vertical.Cells);
        layout.Center.AddRange(horizontal.Center);
        layout.Center2.AddRange(vertical.Center);

        layout.Nodes.AddRange(horizontal.Nodes);
        layout.Nodes.AddRange(vertical.Nodes);

        // La jonction des deux conduits : le seul nœud qui ne soit pas un simple coude, et celui
        // que l'œil cherche.
        foreach (var cell in horizontal.Cells)
            if (vertical.Cells.Contains(cell)) { layout.Nodes.Add(cell); break; }

        return layout;
    }

    // ─── Tracés ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Chemin orthogonal : des segments droits et des coudes à angle droit, d'un bord à l'autre.
    /// </summary>
    /// <remarks>
    /// Les coudes sont biaisés vers le centre latéral (65 %) pour la même raison que la dérive de la
    /// rivière : un chemin qui s'échappe vers un bord n'est jamais vu.
    /// </remarks>
    private static Layout OrthoPath(int cols, int rows, Func<float> rand, int width, bool horizontal)
    {
        var layout = new Layout();

        int length = horizontal ? cols : rows;
        int lateral = horizontal ? rows : cols;

        int v = (int)Range(rand, 8f, lateral - 8f);
        int u = 0;

        void Stamp(int uu, int vv)
        {
            for (int a = 0; a < width; a++)
            for (int b = 0; b < width; b++)
            {
                int r = horizontal ? vv + a : uu + b;
                int c = horizontal ? uu + b : vv + a;

                if (r >= 1 && r < rows - 1 && c >= 1 && c < cols - 1)
                    layout.Cells.Add(new Cell(r, c));
            }

            layout.Center.Add(horizontal ? new Cell(vv, uu) : new Cell(uu, vv));
        }

        while (u < length)
        {
            int run = 7 + Int(rand, 6);
            for (int s = 0; s < run && u < length; s++, u++)
                Stamp(u, v);

            if (u >= length) break;

            layout.Nodes.Add(horizontal ? new Cell(v, u - 1) : new Cell(u - 1, v));

            int dir = rand() < 0.65f
                ? (v < lateral / 2 ? 1 : -1)
                : (Int(rand, 2) == 0 ? 1 : -1);

            int target = Clamp(v + dir * (3 + Int(rand, 5)), 5, lateral - 6);
            int step = target > v ? 1 : -1;

            while (v != target)
            {
                v += step;
                Stamp(u - 1, v);
            }
        }

        return layout;
    }

    /// <summary>Disque de cellules — parvis, mare, plaque de glace.</summary>
    private static void AddBlob(HashSet<Cell> cells, Cell center, float radius, int cols, int rows)
    {
        int ri = (int)MathF.Ceiling(radius);

        for (int r = center.Row - ri; r <= center.Row + ri; r++)
        for (int c = center.Col - ri; c <= center.Col + ri; c++)
        {
            if (r < 1 || r >= rows - 1 || c < 1 || c >= cols - 1) continue;

            float dr = r - center.Row, dc = c - center.Col;
            if (MathF.Sqrt(dr * dr + dc * dc) <= radius) cells.Add(new Cell(r, c));
        }
    }

    // ─── Utilitaires ──────────────────────────────────────────────────────────

    /// <summary>Bords d'une cellule sans voisine dans l'ensemble — c'est là que se dessine la rive.</summary>
    public static IEnumerable<(Cell Cell, int Dr, int Dc)> Banks(HashSet<Cell> cells)
    {
        foreach (var cell in cells)
        {
            if (!cells.Contains(new Cell(cell.Row - 1, cell.Col))) yield return (cell, -1, 0);
            if (!cells.Contains(new Cell(cell.Row + 1, cell.Col))) yield return (cell, 1, 0);
            if (!cells.Contains(new Cell(cell.Row, cell.Col - 1))) yield return (cell, 0, -1);
            if (!cells.Contains(new Cell(cell.Row, cell.Col + 1))) yield return (cell, 0, 1);
        }
    }

    /// <summary>
    /// Sous-échantillonne un axe : un point par cellule donne une polyligne en escalier, qui se lit
    /// comme un tracé cassé et non comme un cours d'eau.
    /// </summary>
    public static List<Cell> Thin(List<Cell> path, int every)
    {
        var result = new List<Cell>(path.Count / Math.Max(1, every) + 1);
        if (path.Count == 0) return result;

        for (int i = 0; i < path.Count; i += every) result.Add(path[i]);
        if (!result[result.Count - 1].Equals(path[path.Count - 1])) result.Add(path[path.Count - 1]);

        return result;
    }

    private static float Range(Func<float> rand, float min, float max) => min + rand() * (max - min);

    private static int Int(Func<float> rand, int exclusiveMax)
        => Math.Min(exclusiveMax - 1, (int)(rand() * exclusiveMax));

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
}
