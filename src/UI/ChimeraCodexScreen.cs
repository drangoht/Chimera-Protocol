using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran « Chimère » — explique le 3e axe de progression (Assimilation / greffes). Un court
/// paragraphe présente le principe (absorber les ennemis, jauges, emplacements, fusions), puis
/// liste toutes les greffes et fusions avec icône, rareté et description. Réutilise CodexScreenBase.
/// Les entrées sont dérivées de grafts.json via AssimilationSystem.Config (source unique).
/// </summary>
public partial class ChimeraCodexScreen : CodexScreenBase
{
    private static readonly Color Magenta = new(0.85f, 0.30f, 0.80f);

    protected override string ScreenTitle => TFallback("CHIMERA_CODEX_TITLE", "CHIMÈRE — ASSIMILATION");
    protected override Color  TitleAccent => Magenta;

    protected override string? IntroText => TFallback("CHIMERA_CODEX_INTRO",
        "Ne te contente pas de tuer les créatures de la Rouille : deviens-les. Chaque archétype "
        + "d'ennemi remplit une jauge d'assimilation ; une fois pleine, tu peux greffer une partie de "
        + "la créature à ton corps. Les greffes occupent des emplacements limités (remplaçables). Deux "
        + "greffes compatibles peuvent fusionner en une forme supérieure qui libère un emplacement.");

    private List<CodexEntry>? _entries;
    protected override IReadOnlyList<CodexEntry> Entries => _entries ??= Build();

    private static List<CodexEntry> Build()
    {
        var list = new List<CodexEntry>();
        var cfg = AssimilationSystem.Instance?.Config;
        if (cfg == null) return list;

        foreach (var g in cfg.Grafts)
            list.Add(ToEntry(g, RarityTag(g.Rarity)));
        foreach (var f in cfg.Fusions)
            list.Add(ToEntry(f, "ASSIM_FUSION_TAG"));
        return list;
    }

    private static CodexEntry ToEntry(GraftTable.GraftDef def, string tagKey)
    {
        string key = def.Id.ToUpperInvariant();
        // Les teintes de greffe dépassent parfois 1.0 (highlight) : on borne pour l'accent d'UI.
        var tint = new Color(
            Mathf.Clamp(def.Tint[0], 0f, 1f),
            Mathf.Clamp(def.Tint[1], 0f, 1f),
            Mathf.Clamp(def.Tint[2], 0f, 1f));
        return new CodexEntry(def.Id, $"GRAFT_{key}_NAME", tagKey, $"GRAFT_{key}_DESC",
                              def.HudIcon, tint);
    }

    private static string RarityTag(string rarity) => rarity switch
    {
        "rare" => "RARITY_RARE",
        "epic" => "RARITY_EPIC",
        _      => "RARITY_COMMON",
    };

    /// <summary>Loc.T avec repli FR si la clé n'est pas encore traduite (nouvelles clés d'écran).</summary>
    private static string TFallback(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
    }
}
