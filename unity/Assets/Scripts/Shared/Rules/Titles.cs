using System.Collections.Generic;

/// <summary>Un titre cosmétique : un identifiant et sa clé de localisation.</summary>
public sealed class TitleDef
{
    public string Id { get; }
    public string NameKey { get; }

    public TitleDef(string id, string nameKey)
    {
        Id = id;
        NameKey = nameKey;
    }
}

/// <summary>
/// Registre des titres cosmétiques — portage de <c>Titles</c>.
///
/// <para>Flair purement esthétique, débloqué par les Défis et choisi au Hub (un seul à la fois),
/// affiché sous le logo du menu principal. <b>Aucun effet de jeu</b> : c'est le dernier maillon de
/// la boucle des défis, celui qui rend la récompense visible. Un titre qu'on gagne sans jamais le
/// voir n'est pas une récompense.</para>
///
/// <para>⚠ Les identifiants doivent correspondre aux récompenses <c>cosmetic</c> de
/// <c>data/challenges.json</c> — un titre débloqué par un défi qui ne figure pas ici serait
/// silencieusement introuvable.</para>
/// </summary>
public static class Titles
{
    public static readonly IReadOnlyList<TitleDef> All = new List<TitleDef>
    {
        new("title_chimera",      "TITLE_CHIMERA"),
        new("title_apex",         "TITLE_APEX"),
        new("title_exterminator", "TITLE_EXTERMINATOR"),
    };

    public static TitleDef? ById(string id)
    {
        foreach (var title in All)
            if (title.Id == id) return title;

        return null;
    }
}
