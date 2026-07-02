using Godot;
using System.Collections.Generic;

/// <summary>
/// Grandes structures de sol thématisées par biome — donnent au terrain une lecture
/// immédiate de l'identité du lieu (purement visuelles, aucune collision) :
///   sanctuaire → chemin pavé orthogonal + place dallée avec rune d'Aether
///   aether     → rivière d'énergie violette sinueuse + poches
///   fournaise  → coulée de lave incandescente + poches de lave
///   givre      → rivière gelée craquelée + plaques de glace
///   neon       → conduits de données orthogonaux avec nœuds lumineux
/// Rendu procédural par cellule de la grille de tuiles (32 px) : Polygon2D teintés
/// + rives/joints en Line2D + quelques PointLight2D (budget ≤ ~10 par arène).
/// Retourne l'ensemble des cellules occupées — GroundRenderer s'en sert pour écarter
/// les trous vitrés et les obstacles.
/// </summary>
public static class FloorFeatures
{
    private const int TileSize = 32;
    private const int Cols = 60;
    private const int Rows = 38;
    private const float StartX = -Constants.ArenaWidth / 2f;
    private const float StartY = -Rows * TileSize / 2f;

    private static Texture2D? _glowTex;

    public static HashSet<(int Row, int Col)> Build(Node2D parent, string biomeId, Color accent,
        RandomNumberGenerator rng)
    {
        var root = new Node2D { Name = "FloorFeatures" };
        parent.AddChild(root);
        return biomeId switch
        {
            "aether"    => BuildAetherRiver(root, accent, rng),
            "fournaise" => BuildLavaFlow(root, accent, rng),
            "givre"     => BuildFrozenRiver(root, accent, rng),
            "neon"      => BuildDataConduits(root, accent, rng),
            _           => BuildPavedPath(root, accent, rng),
        };
    }

    // ─── Sanctuaire — chemin pavé + place ──────────────────────────────────────

    private static HashSet<(int, int)> BuildPavedPath(Node2D root, Color accent, RandomNumberGenerator rng)
    {
        var (cells, _, bends) = OrthoPath(rng, width: 2, horizontal: true);

        // Place dallée circulaire sur un coude (ou au centre du chemin à défaut)
        (int Row, int Col) hub = bends.Count > 0 ? bends[bends.Count / 2] : (Rows / 2, Cols / 2);
        AddBlob(cells, hub.Row, hub.Col, 3.2f);

        // Dalles : cellules en retrait de 2 px — le sol sombre dessous dessine les joints
        var slab = new Color(0.21f, 0.20f, 0.27f);
        foreach (var (r, c) in cells)
        {
            float m = 1f + rng.RandfRange(-0.12f, 0.12f);
            root.AddChild(CellPoly(r, c, new Color(slab.R * m, slab.G * m, slab.B * m), inset: 2f, z: -9));
            // Quelques dalles fendues
            if (rng.Randf() < 0.10f)
            {
                var p = CellCenter(r, c);
                root.AddChild(new Line2D
                {
                    Points       = new[] { p + new Vector2(-9f, -6f), p + new Vector2(2f, 1f), p + new Vector2(-3f, 9f) },
                    Width        = 1.5f,
                    DefaultColor = new Color(0.09f, 0.08f, 0.12f),
                    ZIndex       = -8,
                });
            }
        }

        // Rune d'Aether gravée au centre de la place (cercle accent discret)
        var hubPos = CellCenter(hub.Row, hub.Col);
        var circle = new Vector2[25];
        for (int i = 0; i < 25; i++)
        {
            float a = Mathf.Tau * i / 24f;
            circle[i] = hubPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 56f;
        }
        root.AddChild(new Line2D { Points = circle, Width = 2f, DefaultColor = new Color(accent, 0.35f), ZIndex = -8 });
        AddGlow(root, hubPos, accent, 0.5f);
        return cells;
    }

    // ─── Aether — rivière d'énergie ────────────────────────────────────────────

