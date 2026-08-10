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
/// automatiquement le changement de numéro. Le <b>SHA</b> vit dans un fichier de ressources : le
/// compiler en dur obligerait à recompiler pour le mettre à jour, alors qu'il doit désigner le commit
/// dont le binaire est issu, connu au dernier moment.</para>
///
/// <para>⚠ Ce fichier est écrit par <c>BuildBench.StampGitSha</c>, <b>à chaque build</b>, et non plus
/// par le seul script de release. La version précédente ne bougeait qu'à la publication et
/// <i>restait là</i> : tout build local ultérieur affichait le SHA de la dernière release — le numéro
/// d'un commit qui n'était pas celui du binaire, annoncé avec l'autorité d'un garde-fou. Le tampon
/// existe pour trancher « quel code tourne ici » ; il ne peut le faire que s'il est posé par l'acte
/// qui produit le binaire.</para>
/// </summary>
public static class BuildInfo
{
    /// <summary>Ressource écrite par le build (une ligne : le SHA court, suffixé <c>+</c> si l'arbre était modifié).</summary>
    public const string ResourcePath = "build_sha";

    private static string? _sha;

    /// <summary>Version sémantique du binaire, telle que publiée.</summary>
    public static string Version => Application.version;

    /// <summary>
    /// SHA court du commit dont ce binaire est issu — suffixé <c>+</c> si l'arbre de travail portait
    /// des modifications non commitées, <c>"dev"</c> si git n'a rien pu dire.
    /// </summary>
    /// <remarks>
    /// Les trois cas se lisent différemment : un SHA nu désigne un commit exact et reproductible ;
    /// un SHA suffixé dit que le binaire ne correspond <b>à aucun commit</b> — le cas ordinaire d'une
    /// session de mise au point, et le seul où un rapport de bug n'est pas rejouable tel quel ;
    /// « dev » avoue une ignorance, ce qu'un SHA périmé ne ferait pas.
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
