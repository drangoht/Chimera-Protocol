using UnityEditor;
using UnityEngine;

/// <summary>
/// Découpe en neuf zones des cadres « plaque blindée » (lot de parité visuelle).
///
/// <para>Ces PNG de 48×48 ne sont pas des images à étirer : ce sont des <b>cadres</b>. Sans bordure
/// de découpe, Unity dilate les chanfreins, les rivets et le biseau avec le reste — un panneau large
/// se retrouve avec des coins étirés en bouillie, et des rivets ovales. La bordure fige les coins et
/// ne répète que les bords.</para>
///
/// <para>Les marges reprennent celles du projet Godot, à l'unité près : <b>16</b> partout, sauf le
/// bas des boutons à <b>22</b> — ce bord est « soudé », plus épais que les trois autres, et le couper
/// à 16 tronquerait l'ombre portée qui donne l'épaisseur de la plaque.</para>
/// </summary>
public sealed class UiFrameImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/UiFrames/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;   // pixel art : jamais de lissage
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 1f;

        bool isPopup  = assetPath.Contains("ui_frame_popup");
        bool isButton = assetPath.Contains("ui_frame_button") || assetPath.Contains("ui_frame_card");

        // (gauche, bas, droite, haut)
        importer.spriteBorder = isPopup
            ? new Vector4(20f, 20f, 20f, 28f)
            : isButton ? new Vector4(16f, 22f, 16f, 16f)
                       : new Vector4(16f, 16f, 16f, 16f);
    }
}
