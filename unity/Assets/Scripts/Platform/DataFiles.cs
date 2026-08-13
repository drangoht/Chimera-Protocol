using System.IO;
using UnityEngine;

/// <summary>
/// Accès aux fichiers de tuning (<c>data/*.json</c>) — équivalent des lectures <c>res://data/</c>
/// de Godot (docs/UNITY_MIGRATION_PLAN.md §7.4).
///
/// <para><b>Pourquoi <c>StreamingAssets</c> et non <c>Resources</c>.</b> Le projet pose comme
/// convention explicite que le tuning est <b>modifiable sans recompiler</b>.
/// <c>StreamingAssets</c> conserve des fichiers lisibles sur disque dans le build ;
/// <c>Resources</c> les empaquette dans l'exécutable, ce qui supprimerait cette propriété — et avec
/// elle la possibilité d'ajuster l'équilibrage sans passer par un build.</para>
///
/// <para><b>La lecture elle-même vit dans <see cref="StreamingText"/></b> depuis le portage web :
/// en WebGL, <c>streamingAssetsPath</c> est une URL et non un dossier. Cette classe garde son rôle —
/// nommer les fichiers de tuning et signaler ceux qui manquent — et délègue les octets.</para>
/// </summary>
public static class DataFiles
{
    /// <summary>Sous-dossier des données, relatif à <c>StreamingAssets</c>.</summary>
    private const string Folder = "data";

    /// <summary>Dossier des données, à côté de l'exécutable.</summary>
    public static string Root => Path.Combine(Application.streamingAssetsPath, Folder);

    /// <summary>
    /// Lit un fichier de données par son nom (avec extension). Renvoie <c>null</c> et journalise si
    /// le fichier manque — un tuning absent doit se voir immédiatement, pas produire un jeu aux
    /// valeurs par défaut silencieuses.
    /// </summary>
    public static string? Load(string fileName)
    {
        string? text = StreamingText.Read($"{Folder}/{fileName}");
        if (text == null) Debug.LogError($"[DataFiles] fichier de tuning introuvable : {fileName}");

        return text;
    }
}
