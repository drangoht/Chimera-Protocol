using Godot;

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
    // Murs
    private const string PathWall01    = "res://assets/sprites/tileset/tile_wall_01.png";
    private const string PathWallRust  = "res://assets/sprites/tileset/tile_wall_rust.png";
    private const string PathWallCrack = "res://assets/sprites/tileset/tile_wall_crack_aether.png";
    // Décors
    private const string PathDebris01    = "res://assets/sprites/tileset/tile_debris_01.png";
    private const string PathDebrisMetal = "res://assets/sprites/tileset/tile_debris_metal.png";
    private const string PathRustPool01  = "res://assets/sprites/tileset/tile_rust_pool_01.png";
    private const string PathRustPool02  = "res://assets/sprites/tileset/tile_rust_pool_02.png";
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
    };

    private BiomeDef _biome = Biomes[0];

    public override void _Ready()
    {
        // Biome : forcé par l'écran de sélection de niveau, sinon aléatoire.
        var pick = new RandomNumberGenerator();
        pick.Randomize();
        string? forced = GameManager.Instance?.SelectedBiomeId;
        _biome = System.Array.Find(Biomes, b => b.Id == forced)
                 ?? Biomes[(int)(pick.Randi() % (uint)Biomes.Length)];

        // Pose les modificateurs de gameplay du biome (lus par EnemySpawner / XpSystem)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BiomeEnemySpeedMult = _biome.EnemySpeedMult;
            GameManager.Instance.BiomeXpMult         = _biome.XpMult;
            GameManager.Instance.BiomeAccent         = _biome.Accent;
            GameManager.Instance.BiomeName           = _biome.Name;
            GameManager.Instance.BiomeEffect         = _biome.EffectText;
            GameManager.Instance.CurrentBiomeId      = _biome.Id;
        }

        var rng = new RandomNumberGenerator();
        rng.Seed = pick.Randi();
        AddBackdrop();
        BuildFloor(rng);
        BuildWalls(rng);
        BuildDecor(rng);
        BuildObstacles(rng);
        AddFloorDarkOverlay();
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
            Text                = $"{_biome.Name}\n{_biome.EffectText}",
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
        const float hw = ArenaW / 2f + 256f;
        const float hh = ArenaH / 2f + 256f;
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
        var floorRoot = new Node2D { Name = "Floor" };
        AddChild(floorRoot);
        int startX = -ArenaW / 2;
        int startY = -(GridRows * TileSize / 2);
        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridCols; col++)
            {
                // 1re tuile dominante (~72%), variantes réparties sur le reste.
                int ti = PickTileIndex(rng.Randf(), textures.Length, 0.72f);
                var sprite = new Sprite2D
                {
                    Texture   = textures[ti],
                    Position  = new Vector2(startX + col * TileSize + TileSize / 2f, startY + row * TileSize + TileSize / 2f),
                    ZIndex    = -10,
                    Modulate  = _biome.FloorTint,
                };
                floorRoot.AddChild(sprite);
            }
        }
    }

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
        var decorRoot = new Node2D { Name = "StaticDecor" };
        AddChild(decorRoot);
        var texDebris01    = GD.Load<Texture2D>(PathDebris01);
        var texDebrisMetal = GD.Load<Texture2D>(PathDebrisMetal);
        var texRustPool01  = GD.Load<Texture2D>(PathRustPool01);
        var texRustPool02  = GD.Load<Texture2D>(PathRustPool02);
        var texTechPillar  = GD.Load<Texture2D>(PathTechPillar);

        const int margin   = 80;
        const int maxHalfX = ArenaW / 2 - margin;
        const int maxHalfY = ArenaH / 2 - margin;
        // Zone interdite : rayon 64 px du centre (spawn joueur)
        const int safeZone = 64;

        // 2 débris de pierre (réduit de 4 à 2)
        for (int i = 0; i < 2; i++)
            decorRoot.AddChild(DecorSprite(texDebris01, rng, maxHalfX, maxHalfY, safeZone, zIndex: -8));

        // 2 débris métal (réduit de 3 à 2)
        for (int i = 0; i < 2; i++)
            decorRoot.AddChild(DecorSprite(texDebrisMetal, rng, maxHalfX, maxHalfY, safeZone, zIndex: -8));

        // 2 flaques de Rouille Vivante (réduit de 3 à 2)
        decorRoot.AddChild(DecorSprite(texRustPool01, rng, maxHalfX, maxHalfY, safeZone, zIndex: -9));
        decorRoot.AddChild(DecorSprite(texRustPool02, rng, maxHalfX, maxHalfY, safeZone, zIndex: -9));

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
            CollisionLayer = 1u,
            CollisionMask  = 1u,
            Name           = "Column",
        };
        var sprite = new Sprite2D { Texture = tex, ZIndex = 1 };
        body.AddChild(collShape);
        body.AddChild(sprite);
        parent.AddChild(body);
    }

    // ─── Obstacles Phase 4 ────────────────────────────────────────────────────

    /// <summary>
    /// Place les obstacles solides tactiques dans l'arène.
    /// Type A : Pilier de Sanctuaire (5 instances, StaticBody2D + CapsuleShape2D).
    /// Type B : Épave de Machine (2 instances, StaticBody2D + RectangleShape2D 56×24).
    /// Type C : Caisse Technologique (4 instances, StaticBody2D + RectangleShape2D 28×28).
    /// Type D : Arche Effondrée (2 instances, 2 CollisionShape2D piliers latéraux, rotation 50%).
    /// Zones interdites : rayon 150 px du centre, rayon 48 px de chaque geyser, bande 80 px des murs.
    /// Si un sprite est absent, utilise un Polygon2D placeholder pour ne pas crasher.
    /// </summary>
    private void BuildObstacles(RandomNumberGenerator rng)
    {
        var obstacleRoot = new Node2D { Name = "Obstacles" };
        AddChild(obstacleRoot);

        const int wallBand   = 80;
        const int maxHalfX   = ArenaW / 2 - wallBand;
        const int maxHalfY   = ArenaH / 2 - wallBand;
        const int centerSafe = 150;
        const float geyserSafe = 48f;

        Color accent = _biome.Accent;

        // Positions déjà placées — utilisées pour la contrainte d'alignement caisses
        var cratePositions = new System.Collections.Generic.List<Vector2>(4);

        // Type A — 5 Piliers de Sanctuaire
        for (int i = 0; i < 5; i++)
        {
            Vector2 pos = SafeRandPosObstacle(rng, maxHalfX, maxHalfY, centerSafe, geyserSafe);
            obstacleRoot.AddChild(BuildPillar(pos, accent, i));
        }

        // Type B — 2 Épaves de Machine
        for (int i = 0; i < 2; i++)
        {
            Vector2 pos = SafeRandPosObstacle(rng, maxHalfX, maxHalfY, centerSafe, geyserSafe);
            obstacleRoot.AddChild(BuildWreck(pos, accent, i));
        }

        // Type C — 4 Caisses Technologiques
        // Contrainte anti-mur : jamais 3 caisses alignées sur le même axe X ou Y (tolérance 48 px)
        for (int i = 0; i < 4; i++)
        {
            Vector2 pos;
            int attempts = 0;
            do
            {
                pos = SafeRandPosObstacle(rng, maxHalfX, maxHalfY, centerSafe, geyserSafe);
                attempts++;
            }
            while (attempts < 60 && IsThirdAligned(cratePositions, pos, 48f));
            cratePositions.Add(pos);
            obstacleRoot.AddChild(BuildCrate(pos, accent, i));
        }

        // Type D — 2 Arches Effondrées
        for (int i = 0; i < 2; i++)
        {
            Vector2 pos    = SafeRandPosObstacle(rng, maxHalfX, maxHalfY, centerSafe, geyserSafe);
            bool rotated   = rng.Randf() < 0.5f;
            obstacleRoot.AddChild(BuildArch(pos, accent, i, rotated));
        }
    }

    // ─── Visuel d'obstacle stylisé (corps teinté + contour vif + halo) ──────────
    private static Texture2D? _obstacleLightTex;

    /// <summary>
    /// Ajoute à <paramref name="parent"/> une boîte d'obstacle bien lisible : corps sombre teinté
    /// par l'accent du biome, contour lumineux vif, liseré haut clair, et un halo PointLight2D.
    /// </summary>
    private static void AddObstacleVisual(Node2D parent, float halfW, float halfH, float offsetY, Color accent)
    {
        var bodyCol = new Color(accent.R * 0.40f + 0.04f, accent.G * 0.40f + 0.04f, accent.B * 0.42f + 0.05f, 1f);
        var top     = -halfH + offsetY;
        var bot     =  halfH + offsetY;
        var pts = new Vector2[] { new(-halfW, top), new(halfW, top), new(halfW, bot), new(-halfW, bot) };

        parent.AddChild(new Polygon2D { Polygon = pts, Color = bodyCol, ZIndex = 0 });

        // Contour vif (boucle fermée)
        parent.AddChild(new Line2D
        {
            Points       = new[] { pts[0], pts[1], pts[2], pts[3], pts[0] },
            Width        = 2.5f,
            DefaultColor = accent,
            JointMode    = Line2D.LineJointMode.Round,
            ZIndex       = 1,
        });
        // Liseré haut clair (relief / lisibilité)
        parent.AddChild(new Line2D
        {
            Points       = new[] { new Vector2(-halfW, top), new Vector2(halfW, top) },
            Width        = 2f,
            DefaultColor = accent.Lerp(Colors.White, 0.6f),
            ZIndex       = 2,
        });

        // Halo lumineux additif
        _obstacleLightTex ??= Player.MakeRadialLightTexture(64);
        parent.AddChild(new PointLight2D
        {
            Position     = new Vector2(0f, offsetY),
            Color        = accent,
            Energy       = 0.85f,
            Texture      = _obstacleLightTex,
            TextureScale = Mathf.Max(halfW, halfH) / 11f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        });
    }

    /// <summary>
    /// Retourne true si placer une caisse en <paramref name="candidate"/> crée un 3e alignement
    /// sur le même axe X ou Y (à ±<paramref name="tolerance"/> px) parmi les positions existantes.
    /// </summary>
    private static bool IsThirdAligned(System.Collections.Generic.List<Vector2> placed, Vector2 candidate, float tolerance)
    {
        int sameX = 0, sameY = 0;
        foreach (var p in placed)
        {
            if (MathF.Abs(p.X - candidate.X) < tolerance) sameX++;
            if (MathF.Abs(p.Y - candidate.Y) < tolerance) sameY++;
        }
        return sameX >= 2 || sameY >= 2;
    }

    /// <summary>
    /// Pilier de Sanctuaire — Type A.
    /// Sprite 32×64 + ombre 32×8 (ZIndex=-1) + CapsuleShape2D rayon 12 offset Y+16.
    /// </summary>
    private static StaticBody2D BuildPillar(Vector2 pos, Color accent, int index)
    {
        var body = new StaticBody2D
        {
            Position       = pos,
            CollisionLayer = 1u,
            CollisionMask  = 1u,
            ZIndex         = 1,
            Name           = $"Pillar_{index}",
        };
        var capsule = new CapsuleShape2D { Radius = 12f, Height = 0f };
        body.AddChild(new CollisionShape2D { Shape = capsule, Position = new Vector2(0f, 16f) });

        AddObstacleVisual(body, 9f, 20f, 2f, accent);   // pilier haut, vif, halo
        return body;
    }

    /// <summary>
    /// Épave de Machine — Type B.
    /// Sprite 64×32 + RectangleShape2D(56, 24).
    /// </summary>
    private static StaticBody2D BuildWreck(Vector2 pos, Color accent, int index)
    {
        var body = new StaticBody2D
        {
            Position       = pos,
            CollisionLayer = 1u,
            CollisionMask  = 1u,
            ZIndex         = 1,
            Name           = $"Wreck_{index}",
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(56f, 24f) } });

        AddObstacleVisual(body, 28f, 12f, 0f, accent);  // épave large
        return body;
    }

    /// <summary>
    /// Caisse Technologique — Type C.
    /// Sprite 32×40 + RectangleShape2D(28, 28) offset Y=+6 (fausse perspective, partie basse).
    /// </summary>
    private static StaticBody2D BuildCrate(Vector2 pos, Color accent, int index)
    {
        var body = new StaticBody2D
        {
            Position       = pos,
            CollisionLayer = 1u,
            CollisionMask  = 1u,
            ZIndex         = 1,
            Name           = $"Crate_{index}",
        };
        body.AddChild(new CollisionShape2D
        {
            Shape    = new RectangleShape2D { Size = new Vector2(28f, 28f) },
            Position = new Vector2(0f, 6f),
        });

        AddObstacleVisual(body, 15f, 16f, 4f, accent);  // caisse cubique
        return body;
    }

    /// <summary>
    /// Arche Effondrée — Type D.
    /// Sprite 96×32 (ou 32×96 si <paramref name="rotated"/>=true).
    /// Deux CollisionShape2D séparées pour les piliers latéraux uniquement (passage central libre).
    ///   Pilier gauche : RectangleShape2D(20, 28) offset X=-38
    ///   Pilier droit  : RectangleShape2D(20, 28) offset X=+38
    /// En mode rotaté (90°) les offsets X/Y sont transposés.
    /// </summary>
    private static StaticBody2D BuildArch(Vector2 pos, Color accent, int index, bool rotated)
    {
        var body = new StaticBody2D
        {
            Position        = pos,
            CollisionLayer  = 1u,
            CollisionMask   = 1u,
            ZIndex          = 1,
            RotationDegrees = rotated ? 90f : 0f,
            Name            = $"Arch_{index}",
        };
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20f, 28f) }, Position = new Vector2(-38f, 0f) });
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(20f, 28f) }, Position = new Vector2(38f, 0f) });

        // Deux piliers latéraux lumineux (passage central libre)
        var left  = new Node2D { Position = new Vector2(-38f, 0f) };
        var right = new Node2D { Position = new Vector2(38f, 0f) };
        body.AddChild(left); body.AddChild(right);
        AddObstacleVisual(left,  10f, 15f, 0f, accent);
        AddObstacleVisual(right, 10f, 15f, 0f, accent);

        // Linteau supérieur (visuel uniquement) reliant les deux piliers
        body.AddChild(new Line2D
        {
            Points       = new[] { new Vector2(-44f, -15f), new Vector2(44f, -15f) },
            Width        = 3f,
            DefaultColor = accent,
            ZIndex       = 2,
        });
        return body;
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

    /// <summary>
    /// Placement obstacle : évite centre (150 px), chaque geyser (48 px), et bandes murs (déjà soustrait dans maxHalf*).
    /// </summary>
    private static Vector2 SafeRandPosObstacle(RandomNumberGenerator rng, int maxHalfX, int maxHalfY,
        float centerSafe, float geyserSafe)
    {
        Vector2 pos;
        int attempts = 0;
        do
        {
            pos = new Vector2(SnapGrid(rng.RandfRange(-maxHalfX, maxHalfX)), SnapGrid(rng.RandfRange(-maxHalfY, maxHalfY)));
            attempts++;
            bool tooCloseToCenter  = pos.Length() < centerSafe;
            bool tooCloseToGeyser1 = pos.DistanceTo(Geyser1Pos) < geyserSafe;
            bool tooCloseToGeyser2 = pos.DistanceTo(Geyser2Pos) < geyserSafe;
            if (!tooCloseToCenter && !tooCloseToGeyser1 && !tooCloseToGeyser2)
                break;
        }
        while (attempts < 50);
        return pos;
    }

    private static float SnapGrid(float v) => MathF.Round(v / TileSize) * TileSize;
}
