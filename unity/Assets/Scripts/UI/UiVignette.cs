using UnityEngine;

/// <summary>
/// Vignette d'assombrissement des bords — portage du shader <c>VignetteShader</c> du menu.
///
/// <para>Sous Godot c'était un shader de trois lignes ; ici c'est une <b>texture générée</b>. Le
/// dégradé est statique et couvre l'écran entier : lui consacrer un shader imposerait un matériau à
/// référencer, à embarquer dans le build et à maintenir, pour un résultat identique au pixel près.
/// Un shader ne se justifie que lorsque quelque chose <b>varie</b>.</para>
///
/// <para>Elle n'est pas décorative : l'illustration de couverture est claire en son centre — néons,
/// grille au sol — et les boutons s'y perdent. La vignette recrée le contraste que le fond uni
/// donnait gratuitement.</para>
/// </summary>
public static class UiVignette
{
    private const int Size = 128;

    private static Sprite? _sprite;

    /// <summary>Dégradé radial : transparent au centre, opaque aux coins.</summary>
    public static Sprite Sprite
    {
        get
        {
            if (_sprite != null) return _sprite;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Mêmes bornes que le shader d'origine : rien avant 0,35 du rayon, plein à 0,75.
                float dx = (x + 0.5f) / Size - 0.5f;
                float dy = (y + 0.5f) / Size - 0.5f;
                float t = Mathf.SmoothStep(0.35f, 0.75f, Mathf.Sqrt(dx * dx + dy * dy));

                px[y * Size + x] = new Color32(255, 255, 255, (byte)(t * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            return _sprite;
        }
    }
}
