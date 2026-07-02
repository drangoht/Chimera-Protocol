using Godot;
using System.Collections.Generic;

public partial class GroundRenderer : Node2D
{
    private const int TileSize  = 32;
    private const int ArenaW    = Constants.ArenaWidth;
    private const int ArenaH    = Constants.ArenaHeight;
    private const int WallThick = Constants.WallThickness;
    private const int GridCols = 60;
    private const int GridRows = 38;

    // Sol
    private const string PathFloor01     = "res://assets/sprites/tileset/tile_floor_01.png";
    private const string PathFloor02     = "res://assets/sprites/tileset/tile_floor_02.png";
    private const string PathFloorCrack  = "res://assets/sprites/tileset/tile_floor_crack.png";
    private const string PathFloorRust   = "res://assets/sprites/tileset/tile_floor_rust.png";
    private const string PathFloorDebris = "res://assets/sprites/tileset/tile_floor_debris.png";
    private const string PathFloorGlass  = "res://assets/sprites/tileset/tile_floor_glass.png";
    // Murs
    private const string PathWall01    = "res://assets/sprites/tileset/tile_wall_01.png";
    private const string PathWallRust  = "res://assets/sprites/tileset/tile_wall_rust.png";
    private const string PathWallCrack = "res://assets/sprites/tileset/tile_wall_crack_aether.png";
    // Décors
    private const string PathDebris01    = "res://assets/sprites/tileset/tile_debris_01.png";
    private const string PathDebrisMetal = "res://assets/sprites/tileset/tile_debris_metal.png";
    private const string PathRustPool01  = "res://assets/sprites/tileset/tile_rust_pool_01.png";
    private const string PathTechPillar  = "res://assets/sprites/tileset/tile_tech_pillar.png";
    // Geysers — positions Phase 4
    private static readonly Vector2 Geyser1Pos = new(-500f, -250f);
    private static readonly Vector2 Geyser2Pos = new(480f,  300f);

    // ─── Biomes ────────────────────────────────────────────────────────────────
    // Un biome = une ambiance colorée appliquée aux tuiles (sol/murs) + à l'overlay.
    // Tiré au sort à chaque run, avec un layout d'obstacles également aléatoire :
    // chaque partie a un look distinct sans nouveaux assets (teinte des PNG existants).
    private sealed class BiomeDef
    {
        public string   Name            { get; }
        public Color    FloorTint       { get; }
        public Color    WallTint        { get; }
        public Color    Overlay         { get; }
        public float    EnemySpeedMult  { get; }
        public float    XpMult          { get; }
        public string   EffectText      { get; }
        public string[] FloorTiles      { get; }   // tuiles dédiées (sol)
        public string[] WallTiles       { get; }   // tuiles dédiées (murs)
        public Color    Accent          { get; }   // couleur vive des obstacles + halos
        public string   Id              { get; }
        public BiomeDef(string id, string name, Color floor, Color wall, Color overlay,
                        float enemySpeedMult, float xpMult, string effectText,
                        string[] floorTiles, string[] wallTiles, Color accent)
        {
            Id = id; Name = name; FloorTint = floor; WallTint = wall; Overlay = overlay;
            EnemySpeedMult = enemySpeedMult; XpMult = xpMult; EffectText = effectText;
            FloorTiles = floorTiles; WallTiles = wallTiles; Accent = accent;
        }
    }

    // Tuiles d'origine (Sanctuaire) — la 1re domine, les suivantes sont des variantes rares.
    private static readonly string[] SanctuaryFloors =
        { PathFloor01, PathFloor02, PathFloorCrack, PathFloorRust, PathFloorDebris };
    private static readonly string[] SanctuaryWalls =
        { PathWall01, PathWallRust, PathWallCrack };

    private static string[] BiomeFloors(string b) => new[]
        { $"res://assets/sprites/tileset/biomes/{b}/floor_01.png",
          $"res://assets/sprites/tileset/biomes/{b}/floor_02.png",
          $"res://assets/sprites/tileset/biomes/{b}/floor_03.png" };
    private static string[] BiomeWalls(string b) => new[]
        { $"res://assets/sprites/tileset/biomes/{b}/wall_01.png",
          $"res://assets/sprites/tileset/biomes/{b}/wall_02.png" };

