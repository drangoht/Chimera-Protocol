using System.Collections.Generic;

/// <summary>
/// Registre des perks de départ — bonus de début de run débloqués via les Défis (ChallengeSystem) et
/// équipés au Hub (un seul à la fois, cf. MetaSaveData.EquippedPerk). Leur EFFET est appliqué par
/// GameManager.ApplyStartingPerkHook au démarrage de la run ; ce registre ne porte que l'affichage
/// (nom/description/icône). Les ids DOIVENT correspondre aux récompenses `perk` de data/challenges.json.
/// Cf. docs/DESIGN_CHALLENGES.md (lot 3).
/// </summary>
public sealed class PerkDef
{
    public string Id      = "";
    public string NameKey = "";
    public string DescKey = "";
    public string IconPath = "";

    public PerkDef(string id, string nameKey, string descKey, string iconPath)
    {
        Id = id; NameKey = nameKey; DescKey = descKey; IconPath = iconPath;
    }
}

public static class StartingPerks
{
    public static readonly IReadOnlyList<PerkDef> All = new List<PerkDef>
    {
        new("start_graft_swarm", "PERK_GRAFT_SWARM_NAME", "PERK_GRAFT_SWARM_DESC",
            "res://assets/sprites/grafts/swarm_symbiote_icon.png"),
        new("start_weapon_glaive", "PERK_WEAPON_GLAIVE_NAME", "PERK_WEAPON_GLAIVE_DESC",
            "res://assets/sprites/ui/ui_icon_glaive.png"),
        new("start_extra_slot", "PERK_EXTRA_SLOT_NAME", "PERK_EXTRA_SLOT_DESC",
            "res://assets/sprites/ui/ui_icon_noyau.png"),
    };

    public static PerkDef? ById(string id)
    {
        foreach (var p in All)
            if (p.Id == id) return p;
        return null;
    }
}
