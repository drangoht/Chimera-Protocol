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
        if (assetPath.Contains("/Resources/Environment/")) { ConfigureTile(); return; }
        if (!assetPath.Contains("/Resources/UiFrames/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;   // pixel art : jamais de lissage
        importer.mipmapEnabled = false;
        ConfigureFrame(importer);
    }

    /// <summary>
    /// Tuile de sol : elle doit se <b>répéter</b>, pas s'étirer.
    ///
    /// <para>⚠ <c>SpriteDrawMode.Tiled</c> exige un maillage <b>plein</b> (<c>FullRect</c>). Avec le
    /// maillage serré par défaut, Unity ne répète rien : il étire une seule tuile de 32 px sur toute
    /// l'arène, ce qui donne un aplat uni — un sol qui ressemble exactement au vide qu'il devait
    /// remplacer, sans la moindre erreur.</para>
    /// </summary>
    private void ConfigureTile()
    {
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;

        // 1 px = 1 unité, comme tous les sprites de monde du projet (§7.1).
        importer.spritePixelsPerUnit = 1f;

        // Le maillage plein ne s'expose pas sur l'importeur lui-même : il passe par les réglages de
        // sprite, et c'est LA condition du mode répété.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    private void ConfigureFrame(TextureImporter importer)
    {

        // ⚠ 100, et surtout PAS 1. Le reste du projet importe ses sprites en 1 px = 1 unité (décision
        // structurante du portage), mais une <c>Image</c> uGUI met ses bordures de découpe à l'échelle
        // de <c>referencePixelsPerUnit / spritePixelsPerUnit</c> — soit 100 / 1 = ×100. Les coins d'un
        // cadre de 48 px se dessinaient donc sur 1 600 pixels : c'est ce qui rendait l'interface
        // méconnaissable.
        importer.spritePixelsPerUnit = 100f;

        bool isPopup  = assetPath.Contains("ui_frame_popup");
        bool isButton = assetPath.Contains("ui_frame_button") || assetPath.Contains("ui_frame_card");

        // (gauche, bas, droite, haut)
        importer.spriteBorder = isPopup
            ? new Vector4(20f, 20f, 20f, 28f)
            : isButton ? new Vector4(16f, 22f, 16f, 16f)
                       : new Vector4(16f, 16f, 16f, 16f);
    }
}
