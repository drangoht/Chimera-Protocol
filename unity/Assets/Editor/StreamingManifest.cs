using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Écrit la liste des fichiers de <c>StreamingAssets</c> dans une ressource que le jeu peut lire à
/// l'exécution — <c>Resources/streaming_manifest.txt</c>.
/// </summary>
/// <remarks>
/// <para><b>À quoi sert une liste de fichiers qu'on pourrait énumérer.</b> Sur Windows, justement,
/// on le peut : <c>Directory.GetFiles</c> répond. En WebGL il n'y a pas de dossier à parcourir —
/// <c>StreamingAssets</c> n'est qu'un préfixe d'URL, et rien ne permet de demander au serveur ce
/// qu'il contient. Le jeu doit donc embarquer la liste de ce qu'il aura à télécharger.</para>
///
/// <para><b>Produite par le build, jamais tenue à la main.</b> C'est le même raisonnement que le
/// tampon d'identité git : une liste recopiée dans le code décrit ce qui était vrai le jour où on
/// l'a écrite. Ajouter un JSON de tuning aurait alors marché sur Windows — où le disque répond à
/// tout — et produit un jeu amputé <b>en web seulement</b>, sans erreur au build. La liste étant
/// écrite par l'acte qui construit le binaire, elle ne peut pas décrire autre chose que lui.</para>
/// </remarks>
public static class StreamingManifest
{
    private const string AssetPath = "Assets/Resources/" + StreamingText.ManifestResource + ".txt";

    /// <summary>
    /// Régénère le manifeste. À appeler <b>avant</b> <c>BuildPipeline.BuildPlayer</c> : le fichier
    /// est une ressource, et le build embarque celle que la base d'assets a en mémoire.
    /// </summary>
    [MenuItem("Chimera/Regenerer le manifeste StreamingAssets")]
    public static void Write()
    {
        string root = Application.streamingAssetsPath;

        var entries = new List<string>();

        if (Directory.Exists(root))
        {
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                // Les .meta décrivent l'import Unity et ne sont pas copiés dans le build : les lister
                // produirait autant de 404 au démarrage du jeu web.
                if (path.EndsWith(".meta", StringComparison.Ordinal)) continue;

                entries.Add(path.Substring(root.Length + 1).Replace('\\', '/'));
            }
        }

        entries.Sort(StringComparer.Ordinal);

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, AssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        bool isNew = !File.Exists(full);
        File.WriteAllText(full, string.Join("\n", entries) + "\n");

        // Sur un dépôt où le fichier n'existe pas encore, la base d'assets ne le connaît pas : un
        // ImportAsset seul ne suffirait pas à l'y faire entrer.
        if (isNew) AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

        if (entries.Count == 0)
        {
            // Un manifeste vide donne un jeu aux tables vides. Sur Windows le disque masquerait le
            // problème ; en web il n'y a rien derrière.
            Debug.LogError($"[MANIFEST] aucun fichier trouvé sous {root} — le build web n'aura " +
                           $"ni tuning ni traductions.");
            return;
        }

        Debug.Log($"[MANIFEST] {entries.Count} fichiers : {string.Join(", ", entries)}");
    }
}
