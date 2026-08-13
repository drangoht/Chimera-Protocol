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

        ApplyWebOverride(importer, isMusic);
    }

    /// <summary>
    /// Réglages propres au web, où les deux choix ci-dessus sont l'un impossible et l'autre ruineux.
    /// </summary>
    /// <remarks>
    /// <para><b>La musique ne peut pas être diffusée en streaming.</b> <c>AudioClipLoadType.Streaming</c>
    /// n'existe pas en WebGL : le navigateur n'expose pas de lecture par tampon depuis un fichier
    /// local. Unity ne refuse pas le réglage — il en substitue un autre, en silence. Le symptôme
    /// observé au premier essai était deux lignes dans la console du navigateur, « Trying to get
    /// length of sound which is not loaded yet », et une musique dont on ne pouvait pas connaître la
    /// durée : de quoi casser un fondu croisé sans jamais lever d'erreur.</para>
    ///
    /// <para><b>Et les effets ne peuvent pas rester en PCM.</b> Sur disque, du son non compressé
    /// coûte de la place ; sur le web, il coûte du <b>temps de chargement chez le joueur</b>, et
    /// chaque mégaoctet se paie avant le premier écran. Vorbis divise ce poids sans changer ce qui
    /// compte pour un effet — qu'il soit décompressé en mémoire, donc prêt à partir à l'instant où
    /// l'action a lieu.</para>
    /// </remarks>
    private static void ApplyWebOverride(AudioImporter importer, bool isMusic)
    {
        var web = importer.defaultSampleSettings;

        // Compressée en mémoire et décodée à la volée : la seule façon de ne pas payer 22 Mo de
        // musique décompressée dans le tas d'un onglet.
        web.loadType = isMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        web.compressionFormat = AudioCompressionFormat.Vorbis;
        web.quality = isMusic ? 0.7f : 0.6f;

        // Une piste chargée à l'ouverture du jeu retarde le premier écran ; un effet chargé au
        // moment où il doit sonner arrive en retard. D'où le même partage que sur disque.
        web.preloadAudioData = !isMusic;

        importer.SetOverrideSampleSettings("WebGL", web);
    }

    private void OnPostprocessAudio(AudioClip clip)
    {
        if (assetPath.Contains("/Audio/"))
            Debug.Log($"[AudioImport] {System.IO.Path.GetFileName(assetPath)} — {clip.length:F1} s");
    }

    /// <summary>
    /// Réimporte tous les sons, pour que les règles ci-dessus s'appliquent aux fichiers déjà présents.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Un postprocessor ne vaut que pour ce qui est importé après lui.</b> Modifier ces règles
    /// ne touche <b>aucun</b> des 41 sons déjà dans le projet : ils gardent les réglages figés dans
    /// leur <c>.meta</c>, et le jeu continue de se comporter comme avant. Rien ne le signale — c'est
    /// le même piège que le générateur qui annonce « écrit » pendant que le jeu affiche l'ancienne
    /// image.
    /// </remarks>
    [MenuItem("Chimera/Reimporter tout l'audio")]
    public static void ReimportAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" });

        foreach (string guid in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

        Debug.Log($"[AudioImport] {guids.Length} sons reimportes.");
    }
}
