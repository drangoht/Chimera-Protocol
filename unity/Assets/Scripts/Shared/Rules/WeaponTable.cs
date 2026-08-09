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

        /// <summary>
        /// Toutes les autres valeurs numériques du palier, telles quelles.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b>Un sac générique, et non quinze champs de plus.</b> Le portage ne lisait que six
        /// clés par niveau là où le jeu d'origine en lit seize : <c>chainCount</c> (la Bobine Tesla
        /// passe de 2 à 7 chaînes), <c>droneCount</c> (2 → 4), <c>glaiveCount</c> (1 → 3),
        /// <c>missileCount</c> (2 → 5), <c>coneAngle</c>, <c>arcAngleDegrees</c>, <c>range</c>… Huit
        /// armes ne grandissaient donc <b>que par leurs dégâts</b> : leur forme restait celle du
        /// niveau 1 jusqu'au niveau 20.</para>
        ///
        /// <para>C'est la suite exacte du défaut <c>projectileCount</c> déjà corrigé — la correction
        /// n'avait couvert qu'une clé sur seize. Un sac ferme la famille entière : une clé ajoutée
        /// aux données est désormais lisible <b>sans toucher à ce fichier</b>, et l'oubli redevient
        /// impossible plutôt qu'improbable.</para>
        /// </remarks>
        private readonly Dictionary<string, float>? _shape;

        /// <summary>Valeur de forme du palier, ou <paramref name="fallback"/> si le fichier se tait.</summary>
        public float Shape(string key, float fallback)
            => _shape != null && _shape.TryGetValue(key, out float v) ? v : fallback;

        /// <summary>Même chose en entier — nombres de chaînes, de drones, de missiles.</summary>
        public int ShapeInt(string key, int fallback)
            => _shape != null && _shape.TryGetValue(key, out float v)
                ? (int)Math.Round(v)
                : fallback;

        /// <summary>Clés de forme réellement lues pour ce palier — pour les vérifications.</summary>
        public IReadOnlyCollection<string> ShapeKeys
            => (IReadOnlyCollection<string>?)_shape?.Keys ?? Array.Empty<string>();

        /// <summary>Le sac lui-même, pour le reporter tel quel lors d'une extrapolation.</summary>
        internal Dictionary<string, float>? ShapeBag => _shape;

        public WeaponLevelStats(int level, float damage, float cooldown,
                                int projectileCount, float projectileSpeed, bool piercing,
                                float spreadDegrees = 0f,
                                Dictionary<string, float>? shape = null)
        {
            Level = level;
            Damage = damage;
            Cooldown = cooldown;
            ProjectileCount = projectileCount;
            ProjectileSpeed = projectileSpeed;
            Piercing = piercing;
            SpreadDegrees = spreadDegrees;
            _shape = shape;
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
                            Flt(l, "spreadDegrees"),
                            ShapeOf(l)));
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
            stats.SpreadDegrees,
            // La forme PLAFONNE au dernier palier décrit, comme le nombre de projectiles : seuls les
            // dégâts continuent de monter. Extrapoler un nombre de chaînes rendrait la Bobine Tesla
            // absurde en fin de partie.
            stats.ShapeBag);
    }

    /// <summary>
    /// Toutes les valeurs numériques d'un palier, y compris celles qu'aucun champ nommé ne couvre.
    /// </summary>
    /// <remarks>
    /// On prend <b>tout</b> plutôt qu'une liste blanche : c'est précisément une liste incomplète qui
    /// a laissé dix clés de forme inertes pendant tout le portage. Les champs déjà nommés y figurent
    /// aussi — cela ne coûte rien et évite d'avoir à tenir deux listes cohérentes.
    /// </remarks>
    private static Dictionary<string, float>? ShapeOf(JsonElement level)
    {
        Dictionary<string, float>? shape = null;

        foreach (var prop in level.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number) continue;
            if (!prop.Value.TryGetSingle(out float v)) continue;

            shape ??= new Dictionary<string, float>(StringComparer.Ordinal);
            shape[prop.Name] = v;
        }

        return shape;
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
