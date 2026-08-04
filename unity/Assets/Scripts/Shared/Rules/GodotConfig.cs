using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Lecture du format <c>ConfigFile</c> de Godot — logique pure et testable (Lot 6).
///
/// <para><b>Pourquoi ce parseur existe.</b> Les préférences des joueurs vivent dans
/// <c>user://settings.cfg</c>, au format propriétaire de Godot : sections entre crochets, valeurs
/// typées, et des <c>PackedStringArray("a", "b")</c> pour les listes. Unity n'a rien pour le lire.
/// Sans lui, une mise à jour ferait perdre <b>langue, difficulté, crans de saturation par biome,
/// complétions, records et arsenal découvert</b> — et cette perte est <b>irréversible pour le
/// joueur</b> (§9.3, risque R5).</para>
///
/// <para>Il n'est utilisé qu'<b>une fois</b>, à la migration : ensuite, la version Unity écrit son
/// propre fichier. Écrire du <c>ConfigFile</c> depuis Unity n'aurait servi qu'à maintenir un format
/// que plus personne ne lit.</para>
///
/// <para><b>Tolérant par construction.</b> Une clé inconnue, une valeur mal typée ou une section en
/// trop ne doivent jamais faire échouer la lecture : perdre <i>toute</i> la sauvegarde parce qu'une
/// ligne a changé serait exactement le défaut qu'on cherche à éviter.</para>
/// </summary>
public static class GodotConfig
{
    /// <summary>Fichier analysé : section → clé → valeur brute (telle qu'écrite).</summary>
    public sealed class Document
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new(StringComparer.Ordinal);

        /// <summary>Sections présentes.</summary>
        public IReadOnlyCollection<string> Sections => _sections.Keys;

        internal void Set(string section, string key, string raw)
        {
            if (!_sections.TryGetValue(section, out var map))
                _sections[section] = map = new Dictionary<string, string>(StringComparer.Ordinal);
            map[key] = raw;
        }

        /// <summary>Valeur brute, ou <c>null</c> si la clé est absente.</summary>
        public string? Raw(string section, string key)
            => _sections.TryGetValue(section, out var map) && map.TryGetValue(key, out string? v) ? v : null;

        /// <summary>Toutes les clés d'une section — utile aux tables ouvertes (records, complétions).</summary>
        public IReadOnlyCollection<string> Keys(string section)
            => _sections.TryGetValue(section, out var map) ? map.Keys : Array.Empty<string>();

        public string GetString(string section, string key, string fallback = "")
        {
            string? raw = Raw(section, key);
            return raw == null ? fallback : Unquote(raw);
        }

        public bool GetBool(string section, string key, bool fallback = false)
        {
            string? raw = Raw(section, key);
            if (raw == null) return fallback;
            return raw.Trim() switch { "true" => true, "false" => false, _ => fallback };
        }

        public int GetInt(string section, string key, int fallback = 0)
        {
            string? raw = Raw(section, key);
            return raw != null && int.TryParse(raw.Trim(), NumberStyles.Integer,
                                               CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        public float GetFloat(string section, string key, float fallback = 0f)
        {
            string? raw = Raw(section, key);
            return raw != null && float.TryParse(raw.Trim(), NumberStyles.Float,
                                                 CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        /// <summary>
        /// Contenu d'un <c>PackedStringArray("a", "b")</c>. Renvoie une liste vide si la clé est
        /// absente ou si la valeur n'est pas un tableau — jamais <c>null</c>.
        /// </summary>
        public IReadOnlyList<string> GetStringArray(string section, string key)
            => ParseStringArray(Raw(section, key));
    }

    /// <summary>Analyse le contenu d'un <c>settings.cfg</c>.</summary>
    public static Document Parse(string? text)
    {
        var doc = new Document();
        if (string.IsNullOrWhiteSpace(text)) return doc;

        string section = "";

        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '#') continue;

            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                section = trimmed.Substring(1, trimmed.Length - 2).Trim();
                continue;
            }

            int eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;

            doc.Set(section, trimmed.Substring(0, eq).Trim(), trimmed.Substring(eq + 1).Trim());
        }

        return doc;
    }

    /// <summary>
    /// Décompose une entrée de table <c>"clé:valeur"</c> — la forme retenue par la 1.25.0 pour les
    /// crans de saturation, les complétions et les records par biome.
    /// </summary>
    public static bool TrySplitPair(string entry, out string key, out int value)
    {
        key = "";
        value = 0;

        int sep = entry.LastIndexOf(':');
        if (sep <= 0 || sep == entry.Length - 1) return false;

        key = entry.Substring(0, sep);
        return int.TryParse(entry.Substring(sep + 1), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Table <c>biome → valeur</c> reconstruite depuis un <c>PackedStringArray</c>.</summary>
    public static Dictionary<string, int> ParsePairTable(IReadOnlyList<string> entries)
    {
        var table = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string entry in entries)
            if (TrySplitPair(entry, out string key, out int value)) table[key] = value;
        return table;
    }

    // ─── Détail du format ─────────────────────────────────────────────────────

    private static string Unquote(string raw)
    {
        string t = raw.Trim();
        return t.Length >= 2 && t[0] == '"' && t[^1] == '"' ? t.Substring(1, t.Length - 2) : t;
    }

    private static IReadOnlyList<string> ParseStringArray(string? raw)
    {
        var result = new List<string>();
        if (raw == null) return result;

        string t = raw.Trim();
        int open = t.IndexOf('(');
        int close = t.LastIndexOf(')');
        if (open < 0 || close <= open) return result;

        foreach (string part in t.Substring(open + 1, close - open - 1).Split(','))
        {
            string item = Unquote(part.Trim());
            if (item.Length > 0) result.Add(item);
        }

        return result;
    }
}
