using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Lecture de <c>meta_upgrades.json</c> et application des bonus permanents — logique pure et
/// testable (Lot 6).
///
/// <para><b>Ce que cette classe protège.</b> Les bonus méta étaient appliqués sous Godot par une
/// suite d'affectations à l'intérieur d'un <c>Node</c> : ids et valeurs en dur, aucun test possible.
/// Or ce sont eux qui décident de la puissance du joueur <b>avant même que la run commence</b>, et
/// deux d'entre eux (recharge et réduction de dégâts) doivent passer par les mêmes plafonds que les
/// passifs — sans quoi le Hub rouvrirait exactement le power-creep que <see cref="StatCaps"/>
/// referme.</para>
/// </summary>
public static class MetaUpgradeTable
{
    /// <summary>
    /// Paramètres de la formule d'Échos, lus dans <c>echoesFormula</c>.
    ///
    /// <para><b>Un seul porteur.</b> <see cref="EchoFormula"/> attend treize arguments ; les passer un
    /// par un depuis chaque appelant multiplierait les occasions d'en intervertir deux — une erreur
    /// invisible, puisque le résultat reste un nombre plausible. Il en existait <b>deux</b>, cette
    /// classe et un jumeau côté moteur qui redéclarait les huit mêmes champs avec les huit mêmes
    /// valeurs ; c'était le jumeau que le jeu utilisait, si bien que le bloc <c>echoesFormula</c>
    /// était lu ici, testé, et <b>jeté</b> — les valeurs coïncidant, rien ne dépassait.</para>
    ///
    /// <para>⚠ <b>Source unique du total.</b> Le bilan de fin de run affiche une somme animée et
    /// crédite un total : les deux <b>doivent</b> venir du même calcul. Sous Godot ils venaient de
    /// deux endroits différents et divergeaient dès qu'un multiplicateur de palier entrait en jeu —
    /// le joueur voyait un chiffre et en recevait un autre.</para>
    /// </summary>
    public sealed class EchoParams
    {
        public int TimeDiv   = 20;
        public int KillDiv   = 10;
        public int CoreMult  = 5;
        public int BaseBonus = 10;
        public int CapKills  = 520;
        public int CapCores  = 22;
        public double OvertimeDampening = 0.15;
        public int OvertimeBonusCap = 100;

        /// <summary>Réglages du jeu publié, quand aucun document n'a été chargé.</summary>
        public static readonly EchoParams Default = new();

        /// <summary>
        /// Total gagné. <b>Point d'entrée unique</b> : tout affichage part de cette valeur et se
        /// contente de la parcourir.
        /// </summary>
        public int Total(int runSeconds, int kills, int cores, double tierMult = 1.0)
            => Detailed(runSeconds, kills, cores, tierMult).Total;

        /// <summary>Même calcul, en exposant à part le bonus d'overtime pour l'affichage détaillé.</summary>
        /// <remarks>
        /// ⚠ <b>Le plafond de temps passé à la formule est <c>runSeconds</c> lui-même</b>, jamais les
        /// 780 s de <c>capTimeSecs</c> (champ que ni ce porteur ni son défunt jumeau ne déclarent).
        /// <c>min(t, capTimeSecs)</c> vaut donc toujours <c>t</c> et <c>max(0, t − capTimeSecs)</c>
        /// toujours zéro : le temps n'est <b>pas plafonné</b> et le bonus de surcharge est
        /// <b>toujours nul</b>, alors que <c>meta_upgrades.json</c> calibre l'inverse (211 Échos pour
        /// une run complète, 311 pour une run extrême — plafond explicite contre le farm). Les tests
        /// de <see cref="EchoFormula"/>, eux, passent bien 780 : la règle est juste, c'est l'appel qui
        /// ne l'est pas. Corriger déplace l'économie du jeu — décision d'équilibrage, pas de refacto :
        /// c'est pourquoi ce comportement est reconduit tel quel ici.
        /// </remarks>
        public (int Total, int OvertimeBonus) Detailed(int runSeconds, int kills, int cores,
                                                       double tierMult = 1.0)
            => EchoFormula.CalculateDetailed(runSeconds, kills, cores,
                                             TimeDiv, KillDiv, CoreMult, BaseBonus,
                                             runSeconds, CapKills, CapCores,
                                             OvertimeDampening, OvertimeBonusCap, tierMult);
    }

    /// <summary>Contenu complet du fichier de méta-progression.</summary>
    public sealed class Document
    {
        public readonly Dictionary<string, MetaUpgradeDefinition> Upgrades =
            new(StringComparer.Ordinal);

        public readonly List<MetaUpgradeDefinition> Ordered = new();

        public EchoParams Echoes = new();

        /// <summary>Temps imparti d'une run, en secondes — la frontière standard/overtime.</summary>
        public int RunDurationSeconds = 780;
    }

