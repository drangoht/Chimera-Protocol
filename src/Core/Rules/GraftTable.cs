using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Système d'Assimilation (greffes) — logique PURE testable (aucune dépendance Godot).
/// Modèle : EliteAffixTable. Ne décide QUE des chiffres/seuils/routage lus depuis grafts.json ;
/// l'application (stats, comportements, rendu) vit côté nœuds (AssimilationSystem / GraftManager).
///
/// Parsing via System.Text.Json (pur .NET, disponible en xUnit). Voir docs/DESIGN_ASSIMILATION.md
/// Partie II (§11-18) pour le design chiffré.
/// </summary>
public static class GraftTable
{
    /// <summary>Une contribution de kill vers une jauge donnée.</summary>
    public readonly struct GaugeContribution
    {
        public readonly string Gauge;
        public readonly int Points;
        public GaugeContribution(string gauge, int points) { Gauge = gauge; Points = points; }
    }

    /// <summary>Définition d'une greffe (chiffres bruts lus depuis grafts.json).</summary>
    public class GraftDef
    {
        public string Id = "";
        public string Name = "";
        public string Gauge = "";
        public string SourceAiType = "";
        public string Rarity = "common";
        public string Description = "";
        public string HudIcon = "";
        public float[] Tint = { 1f, 1f, 1f };
        /// <summary>Groupes d'effets (ex. "orbitingAllies") → paramètres numériques.</summary>
        public Dictionary<string, Dictionary<string, double>> Effects = new();
        /// <summary>Modificateurs de stats PlayerStats (damageReductionAdd, maxHpAdd, speedMult).</summary>
        public Dictionary<string, double> StatMods = new();

        public double Effect(string group, string key, double fallback = 0.0)
            => Effects.TryGetValue(group, out var g) && g.TryGetValue(key, out var v) ? v : fallback;

        public bool HasEffect(string group) => Effects.ContainsKey(group);

        public double Stat(string key, double fallback = 0.0)
            => StatMods.TryGetValue(key, out var v) ? v : fallback;
    }

    /// <summary>
    /// Définition d'une fusion de greffes (§15) : deux greffes prérequises se lient en une forme
    /// composée. Hérite de GraftDef (mêmes Effects/StatMods/Tint pour l'application côté nœuds) et
    /// ajoute la recette (Requires), les archétypes qui alimentent sa jauge dédiée, et les paramètres
    /// de cette jauge. À l'acceptation : les 2 greffes sources sont retirées, la fusion équipée
    /// (occupation 2→1, un slot libéré si FreesSlot).
    /// </summary>
    public sealed class FusionDef : GraftDef
    {
        public List<string> Requires = new();       // greffes prérequises (2)
        public List<string> SourceAiTypes = new();  // archétypes qui alimentent la jauge de fusion
        public bool FreesSlot = true;
        public string GaugeKey = "";
        public int GaugeThreshold = 20;
        public int PointsPerKill = 1;
        public int PointsPerEliteKill = 2;
        public double DeclineMult = 1.5;

        /// <summary>Points d'un kill vers la jauge de fusion (0 si l'archétype n'y contribue pas).</summary>
        public int KillPoints(string aiType, bool isElite)
            => SourceAiTypes.Contains(aiType) ? (isElite ? PointsPerEliteKill : PointsPerKill) : 0;
    }

    /// <summary>Configuration complète chargée depuis grafts.json (immuable après parsing).</summary>
    public sealed class GraftConfig
    {
        public int SlotBaseCount = 3;
        public int SlotMaxCount = 5;
        public bool ReplacementWhenFull = true;

        public Dictionary<string, int> Thresholds = new();
        public Dictionary<string, string> AiTypeToGauge = new();
        public int PointsBasicKill = 1;
        public int EliteKillArchetypePoints = 2;
        public int EliteKillStalkerPoints = 1;
        public int MiniBossStalkerPoints = 2;
        public int BossStalkerPoints = 3;
        public bool OwnedGraftPausesGauge = true;
        public bool ResumePausedGaugeFromSavedValue = true;
        public double DeclineThresholdMultiplier = 1.5;

        /// <summary>Clé de jauge des champions (dérivée : greffe dont sourceAiType == "champion").</summary>
        public string ChampionGaugeKey = "stalker";

