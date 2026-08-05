using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Lecture de <c>weapons.json</c> — logique pure et testable, dans la lignée de
/// <see cref="GraftTable"/> et <see cref="ChallengeTable"/> (Lot 3).
///
/// <para><b>Les données ne sont pas recopiées dans le code.</b> Le fichier reste la source de
/// vérité, éditable sans recompiler — c'est une convention explicite du projet. Le port se contente
/// donc de le lire, avec les mêmes règles d'extrapolation au-delà des niveaux définis.</para>
/// </summary>
public static class WeaponTable
{
    /// <summary>Statistiques d'une arme à un niveau donné.</summary>
    public readonly struct WeaponLevelStats
    {
        public readonly int   Level;
        public readonly float Damage;
        public readonly float Cooldown;
        public readonly int   ProjectileCount;
        public readonly float ProjectileSpeed;
        public readonly bool  Piercing;

        /// <summary>Amplitude totale de l'éventail, en degrés. 0 = tir unique droit.</summary>
        public readonly float SpreadDegrees;

        public WeaponLevelStats(int level, float damage, float cooldown,
                                int projectileCount, float projectileSpeed, bool piercing,
                                float spreadDegrees = 0f)
        {
            Level = level;
            Damage = damage;
            Cooldown = cooldown;
            ProjectileCount = projectileCount;
            ProjectileSpeed = projectileSpeed;
            Piercing = piercing;
            SpreadDegrees = spreadDegrees;
        }
    }

    /// <summary>Définition d'une arme : identité + paliers de niveaux.</summary>
    public sealed class WeaponDef
    {
        public string Id = "";
        public string Name = "";
        public string Type = "active";
        public string Rarity = "common";
        public int MaxLevel = 20;
        public List<WeaponLevelStats> Levels = new();

        /// <summary>Dernier niveau réellement décrit par les données (au-delà : extrapolation).</summary>
        public int DefinedMax => Levels.Count;
    }

    /// <summary>Définition d'une fusion : ce qu'elle remplace et ce qu'elle exige.</summary>
    public sealed class FusionDef
    {
        public string Id = "";
        public string Name = "";
        public string Replaces = "";
        public string RequiredWeapon = "";
        public int    RequiredWeaponLevel = 5;
        public string RequiredPassive = "";
    }

    /// <summary>Analyse le contenu de <c>weapons.json</c>.</summary>
    public static (Dictionary<string, WeaponDef> Weapons, Dictionary<string, FusionDef> Fusions)
        Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var weapons = new Dictionary<string, WeaponDef>(StringComparer.Ordinal);
        var fusions = new Dictionary<string, FusionDef>(StringComparer.Ordinal);

        if (root.TryGetProperty("weapons", out var weaponsNode))
        {
            foreach (var w in weaponsNode.EnumerateArray())
            {
                var def = new WeaponDef
                {
                    Id = Str(w, "id"),
                    Name = Str(w, "name"),
                    Type = Str(w, "type", "active"),
                    Rarity = Str(w, "rarity", "common"),
                    MaxLevel = Int(w, "maxLevel", 20),
                };

                if (w.TryGetProperty("levels", out var levels))
                {
                    foreach (var l in levels.EnumerateArray())
                    {
                        def.Levels.Add(new WeaponLevelStats(
                            Int(l, "level", def.Levels.Count + 1),
                            Flt(l, "damage"),
                            Flt(l, "cooldown", 1f),
                            Int(l, "projectileCount", 1),
                            Flt(l, "projectileSpeed", 400f),
                            Bool(l, "piercing"),
                            Flt(l, "spreadDegrees")));
                    }
                }

                if (def.Id.Length > 0) weapons[def.Id] = def;
            }
        }

        if (root.TryGetProperty("fusions", out var fusionsNode))
        {
            foreach (var f in fusionsNode.EnumerateArray())
            {
                var def = new FusionDef
                {
                    Id = Str(f, "id"),
                    Name = Str(f, "name"),
                    Replaces = Str(f, "replaces"),
                };

                if (f.TryGetProperty("requires", out var req))
                {
                    def.RequiredWeapon = Str(req, "weapon");
                    def.RequiredWeaponLevel = Int(req, "weaponLevel", 5);
                    def.RequiredPassive = Str(req, "passive");
                }

                if (def.Id.Length > 0) fusions[def.Id] = def;
            }
        }

        return (weapons, fusions);
    }

    /// <summary>
    /// Statistiques d'une arme au niveau demandé. Au-delà des paliers décrits, les dégâts sont
    /// extrapolés (+10 %/niveau) et les mécaniques — nombre de projectiles, perforation —
    /// <b>plafonnent</b> : c'est ce plafonnement qui empêche une arme de devenir absurde en fin de
    /// partie, et il doit être préservé tel quel.
    /// </summary>
    public static WeaponLevelStats StatsAt(WeaponDef def, int level)
    {
        if (def.Levels.Count == 0)
            return new WeaponLevelStats(level, 0f, 1f, 1, 400f, false);

        int clamped = Math.Clamp(level, 1, def.Levels.Count);
        var stats = def.Levels[clamped - 1];

        if (level <= def.DefinedMax) return stats;

        return new WeaponLevelStats(
            level,
            WeaponLeveling.ExtrapolatedDamage(stats.Damage, level, def.DefinedMax),
            stats.Cooldown,
            stats.ProjectileCount,
            stats.ProjectileSpeed,
            stats.Piercing,
            stats.SpreadDegrees);
    }

    // ─── Lecture tolérante ────────────────────────────────────────────────────

    private static string Str(JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;

    private static float Flt(JsonElement e, string name, float fallback = 0f)
        => e.TryGetProperty(name, out var v) && v.TryGetSingle(out float f) ? f : fallback;

    private static bool Bool(JsonElement e, string name, bool fallback = false)
        => e.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.True
            : fallback;
}
