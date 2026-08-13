using System;
using UnityEngine;

/// <summary>
/// Arguments de lancement — <b>le seul endroit du projet qui sait d'où ils viennent</b>.
///
/// <para>Sur Windows, ce sont les arguments de la ligne de commande : <c>--biome=neon</c>,
/// <c>--seed=12</c>, <c>--invuln</c>. Dans un navigateur, il n'y a pas de ligne de commande — mais il
/// y a une adresse, et sa chaîne de requête joue exactement le même rôle :
/// <c>…/index.html?biome=neon&amp;seed=12&amp;invuln</c> arrive ici sous la forme que le reste du
/// code attend déjà.</para>
///
/// <para><b>Pourquoi centraliser plutôt que garder les appels dispersés.</b> Ils l'étaient : six
/// fichiers interrogeaient <c>Environment.GetCommandLineArgs()</c> chacun de leur côté. C'est la même
/// dispersion que celle des entrées avant leur portage sur le paquet Input System — et le même risque
/// derrière. Sur une plateforme où l'appel n'est pas pris en charge, il lève ; l'exception
/// <b>saute la fin de la méthode appelante</b> sans rien afficher, et ce qui suivait dans cette
/// méthode disparaît. Le projet vient d'y perdre la visée à la souris et son réticule, découverts
/// des mois plus tard.</para>
///
/// <para>Un seul point d'accès, un seul <c>try</c>, et la question ne se pose plus qu'une fois.</para>
/// </summary>
public static class LaunchArgs
{
    private static string[]? _args;

    /// <summary>
    /// Arguments, normalisés en <c>--clé=valeur</c> / <c>--drapeau</c> quelle que soit la plateforme.
    /// Jamais <c>null</c>, jamais une exception.
    /// </summary>
    public static string[] All => _args ??= Read();

    /// <summary>Le drapeau est-il présent ? (<c>--invuln</c>)</summary>
    public static bool Has(string flag)
    {
        foreach (string arg in All)
            if (string.Equals(arg, flag, StringComparison.Ordinal)) return true;

        return false;
    }

    /// <summary>Valeur d'un argument préfixé (<c>--biome=</c>), ou <c>null</c>.</summary>
    public static string? Value(string prefix)
    {
        foreach (string arg in All)
            if (arg.StartsWith(prefix, StringComparison.Ordinal)) return arg.Substring(prefix.Length);

        return null;
    }

    private static string[] Read()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
            return LaunchQuery.ToArgs(Application.absoluteURL, Application.productName);

        try
        {
            return Environment.GetCommandLineArgs();
        }
        catch (Exception e)
        {
            // Aucune plateforme cible ne devrait passer par ici. Mais un tableau vide rendu
            // franchement vaut mieux qu'une exception qui escamote la suite de l'appelant.
            Debug.LogWarning($"[LaunchArgs] arguments illisibles ({e.Message}) — aucun drapeau actif.");
            return Array.Empty<string>();
        }
    }

    /// <summary>Oublie ce qui a été lu — réservé aux tests.</summary>
    public static void Reset() => _args = null;
}
