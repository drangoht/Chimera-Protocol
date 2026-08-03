using System.Collections.Generic;
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
/// </summary>
public static class DataFiles
{
    private static readonly Dictionary<string, string> _cache = new();

    /// <summary>Dossier des données, à côté de l'exécutable.</summary>
    public static string Root => Path.Combine(Application.streamingAssetsPath, "data");

    /// <summary>
    /// Lit un fichier de données par son nom (avec extension). Renvoie <c>null</c> et journalise si
    /// le fichier manque — un tuning absent doit se voir immédiatement, pas produire un jeu aux
    /// valeurs par défaut silencieuses.
    /// </summary>
    public static string? Load(string fileName)
    {
        if (_cache.TryGetValue(fileName, out string? cached)) return cached;

        string path = Path.Combine(Root, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[DataFiles] fichier de tuning introuvable : {path}");
            return null;
        }

        string text = File.ReadAllText(path);
        _cache[fileName] = text;
        return text;
    }

    /// <summary>Vide le cache — utile après une édition à chaud pendant l'équilibrage.</summary>
    public static void ClearCache() => _cache.Clear();
}
