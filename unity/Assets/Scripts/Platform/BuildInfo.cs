using UnityEngine;

/// <summary>
/// Identité du binaire : version publiée et commit dont il est issu.
///
/// <para>Le <b>tampon</b> qu'affiche le menu n'existe pas pour le joueur mais pour le <b>rapport de
/// bug</b> : sans lui, une capture d'écran ne dit pas quelle version elle montre, et une session de
/// test peut porter sur un binaire périmé sans que personne ne s'en aperçoive. Le projet a déjà
/// expédié le binaire de la version précédente sans le voir — le tampon est ce qui l'aurait montré
/// du premier coup d'œil.</para>
///
/// <para>La version vient des réglages du projet (<c>Application.version</c>), donc elle suit
/// automatiquement le changement de numéro. Le <b>SHA</b> est écrit par le script de release, dans un
/// fichier de ressources : le compiler en dur obligerait à recompiler pour le mettre à jour, alors
/// qu'il doit désigner le commit <i>publié</i>, connu au dernier moment.</para>
/// </summary>
public static class BuildInfo
{
    /// <summary>Ressource écrite au moment de la release (une ligne : le SHA court).</summary>
    public const string ResourcePath = "build_sha";

    private static string? _sha;

    /// <summary>Version sémantique du binaire, telle que publiée.</summary>
    public static string Version => Application.version;

    /// <summary>
    /// SHA court du commit publié, ou <c>"dev"</c> pour un binaire construit à la main.
    /// </summary>
    /// <remarks>
    /// La distinction compte : un binaire sans SHA n'est pas une release, et le voir affiché
    /// « dev » évite d'attribuer à une version publiée le comportement d'un build local.
    /// </remarks>
    public static string GitSha
    {
        get
        {
            if (_sha != null) return _sha;

            var asset = Resources.Load<TextAsset>(ResourcePath);
            _sha = asset != null && asset.text.Trim().Length > 0 ? asset.text.Trim() : "dev";

            return _sha;
        }
    }

    /// <summary>Libellé complet affiché en jeu : <c>v1.27.0-a1b2c3d</c>.</summary>
    public static string Label => $"v{Version}-{GitSha}";
}
