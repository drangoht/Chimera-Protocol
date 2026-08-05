using UnityEngine;

/// <summary>
/// Glyphe lointain — anneau hexagonal, rayons et noyau — aperçu « sous l'arène » à travers les
/// tuiles vitrées. Portage de <c>src/Systems/DeepMotifShape.cs</c>.
///
/// <para>Sous Godot, ce glyphe est un <c>_Draw</c> : des appels de tracé, sans le moindre asset.
/// Unity n'offre pas d'équivalent utilisable ici — le <c>SpriteMask</c> qui confine le motif à sa
/// fenêtre n'agit que sur des <c>SpriteRenderer</c>, ce qui exclut <c>LineRenderer</c> comme un mesh
/// maison. On dessine donc une <b>texture</b> une fois pour toutes et on la partage.</para>
///
/// <para>⚠ Le premier portage prenait <c>vfx_particle_noyau</c> comme motif : un sprite de
/// <b>3 × 3 px</b>, mis à l'échelle 46-72, donc un aplat carré de 200 px. Il ne ressemblait à rien
/// de reconnaissable et se lisait comme une dalle peinte sur le sol — la profondeur ne vient pas de
/// la parallaxe seule, mais de ce qu'on reconnaît une <i>structure</i> au fond.</para>
/// </summary>
public static class DeepMotifSprite
{
    /// <summary>Rayon extérieur de l'hexagone, en pixels de texture (celui de Godot).</summary>
    private const float ROuter = 46f;
    private const float RInner = 25f;

    /// <summary>Marge autour du glyphe pour que les traits épais ne soient pas rognés.</summary>
    private const int Margin = 4;

    private const int Size = (int)(ROuter * 2) + Margin * 2;

    private static Sprite? _sprite;

    /// <summary>Le glyphe, en blanc sur transparent — la teinte est appliquée par le renderer.</summary>
    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var pixels = new Color[Size * Size];
        var center = new Vector2(Size / 2f, Size / 2f);

        // ⚠ Traits plus opaques et plus épais que sous Godot (0,60/0,30 et 2,5/1,5 px). Là-bas le
        // glyphe se détache sur le fond parallaxé du monde ; ici il se lit à travers une vitre
        // teintée et sur un puits sombre, qui lui mangent la moitié de son contraste. Une valeur
        // recopiée d'un moteur à l'autre ne rend pas le même effet — c'est l'effet qu'on porte.
        HexRing(pixels, center, ROuter, 3.0f, 0.90f);
        HexRing(pixels, center, RInner, 2.0f, 0.55f);

        // Un rayon sur deux, comme Godot : six branches feraient une roue pleine, trois laissent
        // lire l'hexagone.
        for (int i = 0; i < 6; i += 2)
        {
            float a = Mathf.PI * 2f * i / 6f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Line(pixels, center + dir * RInner, center + dir * ROuter, 2.0f, 0.55f);
        }

        Disc(pixels, center, 4f, 0.90f);

        // ⚠ `Bilinear` et non `Point` : le glyphe est mis à l'échelle ×2 environ, et un filtrage au
        // plus proche y ferait ressortir l'escalier des traits obliques — un lointain ne doit pas
        // être plus net que le sol qui le cache.
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 1f);
        return _sprite;
    }

    private static void HexRing(Color[] pixels, Vector2 center, float radius, float width, float alpha)
    {
        for (int i = 0; i < 6; i++)
        {
            float a0 = Mathf.PI * 2f * i / 6f;
            float a1 = Mathf.PI * 2f * (i + 1) / 6f;

            Line(pixels,
                 center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
                 center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                 width, alpha);
        }
    }

    /// <summary>Trait épais, tracé en marquant les pixels dont la distance au segment est sous la demi-largeur.</summary>
    private static void Line(Color[] pixels, Vector2 a, Vector2 b, float width, float alpha)
    {
        float half = width * 0.5f;

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half - 1));
        int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half + 1));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half - 1));
        int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half + 1));

        var ab = b - a;
        float lengthSquared = Mathf.Max(ab.sqrMagnitude, 0.0001f);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
            float distance = Vector2.Distance(p, a + ab * t);

            // Dégradé sur le dernier pixel : un tracé binaire crénèlerait les obliques.
            float coverage = Mathf.Clamp01(half + 0.5f - distance);
            if (coverage > 0f) Mark(pixels, x, y, alpha * coverage);
        }
    }

    private static void Disc(Color[] pixels, Vector2 center, float radius, float alpha)
    {
        int min = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1));
        int max = Mathf.Min(Size - 1, Mathf.CeilToInt(center.x + radius + 1));

        for (int y = min; y <= max; y++)
        for (int x = min; x <= max; x++)
        {
            float coverage = Mathf.Clamp01(radius + 0.5f - Vector2.Distance(
                new Vector2(x + 0.5f, y + 0.5f), center));

            if (coverage > 0f) Mark(pixels, x, y, alpha * coverage);
        }
    }

    /// <summary>Pose du blanc, en gardant l'alpha le plus fort — les traits se croisent.</summary>
    private static void Mark(Color[] pixels, int x, int y, float alpha)
    {
        int index = y * Size + x;
        if (alpha > pixels[index].a) pixels[index] = new Color(1f, 1f, 1f, alpha);
    }
}
