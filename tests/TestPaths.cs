using System;
using System.IO;

/// <summary>
/// Localise la racine du dépôt depuis le répertoire d'exécution des tests.
///
/// <para>Utile pour les tests qui doivent lire les <b>vraies</b> données du jeu
/// (<c>data/*.json</c>) plutôt qu'un échantillon fabriqué : un analyseur validé sur un extrait
/// inventé prouve qu'il analyse, pas que les données du jeu se chargent.</para>
/// </summary>
public static class TestPaths
{
    /// <summary>Racine du dépôt, trouvée en remontant jusqu'au dossier contenant <c>data/</c>.</summary>
    public static string RepoRoot { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data")) &&
                File.Exists(Path.Combine(dir.FullName, "data", "weapons.json")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Racine du dépôt introuvable depuis " + AppContext.BaseDirectory +
            " — aucun dossier parent ne contient data/weapons.json.");
    }
}
