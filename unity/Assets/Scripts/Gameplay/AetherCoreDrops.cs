using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quels ennemis laissent tomber un Noyau d'Aether, et combien.
///
/// <para>La règle vient des <b>données</b> (<c>meta_upgrades.json</c>, <c>aetherCores.enemyDropRules</c>)
/// et n'est pas recopiée ici : c'est ce qui permet d'ajouter un porteur de Noyau sans toucher au code,
/// et surtout d'éviter qu'une valeur diverge entre les deux moteurs.</para>
///
/// <para>Aujourd'hui une seule entrée — le <b>Colosse Greffé</b>, à coup sûr. C'est cohérent avec ce
/// que le Noyau demande : un adversaire lent et massif laisse une récompense qu'il faut aller
/// chercher, là où une nuée qui meurt en dix endroits à la fois n'en laisserait aucune décision.</para>
/// </summary>
public static class AetherCoreDrops
{
    private readonly struct Rule
    {
        public readonly int Count;
        public readonly float Chance;

        public Rule(int count, float chance) { Count = count; Chance = chance; }
    }

    private static Dictionary<string, Rule>? _rules;

    /// <summary>Nombre d'entrées chargées — observable pour les vérifications.</summary>
    public static int RuleCount => Table.Count;

    private static Dictionary<string, Rule> Table => _rules ??= Load();

    private static Dictionary<string, Rule> Load()
    {
        var rules = new Dictionary<string, Rule>(System.StringComparer.Ordinal);

        string? json = DataFiles.Load("meta_upgrades.json");
        if (json == null) return rules;

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("aetherCores", out var cores)) return rules;
            if (!cores.TryGetProperty("enemyDropRules", out var list)) return rules;

            foreach (var entry in list.EnumerateArray())
            {
                if (!entry.TryGetProperty("enemyId", out var id)) continue;

                int count = entry.TryGetProperty("dropCount", out var c) ? c.GetInt32() : 1;
                float chance = entry.TryGetProperty("dropChance", out var p) ? p.GetSingle() : 1f;

                rules[id.GetString() ?? ""] = new Rule(count, chance);
            }
        }
        catch (System.Text.Json.JsonException e)
        {
            Debug.LogWarning($"[AetherCoreDrops] meta_upgrades.json illisible : {e.Message}");
        }

        return rules;
    }

    /// <summary>
    /// Pose les Noyaux dus à la mort d'un ennemi. Le tirage passe par <see cref="Gd.Randf"/> comme
    /// tout l'aléatoire du jeu : une graine fixée doit rejouer la même partie, butin compris.
    /// </summary>
    public static void OnEnemyDied(string enemyId, Vector3 position)
    {
        if (enemyId.Length == 0 || !Table.TryGetValue(enemyId, out var rule)) return;
        if (rule.Chance < 1f && Gd.Randf() > rule.Chance) return;

        for (int i = 0; i < rule.Count; i++) AetherCoreSpawner.SpawnAt(position);
    }

    /// <summary>Oublie les règles chargées — réservé aux bancs.</summary>
    public static void Reset() => _rules = null;
}
