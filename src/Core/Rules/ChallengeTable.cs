using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Système de Défis / Succès — logique PURE testable (aucune dépendance Godot).
/// Modèle : GraftTable / EliteAffixTable. Ne décide QUE de la définition des défis (lus depuis
/// data/challenges.json) et de leur évaluation à partir d'un <see cref="ChallengeContext"/> plat.
/// L'agrégation du contexte (RunStatsTracker, AssimilationSystem, GameSettings, compteurs sauvegardés)
/// et l'octroi des récompenses vivent côté nœud (ChallengeSystem).
///
/// Parsing via System.Text.Json (pur .NET, disponible en xUnit). Voir docs/DESIGN_CHALLENGES.md.
/// </summary>
public static class ChallengeTable
{
    /// <summary>Type de récompense d'un défi. Le Lot 1 n'octroie que <c>Echoes</c> ;
    /// <c>Perk</c>/<c>Cosmetic</c> sont enregistrés comme débloqués et exploités par les lots 3/4.</summary>
    public enum RewardKind { Echoes, Perk, Cosmetic }

    /// <summary>Définition d'un défi (chiffres bruts lus depuis challenges.json).</summary>
    public sealed class ChallengeDef
    {
        public string Id = "";
        public string NameKey = "";
        public string DescKey = "";
        public string Category = "";       // survival / combat / assimilation / mastery

        public string CondType = "";       // cf. IsMet
        public double CondValue;           // seuil numérique (selon le type)
        public string CondParam = "";      // paramètre optionnel (ex. id de biome)

        public RewardKind RewardType = RewardKind.Echoes;
        public int RewardEchoes;           // si RewardType == Echoes
        public string RewardId = "";       // id de perk / cosmétique (si Perk/Cosmetic)
    }

    /// <summary>
    /// Instantané plat de tout ce dont l'évaluation a besoin, agrégé par ChallengeSystem à la fin de
    /// run. Garde <see cref="IsMet"/> totalement pur/testable.
    /// </summary>
    public readonly record struct ChallengeContext(
        int RunTimeSeconds,
        int RunKills,
        int RunCores,
        bool LevelCompleted,
        string BiomeId,
        int DifficultyRank,        // Facile=0, Normal=1, Difficile=2
        int RunGraftsEquipped,
        bool RunFusionForged,
        long LifetimeKills,
        int LifetimeRuns,
        int BiomesCompletedCount);

    /// <summary>Le défi est-il satisfait par ce contexte de fin de run ?</summary>
    public static bool IsMet(ChallengeDef def, in ChallengeContext ctx)
    {
        switch (def.CondType)
        {
            case "survive_seconds":     return ctx.RunTimeSeconds     >= def.CondValue;
            case "kills_in_run":        return ctx.RunKills           >= def.CondValue;
            case "cores_in_run":        return ctx.RunCores           >= def.CondValue;
            case "grafts_in_run":       return ctx.RunGraftsEquipped  >= def.CondValue;
            case "fusion_in_run":       return ctx.RunFusionForged;
            case "complete_level":      return ctx.LevelCompleted;
            case "complete_biome":      return ctx.LevelCompleted && ctx.BiomeId == def.CondParam;
            case "complete_difficulty": return ctx.LevelCompleted && ctx.DifficultyRank >= (int)def.CondValue;
            case "lifetime_kills":      return ctx.LifetimeKills      >= def.CondValue;
            case "lifetime_runs":       return ctx.LifetimeRuns       >= def.CondValue;
            case "biomes_completed":    return ctx.BiomesCompletedCount >= def.CondValue;
            default:                    return false;   // type inconnu : jamais satisfait (sûr)
        }
    }

    /// <summary>
    /// Retourne les ids des défis nouvellement complétés par ce contexte (satisfaits ET pas déjà
    /// débloqués). Ordre stable = ordre des définitions.
    /// </summary>
    public static List<string> NewlyCompleted(
        IEnumerable<ChallengeDef> defs, in ChallengeContext ctx, ISet<string> alreadyUnlocked)
    {
        var result = new List<string>();
        foreach (var def in defs)
            if (!alreadyUnlocked.Contains(def.Id) && IsMet(def, in ctx))
                result.Add(def.Id);
        return result;
    }

    // ---------------------------------------------------------------------------
    // Parsing challenges.json
    // ---------------------------------------------------------------------------

    /// <summary>Parse le JSON complet en liste de définitions. Ignore les propriétés inconnues.</summary>
    public static List<ChallengeDef> Parse(string json)
    {
        var list = new List<ChallengeDef>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("challenges", out var arr)) return list;

        foreach (var el in arr.EnumerateArray())
        {
            var def = new ChallengeDef
            {
                Id       = GetStr(el, "id"),
                NameKey  = GetStr(el, "nameKey"),
                DescKey  = GetStr(el, "descKey"),
                Category = GetStr(el, "category"),
            };

            if (el.TryGetProperty("condition", out var cond))
            {
                def.CondType  = GetStr(cond, "type");
                def.CondParam = GetStr(cond, "param");
                if (cond.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                    def.CondValue = v.GetDouble();
            }

            if (el.TryGetProperty("reward", out var rew))
            {
                def.RewardType = GetStr(rew, "type") switch
                {
                    "perk"     => RewardKind.Perk,
                    "cosmetic" => RewardKind.Cosmetic,
                    _          => RewardKind.Echoes,
                };
                if (rew.TryGetProperty("value", out var rv) && rv.ValueKind == JsonValueKind.Number)
                    def.RewardEchoes = rv.GetInt32();
                def.RewardId = GetStr(rew, "id");
            }

            if (def.Id.Length > 0) list.Add(def);
        }
        return list;
    }

    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
}
