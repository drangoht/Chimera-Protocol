using UnityEngine;

/// <summary>
/// Silhouettes d'une tourelle de la Ruche : un <b>châssis</b> ombré et un <b>canon</b> qui pivote.
///
/// <para><b>Pourquoi deux pièces et pas une.</b> La première version reprenait telle quelle la flèche
/// du jeu publié — trois polygones plats empilés, tournés vers la cible. C'était lisible et
/// parfaitement étranger au reste du jeu, dont <i>tous</i> les sprites suivent le brief pseudo-3D
/// (<c>docs/ART_BRIEF_PSEUDO3D.md</c>) : lumière venue du haut-gauche, ombre au bas, contact assombri
/// au sol.</para>
///
/// <para>Or un ombrage cuit dans la texture suppose une lumière <b>fixe</b> — et une pièce qui tourne
/// emporte sa lumière avec elle, ce qui trahit immédiatement l'illusion. D'où la séparation, qui
/// n'est pas un contournement mais la bonne lecture de l'objet : le châssis est de la <b>matière</b>
/// (il ne tourne pas, il est ombré, il porte une ombre au sol), le canon est de l'<b>énergie</b> (il
/// pivote, il émet — une lumière ne s'ombre pas).</para>
/// </summary>
public static class TurretSprite
{
    private const int Size = 32;

    /// <summary>Diamètre du châssis à l'échelle 1, en pixels — le repère de dimensionnement.</summary>
    public const float BodyPx = 18f;

    private static Sprite? _body;
    private static Sprite? _barrel;

    /// <summary>Châssis ombré, immobile.</summary>
    public static Sprite Body => _body != null ? _body : _body = BuildBody();

    /// <summary>Canon lumineux, orienté vers la cible.</summary>
    public static Sprite Barrel => _barrel != null ? _barrel : _barrel = BuildBarrel();

    // ─── Palette ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cyan de la palette d'interface — <b>délibérément différent de la teinte de la greffe</b>.
    /// C'est un résultat de playtest (BUG-F01, 2026-07-07) : les tourelles doivent trancher sur une
    /// faune de rouille orange, et se confondre avec elle les rendrait inutiles à regarder.
    /// </summary>
    private static readonly Color Chassis = new(0.27f, 1f, 0.93f);

    /// <summary>Cœur clair — le même rôle que sur les sprites d'ennemis : il empêche l'aplat.</summary>
    private static readonly Color Core = new(0.85f, 1f, 0.98f);

    /// <summary>
    /// Contour bleu-nuit du brief (§5), aligné sur le fond d'interface. Sans lui, un objet cyan
    /// disparaît sur les sols clairs du Sanctuaire.
    /// </summary>
    private static readonly Color Outline = new(0.047f, 0.043f, 0.078f, 0.75f);

    // ─── Châssis ──────────────────────────────────────────────────────────────

    private static Sprite BuildBody()
    {
        var px = new Color[Size * Size];
        float half = Size * 0.5f;

        // Ombre portée AVANT le corps, donc dessous : une ellipse écrasée au pied du châssis (§3 du
        // brief). C'est elle, plus que l'ombrage, qui décolle l'objet du sol — sans elle une tourelle
        // ombrée reste une vignette posée à plat.
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = (x + 0.5f - half) / (BodyPx * 0.42f);
            float dy = (y + 0.5f - (half - BodyPx * 0.44f)) / (BodyPx * 0.19f);

            float d = dx * dx + dy * dy;
            if (d > 1f) continue;

            px[y * Size + x] = new Color(0.02f, 0.02f, 0.05f, 0.34f * (1f - d * 0.6f));
        }

        // Châssis octogonal : ni disque (qui roule) ni carré (qui est un bloc de décor). Un octogone
        // se lit comme une pièce usinée, ce qu'est une tourelle.
        float r = BodyPx * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float ax = Mathf.Abs(x + 0.5f - half);
            float ay = Mathf.Abs(y + 0.5f - half);

            // Intersection d'un carré et d'un losange = octogone régulier.
            if (ax > r || ay > r || ax + ay > r * 1.42f) continue;

