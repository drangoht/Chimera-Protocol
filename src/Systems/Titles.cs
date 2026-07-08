using System.Collections.Generic;

/// <summary>
/// Registre des titres cosmétiques — flair purement esthétique débloqué via les Défis (ChallengeSystem)
/// et sélectionné au Hub (un seul à la fois, cf. MetaSaveData.EquippedCosmetic). Affiché sur le menu
/// principal. Aucun effet de gameplay. Les ids DOIVENT correspondre aux récompenses `cosmetic` de
/// data/challenges.json. Cf. docs/DESIGN_CHALLENGES.md (lot 4).
/// </summary>
public sealed class TitleDef
{
    public string Id      = "";
    public string NameKey = "";

    public TitleDef(string id, string nameKey) { Id = id; NameKey = nameKey; }
}

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
        foreach (var t in All)
            if (t.Id == id) return t;
        return null;
    }
}
