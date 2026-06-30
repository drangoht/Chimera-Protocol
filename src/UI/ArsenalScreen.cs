using Godot;
using System.Collections.Generic;

/// <summary>Arsenal — liste toutes les armes avec icône et description.</summary>
public partial class ArsenalScreen : CodexScreenBase
{
    protected override string ScreenTitle => Loc.T("MENU_ARSENAL");
    protected override Color  TitleAccent => new(0.267f, 1f, 0.933f);  // cyan

    // Armes (actives + fusions) puis passifs
    private static readonly List<CodexEntry> _all = BuildAll();
    protected override IReadOnlyList<CodexEntry> Entries => _all;

    private static List<CodexEntry> BuildAll()
    {
        var list = new List<CodexEntry>();
        list.AddRange(Codex.Weapons);
        list.AddRange(Codex.Passives);
        return list;
    }

    // Une ARME (active ou fusion) non découverte est masquée. Les passifs restent toujours visibles ;
    // les armes de signature sont toujours considérées découvertes (cf. GameSettings.IsDiscovered).
    protected override bool IsEntryLocked(CodexEntry e)
    {
        bool isWeapon = e.Tag is "TAG_ACTIVE" or "TAG_FUSION";
        return isWeapon && !(GameSettings.Instance?.IsDiscovered(e.Id) ?? true);
    }
}
