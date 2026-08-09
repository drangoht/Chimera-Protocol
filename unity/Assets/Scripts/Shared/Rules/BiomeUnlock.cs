using System.Collections.Generic;

/// <summary>
/// Déblocage progressif des niveaux — logique pure et testable (Lot 6).
///
/// <para>Un biome s'ouvre en <b>terminant le précédent</b>, c'est-à-dire en battant son boss. C'est
/// la seule porte du jeu : elle donne une raison de gagner au-delà du score, et elle garantit que le
/// joueur rencontre les paliers de menace dans l'ordre pour lequel ils sont calibrés
/// (<see cref="LevelThreat"/>).</para>
///
/// <para>⚠ « Terminé » se lit <b>toutes difficultés confondues</b>, comme sous Godot : un joueur qui a
/// battu un biome au cran I ne doit pas voir la suite se refermer parce qu'il joue désormais au cran
/// III. La progression déjà acquise ne se reprend jamais.</para>
/// </summary>
public static class BiomeUnlock
{
    /// <summary>Le biome est-il jouable ?</summary>
    public static bool IsUnlocked(string biomeId, IReadOnlyDictionary<string, int> completions)
    {
        int tier = LevelThreat.TierOf(biomeId);
        if (tier <= 0) return true;   // le premier niveau est toujours ouvert

        string previous = LevelThreat.Order[tier - 1];
        return completions.TryGetValue(previous, out int count) && count > 0;
    }

    /// <summary>Biome qui garde la porte, ou <c>null</c> si celui-ci est déjà ouvert.</summary>
    public static string? BlockedBy(string biomeId, IReadOnlyDictionary<string, int> completions)
    {
        if (IsUnlocked(biomeId, completions)) return null;
        return LevelThreat.Order[LevelThreat.TierOf(biomeId) - 1];
    }

    /// <summary>
    /// Cran de saturation le plus haut sélectionnable sur ce biome : le plus haut <b>battu</b>, plus
    /// un. On ne gravit l'échelle qu'un barreau à la fois, et seulement après avoir prouvé le
    /// précédent.
    /// </summary>
    /// <remarks>
    /// ⚠ La convention de <see cref="SaturationTable.MaxSelectable"/> fait foi : <b>0 signifie
    /// « aucun cran battu »</b>, et un joueur neuf peut donc déjà choisir le cran I. Ne pas la
    /// redéfinir ici — deux conventions pour une même valeur, c'est un décalage d'un cran qui
    /// n'apparaît qu'en jeu.
    /// </remarks>
    public static int MaxSelectableRank(string biomeId, IReadOnlyDictionary<string, int> beatenByLevel)
    {
        // ⚠ Le défaut est « rien battu », PAS « cran 0 battu ». Les confondre — ce que faisait ce
        // code — ouvrait le cran I à un joueur qui n'avait jamais terminé le niveau : après une
        // remise à zéro, l'échelle repartait avec un barreau d'avance.
        int beaten = beatenByLevel.TryGetValue(biomeId, out int b) ? b : SaturationTable.NoneBeaten;
        return SaturationTable.MaxSelectable(beaten);
    }
}