    private static readonly BiomeDef[] Biomes =
    {
        // Sanctuaire Rouillé — tuiles d'origine (navy foncé → boost Modulate >1.0)
        new("sanctuaire", "Sanctuaire Rouillé", new(1.18f, 1.18f, 1.24f), new(1.12f, 1.12f, 1.18f),
            new(0f, 0.02f, 0.08f, 0.06f), 1.00f, 1.00f, "Terrain neutre",
            SanctuaryFloors, SanctuaryWalls, new(0.30f, 0.85f, 0.95f)),
        // Friche d'Aether — tuiles violettes dédiées : Aether dense → plus d'XP
        new("aether", "Friche d'Aether",    new(1.10f, 1.10f, 1.10f), new(1.06f, 1.06f, 1.06f),
            new(0.05f, 0f, 0.10f, 0.07f), 1.00f, 1.20f, "Aether dense : +20% d'XP",
            BiomeFloors("aether"), BiomeWalls("aether"), new(0.62f, 0.40f, 1.0f)),
        // Fournaise — tuiles rouille-orange dédiées : chaleur → ennemis agités
        new("fournaise", "Fournaise",          new(1.10f, 1.10f, 1.10f), new(1.06f, 1.06f, 1.06f),
            new(0.10f, 0.02f, 0f, 0.07f), 1.18f, 1.00f, "Chaleur : ennemis +18% rapides",
            BiomeFloors("fournaise"), BiomeWalls("fournaise"), new(1.0f, 0.50f, 0.18f)),
        // Givre Cryogénique — tuiles bleu-glace dédiées : gel → ennemis ralentis
        new("givre", "Givre Cryogénique",  new(1.10f, 1.10f, 1.10f), new(1.06f, 1.06f, 1.06f),
            new(0f, 0.06f, 0.09f, 0.06f), 0.82f, 1.00f, "Givre : ennemis -18% lents",
            BiomeFloors("givre"), BiomeWalls("givre"), new(0.62f, 0.88f, 0.95f)),
        // Secteur Néon — secteur de données overclocké : ennemis +10% rapides MAIS +15% XP (risk/reward)
        new("neon", "Secteur Néon",        new(1.10f, 1.10f, 1.10f), new(1.06f, 1.06f, 1.06f),
            new(0.06f, 0f, 0.10f, 0.06f), 1.10f, 1.15f, "Overclock : ennemis +10% rapides, +15% XP",
            BiomeFloors("neon"), BiomeWalls("neon"), new(0.95f, 0.30f, 0.85f)),
    };

    private BiomeDef _biome = Biomes[0];

    public override void _Ready()
    {
        // Biome : forcé par --biome=<id> (debug/captures), sinon par l'écran de
        // sélection de niveau, sinon aléatoire.
        var pick = new RandomNumberGenerator();
        pick.Randomize();
        string? forced = DebugHooks.ForcedBiome ?? GameManager.Instance?.SelectedBiomeId;
        _biome = System.Array.Find(Biomes, b => b.Id == forced)
                 ?? Biomes[(int)(pick.Randi() % (uint)Biomes.Length)];

        // Pose les modificateurs de gameplay du biome (lus par EnemySpawner / XpSystem)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BiomeEnemySpeedMult = _biome.EnemySpeedMult;
            GameManager.Instance.BiomeXpMult         = _biome.XpMult;
            GameManager.Instance.BiomeAccent         = _biome.Accent;
            GameManager.Instance.BiomeName           = Loc.T($"BIOME_{_biome.Id.ToUpperInvariant()}_NAME");
            GameManager.Instance.BiomeEffect         = Loc.T($"BIOME_{_biome.Id.ToUpperInvariant()}_EFFECT");
            GameManager.Instance.CurrentBiomeId      = _biome.Id;
        }

