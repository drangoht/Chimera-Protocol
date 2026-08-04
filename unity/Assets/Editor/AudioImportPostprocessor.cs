using UnityEditor;
using UnityEngine;

/// <summary>
/// Réglages d'import des sons, appliqués automatiquement (Lot audio).
///
/// <para>Même principe que le postprocessor de sprites : 41 fichiers ne se règlent pas à la souris,
/// et un réglage oublié ne se voit pas — il s'entend, plus tard, sur une machine plus lente.</para>
///
/// <para><b>Musiques et effets n'ont pas le même besoin.</b> Une piste de 2 à 3 minutes chargée
/// entièrement en mémoire décompressée coûte des dizaines de mégaoctets et fige le chargement de la
/// scène ; un effet court diffusé en streaming, lui, arrive <b>en retard</b> sur l'action et se met à
/// craquer dès que trente ennemis meurent en même temps. D'où deux traitements opposés.</para>
/// </summary>
public sealed class AudioImportPostprocessor : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.Contains("/Audio/")) return;

        var importer = (AudioImporter)assetImporter;
        bool isMusic = assetPath.Contains("/Audio/music/");

        var settings = importer.defaultSampleSettings;

        if (isMusic)
        {
            // Diffusée depuis le disque : la mémoire ne porte qu'un tampon, pas la piste entière.
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
        }
        else
        {
            // Décompressé au chargement : un effet doit partir à l'instant où l'action a lieu, et
            // supporter d'être joué des dizaines de fois par seconde.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
        }

        // Le jeu est en 2D : la spatialisation ferait varier le volume selon la position d'une
        // source qui, elle, n'a pas de position — des effets tantôt inaudibles, tantôt saturés.
        importer.forceToMono = isMusic ? false : true;
        importer.defaultSampleSettings = settings;
    }

    private void OnPostprocessAudio(AudioClip clip)
    {
        if (assetPath.Contains("/Audio/"))
            Debug.Log($"[AudioImport] {System.IO.Path.GetFileName(assetPath)} — {clip.length:F1} s");
    }
}