    private static HashSet<(int, int)> BuildAetherRiver(Node2D root, Color accent, RandomNumberGenerator rng)
    {
        var (cells, center) = River(rng);
        AddBlob(cells, (int)rng.RandfRange(6f, Rows - 7f), (int)rng.RandfRange(10f, Cols - 11f), 2.4f);

        PaintCells(root, cells, rng, new Color(0.24f, 0.11f, 0.42f), jitter: 0.22f, inset: 0f, z: -9);
        // Flux central lumineux
        root.AddChild(new Line2D
        {
            Points       = Thin(center, 2),
            Width        = 6f,
            DefaultColor = new Color(accent, 0.45f),
            JointMode    = Line2D.LineJointMode.Round,
            ZIndex       = -8,
        });
        PaintBanks(root, cells, new Color(accent, 0.50f), 2f);
        // Étincelles
        foreach (var (r, c) in cells)
            if (rng.Randf() < 0.10f)
                root.AddChild(Dot(CellCenter(r, c) + RandOffset(rng, 10f), 2f, new Color(0.85f, 0.75f, 1f, 0.7f)));
        AddGlows(root, center, accent, 0.45f, every: 9);
        return cells;
    }

    // ─── Fournaise — coulée de lave ────────────────────────────────────────────

    private static HashSet<(int, int)> BuildLavaFlow(Node2D root, Color accent, RandomNumberGenerator rng)
    {
        var (cells, center) = River(rng);
        AddBlob(cells, (int)rng.RandfRange(6f, Rows - 7f), (int)rng.RandfRange(10f, Cols - 11f), 2.6f);

        PaintCells(root, cells, rng, new Color(0.60f, 0.16f, 0.03f), jitter: 0.30f, inset: 0f, z: -9);
        // Cœur en fusion
        root.AddChild(new Line2D
        {
            Points       = Thin(center, 2),
            Width        = 8f,
            DefaultColor = new Color(1f, 0.50f, 0.14f, 0.70f),
            JointMode    = Line2D.LineJointMode.Round,
            ZIndex       = -8,
        });
        // Rive incandescente (le bord doit brûler, pas s'éteindre — lisibilité sur sol brun)
        PaintBanks(root, cells, new Color(1f, 0.45f, 0.12f, 0.65f), 2.5f);
        // Braises
        foreach (var (r, c) in cells)
            if (rng.Randf() < 0.14f)
                root.AddChild(Dot(CellCenter(r, c) + RandOffset(rng, 10f), 2.5f, accent.Lerp(Colors.White, 0.3f)));
        AddGlows(root, center, new Color(1f, 0.50f, 0.18f), 0.55f, every: 8);
        return cells;
    }

    // ─── Givre — rivière gelée ─────────────────────────────────────────────────

    private static HashSet<(int, int)> BuildFrozenRiver(Node2D root, Color accent, RandomNumberGenerator rng)
    {
        var (cells, center) = River(rng);
        // Deux plaques de glace annexes
        for (int i = 0; i < 2; i++)
            AddBlob(cells, (int)rng.RandfRange(6f, Rows - 7f), (int)rng.RandfRange(8f, Cols - 9f), 2.2f);

        PaintCells(root, cells, rng, new Color(0.24f, 0.36f, 0.44f), jitter: 0.15f, inset: 0f, z: -9);
        PaintBanks(root, cells, new Color(accent, 0.55f), 2f);
        // Craquelures
        foreach (var (r, c) in cells)
            if (rng.Randf() < 0.16f)
            {
                var p = CellCenter(r, c) + RandOffset(rng, 6f);
                root.AddChild(new Line2D
                {
                    Points       = new[] { p + new Vector2(-10f, -4f), p + new Vector2(-1f, 2f), p + new Vector2(4f, -3f), p + new Vector2(11f, 5f) },
                    Width        = 1.5f,
                    DefaultColor = new Color(0.85f, 0.95f, 1f, 0.30f),
                    ZIndex       = -8,
                });
            }
        AddGlows(root, center, accent, 0.25f, every: 12);
        return cells;
    }

    // ─── Néon — conduits de données ────────────────────────────────────────────