        var rng = new RandomNumberGenerator();
        rng.Seed = pick.Randi();
        AddBackdrop();
        // Features de sol (rivière/lave/chemin/conduits) AVANT le sol : les trous vitrés
        // et les obstacles doivent connaître les cellules occupées pour les éviter.
        _featureCells = FloorFeatures.Build(this, _biome.Id, _biome.Accent, rng);
        BuildFloor(rng);
        BuildWalls(rng);
        BuildDecor(rng);
        BuildObstacles(rng);
        AddFloorDarkOverlay();
        AddAtmosphere();
        AnnounceBiome();
    }

    /// <summary>
    /// Affiche le nom du biome en début de run (Label centré qui apparaît puis s'efface).
    /// Auto-contenu (CanvasLayer interne) — ne dépend pas du HUD.
    /// </summary>
    private void AnnounceBiome()
    {
        var layer = new CanvasLayer { Layer = 80 };
        AddChild(layer);

        var label = new Label
        {
            Text                = $"{Loc.T($"BIOME_{_biome.Id.ToUpperInvariant()}_NAME")}\n{Loc.T($"BIOME_{_biome.Id.ToUpperInvariant()}_EFFECT")}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.20f, AnchorBottom = 0.32f,
            Modulate            = new Color(1f, 1f, 1f, 0f),
        };
        label.AddThemeFontSizeOverride("font_size", 34);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        label.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(label);

        // Apparition (0→1, 0.4 s) → maintien → fondu (1→0, 1.0 s) → libère le calque
        var tw = CreateTween();
        tw.TweenProperty(label, "modulate:a", 1f, 0.4);
        tw.TweenInterval(1.6);
        tw.TweenProperty(label, "modulate:a", 0f, 1.0);
        tw.TweenCallback(Callable.From(layer.QueueFree));
    }

    // ─── Fond ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Grand fond sombre opaque débordant largement l'arène (ZIndex le plus bas).
    /// Empêche le scintillement de bord : quand la caméra est clampée au bord de carte,
    /// le sous-pixel pouvait révéler le vide (couleur de fond du viewport) au-delà du sol.
    /// Ce backdrop garantit qu'il y a toujours du contenu opaque derrière.
    /// </summary>
    private void AddBackdrop()
    {
        // Marge alignée sur BiomeAtmosphere.BackdropMargin : doit couvrir la caméra à son
        // excursion max (clamp joueur ~928×576) + demi-viewport (640×360 à zoom 1), pour qu'aucun
        // vide brut (clear color) n'apparaisse derrière la tuile parallax transparente.
        const float hw = ArenaW / 2f + 650f;
        const float hh = ArenaH / 2f + 650f;
        AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh) },
            Color   = new Color(0.025f, 0.035f, 0.075f, 1f),
            ZIndex  = -12,
        });
    }

    // ─── Sol ─────────────────────────────────────────────────────────────────

    private void BuildFloor(RandomNumberGenerator rng)
    {
        var textures = LoadTextures(_biome.FloorTiles);
        var glassTex = GD.Load<Texture2D>(PathFloorGlass);
        int startX = -ArenaW / 2;
        int startY = -(GridRows * TileSize / 2);
        var glassCells = PickGlassCells(rng, startX, startY);
        var floorRoot = new Node2D { Name = "Floor" };
        AddChild(floorRoot);
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                Texture2D tex;
                if (glassTex != null && glassCells.Contains((row, col)))
                {
                    // Trou vitré : laisse voir le fond parallax (BiomeAtmosphere.BuildBackdropVoid)
                    // à travers le sol, pas seulement au-delà des murs.
                    tex = glassTex;
                }
                else
                {
                    // 1re tuile dominante (~86%), variantes réparties sur le reste
                    // (zone de repos visuel — le sol doit rester majoritairement calme).
                    int ti = PickTileIndex(rng.Randf(), textures.Length, 0.86f);
                    tex = textures[ti];
                }
                var sprite = new Sprite2D
                {
                    Texture   = tex,
                    Position  = new Vector2(startX + col * TileSize + TileSize / 2f, startY + row * TileSize + TileSize / 2f),
                    ZIndex    = -10,
                    Modulate  = _biome.FloorTint,
                };
                floorRoot.AddChild(sprite);
            }
        }
    }

    /// <summary>
    /// Sélectionne quelques amas de tuiles « vitre » (2-3 × 2-3), dispersés et loin des bords,
    /// pour accentuer visuellement la profondeur du fond parallax pendant le run (pas seulement
    /// en collant les murs). Remplit au passage <see cref="_glassClusterCenters"/> (position monde
    /// du centre de chaque amas) : BiomeAtmosphere s'en sert pour garantir un motif profond derrière
    /// CHAQUE amas plutôt que de compter sur un tirage aléatoire indépendant qui en rate certains.
    /// </summary>
    private HashSet<(int Row, int Col)> PickGlassCells(RandomNumberGenerator rng, int startX, int startY)
    {
        var cells = new HashSet<(int, int)>();
        _glassClusterCenters.Clear();
        const int margin = 5;
        int clusters = 3 + (int)(rng.Randi() % 2); // 3-4 amas par run (anti-fouillis)
        for (int c = 0; c < clusters; c++)
        {
            // Réessaie si l'amas chevauche une feature de sol (rivière, lave, chemin…)
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int w = 2 + (int)(rng.Randi() % 2);
                int h = 2 + (int)(rng.Randi() % 2);
                int row0 = margin + (int)(rng.Randi() % (uint)(GridRows - 2 * margin - h));
                int col0 = margin + (int)(rng.Randi() % (uint)(GridCols - 2 * margin - w));

                bool onFeature = false;
                for (int r = row0; r < row0 + h && !onFeature; r++)
                    for (int cc = col0; cc < col0 + w && !onFeature; cc++)
                        if (_featureCells.Contains((r, cc)))
                            onFeature = true;
                if (onFeature) continue;

                for (int r = row0; r < row0 + h; r++)
                    for (int cc = col0; cc < col0 + w; cc++)
                        cells.Add((r, cc));

                float centerCol = col0 + w / 2f;
                float centerRow = row0 + h / 2f;
                _glassClusterCenters.Add(new Vector2(startX + centerCol * TileSize, startY + centerRow * TileSize));
                break;
            }
        }
        return cells;
    }

    private readonly List<Vector2> _glassClusterCenters = new();

    /// <summary>Cellules occupées par les features de sol (rivière, lave, chemin…).</summary>
    private HashSet<(int Row, int Col)> _featureCells = new();

    // ─── Murs ─────────────────────────────────────────────────────────────────

    private void BuildWalls(RandomNumberGenerator rng)
    {
        var wallTex  = LoadTextures(_biome.WallTiles);
        var wallRoot = new Node2D { Name = "WallTiles" };
        AddChild(wallRoot);

        // Murs horizontaux (haut + bas) : 63 tiles — (1920+64)/32 = 62 → arrondi 63
        int hTiles  = 63;
        int hStartX = -(hTiles * TileSize / 2);
        int yTop = -(ArenaH / 2 + WallThick / 2);
        int yBot =  (ArenaH / 2 + WallThick / 2);
        for (int i = 0; i < hTiles; i++)
        {
            float x = hStartX + i * TileSize + TileSize / 2f;
            wallRoot.AddChild(WallSprite(wallTex, rng, x, yTop, rot: 0f, tint: _biome.WallTint));
            wallRoot.AddChild(WallSprite(wallTex, rng, x, yBot, rot: 0f, tint: _biome.WallTint));
        }

        // Murs verticaux (gauche + droite) : 41 tiles — (1216+64)/32 = 40 → arrondi 41
        int vTiles  = 41;
        int vStartY = -(vTiles * TileSize / 2);
        int xLeft  = -(ArenaW / 2 + WallThick / 2);
        int xRight =  (ArenaW / 2 + WallThick / 2);
        for (int j = 0; j < vTiles; j++)
        {
            float y = vStartY + j * TileSize + TileSize / 2f;
            wallRoot.AddChild(WallSprite(wallTex, rng, xLeft,  y, rot: 90f, tint: _biome.WallTint));
            wallRoot.AddChild(WallSprite(wallTex, rng, xRight, y, rot: 90f, tint: _biome.WallTint));
        }
    }

    private static Sprite2D WallSprite(Texture2D[] tex, RandomNumberGenerator rng,
        float x, float y, float rot, Color tint)
    {
        // 1re tuile dominante (~80%), variantes réparties sur le reste.
        int wi = PickTileIndex(rng.Randf(), tex.Length, 0.80f);
        return new Sprite2D
        {
            Texture         = tex[wi],
            Position        = new Vector2(x, y),
            RotationDegrees = rot,
            ZIndex          = -9,
            Modulate        = tint,
        };
    }

    // ─── Décors superposés ────────────────────────────────────────────────────

    private void BuildDecor(RandomNumberGenerator rng)
    {
        // Le décor rouillé (débris, flaques, pilier tech) appartient au Sanctuaire.
        // Les autres biomes restent épurés : leur identité passe par les tuiles dédiées,
        // l'atmosphère et les obstacles thématisés — lisibilité avant tout.
        if (_biome.Id != "sanctuaire") return;

        var decorRoot = new Node2D { Name = "StaticDecor" };
        AddChild(decorRoot);
        var texDebris01    = GD.Load<Texture2D>(PathDebris01);
        var texDebrisMetal = GD.Load<Texture2D>(PathDebrisMetal);
        var texRustPool01  = GD.Load<Texture2D>(PathRustPool01);
        var texTechPillar  = GD.Load<Texture2D>(PathTechPillar);

        const int margin   = 80;
        const int maxHalfX = ArenaW / 2 - margin;
        const int maxHalfY = ArenaH / 2 - margin;
        // Zone interdite : rayon 64 px du centre (spawn joueur)
        const int safeZone = 64;

        // 1 débris de pierre + 1 débris métal + 1 flaque de Rouille (réduit — anti-fouillis)
        decorRoot.AddChild(DecorSprite(texDebris01, rng, maxHalfX, maxHalfY, safeZone, zIndex: -8));
        decorRoot.AddChild(DecorSprite(texDebrisMetal, rng, maxHalfX, maxHalfY, safeZone, zIndex: -8));
        decorRoot.AddChild(DecorSprite(texRustPool01, rng, maxHalfX, maxHalfY, safeZone, zIndex: -9));

        // Pilier tech — élément narratif conservé
        PlaceColumn(decorRoot, texTechPillar, rng, maxHalfX, maxHalfY, safeZone);
    }

    private static Sprite2D DecorSprite(Texture2D tex, RandomNumberGenerator rng,
        int maxHalfX, int maxHalfY, int safeZoneRadius, int zIndex)
    {
        Vector2 pos = SafeRandPos(rng, maxHalfX, maxHalfY, safeZoneRadius);
        return new Sprite2D { Texture = tex, Position = pos, ZIndex = zIndex };
    }

    private static void PlaceColumn(Node2D parent, Texture2D tex, RandomNumberGenerator rng,
        int maxHalfX, int maxHalfY, int safeZoneRadius)
    {
        Vector2 pos = SafeRandPos(rng, maxHalfX, maxHalfY, safeZoneRadius);
        var shape = new RectangleShape2D { Size = new Vector2(28f, 28f) };
        var collShape = new CollisionShape2D { Shape = shape, Position = new Vector2(0f, 16f) };
        var body = new StaticBody2D
        {
            Position       = pos,
            CollisionLayer = 3u, // bit 1 (joueur via mask=1) + bit 2 (bloque aussi les ennemis)
            CollisionMask  = 1u,
            Name           = "Column",
        };
        var sprite = new Sprite2D { Texture = tex, ZIndex = 1 };
        body.AddChild(collShape);
        body.AddChild(sprite);
        parent.AddChild(body);
    }

    // ─── Obstacles thématisés par biome ──────────────────────────────────────

    /// <summary>
    /// Place les obstacles infranchissables selon un gabarit structuré tiré au sort —
    /// « quadrants » (un amas par quadrant), « anneau » (ellipse autour du centre) ou
    /// « allées » (deux rangées créant trois couloirs) — au lieu d'un scatter aléatoire.
    /// Le visuel et la collision de chaque obstacle sont délégués à <see cref="BiomeObstacles"/> :
    /// une silhouette propre à chaque biome (pilier, cristal, basalte, glace, pylône).
    /// Zones interdites : rayon 170 px du centre, 90 px des geysers, bande 96 px des murs.
    /// </summary>
    private void BuildObstacles(RandomNumberGenerator rng)
    {
        var obstacleRoot = new Node2D { Name = "Obstacles" };
        AddChild(obstacleRoot);
        int i = 0;
        foreach (var raw in LayoutPositions(rng))
            obstacleRoot.AddChild(BiomeObstacles.Build(_biome.Id, _biome.Accent, i++, AvoidFeatures(NudgeSafe(raw))));
    }

    /// <summary>
    /// Décale verticalement un obstacle qui tomberait sur une feature de sol
    /// (un pilier au milieu de la lave casse la lecture « rivière »).
    /// </summary>
    private Vector2 AvoidFeatures(Vector2 pos)
    {
        foreach (float dy in new[] { 0f, 64f, -64f, 128f, -128f, 192f })
        {
            var p = NudgeSafe(pos + new Vector2(0f, dy));
            if (!OnFeature(p)) return p;
        }
        return pos;
    }

    private bool OnFeature(Vector2 p)
    {
        int col = (int)MathF.Floor((p.X + ArenaW / 2f) / TileSize);
        int row = (int)MathF.Floor((p.Y + GridRows * TileSize / 2f) / TileSize);
        return _featureCells.Contains((row, col));
    }

    /// <summary>
    /// Positions brutes du gabarit d'obstacles (avant NudgeSafe). 3 gabarits symétriques,
    /// 6 à 10 obstacles par run — moins nombreux qu'avant mais plus grands et lisibles.
    /// </summary>
    private static List<Vector2> LayoutPositions(RandomNumberGenerator rng)
    {
        var pts = new List<Vector2>();
        float J(float amp) => rng.RandfRange(-amp, amp);
        switch (rng.Randi() % 3u)
        {
            case 0u: // Quadrants — amas triangle et paires alternés
            {
                int q = 0;
                foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    var anchor = new Vector2(sx * 470f + J(48f), sy * 290f + J(36f));
                    if (q++ % 2 == 0)
                    {
                        pts.Add(anchor + new Vector2(0f, -64f));
                        pts.Add(anchor + new Vector2(-60f, 48f));
                        pts.Add(anchor + new Vector2(60f, 48f));
                    }
                    else
                    {
                        var off = rng.Randf() < 0.5f ? new Vector2(0f, 60f) : new Vector2(64f, 0f);
                        pts.Add(anchor - off);
                        pts.Add(anchor + off);
                    }
                }
                break;
            }
            case 1u: // Anneau — 6 sur l'ellipse + 2 sentinelles intérieures
            {
                for (int k = 0; k < 6; k++)
                {
                    float a = Mathf.DegToRad(60f * k + J(10f));
                    pts.Add(new Vector2(MathF.Cos(a) * 560f, MathF.Sin(a) * 340f));
                }
                pts.Add(new Vector2(-250f + J(24f), J(24f)));
                pts.Add(new Vector2(250f + J(24f), J(24f)));
                break;
            }
            default: // Allées — deux rangées horizontales → trois couloirs de circulation
            {
                foreach (float x in new[] { -560f, 0f, 560f })
                {
                    pts.Add(new Vector2(x + J(32f), -270f + J(24f)));
                    pts.Add(new Vector2(x + J(32f), 270f + J(24f)));
                }
                break;
            }
        }
        return pts;
    }

    /// <summary>
    /// Écarte une position d'obstacle des zones interdites (centre, geysers, murs)
    /// en la repoussant radialement, puis l'aligne sur la grille de tuiles.
    /// </summary>
    private static Vector2 NudgeSafe(Vector2 pos)
    {
        const float centerSafe = 170f;
        const float geyserSafe = 90f;
        const float maxX = ArenaW / 2f - 96f;
        const float maxY = ArenaH / 2f - 96f;

        if (pos.Length() < centerSafe)
            pos = (pos.LengthSquared() > 0.01f ? pos.Normalized() : Vector2.Right) * centerSafe;
        foreach (var g in new[] { Geyser1Pos, Geyser2Pos })
            if (pos.DistanceTo(g) < geyserSafe)
            {
                var dir = pos - g;
                pos = g + (dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector2.Down) * geyserSafe;
            }
        pos.X = Mathf.Clamp(pos.X, -maxX, maxX);
        pos.Y = Mathf.Clamp(pos.Y, -maxY, maxY);
        return new Vector2(SnapGrid(pos.X), SnapGrid(pos.Y));
    }

    // ─── Atmosphère (brume + rais de lumière + poussière parallaxe) ─────────────

    /// <summary>Ajoute la couche atmosphérique thématisée par le biome (Phase 2).</summary>
    private void AddAtmosphere()
    {
        var atmo = new BiomeAtmosphere();
        AddChild(atmo);
        atmo.Configure(_biome.Id, _biome.Accent, _glassClusterCenters);
    }

    // ─── Overlay sombre ──────────────────────────────────────────────────────

    /// <summary>
    /// Polygon2D semi-transparent bleu-noir couvrant l'arène entière (ZIndex -7).
    /// Renforce le contraste entre le fond et les entités (joueur, ennemis, objets).
    /// ShaderMaterial floor_grid.gdshader appliqué pour la grille holographique subliminal.
    /// </summary>
    private void AddFloorDarkOverlay()
    {
        float hw = ArenaW / 2f;
        float hh = ArenaH / 2f;
        var overlay = new Polygon2D
        {
            Polygon = new Vector2[] { new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh) },
            Color   = _biome.Overlay,
            ZIndex  = -7,
        };

        // Shader de grille holographique — vestiges du réseau Aether sous les ruines
        var gridShader = GD.Load<Shader>("res://assets/shaders/floor_grid.gdshader");
        if (gridShader != null)
        {
            var shaderMat = new ShaderMaterial { Shader = gridShader };
            overlay.Material = shaderMat;
        }

        AddChild(overlay);
    }

    // ─── Utilitaires ──────────────────────────────────────────────────────────

    /// <summary>Charge un tableau de textures depuis des chemins res://.</summary>
    private static Texture2D[] LoadTextures(string[] paths)
    {
        var arr = new Texture2D[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            arr[i] = GD.Load<Texture2D>(paths[i]);
        return arr;
    }

    /// <summary>
    /// Choisit un index de tuile : la 1re tuile domine (proba = <paramref name="dominant"/>),
    /// les variantes se partagent le reste uniformément. Marche pour n'importe quel nombre de tuiles.
    /// </summary>
    private static int PickTileIndex(float r, int count, float dominant)
    {
        if (count <= 1 || r < dominant) return 0;
        int idx = 1 + (int)((r - dominant) / (1f - dominant) * (count - 1));
        return Mathf.Clamp(idx, 1, count - 1);
    }


    private static Vector2 SafeRandPos(RandomNumberGenerator rng, int maxHalfX, int maxHalfY, int safeZoneRadius)
    {
        Vector2 pos;
        int attempts = 0;
        do
        {
            pos = new Vector2(SnapGrid(rng.RandfRange(-maxHalfX, maxHalfX)), SnapGrid(rng.RandfRange(-maxHalfY, maxHalfY)));
            attempts++;
        }
        while (pos.Length() < safeZoneRadius && attempts < 30);
        return pos;
    }

    private static float SnapGrid(float v) => MathF.Round(v / TileSize) * TileSize;
}
