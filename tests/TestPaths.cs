using System;
using System.IO;

/// <summary>
/// Localise le dépôt et les <b>vraies</b> données du jeu depuis le répertoire d'exécution des tests.
///
/// <para>Utile pour les tests qui doivent lire les données réelles plutôt qu'un échantillon
/// fabriqué : un analyseur validé sur un extrait inventé prouve qu'il analyse, pas que les données
/// du jeu se chargent.</para>
///
/// <para>⚠ Ces chemins pointent <b>là où le jeu lit</b>, et nulle part ailleurs. Tant que le dépôt
/// portait les deux moteurs, <c>data/</c> et <c>localization/</c> existaient en double — à la racine
/// pour Godot, sous <c>StreamingAssets</c> pour Unity — et un test vérifiait que les copies ne
/// divergeaient pas. Godot parti, la copie racine a été supprimée : il n'y a plus qu'un exemplaire,
/// donc plus de dérive possible, et plus rien à vérifier. Un test qui lirait une autre copie
/// vaudrait pour un fichier que le jeu n'ouvre jamais.</para>
/// </summary>
public static class TestPaths
{
    /// <summary>Racine du dépôt, trouvée en remontant jusqu'au dossier contenant <c>unity/</c>.</summary>
    public static string RepoRoot { get; } = FindRoot();

    /// <summary>Racine du projet Unity.</summary>
    public static string UnityProject { get; } = Path.Combine(RepoRoot, "unity");

    /// <summary>Dossier <c>Assets/</c> du projet Unity.</summary>
    public static string UnityAssets { get; } = Path.Combine(UnityProject, "Assets");

    /// <summary>Données de tuning JSON — celles que le binaire embarque et lit à l'exécution.</summary>
    public static string Data { get; } = Path.Combine(UnityAssets, "StreamingAssets", "data");

    /// <summary>Table de traduction <c>ui.csv</c> — même remarque.</summary>
    public static string Localization { get; } = Path.Combine(UnityAssets, "StreamingAssets", "localization");

    /// <summary>Sons du jeu, tels qu'ils sont chargés par <c>Resources.Load</c>.</summary>
    public static string Sfx { get; } = Path.Combine(UnityAssets, "Resources", "Audio", "sfx");

    /// <summary>Sauvegardes réelles figées, pour les tests de migration.</summary>
    public static string Fixtures { get; } = Path.Combine(RepoRoot, "tests", "fixtures");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(
                    dir.FullName, "unity", "Assets", "StreamingAssets", "data", "weapons.json")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Racine du dépôt introuvable depuis " + AppContext.BaseDirectory +
            " — aucun dossier parent ne contient unity/Assets/StreamingAssets/data/weapons.json.");
    }
}
