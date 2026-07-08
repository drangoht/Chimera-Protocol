using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran « Perks » — décrit tous les perks de départ (débloqués via les Défis, équipés au Hub). Comme
/// ChallengesScreen : aucune entrée masquée (la description reste visible pour donner un but), statut
/// débloqué (or) / verrouillé (gris) encodé dans l'accent + le tag. Réutilise CodexScreenBase.
/// Entrées dérivées de StartingPerks.All (source unique). Cf. docs/DESIGN_CHALLENGES.md (lot 4 / menu).
/// </summary>
public partial class PerksScreen : CodexScreenBase
{
    private static readonly Color Gold = new(1f, 0.8f, 0.27f);
    private static readonly Color Dim  = new(0.55f, 0.57f, 0.66f);

    protected override string ScreenTitle => TFallback("PERKS_TITLE", "PERKS DE DÉPART");
    protected override Color  TitleAccent => new(0.667f, 0.267f, 1f);

    protected override string? IntroText => TFallback("PERKS_INTRO",
        "Les perks de départ sont des bonus de début de run, débloqués en accomplissant des Défis et "
        + "équipés au Hub (un seul à la fois). Chaque nouvelle run applique le perk équipé.");

    private List<CodexEntry>? _entries;
    protected override IReadOnlyList<CodexEntry> Entries => _entries ??= Build();

    private static List<CodexEntry> Build()
    {
        var list = new List<CodexEntry>();
        var meta = MetaProgressionSystem.Instance;
        foreach (var p in StartingPerks.All)
        {
            bool unlocked = meta?.Meta.UnlockedPerks.Contains(p.Id) ?? false;
            string statusKey = unlocked ? "CHAL_STATUS_DONE" : "PERK_STATUS_LOCKED";
            Color accent = unlocked ? Gold : Dim;
            list.Add(new CodexEntry(p.Id, p.NameKey, statusKey, p.DescKey, p.IconPath, accent));
        }
        return list;
    }

    private static string TFallback(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
    }
}
