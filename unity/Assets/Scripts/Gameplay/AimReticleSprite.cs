using UnityEngine;

/// <summary>
/// Réticule des armes dirigées : un chevron creux pointant dans la direction visée.
///
/// <para>Un point ou un cercle diraient <i>où</i> sans dire <i>vers où</i> — or c'est la direction
/// qui compte, la Lance Vectorielle traversant tout ce qui est aligné. La forme est asymétrique
/// exprès : orientée par rotation, elle reste lisible sous n'importe quel angle.</para>
///
/// <para>Sprite tracé au runtime plutôt qu'importé : il pèse un pixel de dépôt, et surtout il ne
/// peut pas manquer à l'appel — le lot précédent a montré qu'un asset présent mais non atteint
/// (hors de <c>Resources/</c>, ou sans table) est indiscernable d'un asset absent.</para>
/// </summary>
public static class AimReticleSprite
{
    private const int Size = 24;

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var pixels = new Color[Size * Size];

        // Chevron « > » : deux branches partant de la pointe droite. Épaisseur 2 px pour rester
        // visible sur un sol clair comme sur un sol sombre.
        for (int i = 0; i < Size; i++)
        for (int j = 0; j < Size; j++)
        {
            float x = i - Size * 0.5f + 0.5f;
            float y = j - Size * 0.5f + 0.5f;

            // Distance au « V » : |y| = pente × (pointe − x).
            float branch = Mathf.Abs(Mathf.Abs(y) - (9f - x) * 0.8f);
            bool inside = x > -2f && x < 10f && Mathf.Abs(y) < 9f && branch < 1.4f;

            if (inside) pixels[j * Size + i] = new Color(1f, 1f, 1f, 1f);
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // net : c'est un repère, pas un effet
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        // PPU 1 : le sprite mesure 24 unités, soit 24 px du monde — la même convention que les
        // sprites d'entités du projet.
        _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 1f);
        return _sprite;
    }
}
