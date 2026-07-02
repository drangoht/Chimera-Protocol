using Godot;

/// <summary>
/// Fabrique d'obstacles infranchissables thématisés par biome.
/// Chaque biome possède sa propre silhouette, immédiatement identifiable :
///   sanctuaire → pilier de pierre fissuré d'Aether (intact ou brisé)
///   aether     → cristal violet à facettes + éclat satellite
///   fournaise  → bloc de basalte veiné de lave
///   givre      → monolithe de glace à facette claire
///   neon       → pylône de données à bandes lumineuses + antenne
/// Tous partagent la même grammaire de lisibilité (règle ARENA_DA_BRIEF §2 : lire
/// « infranchissable » en moins de 300 ms) : silhouette plus haute que le joueur (32 px),
/// ombre portée au sol, contour accent vif, halo lumineux additif.
/// </summary>
public static class BiomeObstacles
{
    private static Texture2D? _haloTex;

    public static StaticBody2D Build(string biomeId, Color accent, int index, Vector2 pos)
    {
        var body = new StaticBody2D
        {
            Position       = pos,
            CollisionLayer = 3u, // bit 1 (joueur via mask=1) + bit 2 (bloque aussi les ennemis)
            CollisionMask  = 1u,
            ZIndex         = 1,
            Name           = $"Obstacle_{biomeId}_{index}",
        };
        switch (biomeId)
        {
            case "aether":    BuildCrystal(body, accent, index);     break;
            case "fournaise": BuildBasalt(body, accent);             break;
            case "givre":     BuildIceMonolith(body, accent);        break;
            case "neon":      BuildDataPylon(body, accent);          break;
            default:          BuildStonePillar(body, accent, index); break;
        }
        return body;
    }

    // ─── Sanctuaire — Pilier de pierre fissuré ─────────────────────────────────

