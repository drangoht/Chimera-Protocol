using UnityEditor;
using UnityEngine;

/// <summary>
/// Déclare sur quelles plateformes chaque bibliothèque native est incluse.
/// </summary>
/// <remarks>
/// <para><b>Le défaut d'Unity est « partout ».</b> Une DLL déposée dans <c>Plugins/</c> arrive avec
/// un <c>.meta</c> minimal, et l'importateur en déduit qu'elle vaut pour toutes les cibles — y
/// compris celles où elle ne peut pas fonctionner. <c>DiscordRPC</c> ouvre un tube nommé et lance des
/// fils d'exécution : sur le web, ni l'un ni l'autre n'existe.</para>
///
/// <para><b>Posé par script plutôt qu'à la main</b>, pour la raison qui vaut déjà pour l'icône et le
/// tampon git : un réglage fait dans l'inspecteur ne vaut que sur le poste où il a été fait. Ici
/// s'ajoute un piège propre aux <c>.meta</c> — les écrire à la main est le premier des pièges
/// d'assets recensés par le projet, et un <c>.meta</c> mal formé se solde par un réimport silencieux
/// qui rétablit les valeurs par défaut.</para>
///
/// <para>⚠ Cette exclusion est ce qui rend vrais les <c>#if UNITY_WEBGL</c> de
/// <see cref="DiscordPresence"/> : sans elle, la bibliothèque serait compilée pour le web et le
/// build échouerait — ou pire, réussirait en embarquant du code inerte.</para>
/// </remarks>
public static class PluginPlatforms
{
    private const string DiscordDll = "Assets/Plugins/DiscordRPC.dll";

    /// <summary>Applique les exclusions. Appelé par les cibles de build, et disponible au menu.</summary>
    [MenuItem("Chimera/Appliquer les plateformes des plugins")]
    public static void Apply()
    {
        var importer = AssetImporter.GetAtPath(DiscordDll) as PluginImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[PLUGINS] {DiscordDll} introuvable — rien à exclure.");
            return;
        }

        bool changed = false;

        if (!importer.GetCompatibleWithAnyPlatform())
        {
            importer.SetCompatibleWithAnyPlatform(true);
            changed = true;
        }

        // « Compatible avec toutes » puis exclusion explicite du web : c'est la seule combinaison qui
        // laisse la bibliothèque disponible sur les cibles futures sans avoir à les énumérer.
        if (importer.GetExcludeFromAnyPlatform(BuildTarget.WebGL) == false)
        {
            importer.SetExcludeFromAnyPlatform(BuildTarget.WebGL, true);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();

        Debug.Log($"[PLUGINS] DiscordRPC exclu du web (modifié : {changed}).");
    }
}
