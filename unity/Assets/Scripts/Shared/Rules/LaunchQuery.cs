using System;
using System.Collections.Generic;

/// <summary>
/// Traduit la chaîne de requête d'une URL en arguments de ligne de commande — logique pure et
/// testable.
///
/// <para><b>À quoi ça sert.</b> Tous les drapeaux de mise au point du jeu (<c>--biome=</c>,
/// <c>--seed=</c>, <c>--invuln</c>, <c>--lang=</c>…) sont lus depuis la ligne de commande. Un
/// navigateur n'en a pas, mais il a une adresse : <c>…/index.html?biome=neon&amp;invuln</c> doit
/// produire <b>exactement</b> ce que <c>--biome=neon --invuln</c> produit sur Windows.</para>
///
/// <para><b>Pure, donc vérifiée.</b> La conversion tient en vingt lignes et a une demi-douzaine de
/// cas limites — fragment, valeur encodée, séparateur vide, clé sans valeur. Écrite dans la couche
/// moteur, aucun de ces cas n'aurait de test, et une erreur ne se manifesterait que par un drapeau
/// silencieusement inopérant sur une seule plateforme. C'est le mode d'échec que ce projet paie le
/// plus cher.</para>
/// </summary>
public static class LaunchQuery
{
    /// <summary>
    /// Arguments équivalents à ceux d'une ligne de commande, pour l'adresse donnée.
    /// </summary>
    /// <param name="url">Adresse complète de la page, éventuellement <c>null</c>.</param>
    /// <param name="programName">
    /// Ce qui tiendra lieu d'<c>argv[0]</c>. La ligne de commande rend toujours le nom du programme
    /// en premier, et du code existant compte les arguments ou saute le premier : ne pas l'imiter
    /// ferait diverger les deux plateformes sur un détail invisible.
    /// </param>
    public static string[] ToArgs(string? url, string programName)
    {
        var args = new List<string> { programName };

        int mark = url?.IndexOf('?') ?? -1;
        if (url == null || mark < 0) return args.ToArray();

        // Le fragment n'appartient pas à la requête. Sans cette coupe, « ?lang=en#accueil » donnerait
        // la langue « en#accueil » — qui ne correspond à rien, donc un repli silencieux sur la langue
        // par défaut.
        string query = url.Substring(mark + 1);
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);

        foreach (string pair in query.Split('&'))
        {
            if (pair.Length == 0) continue;

            int equals = pair.IndexOf('=');

            string key = equals < 0 ? pair : pair.Substring(0, equals);

            // Une clé vide viendrait d'une URL tronquée ou construite à la main. Produire « -- » ou
            // « --=valeur » donnerait un argument que les appelants prendraient pour une clé.
            if (key.Length == 0) continue;

            args.Add(equals < 0
                ? $"--{key}"
                : $"--{key}={Uri.UnescapeDataString(pair.Substring(equals + 1))}");
        }

        return args.ToArray();
    }
}