        public List<GraftDef> Grafts = new();
        public List<FusionDef> Fusions = new();

        /// <summary>Greffe associée à une clé de jauge (1:1), ou null.</summary>
        public GraftDef? GraftForGauge(string gauge)
        {
            foreach (var g in Grafts) if (g.Gauge == gauge) return g;
            return null;
        }

        /// <summary>Cherche une greffe OU une fusion par id (le HUD/écran/pause affichent les deux).</summary>
        public GraftDef? GraftById(string id)
        {
            foreach (var g in Grafts) if (g.Id == id) return g;
            foreach (var f in Fusions) if (f.Id == id) return f;
            return null;
        }

        /// <summary>Fusion associée à une clé de jauge de fusion, ou null.</summary>
        public FusionDef? FusionForGauge(string gaugeKey)
        {
            foreach (var f in Fusions) if (f.GaugeKey == gaugeKey) return f;
            return null;
        }

        public FusionDef? FusionById(string id)
        {
            foreach (var f in Fusions) if (f.Id == id) return f;
            return null;
        }

        /// <summary>Toutes les clés de jauge connues (ordre stable : ordre des greffes).</summary>
        public IEnumerable<string> GaugeKeys()
        {
            foreach (var g in Grafts) yield return g.Gauge;
        }
    }

    // -------------------------------------------------------------------------
    // Parsing (System.Text.Json — pur .NET)
    // -------------------------------------------------------------------------

    /// <summary>Parse le contenu de grafts.json. Les clés commençant par '_' (commentaires) sont ignorées.</summary>
    public static GraftConfig Parse(string json)
    {
        var cfg = new GraftConfig();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("slots", out var slots))
        {
            cfg.SlotBaseCount = GetInt(slots, "baseCount", cfg.SlotBaseCount);
            cfg.SlotMaxCount = GetInt(slots, "maxCount", cfg.SlotMaxCount);
            cfg.ReplacementWhenFull = GetBool(slots, "replacementWhenFull", cfg.ReplacementWhenFull);
        }

        if (root.TryGetProperty("gauges", out var gauges))
        {
            if (gauges.TryGetProperty("thresholds", out var th))
                foreach (var p in th.EnumerateObject())
                    if (!p.Name.StartsWith("_")) cfg.Thresholds[p.Name] = p.Value.GetInt32();

            if (gauges.TryGetProperty("aiTypeToGauge", out var map))
                foreach (var p in map.EnumerateObject())
                    if (!p.Name.StartsWith("_")) cfg.AiTypeToGauge[p.Name] = p.Value.GetString() ?? "";

            cfg.PointsBasicKill = GetInt(gauges, "pointsBasicKill", cfg.PointsBasicKill);
            cfg.EliteKillArchetypePoints = GetInt(gauges, "eliteKillArchetypePoints", cfg.EliteKillArchetypePoints);
            cfg.EliteKillStalkerPoints = GetInt(gauges, "eliteKillStalkerPoints", cfg.EliteKillStalkerPoints);
            cfg.MiniBossStalkerPoints = GetInt(gauges, "miniBossStalkerPoints", cfg.MiniBossStalkerPoints);
            cfg.BossStalkerPoints = GetInt(gauges, "bossStalkerPoints", cfg.BossStalkerPoints);
            cfg.OwnedGraftPausesGauge = GetBool(gauges, "ownedGraftPausesGauge", cfg.OwnedGraftPausesGauge);
            cfg.ResumePausedGaugeFromSavedValue = GetBool(gauges, "resumePausedGaugeFromSavedValue", cfg.ResumePausedGaugeFromSavedValue);
            cfg.DeclineThresholdMultiplier = GetDouble(gauges, "declineThresholdMultiplier", cfg.DeclineThresholdMultiplier);
        }

