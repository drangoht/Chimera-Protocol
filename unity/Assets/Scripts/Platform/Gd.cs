using System;
using UnityEngine;

/// <summary>
/// Équivalents des fonctions globales <c>GD.*</c> de Godot (162 sites d'appel —
/// docs/UNITY_MIGRATION_PLAN.md §4.2).
///
/// <para><b>L'aléatoire est le point sensible</b>, et pas pour des raisons de gameplay : toute la
/// méthode de réglage du projet repose sur <c>--seed=&lt;n&gt;</c> et sur des campagnes appariées.
/// La source d'aléa est donc <see cref="Pcg32"/>, qui reproduit celle de Godot bit pour bit, et
/// <b>non</b> <c>UnityEngine.Random</c> — lequel est un état global partagé avec le moteur (les
/// systèmes de particules y puisent), donc impossible à rendre reproductible.</para>
/// </summary>
public static class Gd
{
    private static readonly Pcg32 _rng = new(0UL);

    /// <summary>
    /// Réamorce le générateur global — équivalent de <c>GD.Seed</c>. Point d'entrée du drapeau
    /// <c>--seed</c> du banc.
    /// </summary>
    /// <summary>
    /// Le générateur lui-même, pour la logique pure qui en prend un en paramètre (disposition de
    /// l'arène, par exemple). <b>Le même flux que <see cref="Randf"/></b> : deux générateurs
    /// distincts rendraient une graine incapable de reproduire une run entière.
    /// </summary>
    public static Pcg32 Rng => _rng;

    public static void Seed(ulong seed) => _rng.Seed(seed);

    /// <summary>Réamorce depuis l'horloge, pour une partie normale — équivalent de <c>randomize()</c>.</summary>
    public static void Randomize() => _rng.Seed((ulong)DateTime.Now.Ticks);

    /// <summary>Entier non signé sur 32 bits — équivalent de <c>GD.Randi</c>.</summary>
    public static uint Randi() => _rng.NextUInt();

    /// <summary>Réel dans [0, 1] — équivalent de <c>GD.Randf</c>.</summary>
    public static float Randf() => _rng.NextFloat();

    /// <summary>
    /// Réel dans [from, to] — équivalent de <c>GD.RandRange(double, double)</c>.
    /// ⚠ Ne reproduit PAS les valeurs de Godot : voir <see cref="Pcg32.RangeDouble"/>.
    /// </summary>
    public static double RandRange(double from, double to) => _rng.RangeDouble(from, to);

    /// <summary>Entier dans [from, to], <b>bornes incluses</b> — équivalent de <c>randi_range</c>.</summary>
    public static int RandRange(int from, int to) => _rng.RangeInt(from, to);

    /// <summary>Trace d'exécution — équivalent de <c>GD.Print</c>.</summary>
    public static void Print(params object[] what) => Debug.Log(Join(what));

    /// <summary>Avertissement — équivalent de <c>GD.PushWarning</c>.</summary>
    public static void PushWarning(params object[] what) => Debug.LogWarning(Join(what));

    /// <summary>Erreur — équivalent de <c>GD.PushError</c>.</summary>
    public static void PushError(params object[] what) => Debug.LogError(Join(what));

    private static string Join(object[] what)
    {
        if (what == null || what.Length == 0) return string.Empty;
        if (what.Length == 1) return what[0]?.ToString() ?? "null";

        var sb = new System.Text.StringBuilder();
        foreach (object o in what) sb.Append(o?.ToString() ?? "null");
        return sb.ToString();
    }
}