    private static HashSet<(int, int)> BuildDataConduits(Node2D root, Color accent, RandomNumberGenerator rng)
    {
        var (hCells, hCenter, hBends) = OrthoPath(rng, width: 1, horizontal: true);
        var (vCells, vCenter, vBends) = OrthoPath(rng, width: 1, horizontal: false);
        var cells = new HashSet<(int, int)>(hCells);
        cells.UnionWith(vCells);

        // Lit sombre du conduit (le canal se creuse dans le sol)
        PaintCells(root, cells, rng, new Color(0.04f, 0.03f, 0.08f), jitter: 0.10f, inset: 0f, z: -9);

        // Traces lumineuses le long des deux routes
        foreach (var pts in new[] { hCenter, vCenter })
            root.AddChild(new Line2D
            {
                Points       = pts.ToArray(),
                Width        = 4f,
                DefaultColor = new Color(accent, 0.85f),
                JointMode    = Line2D.LineJointMode.Round,
                ZIndex       = -8,
            });

        // Nœuds : coudes des deux routes + jonctions
        var nodes = new List<(int, int)>(hBends);
        nodes.AddRange(vBends);
        foreach (var cell in hCells)
            if (vCells.Contains(cell)) { nodes.Add(cell); break; }
        int lights = 0;
        foreach (var (r, c) in nodes)
        {
            var p = CellCenter(r, c);
            var box = new Vector2[] { p + new Vector2(-8f, -8f), p + new Vector2(8f, -8f),
                                      p + new Vector2(8f, 8f), p + new Vector2(-8f, 8f) };
            root.AddChild(new Polygon2D { Polygon = box, Color = new Color(0.02f, 0.01f, 0.05f), ZIndex = -8 });
            root.AddChild(new Line2D
            {
                Points       = new[] { box[0], box[1], box[2], box[3], box[0] },
                Width        = 2f,
                DefaultColor = accent,
                ZIndex       = -8,
            });
            if (lights++ < 8) AddGlow(root, p, accent, 0.5f);
        }
        return cells;
    }

    // ─── Générateurs de tracés ─────────────────────────────────────────────────

    /// <summary>Rivière sinueuse traversant l'arène de gauche à droite (largeur 1.4-3 tuiles).</summary>
    private static (HashSet<(int, int)> Cells, List<Vector2> Center) River(RandomNumberGenerator rng)
    {
        var cells  = new HashSet<(int, int)>();
        var center = new List<Vector2>();
        float row   = rng.RandfRange(9f, Rows - 9f);
        float drift = rng.RandfRange(-0.5f, 0.5f);
        float width = rng.RandfRange(1.6f, 2.4f);
        for (int col = 0; col < Cols; col++)
        {
            // Biais doux vers le centre vertical : la rivière traverse toujours la zone
            // visible au spawn (caméra centrée) au lieu de longer un bord hors champ.
            float bias = (Rows / 2f - row) * 0.014f;
            drift = Mathf.Clamp(drift + rng.RandfRange(-0.30f, 0.30f) + bias, -1.1f, 1.1f);
            row   = Mathf.Clamp(row + drift, 4f, Rows - 5f);
            width = Mathf.Clamp(width + rng.RandfRange(-0.20f, 0.20f), 1.4f, 3.0f);
            int r0 = (int)MathF.Floor(row - width / 2f);
            int r1 = (int)MathF.Ceiling(row + width / 2f);
            for (int r = r0; r <= r1; r++)
                if (r >= 1 && r < Rows - 1)
                    cells.Add((r, col));
            center.Add(CellCenter((int)MathF.Round(row), col));
        }
        return (cells, center);
    }

    /// <summary>
    /// Chemin orthogonal (segments droits + coudes) traversant l'arène.
    /// <paramref name="horizontal"/> : gauche→droite, sinon haut→bas.
    /// </summary>
    private static (HashSet<(int, int)> Cells, List<Vector2> Center, List<(int, int)> Bends) OrthoPath(
        RandomNumberGenerator rng, int width, bool horizontal)
    {
        var cells  = new HashSet<(int, int)>();
        var center = new List<Vector2>();
        var bends  = new List<(int, int)>();
        int len = horizontal ? Cols : Rows;
        int lat = horizontal ? Rows : Cols;
        int v = (int)rng.RandfRange(8f, lat - 8f);
        int u = 0;

        void Stamp(int uu, int vv)
        {
            for (int a = 0; a < width; a++)
            for (int b = 0; b < width; b++)
            {
                int r = horizontal ? vv + a : uu + b;
                int c = horizontal ? uu + b : vv + a;
                if (r >= 1 && r < Rows - 1 && c >= 1 && c < Cols - 1)
                    cells.Add((r, c));
            }
            center.Add(horizontal ? CellCenter(vv, uu) : CellCenter(uu, vv));
        }

        while (u < len)
        {
            int run = 7 + (int)(rng.Randi() % 6);
            for (int s = 0; s < run && u < len; s++, u++)
                Stamp(u, v);
            if (u >= len) break;
            bends.Add(horizontal ? (v, u - 1) : (u - 1, v));
            // Coudes biaisés vers le centre latéral (65 %) : le chemin reste dans la zone visible
            int dir = rng.Randf() < 0.65f ? (v < lat / 2 ? 1 : -1) : (rng.Randi() % 2 == 0 ? 1 : -1);
            int target = Mathf.Clamp(v + dir * (3 + (int)(rng.Randi() % 5)), 5, lat - 6);
            int step = target > v ? 1 : -1;
            while (v != target)
            {
                v += step;
                Stamp(u - 1, v);
            }
        }
        return (cells, center, bends);
    }

