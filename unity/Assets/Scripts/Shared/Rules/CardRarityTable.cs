using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Rareté de chaque carte de passage de niveau, lue depuis <c>levelup_config.json</c>
/// (bloc <c>rarityByCard</c>) — logique pure et testable.
///
/// <para><b>C'est LA source</b>, et elle couvre armes <i>et</i> passifs : 18 entrées réparties en
/// commun / rare / épique. La rareté décide à la fois du <b>poids de tirage</b>
/// (<see cref="RarityWeights"/>) et du cadre affiché sur la carte — les deux doivent venir d'ici,
/// sans quoi une carte tirée comme commune s'encadre en épique.</para>
/// </summary>
/// <remarks>
/// ⚠ <b>Le portage ne chargeait pas ce fichier du tout.</b> La rareté était déduite de
/// <c>weapons.json</c> pour les armes, et <b>aplatie sur « rare » pour tous les passifs</b> — alors
/// que la table en distingue des communs (<c>thermal_core</c>, <c>reinforced_plating</c>,
/// <c>servo_motors</c>) et des rares (<c>capacitor</c>). Combiné au tirage uniforme qui régnait
/// jusqu'au 2026-08-09, cela faisait deux couches d'écart entre la rareté annoncée et la rareté
/// vécue.
/// </remarks>
public static class CardRarityTable
{
    /// <summary>Rareté par défaut d'une carte absente de la table.</summary>
    public const string Fallback = "common";

    /// <summary>Analyse le bloc <c>rarityByCard</c>. Tolérant : un fichier muet rend une table vide.</summary>
    public static Dictionary<string, string> Parse(string json)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("rarityByCard", out var node)
            || node.ValueKind != JsonValueKind.Object)
            return table;

        foreach (var entry in node.EnumerateObject())
            if (entry.Value.ValueKind == JsonValueKind.String
                && entry.Value.GetString() is { Length: > 0 } rarity)
                table[entry.Name] = rarity;

        return table;
    }

    /// <summary>Rareté d'une carte, ou <see cref="Fallback"/> si la table ne la connaît pas.</summary>
    public static string Of(IReadOnlyDictionary<string, string> table, string id)
        => table.TryGetValue(id, out string? rarity) ? rarity : Fallback;
}
