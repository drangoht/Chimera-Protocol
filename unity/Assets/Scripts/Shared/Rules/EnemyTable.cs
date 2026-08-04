using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Lecture de <c>enemies.json</c> et <c>enemies_biome_expansion.json</c> — logique pure et
/// testable (Lot 4).
///
/// <para>Le bestiaire est <b>data-driven</b> : un ennemi n'est pas une classe mais une entrée de
/// données, avec un <c>ai.type</c> qui désigne un comportement partagé. C'est ce qui permet
/// d'avoir 31 ennemis pour une poignée de comportements — et c'est une propriété à conserver, sans
/// quoi le port produirait 31 classes là où le jeu d'origine en a quatre.</para>
/// </summary>
public static class EnemyTable
{
    /// <summary>Comportements d'IA reconnus. Un type inconnu retombe sur la poursuite directe.</summary>
    public enum AiType
    {
        StraightChase,
        ErraticChase,
        RangedKiter,
        SlowHunter,
        LungingChaser,
        ChargingBruiser,
        ConeKiter,
        ShieldedChaser,
        BossCore,
    }

    /// <summary>Définition complète d'un ennemi.</summary>
    public sealed class EnemyDef
    {
        public string Id = "";
        public string Name = "";
        public string Role = "";
        public float  MaxHp = 20f;
        public float  Speed = 120f;
        public float  DamagePerSecond = 5f;
        public int    XpValue = 1;
        public float  ContactRadius = 24f;
        public float  DetectionRange = 9999f;
        public float  SpawnStartMinute;
        public float  SpawnWeight = 1f;
        public float  HpScalingPerMinute = 0.12f;
        public float  DamageScalingPerMinute = 0.06f;
        public AiType Ai = AiType.StraightChase;
        public string FramesPath = "";
        public string Biome = "";

        /// <summary>
        /// Nombre maximal d'exemplaires vivants simultanément. <b>0 = faune ordinaire</b> (aucune
        /// limite propre, seul le plafond global s'applique) ; 1 = champion.
        ///
        /// <para>C'est le garde-fou qui a un jour manqué au boss : sans lui, un second Noyau Rouillé
        /// apparaissait toutes les 28-50 s avant que le premier soit mort, et le combat devenait
        /// littéralement impossible à finir.</para>
        /// </summary>
        public int MaxSimultaneous;

        /// <summary>Ce type est-il un champion (mini-boss ou boss) ?</summary>
        public bool IsChampion => MaxSimultaneous > 0;
    }

    /// <summary>Analyse un ou plusieurs fichiers de bestiaire et les fusionne.</summary>
    public static Dictionary<string, EnemyDef> Parse(params string[] jsonDocuments)
    {
        var result = new Dictionary<string, EnemyDef>(StringComparer.Ordinal);

        foreach (string json in jsonDocuments)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("enemies", out var list)) continue;

            foreach (var e in list.EnumerateArray())
            {
                var def = new EnemyDef
                {
                    Id = Str(e, "id"),
                    Name = Str(e, "name"),
                    Role = Str(e, "role"),
                    MaxHp = Flt(e, "maxHp", 20f),
                    Speed = Flt(e, "speed", 120f),
                    DamagePerSecond = Flt(e, "damagePerSecond", 5f),
                    XpValue = Int(e, "xpValue", 1),
                    ContactRadius = Flt(e, "contactRadius", 24f),
                    DetectionRange = Flt(e, "detectionRange", 9999f),
                    SpawnStartMinute = Flt(e, "spawnStartMinute"),
                    SpawnWeight = Flt(e, "spawnWeight", 1f),
                    HpScalingPerMinute = Flt(e, "hpScalingPerMinute", 0.12f),
                    DamageScalingPerMinute = Flt(e, "damageScalingPerMinute", 0.06f),
                    FramesPath = Str(e, "framesPath"),
                    Biome = Str(e, "biome"),
                    MaxSimultaneous = Int(e, "maxSimultaneous"),
                };

                if (e.TryGetProperty("ai", out var ai)) def.Ai = ParseAi(Str(ai, "type"));

                if (def.Id.Length > 0) result[def.Id] = def;
            }
        }

        return result;
    }

    /// <summary>
    /// Convertit le libellé du JSON en comportement. Un type inconnu <b>ne fait pas échouer le
    /// chargement</b> : il retombe sur la poursuite directe, pour qu'une donnée mal saisie donne un
    /// ennemi jouable plutôt qu'un bestiaire vide.
    /// </summary>
    public static AiType ParseAi(string raw) => raw switch
    {
        "straight_chase"   => AiType.StraightChase,
        "erratic_chase"    => AiType.ErraticChase,
        "ranged_kiter"     => AiType.RangedKiter,
        "slow_hunter"      => AiType.SlowHunter,
        "lunging_chaser"   => AiType.LungingChaser,
        "charging_bruiser" => AiType.ChargingBruiser,
        "cone_kiter"       => AiType.ConeKiter,
        "shielded_chaser"  => AiType.ShieldedChaser,
        "boss_core"        => AiType.BossCore,
        _                  => AiType.StraightChase,
    };

    /// <summary>
    /// Libellé de données d'un comportement — l'inverse exact de <see cref="ParseAi"/>.
    ///
    /// <para>Nécessaire au routage des éliminations vers les jauges d'Assimilation, qui indexe les
    /// archétypes par <b>leur nom dans le JSON</b> (<c>straight_chase</c>…). Sans cet inverse, la
    /// correspondance se referait à la main quelque part, et un seul libellé mal recopié suffirait à
    /// rendre une jauge entière inatteignable — sans erreur, la greffe correspondante ne serait
    /// simplement jamais proposée.</para>
    /// </summary>
    public static string AiKey(AiType ai) => ai switch
    {
        AiType.StraightChase   => "straight_chase",
        AiType.ErraticChase    => "erratic_chase",
        AiType.RangedKiter     => "ranged_kiter",
        AiType.SlowHunter      => "slow_hunter",
        AiType.LungingChaser   => "lunging_chaser",
        AiType.ChargingBruiser => "charging_bruiser",
        AiType.ConeKiter       => "cone_kiter",
        AiType.ShieldedChaser  => "shielded_chaser",
        AiType.BossCore        => "boss_core",
        _                      => "straight_chase",
    };

    /// <summary>
    /// Ennemis dont la fenêtre d'apparition est ouverte à un instant donné, avec leur poids.
    /// </summary>
    public static List<(EnemyDef Def, float Weight)> Eligible(
        IEnumerable<EnemyDef> all, float minutes, string? biome = null)
    {
        var pool = new List<(EnemyDef, float)>();

        foreach (var def in all)
        {
            if (minutes < def.SpawnStartMinute) continue;
            if (def.Ai == AiType.BossCore) continue;   // le boss n'apparaît jamais par la vague

            // Un ennemi marqué d'un biome n'apparaît que là ; les autres sont universels.
            if (biome != null && def.Biome.Length > 0 && def.Biome != biome) continue;

            if (def.SpawnWeight > 0f) pool.Add((def, def.SpawnWeight));
        }

        return pool;
    }

    private static string Str(JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;

    private static float Flt(JsonElement e, string name, float fallback = 0f)
        => e.TryGetProperty(name, out var v) && v.TryGetSingle(out float f) ? f : fallback;
}
