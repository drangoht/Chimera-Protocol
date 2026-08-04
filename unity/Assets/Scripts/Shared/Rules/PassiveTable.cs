using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Lecture de la section <c>passives</c> de <c>weapons.json</c> — logique pure et testable
/// (Lot 5 bis, « la run complète »).
///
/// <para><b>Pourquoi une table à part.</b> Côté Godot, les deltas de passifs étaient lus et appliqués
/// dans le même bloc, au cœur d'un <c>Node</c> : c'est là que s'était logé le power-creep de la
/// 1.22.0 — l'extrapolation au-delà des trois niveaux définis était invisible depuis l'extérieur et
/// donc intestable. Ici, la <b>lecture</b> et le <b>calcul du delta</b> vivent dans cette classe pure ;
/// le composant Unity ne fait plus qu'écrire le résultat dans les statistiques du joueur.</para>
///
/// <para><b>Les 4 passifs ne définissent que 3 niveaux pour un plafond de 20.</b> Au-delà,
/// <see cref="PassiveScaling"/> amortit — sauf les <b>PV max</b>, seul levier défensif non plafonné du
/// joueur : les amortir avait fermé la fenêtre d'overtime (GDD §31.6). C'est
/// <see cref="IsDamped"/> qui porte cette exception, en un seul endroit.</para>
/// </summary>
public static class PassiveTable
{
    /// <summary>Les quatre passifs du jeu, dans l'ordre d'affichage.</summary>
    public static readonly string[] AllPassiveIds =
        { "thermal_core", "reinforced_plating", "servo_motors", "capacitor" };

    /// <summary>Champs portés par un niveau de passif. Tous facultatifs — un passif n'en remplit que les siens.</summary>
    public readonly struct PassiveLevelStats
    {
        public readonly int   Level;
        public readonly float DamageMultiplierBonus;
        public readonly float MaxHpBonus;
        public readonly float DamageReduction;
        public readonly float SpeedBonus;
        public readonly float CooldownReduction;

        public PassiveLevelStats(int level, float damageMultiplierBonus, float maxHpBonus,
                                 float damageReduction, float speedBonus, float cooldownReduction)
        {
            Level = level;
            DamageMultiplierBonus = damageMultiplierBonus;
            MaxHpBonus = maxHpBonus;
            DamageReduction = damageReduction;
            SpeedBonus = speedBonus;
            CooldownReduction = cooldownReduction;
        }
    }

    /// <summary>Définition d'un passif : identité + paliers.</summary>
    public sealed class PassiveDef
    {
        public string Id = "";
        public string Name = "";
        public int MaxLevel = 20;
        public List<PassiveLevelStats> Levels = new();

        /// <summary>Dernier niveau réellement décrit par les données (au-delà : extrapolation amortie).</summary>
        public int DefinedMax => Levels.Count;
    }

    /// <summary>Analyse la section <c>passives</c> de <c>weapons.json</c>.</summary>
    public static Dictionary<string, PassiveDef> Parse(string json)
    {
        var result = new Dictionary<string, PassiveDef>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("passives", out var list)) return result;

        foreach (var p in list.EnumerateArray())
        {
            var def = new PassiveDef
            {
                Id = Str(p, "id"),
                Name = Str(p, "name"),
                MaxLevel = Int(p, "maxLevel", 20),
            };

            if (p.TryGetProperty("levels", out var levels))
            {
                foreach (var l in levels.EnumerateArray())
                {
                    def.Levels.Add(new PassiveLevelStats(
                        Int(l, "level", def.Levels.Count + 1),
                        Flt(l, "damageMultiplierBonus"),
                        Flt(l, "maxHpBonus"),
                        Flt(l, "damageReduction"),
                        Flt(l, "speedBonus"),
                        Flt(l, "cooldownReduction")));
                }
            }

            if (def.Id.Length > 0) result[def.Id] = def;
        }

        return result;
    }

    /// <summary>
    /// Paliers appliqués au passage au niveau <paramref name="level"/>. Au-delà des niveaux définis,
    /// on relit le <b>dernier</b> palier décrit : c'est lui qui sert de base à l'extrapolation.
    /// </summary>
    public static PassiveLevelStats StatsAt(PassiveDef def, int level)
    {
        if (def.Levels.Count == 0) return new PassiveLevelStats(level, 0f, 0f, 0f, 0f, 0f);

        int clamped = Math.Clamp(level, 1, def.Levels.Count);
        return def.Levels[clamped - 1];
    }

    /// <summary>
    /// Cette statistique est-elle soumise à l'amortissement de <see cref="PassiveScaling"/> ?
    ///
    /// <para><b>Les PV max sont la seule exception</b>, et elle est délibérée : points plats et
    /// additifs, ils croissent linéairement face à une menace quadratique et n'ont jamais participé
    /// au power-creep. Les amortir plafonnait les PV à 451 dès la 11ᵉ minute et ramenait la survie en
    /// overtime à ~1 min contre les 5-10 min sur lesquelles l'économie d'Échos est dimensionnée.</para>
    /// </summary>
    public static bool IsDamped(PassiveStat stat) => stat != PassiveStat.MaxHp;

    /// <summary>
    /// Delta effectivement appliqué au passage au niveau <paramref name="level"/>, amortissement
    /// compris. Point d'entrée unique : le composant Unity n'a plus à savoir quelle stat s'amortit.
    /// </summary>
    public static float DeltaFor(PassiveDef def, PassiveStat stat, int level)
    {
        var stats = StatsAt(def, level);
        float defined = stat switch
        {
            PassiveStat.DamageMultiplier => stats.DamageMultiplierBonus,
            PassiveStat.MaxHp            => stats.MaxHpBonus,
            PassiveStat.DamageReduction  => stats.DamageReduction,
            PassiveStat.Speed            => stats.SpeedBonus,
            PassiveStat.CooldownReduction=> stats.CooldownReduction,
            _                            => 0f,
        };

        if (defined == 0f) return 0f;

        return IsDamped(stat)
            ? PassiveScaling.ExtrapolatedDelta(defined, level, def.DefinedMax)
            : defined;
    }

    // ─── Lecture tolérante ────────────────────────────────────────────────────

    private static string Str(JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;

    private static float Flt(JsonElement e, string name, float fallback = 0f)
        => e.TryGetProperty(name, out var v) && v.TryGetSingle(out float f) ? f : fallback;
}

/// <summary>Statistique du joueur touchée par un passif.</summary>
public enum PassiveStat
{
    DamageMultiplier,
    MaxHp,
    DamageReduction,
    Speed,
    CooldownReduction,
}
