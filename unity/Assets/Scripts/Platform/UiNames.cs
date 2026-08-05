using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Noms affichables des armes et des passifs — <b>source unique</b>.
///
/// <para>Le HUD écrivait <c>IMPULSE CANNON</c> (l'identifiant technique rendu lisible) là où l'écran
/// de pause disait « Canon à Impulsions » : deux noms pour la même arme, sur deux écrans que le
/// joueur ouvre à quelques secondes d'intervalle. Le repli sur l'identifiant existait « en
/// attendant la table de localisation » — elle était déjà chargée par ailleurs.</para>
///
/// <para>Le repli subsiste et reste <b>volontaire</b> : un identifiant affiché en clair se repère
/// immédiatement à l'écran, là où un blanc passerait inaperçu.</para>
/// </summary>
public static class UiNames
{
    private static Dictionary<string, string>? _names;

    /// <summary>Nom affichable d'une arme, d'une fusion ou d'un passif.</summary>
    public static string Of(string id)
    {
        if (id.Length == 0) return "";

        _names ??= Load();

        return _names.TryGetValue(id, out string? name) ? name : Pretty(id);
    }

    /// <summary>Identifiant rendu lisible : <c>tesla_coil</c> → <c>TESLA COIL</c>.</summary>
    public static string Pretty(string id) => id.Replace('_', ' ').ToUpperInvariant();

    private static Dictionary<string, string> Load()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        string? json = DataFiles.Load("weapons.json");
        if (json == null) return names;

        var table = WeaponTable.Parse(json);

        foreach (var (id, def) in table.Weapons) names[id] = def.Name;
        foreach (var (id, def) in table.Fusions) names[id] = def.Name;

        // ⚠ Les passifs ne sont PAS dans weapons.json : ils ont leur propre table, et leur nom
        // s'obtient par clé de traduction. Sans cette boucle, l'arsenal du HUD afficherait le nom
        // traduit de ses armes et l'identifiant brut de ses passifs — sur la même colonne.
        foreach (string id in PassiveIds)
        {
            string key = $"PAS_{id.ToUpperInvariant()}_NAME";
            string translated = Loc.T(key);
            names[id] = translated != key ? translated : Pretty(id);
        }

        return names;
    }

    /// <summary>
    /// Les quatre passifs du jeu. Ils ne vivent pas dans <c>weapons.json</c> et n'ont pas de table
    /// propre côté données : leur nom s'obtient par clé de traduction.
    /// </summary>
    private static readonly string[] PassiveIds =
    {
        "thermal_core", "reinforced_plating", "servo_motors", "capacitor",
    };

    /// <summary>Oublie la table chargée — réservé aux bancs.</summary>
    public static void Reset() => _names = null;
}