    /// <summary>Ajoute un disque de cellules (place, poche de lave, plaque de glace…).</summary>
    private static void AddBlob(HashSet<(int, int)> cells, int rowC, int colC, float radius)
    {
        int ri = (int)MathF.Ceiling(radius);
        for (int r = rowC - ri; r <= rowC + ri; r++)
        for (int c = colC - ri; c <= colC + ri; c++)
            if (r >= 1 && r < Rows - 1 && c >= 1 && c < Cols - 1
                && new Vector2(c - colC, r - rowC).Length() <= radius)
                cells.Add((r, c));
    }

    // ─── Rendu ─────────────────────────────────────────────────────────────────

    private static void PaintCells(Node2D root, HashSet<(int, int)> cells, RandomNumberGenerator rng,
        Color baseCol, float jitter, float inset, int z)
    {
        foreach (var (r, c) in cells)
        {
            float m = 1f + rng.RandfRange(-jitter, jitter);
            root.AddChild(CellPoly(r, c, new Color(baseCol.R * m, baseCol.G * m, baseCol.B * m, baseCol.A), inset, z));
        }
    }

    /// <summary>Trace les rives : une ligne sur chaque bord de cellule sans voisin dans l'ensemble.</summary>
    private static void PaintBanks(Node2D root, HashSet<(int, int)> cells, Color color, float width)
    {
        foreach (var (r, c) in cells)
        {
            float x0 = StartX + c * TileSize, y0 = StartY + r * TileSize;
            float x1 = x0 + TileSize,         y1 = y0 + TileSize;
            if (!cells.Contains((r - 1, c))) AddEdge(root, new(x0, y0), new(x1, y0), color, width);
            if (!cells.Contains((r + 1, c))) AddEdge(root, new(x0, y1), new(x1, y1), color, width);
            if (!cells.Contains((r, c - 1))) AddEdge(root, new(x0, y0), new(x0, y1), color, width);
            if (!cells.Contains((r, c + 1))) AddEdge(root, new(x1, y0), new(x1, y1), color, width);
        }
    }

    private static void AddEdge(Node2D root, Vector2 a, Vector2 b, Color color, float width) =>
        root.AddChild(new Line2D { Points = new[] { a, b }, Width = width, DefaultColor = color, ZIndex = -8 });

    private static Polygon2D CellPoly(int r, int c, Color color, float inset, int z)
    {
        float x0 = StartX + c * TileSize + inset, y0 = StartY + r * TileSize + inset;
        float x1 = x0 + TileSize - 2f * inset,    y1 = y0 + TileSize - 2f * inset;
        return new Polygon2D
        {
            Polygon = new Vector2[] { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) },
            Color   = color,
            ZIndex  = z,
        };
    }

    private static Polygon2D Dot(Vector2 p, float half, Color color) => new()
    {
        Polygon = new Vector2[] { new(p.X - half, p.Y - half), new(p.X + half, p.Y - half),
                                  new(p.X + half, p.Y + half), new(p.X - half, p.Y + half) },
        Color   = color,
        ZIndex  = -8,
    };

    private static Vector2 RandOffset(RandomNumberGenerator rng, float amp) =>
        new(rng.RandfRange(-amp, amp), rng.RandfRange(-amp, amp));

    /// <summary>Sous-échantillonne une polyline (adoucit les Line2D de flux).</summary>
    private static Vector2[] Thin(List<Vector2> pts, int every)
    {
        var outPts = new List<Vector2>(pts.Count / every + 1);
        for (int i = 0; i < pts.Count; i += every)
            outPts.Add(pts[i]);
        if (outPts[^1] != pts[^1]) outPts.Add(pts[^1]);
        return outPts.ToArray();
    }

    private static void AddGlows(Node2D root, List<Vector2> center, Color color, float energy, int every)
    {
        for (int i = every / 2; i < center.Count; i += every)
            AddGlow(root, center[i], color, energy);
    }

    private static void AddGlow(Node2D root, Vector2 pos, Color color, float energy)
    {
        _glowTex ??= Player.MakeRadialLightTexture(64);
        root.AddChild(new PointLight2D
        {
            Position     = pos,
            Color        = color,
            Energy       = energy,
            Texture      = _glowTex,
            TextureScale = 4.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        });
    }

    private static Vector2 CellCenter(int r, int c) =>
        new(StartX + c * TileSize + TileSize / 2f, StartY + r * TileSize + TileSize / 2f);
}
