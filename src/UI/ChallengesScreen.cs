using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran « Défis » — liste tous les défis/succès (objectif + récompense + statut accompli/à faire) et
/// la progression globale X/N. Réutilise CodexScreenBase (fondu, retour, défilement clavier). Les
/// entrées sont dérivées de ChallengeSystem.Defs (source unique) ; le statut vient de la sauvegarde.
///
/// Contrairement au Bestiaire/Arsenal, AUCUNE entrée n'est masquée : l'objectif d'un défi non accompli
/// doit rester visible (c'est le but à viser). Le statut est encodé dans l'accent (or = accompli) et le
/// tag ; l'objectif + la récompense sont composés dans la description (déjà localisée, passée telle
/// quelle — Loc.T renvoie une phrase non-clé inchangée). Cf. docs/DESIGN_CHALLENGES.md (lot 2).
/// </summary>
public partial class ChallengesScreen : CodexScreenBase
{
    private static readonly Color Gold = new(1f, 0.8f, 0.27f);
    private static readonly Color Dim  = new(0.55f, 0.57f, 0.66f);

    private const string NoyauIcon = "res://assets/sprites/ui/ui_icon_noyau.png";
    private const string ExtraSlotIcon = "res://assets/sprites/ui/ui_icon_extra_slot.png";
    private const string TitleIcon = "res://assets/sprites/ui/ui_icon_title.png";
    private const string EchoIcon = "res://assets/sprites/ui/ui_icon_echo.png";

    protected override string ScreenTitle => TFallback("CHALLENGES_TITLE", "DÉFIS");
    protected override Color  TitleAccent => Gold;

    protected override string? IntroText
    {
        get
        {
            var cs = ChallengeSystem.Instance;
            int done  = cs?.UnlockedCount() ?? 0;
            int total = cs?.Defs.Count ?? 0;
            string intro = TFallback("CHALLENGES_INTRO",
                "Accomplis des objectifs en jeu pour gagner des Échos et débloquer des perks de départ "
                + "et des titres. Chaque défi se valide en fin de run.");
            return $"{intro}\n{Loc.T("CHALLENGES_PROGRESS", done, total)}";
        }
    }

    private List<CodexEntry>? _entries;
    protected override IReadOnlyList<CodexEntry> Entries => _entries ??= Build();

    private static List<CodexEntry> Build()
    {
        var list = new List<CodexEntry>();
        var cs = ChallengeSystem.Instance;
        if (cs == null) return list;

        foreach (var def in cs.Defs)
        {
            bool done = cs.IsUnlocked(def.Id);
            string statusKey = done ? "CHAL_STATUS_DONE" : "CHAL_STATUS_TODO";
            Color accent = done ? Gold : Dim;

            // Description composée (déjà localisée) : objectif  ·  récompense.
            string composed = $"{Loc.T(def.DescKey)}    ·    {RewardText(def)}";

            list.Add(new CodexEntry(def.Id, def.NameKey, statusKey, composed, RewardIcon(def), accent));
        }
        return list;
    }

    private static string RewardText(ChallengeTable.ChallengeDef def) => def.RewardType switch
    {
        ChallengeTable.RewardKind.Perk     => TFallback("CHAL_REWARD_PERK", "Récompense : perk de départ"),
        ChallengeTable.RewardKind.Cosmetic => TFallback("CHAL_REWARD_COSMETIC", "Récompense : titre"),
        _                                  => Loc.T("CHAL_REWARD_ECHOES", def.RewardEchoes),
    };

    /// <summary>Icône illustrant la récompense : perks connus pointent vers leur asset réel,
    /// titres cosmétiques vers l'icône dédiée, Échos vers l'icône de monnaie méta ; sinon repli
    /// sur l'icône de Noyau (toujours présente).</summary>
    private static string RewardIcon(ChallengeTable.ChallengeDef def)
    {
        switch (def.RewardType)
        {
            case ChallengeTable.RewardKind.Perk:
                switch (def.RewardId)
                {
                    case "start_graft_swarm":   return "res://assets/sprites/grafts/swarm_symbiote_icon.png";
                    case "start_weapon_glaive": return Codex.IconPath("glaive") ?? NoyauIcon;
                    case "start_extra_slot":    return ExtraSlotIcon;
                }
                return NoyauIcon;
            case ChallengeTable.RewardKind.Cosmetic:
                return TitleIcon;
            default: // RewardKind.Echoes
                return EchoIcon;
        }
    }

    /// <summary>Loc.T avec repli FR si la clé n'est pas encore traduite.</summary>
    private static string TFallback(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
    }
}
