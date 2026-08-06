using UnityEngine;

/// <summary>
/// Ombrage pseudo-3D des sprites fabriqués à l'exécution — portage de <c>tools/pseudo3d_lib.py</c>
/// (<c>docs/ART_BRIEF_PSEUDO3D.md</c>).
///
/// <para>Le brief pose une lumière venue du <b>haut-gauche</b>, une ombre en bas, un contact assombri
/// au sol, et un contour d'un pixel qui décolle la silhouette des fonds clairs. Tout le bestiaire et
/// tout le décor du jeu sont dessinés ainsi ; une pièce fabriquée au runtime sans cet ombrage se
/// reconnaît immédiatement comme une vignette plate posée sur le jeu.</para>
///
/// <para>⚠ <b>Un ombrage cuit suppose une lumière FIXE.</b> Ce qui pivote (l'œil d'une greffe, le canon
/// d'une tourelle) emporterait sa lumière avec lui et trahirait l'illusion : ces pièces-là doivent
/// être traitées comme de l'<b>énergie</b> — lumineuses, non ombrées — et non comme de la matière.</para>
/// </summary>
public static class Pseudo3D
{
    /// <summary>Contour bleu-nuit du brief (§5), aligné sur le fond d'interface.</summary>
    public static readonly Color Outline = new(0.047f, 0.043f, 0.078f, 0.75f);

    private enum Face { Highlight, BaseLight, Shadow, Contact }

    /// <summary>
    /// Classe chaque pixel opaque par sa place dans la silhouette et lui applique la face
    /// correspondante — portage direct de <c>shade_sprite</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ En espace texture, <c>y</c> croît vers le <b>haut</b> : le contact est en <c>y - minY</c> et
    /// la lumière en <c>maxY - y</c>. Inverser les deux donne un objet éclairé par le sol — discret,
    /// et immédiatement « faux » à l'œil sans qu'on puisse dire pourquoi.
    ///
    /// <para>Les pixels semi-transparents (une ombre portée dessinée sous la pièce) sont exclus du
    /// calcul des bornes comme de l'ombrage.</para>
    /// </remarks>
    public static void Shade(Color[] px, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (px[y * width + x].a < 0.999f) continue;
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
            var c = px[y * width + x];
            if (c.a < 0.999f) continue;

            int fromBottom = y - minY;
            int fromTop = maxY - y;

            Face face;
            if (fromBottom < contactRows)     face = Face.Contact;
            else if (fromTop < highlightRows) face = Face.Highlight;
            else face = (x - minX) < w * 0.5f ? Face.BaseLight : Face.Shadow;

            px[y * width + x] = Apply(c, face);
        }
    }

    /// <summary>
    /// Applique une face au pixel.
    /// </summary>
    /// <remarks>
    /// ⚠ Les coefficients agissent en <b>TSV</b>, sur la valeur et la saturation, jamais en RVB. La
    /// contrainte dure du brief est que la <i>teinte</i> ne bouge pas : un assombrissement RVB
    /// désature vers le gris et fait virer un objet cyan au bleu sale, ce qui se lit « mauvaise
    /// couleur » et non « dans l'ombre ». Et jamais de noir ni de blanc purs.
    /// </remarks>
    private static Color Apply(Color c, Face face)
    {
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
    public static void AddOutline(Color[] px, int width, int height)
    {
        var outlined = new Color[px.Length];
        System.Array.Copy(px, outlined, px.Length);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (px[y * width + x].a > 0.5f) continue;

            bool touches = false;

            for (int dy = -1; dy <= 1 && !touches; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (px[ny * width + nx].a > 0.999f) { touches = true; break; }
            }

            if (touches) outlined[y * width + x] = Outline;
        }

        System.Array.Copy(outlined, px, px.Length);
    }

    /// <summary>
    /// Emballe un tampon de pixels en sprite.
    /// </summary>
    /// <remarks>
    /// PPU 1, comme tous les sprites du monde : le sprite mesure <paramref name="width"/> unités, et
    /// l'échelle qu'on lui donnera est un <b>rapport</b> — jamais une taille posée telle quelle.
    /// </remarks>
    public static Sprite Make(Color[] px, int width, int height, Vector2? pivot = null)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height),
                             pivot ?? new Vector2(0.5f, 0.5f), 1f);
    }
}
