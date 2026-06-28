using Godot;
using System.Collections.Generic;

/// <summary>Bestiaire — liste tous les ennemis avec image et description.</summary>
public partial class BestiaryScreen : CodexScreenBase
{
    protected override string ScreenTitle => Loc.T("MENU_BESTIARY");
    protected override Color  TitleAccent => new(0.85f, 0.35f, 0.25f);  // rouge-rouille
    protected override IReadOnlyList<CodexEntry> Entries => Codex.Enemies;
}