            float dist = Mathf.Sqrt(ax * ax + ay * ay) / r;
            px[y * Size + x] = dist < 0.34f ? Core : Chassis;
        }

        Shade(px);
        AddOutline(px);

        return Make(px);
    }

    // ─── Canon ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Canon : une lance courte qui s'affine et s'éclaircit vers la pointe.
    /// </summary>
    /// <remarks>
    /// Le pivot est posé à la <b>base</b> (0 sur l'axe long) et non au centre : la pièce doit tourner
    /// autour du châssis, pas autour d'elle-même. Un pivot centré ferait pendre le canon d'un côté
    /// puis de l'autre au lieu de viser.
    /// </remarks>
    private static Sprite BuildBarrel()
    {
        const int W = 16;
        const int H = 8;

        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);                    // 0 à la base, 1 à la pointe
            float halfWidth = Mathf.Lerp(2.6f, 0.9f, t);     // s'affine

            float dy = Mathf.Abs(y + 0.5f - H * 0.5f);
            if (dy > halfWidth) continue;

            // S'éclaircit vers la pointe : c'est ce dégradé qui le fait lire comme une émission et
            // non comme une tige peinte.
            px[y * W + x] = Color.Lerp(Chassis, Core, t * t);
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.12f, 0.5f), 1f);
    }

    // ─── Ombrage pseudo-3D (portage de tools/pseudo3d_lib.py) ─────────────────

    /// <summary>
    /// Classe chaque pixel opaque par sa place dans la silhouette et lui applique la face
    /// correspondante — portage direct de <c>shade_sprite</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Les coefficients agissent en <b>TSV</b>, sur la valeur et la saturation, jamais en RVB. La
    /// contrainte dure du brief est que la <i>teinte</i> ne bouge pas : un assombrissement RVB
    /// désature vers le gris et fait virer un objet cyan au bleu sale, ce qui se lit « mauvaise
    /// couleur » et non « dans l'ombre ». Et jamais de noir ni de blanc purs.
    /// </remarks>
    private static void Shade(Color[] px)
    {
        // Bornes de la silhouette opaque — l'ombre portée est semi-transparente, donc exclue.
        int minX = Size, minY = Size, maxX = -1, maxY = -1;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (px[y * Size + x].a < 0.999f) continue;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        if (maxX < 0) return;

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;

        int contactRows = h <= 10 ? 1 : 2;
        int highlightRows = Mathf.Max(1, Mathf.RoundToInt(h * 0.30f));

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var c = px[y * Size + x];
            if (c.a < 0.999f) continue;

            // ⚠ En espace texture, y croît vers le HAUT : le contact est donc en bas (y - minY) et
            // la lumière en haut (maxY - y). Inverser les deux donne un objet éclairé par le sol,
            // défaut discret et immédiatement « faux » à l'œil sans qu'on sache dire pourquoi.
            int fromBottom = y - minY;
            int fromTop = maxY - y;

            Face face;
            if (fromBottom < contactRows)     face = Face.Contact;
            else if (fromTop < highlightRows) face = Face.Highlight;
            else face = (x - minX) < w * 0.5f ? Face.BaseLight : Face.Shadow;

            px[y * Size + x] = Apply(c, face);
        }
    }

    private enum Face { Highlight, BaseLight, Shadow, Contact }

    private static Color Apply(Color c, Face face)
    {
        // (multiplicateur de Valeur, multiplicateur de Saturation) — FACE_COEFFS du brief.
        // `base_light` est la moyenne explicite de `base` et `highlight` : le flanc gauche d'un
        // volume est à mi-chemin, la lumière venant aussi de la gauche.
        (float dv, float ds) = face switch
        {
            Face.Highlight => (1.35f, 0.85f),
            Face.BaseLight => (1.175f, 0.925f),
            Face.Shadow    => (0.55f, 1.10f),
            _              => (0.35f, 1.15f),
        };

        Color.RGBToHSV(c, out float hue, out float sat, out float val);

        val = Mathf.Clamp(val * dv, 0.02f, 0.98f);   // jamais 0 ni 1 purs
        sat = Mathf.Clamp01(sat * ds);

        var shaded = Color.HSVToRGB(hue, sat, val);
        return new Color(shaded.r, shaded.g, shaded.b, c.a);
    }

    /// <summary>Contour d'un pixel autour de la silhouette — le test de lisibilité du §5.</summary>
    private static void AddOutline(Color[] px)
    {
        var outlined = new Color[px.Length];
        System.Array.Copy(px, outlined, px.Length);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            if (px[y * Size + x].a > 0.5f) continue;

            bool touches = false;

            for (int dy = -1; dy <= 1 && !touches; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                if (px[ny * Size + nx].a > 0.999f) { touches = true; break; }
            }

            if (touches) outlined[y * Size + x] = Outline;
        }

        System.Array.Copy(outlined, px, px.Length);
    }

    private static Sprite Make(Color[] px)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(px);
        tex.Apply();

        // PPU 1, comme tous les sprites du monde : le sprite mesure Size unités, et l'échelle est un
        // RAPPORT — jamais une taille posée telle quelle.
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 1f);
    }
}
