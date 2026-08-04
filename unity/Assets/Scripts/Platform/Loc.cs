using System.IO;
using UnityEngine;

/// <summary>
/// Traduction des libellés — équivalent de <c>Loc.T("CLÉ")</c> côté Godot (Lot 6).
///
/// <para>Le fichier <c>ui.csv</c> est copié tel quel dans <c>StreamingAssets</c> : il reste éditable
/// sans recompiler, comme les données de tuning, et c'est le <b>même fichier</b> que celui utilisé
/// par le projet Godot. L'analyse vit dans <see cref="LocTable"/> (pure et testée).</para>
/// </summary>
public static class Loc
{
    private static LocTable.Document? _doc;

    /// <summary>
    /// Langue courante. <b>Poussée par les réglages</b>, jamais tirée depuis eux : cette classe vit
    /// dans la couche Platform, qui ne peut pas dépendre du Gameplay sans créer un cycle entre
    /// assemblies.
    /// </summary>
    public static string Language { get; set; } = "fr";

    private static LocTable.Document Doc
    {
        get
        {
            if (_doc != null) return _doc;

            string path = Path.Combine(Application.streamingAssetsPath, "localization", "ui.csv");

            if (!File.Exists(path))
            {
                // Sans traduction, l'interface affiche ses clés : illisible, mais explicite — et bien
                // préférable à des libellés vides qu'on prendrait pour un défaut d'affichage.
                Debug.LogError($"[Loc] table de traduction introuvable : {path}");
                _doc = LocTable.Parse(null);
                return _doc;
            }

            _doc = LocTable.Parse(File.ReadAllText(path));
            Debug.Log($"[Loc] {_doc.Count} libelles charges ({Language}).");
            return _doc;
        }
    }

    /// <summary>Texte traduit, ou la clé elle-même si elle manque.</summary>
    public static string T(string key) => Doc.Get(key, Language);

    /// <summary>Texte traduit avec paramètres de format.</summary>
    public static string T(string key, params object[] args)
    {
        string pattern = T(key);
        try
        {
            return string.Format(pattern, args);
        }
        catch (System.FormatException)
        {
            // Un motif mal formé dans une seule langue ne doit pas faire tomber l'écran entier.
            return pattern;
        }
    }

    /// <summary>Oublie la table — à appeler si la langue change en cours de session.</summary>
    public static void Reset() => _doc = null;
}