    /// <summary>Analyse <c>meta_upgrades.json</c>. Un fichier vide rend un document par défaut.</summary>
    public static Document Parse(string? json)
    {
        var doc = new Document();
        if (string.IsNullOrWhiteSpace(json)) return doc;

        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        if (root.TryGetProperty("runDurationSeconds", out var rd) && rd.TryGetInt32(out int seconds) && seconds > 0)
            doc.RunDurationSeconds = seconds;

        if (root.TryGetProperty("echoesFormula", out var f))
        {
            var e = doc.Echoes;
            e.TimeDiv   = Int(f, "timeDiv",   e.TimeDiv);
            e.KillDiv   = Int(f, "killDiv",   e.KillDiv);
            e.CoreMult  = Int(f, "coreMult",  e.CoreMult);
            e.BaseBonus = Int(f, "baseBonus", e.BaseBonus);
            e.CapKills  = Int(f, "capKills",  e.CapKills);
            e.CapCores  = Int(f, "capCores",  e.CapCores);
            e.OvertimeBonusCap = Int(f, "overtimeBonusCap", e.OvertimeBonusCap);

            if (f.TryGetProperty("overtimeDampening", out var od) && od.TryGetDouble(out double d))
                e.OvertimeDampening = d;
        }

        if (root.TryGetProperty("upgrades", out var upgrades))
        {
            foreach (var u in upgrades.EnumerateArray())
            {
                var def = new MetaUpgradeDefinition
                {
                    Id          = Str(u, "id"),
                    Name        = Str(u, "name"),
                    Description = Str(u, "description"),
                    MaxLevel    = Int(u, "maxLevel", 1),
                    StatTarget  = Str(u, "statTarget"),
                };

                if (u.TryGetProperty("costPerLevel", out var costs))
                    foreach (var c in costs.EnumerateArray())
                        if (c.TryGetInt32(out int cost)) def.CostPerLevel.Add(cost);

                if (u.TryGetProperty("effectPerLevel", out var effects))
                    foreach (var e in effects.EnumerateArray())
                        if (e.TryGetDouble(out double effect)) def.EffectPerLevel.Add(effect);

                if (def.Id.Length == 0) continue;

                doc.Upgrades[def.Id] = def;
                doc.Ordered.Add(def);
            }
        }

        return doc;
    }

    /// <summary>
    /// Coût du <b>prochain</b> niveau, ou <c>-1</c> si l'amélioration est au maximum. Renvoyer un
    /// coût pour un niveau inexistant afficherait un prix sur un bouton qui ne peut rien acheter.
    /// </summary>
    public static int NextCost(MetaUpgradeDefinition def, int currentLevel)
    {
        if (currentLevel >= def.MaxLevel) return -1;
        return currentLevel < def.CostPerLevel.Count ? def.CostPerLevel[currentLevel] : -1;
    }

    /// <summary>Total dépensé pour atteindre <paramref name="level"/> — base du remboursement.</summary>
    public static int SpentUpTo(MetaUpgradeDefinition def, int level)
    {
        int total = 0;
        for (int i = 0; i < level && i < def.CostPerLevel.Count; i++) total += def.CostPerLevel[i];
        return total;
    }

    /// <summary>Bonus permanents à appliquer aux statistiques du joueur avant une run.</summary>
    public readonly struct Bonuses
    {
        public readonly float MaxHp;
        public readonly float DamageMultiplier;
        public readonly float Speed;
        public readonly float CooldownReduction;
        public readonly float DamageReduction;
        public readonly float HpRegenPerSecond;

        public Bonuses(float maxHp, float damageMultiplier, float speed,
                       float cooldownReduction, float damageReduction, float hpRegen)
        {
            MaxHp = maxHp;
            DamageMultiplier = damageMultiplier;
            Speed = speed;
            CooldownReduction = cooldownReduction;
            DamageReduction = damageReduction;
            HpRegenPerSecond = hpRegen;
        }
    }

    /// <summary>
    /// Bonus cumulés pour les niveaux possédés. <paramref name="levelOf"/> est injecté : cette
    /// fonction ne connaît ni la sauvegarde ni le moteur, ce qui la rend vérifiable seule.
    ///
    /// <para>⚠ Les plafonds ne sont <b>pas</b> appliqués ici : ils le sont à l'écriture dans les
    /// statistiques, en même temps que les passifs de run. Les appliquer deux fois — une fois sur le
    /// bonus méta seul, une fois sur le total — donnerait un résultat différent selon l'ordre.</para>
    /// </summary>
    public static Bonuses BonusesFor(Func<string, int> levelOf)
        => new(
            maxHp: levelOf("hp_boost") * 20f + levelOf("hp_boost_2") * 35f,
            damageMultiplier: levelOf("damage_boost") * 0.10f + levelOf("damage_boost_2") * 0.08f,
            speed: levelOf("speed_boost") * 15f,
            cooldownReduction: levelOf("cooldown_reduction") * 0.05f
                             + levelOf("cooldown_reduction_2") * 0.04f,
            damageReduction: levelOf("damage_reduction") * 0.05f
                           + levelOf("damage_reduction_2") * 0.04f,
            hpRegen: levelOf("hp_regen") * 0.4f);

    // ─── Lecture tolérante ────────────────────────────────────────────────────

    private static string Str(JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? fallback : fallback;

    private static int Int(JsonElement e, string name, int fallback = 0)
        => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
}
