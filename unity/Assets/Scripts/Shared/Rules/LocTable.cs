using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Lecture de <c>localization/ui.csv</c> — logique pure et testable (Lot 6).
///
/// <para>Le CSV reste la <b>source de vérité</b> des trois langues : seul le lecteur change de moteur.
/// Il porte des virgules dans les textes et des guillemets d'échappement, donc un
/// <c>Split(',')</c> naïf couperait des phrases au milieu — et le symptôme serait une interface
/// tronquée dans une seule langue, la plus difficile à remarquer.</para>
/// </summary>
public static class LocTable
{
    /// <summary>Langues gérées, dans l'ordre des colonnes du fichier.</summary>
    public static readonly string[] Languages = { "en", "fr", "es" };

    /// <summary>Table analysée : clé → textes par langue.</summary>
    public sealed class Document
    {
        private readonly Dictionary<string, string[]> _rows = new(StringComparer.Ordinal);

        /// <summary>Nombre de clés lues.</summary>
        public int Count => _rows.Count;

        /// <summary>Clés lues — permet de vérifier la table entière plutôt qu'une entrée choisie.</summary>
        public IEnumerable<string> Keys => _rows.Keys;

        internal void Add(string key, string[] values) => _rows[key] = values;

        /// <summary>
        /// Texte d'une clé dans une langue. <b>Renvoie la clé elle-même si elle est absente</b> :
        /// un libellé manquant doit se voir à l'écran et se chercher, jamais laisser un blanc.
        /// </summary>
        public string Get(string key, string language)
        {
            if (!_rows.TryGetValue(key, out var values)) return key;

            int column = Array.IndexOf(Languages, language);
            if (column < 0) column = 0;

            return column < values.Length && values[column].Length > 0 ? values[column] : key;
        }

        /// <summary>La clé existe-t-elle ?</summary>
        public bool Has(string key) => _rows.ContainsKey(key);
    }

    /// <summary>Analyse le contenu du CSV. La première ligne est l'en-tête et n'est pas une clé.</summary>
    public static Document Parse(string? csv)
    {
        var doc = new Document();
        if (string.IsNullOrWhiteSpace(csv)) return doc;

        bool header = true;

        foreach (string line in csv.Split('\n'))
        {
            string row = line.TrimEnd('\r');
            if (row.Length == 0) continue;

            if (header) { header = false; continue; }

            var fields = SplitCsvLine(row);
            if (fields.Count < 2) continue;

            string key = fields[0].Trim();
            if (key.Length == 0) continue;

            var values = new string[fields.Count - 1];
            for (int i = 1; i < fields.Count; i++) values[i - 1] = Unescape(fields[i]);

            doc.Add(key, values);
        }

        return doc;
    }

    /// <summary>
    /// Rend leur sens aux séquences échappées du CSV — aujourd'hui le seul <c>\n</c>.
    ///
    /// <para>⚠ L'importeur de traductions de Godot fait cette conversion pour nous ; le portage lit
    /// le CSV <b>brut</b> et ne la faisait pas. Symptôme : les deux barres apparaissaient
    /// <b>littéralement</b> au milieu des phrases — « te traque.\nTu lui arracheras » —, sur toutes
    /// les lignes de la cinématique d'ouverture, c'est-à-dire sur le seul texte narratif du jeu.
    /// Aucune erreur, aucun test rouge : juste une faute de frappe apparente dans trois langues.</para>
    /// </summary>
    public static string Unescape(string value)
        => value.IndexOf('\\') < 0 ? value : value.Replace("\\n", "\n");

    /// <summary>
    /// Découpe une ligne CSV en respectant les guillemets : <c>a,"b,c",d</c> donne trois champs.
    /// Un guillemet doublé à l'intérieur d'un champ cité vaut un guillemet littéral.
    /// </summary>
    public static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (quoted)
            {
                if (c != '"') { current.Append(c); continue; }

                if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = false;
                continue;
            }

            if (c == '"') { quoted = true; continue; }
            if (c == ',') { fields.Add(current.ToString()); current.Clear(); continue; }

            current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
