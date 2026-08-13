using UnityEngine;

/// <summary>
/// Signature <b>dorée</b> des armes fusionnées, en plein combat.
///
/// <para><b>Le problème qu'elle règle.</b> La forge d'une fusion s'annonce désormais (fanfare +
/// bandeau), mais une fois l'annonce passée, plus rien ne distinguait une fusion d'une arme
/// ordinaire : chacune hérite du VFX de son archétype — l'Égide dessine le champ violet du Champ de
/// Surcharge, la Lame de Fusion l'anneau de la Lame Plasma. Le joueur savait qu'il avait fusionné ;
/// il ne le <b>voyait</b> plus.</para>
///
/// <para><b>L'or, et rien d'autre.</b> C'est la couleur que le jeu emploie partout pour « acquis,
/// définitif » — les Échos, les récompenses, le bandeau de fusion — et <b>aucune arme de base ne
/// l'utilise</b>. Une teinte inemployée ailleurs se reconnaît sans être apprise : dès que du doré
/// apparaît autour du joueur, c'est une fusion qui vient d'agir.</para>
///
/// <para><b>Un point unique, jamais neuf appels.</b> La marque est posée par
/// <see cref="WeaponBase"/> après un tir réussi, exactement comme le son de tir. Neuf fusions
/// existent et d'autres viendront : les faire s'annoncer chacune dans son coin reproduirait le
/// défaut des quatorze armes muettes — quatorze sur seize l'étaient parce qu'un appel écrit arme par
/// arme ne se porte jamais en entier.</para>
/// </summary>
public static class FusionMark
{
    /// <summary>Or #FFCC44 — « acquis, définitif », dans tout le jeu.</summary>
    private static readonly Color Gold = new(1.000f, 0.800f, 0.267f);

    /// <summary>
    /// Intervalle minimal entre deux marques d'une <b>même</b> arme, en secondes.
    /// </summary>
    /// <remarks>
    /// La Lame de Fusion frappe toutes les 0,35 s et l'Essaim Orbital plus souvent encore : sans
    /// cette borne, la marque deviendrait un clignotement continu — c'est-à-dire un fond, et un fond
    /// ne signale plus rien. Espacée, elle reste un <b>événement</b> qu'on remarque.
    /// </remarks>
    private const float MinInterval = 0.22f;

    /// <summary>
    /// Marque un tir de fusion au point <paramref name="at"/>. Sans effet si la même arme s'est
    /// déjà annoncée il y a moins de <see cref="MinInterval"/>.
    /// </summary>
    /// <param name="nextAllowed">
    /// Échéance portée par l'arme elle-même, mise à jour ici.
    /// </param>
    /// <remarks>
    /// ⚠ L'échéance vit <b>dans l'arme</b>, et non dans une table statique indexée par identité.
    /// Une table statique survit au changement de scène, alors que <c>Time.time</c> repart à zéro :
    /// une échéance héritée de la run précédente resterait dans le futur et <b>éteindrait</b> la
    /// marque de cette arme pour toute la partie — un effet qui disparaît sans erreur. Un champ
    /// d'instance naît avec l'arme et meurt avec elle ; il n'y a rien à purger, donc rien à oublier
    /// de purger.
    /// </remarks>
    public static void TryDraw(ref float nextAllowed, Vector2 at, int level)
    {
        float now = Time.time;
        if (now < nextAllowed) return;

        nextAllowed = now + MinInterval;

        int p = Mathf.Clamp(level, 1, 8);

        // Anneau fin plutôt qu'une lueur pleine : une fusion est portée par le joueur, et un halo
        // posé sur lui masquerait sa silhouette — donc sa position, la seule information dont il ne
        // peut jamais se passer. L'anneau encadre au lieu de recouvrir.
        //
        // ⚠ Serré autour du porteur (22 px + le niveau), et non large : un grand cercle net se lit
        // comme une **portée d'arme** ou un bouclier, et le joueur chercherait à s'en servir comme
        // tel. Collé à la silhouette, il se lit pour ce qu'il est — une marque sur le porteur.
        Vfx.Ring(at, 22f + p * 1.5f, Gold, 2f, 0.20f);

        Vfx.Burst(at, Gold, new Color(Gold.r, Gold.g, Gold.b, 0f),
                  4, 90f, 190f, 4f, 0.22f);
    }
}
