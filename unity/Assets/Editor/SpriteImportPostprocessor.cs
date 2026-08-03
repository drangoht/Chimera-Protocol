using UnityEditor;
using UnityEngine;

/// <summary>
/// Applique automatiquement les réglages d'import du pixel art à tout ce qui entre dans
/// <c>Assets/Art/</c> (docs/UNITY_MIGRATION_PLAN.md §7.1).
///
/// <para><b>Pourquoi ce fichier doit exister AVANT le premier import.</b> Les valeurs par défaut
/// d'Unity — filtrage bilinéaire et compression avec perte — <b>détruisent</b> du pixel art 32×32 :
/// contours flous, franges de couleur sur l'alpha. Le projet compte <b>905 PNG</b> ; les importer
/// avant ce script obligerait à tout réimporter. C'est pourquoi le plan en fait le tout premier
/// fichier Unity à écrire.</para>
///
/// <para><b>Le réglage le plus structurant : <c>spritePixelsPerUnit = 1</c>.</b> Godot travaille en
/// <b>pixels</b> comme unité de monde. Choisir 1 pixel = 1 unité Unity fait que toutes les valeurs
/// numériques du jeu — vitesses (380), rayons de contact (24), demi-arène (900), distances de
/// kite (250) — se transposent <b>telles quelles</b>, sur ~24 300 lignes. N'importe quelle autre
/// valeur imposerait un facteur de conversion à chaque coordonnée, c'est-à-dire une classe entière
/// de bugs silencieux (un seul oubli et une hitbox est fausse).</para>
/// </summary>
public sealed class SpriteImportPostprocessor : AssetPostprocessor
{
    private const string ArtRoot = "Assets/Art/";

    /// <summary>
    /// Incrémenter cette version force Unity à réimporter tous les assets concernés — à faire si
    /// l'un des réglages ci-dessous change, sinon les assets déjà importés gardent les anciens.
    /// </summary>
    public override uint GetVersion() => 2;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRoot, System.StringComparison.Ordinal)) return;

        var importer = (TextureImporter)assetImporter;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1f;              // 1 px = 1 unité — voir la remarque de classe
        importer.filterMode          = FilterMode.Point; // équivalent de texture_filter = Nearest
        importer.mipmapEnabled       = false;
        importer.wrapMode            = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture         = true;

        // Maillage et pivot vivent dans TextureImporterSettings, et non sur l'importeur lui-même.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        // Maillage plein : le mode « Tight » recadre sur les pixels opaques, ce qui décale le pivot
        // d'un sprite dont les bords sont transparents — donc décale la silhouette par rapport à sa
        // hitbox. Sur des sprites 32×32 le gain de remplissage serait de toute façon négligeable.
        settings.spriteMeshType  = SpriteMeshType.FullRect;
        settings.spriteExtrude   = 0;

        // Pivot au centre, comme dans Godot où un Sprite2D est centré sur la position du nœud.
        settings.spriteAlignment = (int)SpriteAlignment.Center;

        importer.SetTextureSettings(settings);

        // Compression : aucune. Un format avec perte sur du pixel art produit des franges visibles
        // sur les aplats et l'alpha. Le coût mémoire de 905 sprites 32×32 non compressés est
        // négligeable (~3,7 Mo en RGBA32).
        var platform = importer.GetDefaultPlatformTextureSettings();
        platform.format             = TextureImporterFormat.RGBA32;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);
    }
}
