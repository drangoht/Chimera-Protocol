using UnityEngine;

/// <summary>
/// Pastille radiale douce, partagée : le grain de poussière en suspension de l'atmosphère.
///
/// <para>Équivalent de <c>Player.MakeRadialLightTexture(16)</c> sous Godot, qui sert exactement au
/// même usage. Elle fait <b>1 unité</b> de côté : l'échelle passée par l'appelant est donc une
/// taille en pixels, ce qui n'est vrai d'aucun sprite importé du jeu.</para>
/// </summary>
public static class SoftDotSprite
{
    private const int Size = 32;

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var pixels = new Color[Size * Size];
        var center = new Vector2(Size / 2f, Size / 2f);
        float radius = Size / 2f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;

            // Chute au carré : un dégradé linéaire laisse un disque au bord encore net, et c'est le
            // bord qui trahit une particule.
            float a = Mathf.Clamp01(1f - d);
            pixels[y * Size + x] = new Color(1f, 1f, 1f, a * a);
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        // PPU = Size : le sprite mesure 1 unité, donc `localScale = n` donne n pixels.
        _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        return _sprite;
    }
}
