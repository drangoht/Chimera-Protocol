using UnityEngine;

/// <summary>
/// Traduction des libellés — équivalent de <c>Loc.T("CLÉ")</c> côté Godot (Lot 6).
///
/// <para>Le fichier <c>ui.csv</c> est copié tel quel dans <c>StreamingAssets</c> : il reste éditable
/// sans recompiler, comme les données de tuning, et c'est le <b>même fichier</b> que celui utilisé
/// par le projet Godot. L'analyse vit dans <see cref="LocTable"/> (pure et testée), la lecture dans
/// <see cref="StreamingText"/> (qui seul sait que le web n'a pas de disque).</para>
/// </summary>
public static class Loc
{
    /// <summary>Chemin de la table, relatif à <c>StreamingAssets</c>.</summary>
    private const string CsvPath = "localization/ui.csv";

    private static LocTable.Document? _doc;

    /// <summary>
    /// Langue courante. <b>Poussée par les réglages</b>, jamais tirée depuis eux : cette classe vit
    /// dans la couche Platform, qui ne peut pas dépendre du Gameplay sans créer un cycle entre
    /// assemblies.
    /// </summary>
    /// <remarks>
    /// ⚠ La valeur initiale lit <c>--lang=</c> <b>ici</b>, à l'initialisation du champ, et pas
    /// seulement au chargement des réglages. Le drapeau y était bien appliqué — trop tard : les
    /// premiers écrans demandent leurs libellés avant que quoi que ce soit n'ait touché
    /// <c>GameSettings.Current</c>, et une capture lancée avec <c>--lang=en</c> sortait un menu
    /// entièrement français. Rien ne le signalait : le journal annonçait « 467 libellés chargés »,
    /// et il fallait regarder l'image pour le voir. C'est la même parade que
    /// <c>RunConfig.BiomeFromCommandLine</c>, pour la même raison — un drapeau lu depuis un
    /// <c>Start</c> arrive après ceux qui le consultent.
    /// </remarks>
    public static string Language { get; set; } = DebugHooks.Language ?? "fr";

    private static LocTable.Document Doc
    {
        get
        {
            if (_doc != null) return _doc;

            string? csv = StreamingText.Read(CsvPath);

            if (csv == null)
            {
                // Sans traduction, l'interface affiche ses clés : illisible, mais explicite — et bien
                // préférable à des libellés vides qu'on prendrait pour un défaut d'affichage.
                Debug.LogError($"[Loc] table de traduction introuvable : {CsvPath}");
                _doc = LocTable.Parse(null);
                return _doc;
            }

            _doc = LocTable.Parse(csv);
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
    /// <remarks>
    /// ⚠ Vide aussi les caches de texte <b>dérivés</b> de la table, et non la seule table : depuis
    /// que les noms d'armes viennent d'ici, <see cref="UiNames"/> garde une copie résolue dans la
    /// langue où on la lui a demandée. Recharger la table sans la vider laissait un HUD dans
    /// l'ancienne langue au milieu d'une interface traduite — le genre de défaut qu'on ne voit
    /// qu'en changeant de langue en cours de partie, donc jamais.
    /// C'est le point UNIQUE où cet oubli se fait : tout nouveau cache de texte se vide ici.
    /// </remarks>
    public static void Reset()
    {
        _doc = null;
        UiNames.Reset();
    }
}
