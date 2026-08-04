using UnityEngine;

/// <summary>
/// Primitives graphiques partagées par l'interface (Lot 5).
///
/// <para>Vit dans la couche <b>Platform</b> parce que le HUD (assembly Gameplay) et les écrans
/// (assembly UI) en ont tous deux besoin : les placer dans l'un des deux créerait un cycle de
/// dépendances entre assemblies.</para>
/// </summary>
public static class UiPrimitives
{
    private static Sprite? _white;

    /// <summary>
    /// Sprite blanc uni, partagé.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Indispensable dès qu'une <c>Image</c> utilise <c>Image.Type.Filled</c>.</b> Sans sprite,
    /// Unity <b>ignore purement et simplement <c>fillAmount</c></b> et dessine le quad entier : une
    /// barre de vie ou d'XP reste alors visuellement pleine quoi qu'il arrive, sans erreur ni
    /// avertissement. Le symptôme se lit « les valeurs ne changent pas » alors que le jeu, lui,
    /// fonctionne parfaitement — c'est exactement ce qui a été observé en jouant.
    /// </remarks>
    public static Sprite White
    {
        get
        {
            if (_white != null) return _white;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();

            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }
    }
}