        if (root.TryGetProperty("grafts", out var grafts) && grafts.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in grafts.EnumerateArray())
            {
                var def = new GraftDef
                {
                    Id = GetString(e, "id"),
                    Name = GetString(e, "name"),
                    Gauge = GetString(e, "gauge"),
                    SourceAiType = GetString(e, "sourceAiType"),
                    Rarity = GetString(e, "rarity", "common"),
                    Description = GetString(e, "description"),
                    HudIcon = GetString(e, "hudIcon"),
                };

                if (e.TryGetProperty("tint", out var tint) && tint.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<float>();
                    foreach (var t in tint.EnumerateArray()) list.Add((float)t.GetDouble());
                    if (list.Count >= 3) def.Tint = new[] { list[0], list[1], list[2] };
                }

                if (e.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Object)
                    foreach (var group in effects.EnumerateObject())
                    {
                        if (group.Name.StartsWith("_") || group.Value.ValueKind != JsonValueKind.Object) continue;
                        var pars = new Dictionary<string, double>();
                        foreach (var p in group.Value.EnumerateObject())
                            if (!p.Name.StartsWith("_") && p.Value.ValueKind == JsonValueKind.Number)
                                pars[p.Name] = p.Value.GetDouble();
                        def.Effects[group.Name] = pars;
                    }

                if (e.TryGetProperty("statMods", out var sm) && sm.ValueKind == JsonValueKind.Object)
                    foreach (var p in sm.EnumerateObject())
                        if (!p.Name.StartsWith("_") && p.Value.ValueKind == JsonValueKind.Number)
                            def.StatMods[p.Name] = p.Value.GetDouble();

                cfg.Grafts.Add(def);

                if (def.SourceAiType == "champion" && !string.IsNullOrEmpty(def.Gauge))
                    cfg.ChampionGaugeKey = def.Gauge;
            }
        }

        if (root.TryGetProperty("fusions", out var fusions) && fusions.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in fusions.EnumerateArray())
            {
                var def = new FusionDef
                {
                    Id = GetString(e, "id"),
                    Name = GetString(e, "name"),
                    Rarity = GetString(e, "rarity", "epic"),
                    Description = GetString(e, "description"),
                    HudIcon = GetString(e, "hudIcon"),
                    FreesSlot = GetBool(e, "freesSlot", true),
                };

                if (e.TryGetProperty("tint", out var tint) && tint.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<float>();
                    foreach (var t in tint.EnumerateArray()) list.Add((float)t.GetDouble());
                    if (list.Count >= 3) def.Tint = new[] { list[0], list[1], list[2] };
                }

                if (e.TryGetProperty("requires", out var req) && req.ValueKind == JsonValueKind.Array)
                    foreach (var r in req.EnumerateArray())
                        if (r.ValueKind == JsonValueKind.String) def.Requires.Add(r.GetString() ?? "");

                if (e.TryGetProperty("sourceAiTypes", out var srcs) && srcs.ValueKind == JsonValueKind.Array)
                    foreach (var s in srcs.EnumerateArray())
                        if (s.ValueKind == JsonValueKind.String) def.SourceAiTypes.Add(s.GetString() ?? "");

                if (e.TryGetProperty("gauge", out var g) && g.ValueKind == JsonValueKind.Object)
                {
                    def.GaugeKey = GetString(g, "key");
                    def.GaugeThreshold = GetInt(g, "threshold", def.GaugeThreshold);
                    def.PointsPerKill = GetInt(g, "pointsPerKillSourceArchetype", def.PointsPerKill);
                    def.PointsPerEliteKill = GetInt(g, "pointsPerEliteKillSourceArchetype", def.PointsPerEliteKill);
                    def.DeclineMult = GetDouble(g, "declineThresholdMultiplier", def.DeclineMult);
                }

                if (e.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Object)
                    foreach (var group in effects.EnumerateObject())
                    {
                        if (group.Name.StartsWith("_") || group.Value.ValueKind != JsonValueKind.Object) continue;
                        var pars = new Dictionary<string, double>();
                        foreach (var p in group.Value.EnumerateObject())
                            if (!p.Name.StartsWith("_") && p.Value.ValueKind == JsonValueKind.Number)
                                pars[p.Name] = p.Value.GetDouble();
                        def.Effects[group.Name] = pars;
                    }

                if (e.TryGetProperty("statMods", out var sm) && sm.ValueKind == JsonValueKind.Object)
                    foreach (var p in sm.EnumerateObject())
                        if (!p.Name.StartsWith("_") && p.Value.ValueKind == JsonValueKind.Number)
                            def.StatMods[p.Name] = p.Value.GetDouble();

                cfg.Fusions.Add(def);

                // Le seuil de la jauge de fusion rejoint la table générale : EffectiveThreshold
                // (bonus méta + malus de refus) fonctionne alors uniformément pour les fusions.
                if (!string.IsNullOrEmpty(def.GaugeKey))
                    cfg.Thresholds[def.GaugeKey] = def.GaugeThreshold;
            }
        }

        return cfg;
    }

    // -------------------------------------------------------------------------
    // Routage kill → jauge (pur, déterministe — §12.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Répartit les points d'un kill sur les jauges concernées. Priorité :
    /// boss &gt; mini-boss &gt; élite &gt; basique (catégories exclusives).
    /// - basique : +pointsBasicKill vers la jauge de son archétype.
    /// - élite   : +eliteKillArchetypePoints vers sa jauge d'archétype ET +eliteKillStalkerPoints vers 'stalker'.
    /// - mini-boss : +miniBossStalkerPoints vers 'stalker'.
    /// - boss    : +bossStalkerPoints vers 'stalker'.
    /// Un archétype inconnu (non présent dans aiTypeToGauge) ne contribue à aucune jauge d'archétype.
    /// </summary>
    public static List<GaugeContribution> RouteKill(
        GraftConfig cfg, string aiType, bool isElite, bool isMiniBoss, bool isBoss)
    {
        var result = new List<GaugeContribution>(2);

        if (isBoss)
        {
            result.Add(new GaugeContribution(cfg.ChampionGaugeKey, cfg.BossStalkerPoints));
            return result;
        }
        if (isMiniBoss)
        {
            result.Add(new GaugeContribution(cfg.ChampionGaugeKey, cfg.MiniBossStalkerPoints));
            return result;
        }
        if (isElite)
        {
            if (cfg.AiTypeToGauge.TryGetValue(aiType, out var g) && !string.IsNullOrEmpty(g))
                result.Add(new GaugeContribution(g, cfg.EliteKillArchetypePoints));
            result.Add(new GaugeContribution(cfg.ChampionGaugeKey, cfg.EliteKillStalkerPoints));
            return result;
        }

        if (cfg.AiTypeToGauge.TryGetValue(aiType, out var basicGauge) && !string.IsNullOrEmpty(basicGauge))
            result.Add(new GaugeContribution(basicGauge, cfg.PointsBasicKill));
        return result;
    }

    // -------------------------------------------------------------------------
    // Seuils & slots (purs — §12.3, §17)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seuil effectif après le bonus méta 'graft_metabolism' : round(threshold * (1 - bonus)), min 1.
    /// Arrondi au plus proche (et non ceil) pour reproduire les valeurs cibles du design (§17 :
    /// 30→21, 24→17, 14→10, 7→5, 3→2) — avec ceil, la jauge stalker (seuil 3) resterait à 3 et
    /// l'upgrade n'aurait aucun effet sur elle.
    /// </summary>
    public static int EffectiveThreshold(int baseThreshold, double gaugeSpeedBonus)
    {
        if (gaugeSpeedBonus < 0.0) gaugeSpeedBonus = 0.0;
        if (gaugeSpeedBonus > 0.95) gaugeSpeedBonus = 0.95;
        int t = (int)Math.Round(baseThreshold * (1.0 - gaugeSpeedBonus), MidpointRounding.AwayFromZero);
        return Math.Max(1, t);
    }

    /// <summary>Seuil après un refus (§12.3) : ceil(effectiveThreshold * declineMultiplier).</summary>
    public static int DeclinedThreshold(int effectiveThreshold, double declineMultiplier)
        => Math.Max(1, (int)Math.Ceiling(effectiveThreshold * declineMultiplier));

    /// <summary>Nombre de slots pour la run : min(base + bonus méta, max).</summary>
    public static int SlotCount(GraftConfig cfg, int metaSlotBonus)
    {
        if (metaSlotBonus < 0) metaSlotBonus = 0;
        return Math.Min(cfg.SlotBaseCount + metaSlotBonus, cfg.SlotMaxCount);
    }

    // -------------------------------------------------------------------------
    // Helpers de parsing
    // -------------------------------------------------------------------------

    private static int GetInt(JsonElement e, string key, int fallback)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    private static double GetDouble(JsonElement e, string key, double fallback)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;

    private static bool GetBool(JsonElement e, string key, bool fallback)
        => e.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean() : fallback;

    private static string GetString(JsonElement e, string key, string fallback = "")
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
}
