using UnityEngine;

/// <summary>
/// Silhouette d'un drone orbital : un <b>losange</b> à cœur clair, reprise du <c>Polygon2D</c> de
/// <c>DroneEntity.tscn</c> (quatre sommets, cyan).
///
/// <para>⚠ Le portage posait <c>UiPrimitives.White</c> : un <b>carré</b>. Rien ne cassait — l'essaim
/// orbitait, frappait, tuait — mais cinq carrés qui tournent autour du joueur ne se lisent pas comme
/// des drones ; ils se lisent comme un placeholder oublié. C'est la deuxième fois dans ce portage
/// qu'une primitive de dépannage survit jusqu'à l'écran, après les motifs de sol.</para>
///
/// <para>Le losange n'est pas qu'un décor : il <b>pointe</b>, donc il donne une orientation à un
/// objet qui tourne, ce qu'un carré ne peut pas faire.</para>
/// </summary>
public static class DroneSprite
{
    private const int Size = 16;

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var pixels = new Color[Size * Size];
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            // Distance de Manhattan au centre : sa boule est un losange.
            float d = (Mathf.Abs(x + 0.5f - half) + Mathf.Abs(y + 0.5f - half)) / half;
            if (d > 1f) continue;

            // Cœur clair et bord soutenu : sans ce dégradé, le losange s'aplatit en tache unie dès
            // qu'il passe devant un sol de la même teinte.
            float t = Mathf.Clamp01(1f - d);
            pixels[y * Size + x] = new Color(0.55f + 0.45f * t, 1f, 1f, 1f);
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        // PPU 1, comme tous les sprites de monde : `localScale` vaut alors une taille en pixels.
        _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 1f);
        return _sprite;
    }
}