    private static void BuildStonePillar(StaticBody2D body, Color accent, int index)
    {
        bool broken = index % 2 == 1;
        float top   = broken ? -22f : -36f;

        body.AddChild(new CollisionShape2D
        {
            Shape    = new CapsuleShape2D { Radius = 13f, Height = 0f },
            Position = new Vector2(0f, 14f),
        });
        AddShadow(body, 20f, 7f, 26f);

        var stone = new Color(0.17f, 0.16f, 0.21f);
        // Fût trapézoïdal ; version brisée = sommet déchiqueté
        Vector2[] shaft = broken
            ? new Vector2[] { new(-12f, top + 6f), new(-6f, top), new(0f, top + 5f),
                              new(7f, top - 2f), new(12f, top + 6f), new(14f, 24f), new(-14f, 24f) }
            : new Vector2[] { new(-11f, top), new(11f, top), new(14f, 24f), new(-14f, 24f) };
        body.AddChild(new Polygon2D { Polygon = shaft, Color = stone, ZIndex = 1 });

        if (!broken) // chapiteau
            body.AddChild(new Polygon2D
            {
                Polygon = new Vector2[] { new(-16f, top - 8f), new(16f, top - 8f), new(13f, top), new(-13f, top) },
                Color   = stone.Lightened(0.12f),
                ZIndex  = 2,
            });

        // Fissure d'Aether — lueur intérieure de la pierre
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(-2f, top + 10f), new(2f, -4f), new(-1f, 8f), new(3f, 20f) },
            Width        = 2f,
            DefaultColor = accent,
            ZIndex       = 3,
        });

        body.AddChild(Outline(shaft, accent));
        AddHalo(body, accent, 20f, 6f, 0.8f);
    }

    // ─── Aether — Cristal à facettes ───────────────────────────────────────────

    private static void BuildCrystal(StaticBody2D body, Color accent, int index)
    {
        body.AddChild(new CollisionShape2D
        {
            Shape    = new CapsuleShape2D { Radius = 12f, Height = 0f },
            Position = new Vector2(0f, 10f),
        });
        AddShadow(body, 18f, 6f, 24f);

        var pts  = new Vector2[] { new(0f, -38f), new(13f, -14f), new(9f, 22f), new(-9f, 22f), new(-13f, -14f) };
        var dark = new Color(accent.R * 0.30f, accent.G * 0.22f, accent.B * 0.38f);
        body.AddChild(new Polygon2D { Polygon = pts, Color = dark, ZIndex = 1 });

        // Facette éclairée (côté gauche) + arête centrale claire
        body.AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(0f, -38f), new(-13f, -14f), new(-9f, 22f), new(0f, 16f) },
            Color   = new Color(accent, 0.45f),
            ZIndex  = 2,
        });
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(0f, -38f), new(0f, 16f) },
            Width        = 1.5f,
            DefaultColor = accent.Lerp(Colors.White, 0.5f),
            ZIndex       = 3,
        });
        body.AddChild(Outline(pts, accent));

        // Éclat satellite, alterné gauche/droite pour varier les silhouettes
        float sx  = index % 2 == 0 ? 16f : -16f;
        var   sat = new Node2D { Position = new Vector2(sx, 13f) };
        var   sp  = new Vector2[] { new(0f, -12f), new(5f, -3f), new(0f, 9f), new(-5f, -3f) };
        sat.AddChild(new Polygon2D { Polygon = sp, Color = dark.Lightened(0.08f), ZIndex = 1 });
        sat.AddChild(Outline(sp, accent, 1.5f));
        body.AddChild(sat);

        AddHalo(body, accent, 22f, 0f, 1.0f);
    }

    // ─── Fournaise — Bloc de basalte veiné de lave ─────────────────────────────

    private static void BuildBasalt(StaticBody2D body, Color accent)
    {
        body.AddChild(new CollisionShape2D
        {
            Shape    = new RectangleShape2D { Size = new Vector2(46f, 26f) },
            Position = new Vector2(0f, 2f),
        });
        AddShadow(body, 30f, 8f, 20f);

        var pts  = new Vector2[] { new(-27f, 4f), new(-18f, -16f), new(-2f, -20f), new(17f, -15f),
                                   new(27f, 4f), new(17f, 17f), new(-17f, 17f) };
        var rock = new Color(0.13f, 0.09f, 0.08f);
        body.AddChild(new Polygon2D { Polygon = pts, Color = rock, ZIndex = 1 });

        // Facette supérieure plus claire (relief)
        body.AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(-18f, -16f), new(-2f, -20f), new(17f, -15f), new(10f, -6f), new(-10f, -5f) },
            Color   = rock.Lightened(0.10f),
            ZIndex  = 2,
        });

        // Veines de lave incandescentes
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(-20f, 6f), new(-8f, 1f), new(-2f, 8f), new(8f, 3f), new(18f, 9f) },
            Width        = 2.5f,
            DefaultColor = accent,
            ZIndex       = 3,
        });
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(-4f, -14f), new(2f, -6f), new(-2f, 0f) },
            Width        = 2f,
            DefaultColor = accent,
            ZIndex       = 3,
        });
        // Points de braise
        foreach (var p in new Vector2[] { new(-12f, 10f), new(14f, -2f) })
            body.AddChild(new Polygon2D
            {
                Polygon = new Vector2[] { new(p.X - 1.5f, p.Y - 1.5f), new(p.X + 1.5f, p.Y - 1.5f),
                                          new(p.X + 1.5f, p.Y + 1.5f), new(p.X - 1.5f, p.Y + 1.5f) },
                Color   = accent.Lerp(Colors.White, 0.35f),
                ZIndex  = 3,
            });

        body.AddChild(Outline(pts, accent));
        AddHalo(body, accent, 26f, 4f, 0.9f);
    }

    // ─── Givre — Monolithe de glace ────────────────────────────────────────────

    private static void BuildIceMonolith(StaticBody2D body, Color accent)
    {
        body.AddChild(new CollisionShape2D
        {
            Shape    = new CapsuleShape2D { Radius = 13f, Height = 0f },
            Position = new Vector2(0f, 12f),
        });
        AddShadow(body, 20f, 7f, 26f);

        var pts = new Vector2[] { new(-3f, -40f), new(9f, -24f), new(14f, 22f), new(-14f, 22f), new(-11f, -20f) };
        var ice = new Color(accent.R * 0.45f, accent.G * 0.55f, accent.B * 0.62f);
        body.AddChild(new Polygon2D { Polygon = pts, Color = ice, ZIndex = 1 });

        // Facette gauche translucide + arête de lumière au sommet
        body.AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(-3f, -40f), new(-11f, -20f), new(-14f, 22f), new(-4f, 18f) },
            Color   = new Color(accent, 0.38f),
            ZIndex  = 2,
        });
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(-3f, -40f), new(9f, -24f) },
            Width        = 2f,
            DefaultColor = accent.Lerp(Colors.White, 0.6f),
            ZIndex       = 3,
        });
        body.AddChild(Outline(pts, accent));

        // Éclat de glace secondaire au pied
        var sat = new Node2D { Position = new Vector2(-19f, 12f) };
        var sp  = new Vector2[] { new(0f, -15f), new(7f, 8f), new(-7f, 8f) };
        sat.AddChild(new Polygon2D { Polygon = sp, Color = ice.Lightened(0.08f), ZIndex = 1 });
        sat.AddChild(Outline(sp, accent, 1.5f));
        body.AddChild(sat);

        AddHalo(body, accent, 22f, 4f, 0.85f);
    }

    // ─── Néon — Pylône de données ──────────────────────────────────────────────

    private static void BuildDataPylon(StaticBody2D body, Color accent)
    {
        body.AddChild(new CollisionShape2D
        {
            Shape    = new RectangleShape2D { Size = new Vector2(26f, 46f) },
            Position = new Vector2(0f, 3f),
        });
        AddShadow(body, 18f, 6f, 28f);

        var pts = new Vector2[] { new(-14f, -30f), new(14f, -30f), new(14f, 26f), new(-14f, 26f) };
        body.AddChild(new Polygon2D { Polygon = pts, Color = new Color(0.07f, 0.05f, 0.11f), ZIndex = 1 });

        // Bandes de données horizontales (alternance vive/atténuée)
        for (int k = 0; k < 3; k++)
            body.AddChild(new Line2D
            {
                Points       = new Vector2[] { new(-10f, -20f + k * 12f), new(10f, -20f + k * 12f) },
                Width        = 3f,
                DefaultColor = new Color(accent, k % 2 == 0 ? 1f : 0.55f),
                ZIndex       = 3,
            });

        // Antenne + diode au sommet
        body.AddChild(new Line2D
        {
            Points       = new Vector2[] { new(0f, -30f), new(0f, -46f) },
            Width        = 2f,
            DefaultColor = accent,
            ZIndex       = 3,
        });
        body.AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(-2f, -49f), new(2f, -49f), new(2f, -45f), new(-2f, -45f) },
            Color   = accent.Lerp(Colors.White, 0.5f),
            ZIndex  = 3,
        });

        body.AddChild(Outline(pts, accent));
        AddHalo(body, accent, 24f, -4f, 1.0f);
    }

    // ─── Grammaire commune de lisibilité ───────────────────────────────────────

    /// <summary>Ombre portée elliptique au pied de l'obstacle (ancre l'objet au sol).</summary>
    private static void AddShadow(Node2D parent, float halfW, float halfH, float y)
    {
        var pts = new Vector2[12];
        for (int i = 0; i < 12; i++)
        {
            float a = Mathf.Tau * i / 12f;
            pts[i] = new Vector2(Mathf.Cos(a) * halfW, y + Mathf.Sin(a) * halfH);
        }
        parent.AddChild(new Polygon2D { Polygon = pts, Color = new Color(0f, 0f, 0f, 0.38f), ZIndex = 0 });
    }

    /// <summary>Contour accent vif fermé — la frontière infranchissable doit sauter aux yeux.</summary>
    private static Line2D Outline(Vector2[] pts, Color color, float width = 2.5f)
    {
        var loop = new Vector2[pts.Length + 1];
        pts.CopyTo(loop, 0);
        loop[^1] = pts[0];
        return new Line2D
        {
            Points       = loop,
            Width        = width,
            DefaultColor = color,
            JointMode    = Line2D.LineJointMode.Round,
            ZIndex       = 4,
        };
    }

    /// <summary>Halo lumineux additif — repérable de loin même en combat dense.</summary>
    private static void AddHalo(Node2D parent, Color accent, float radius, float offsetY, float energy)
    {
        _haloTex ??= Player.MakeRadialLightTexture(64);
        parent.AddChild(new PointLight2D
        {
            Position     = new Vector2(0f, offsetY),
            Color        = accent,
            Energy       = energy,
            Texture      = _haloTex,
            TextureScale = radius / 11f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        });
    }
}
