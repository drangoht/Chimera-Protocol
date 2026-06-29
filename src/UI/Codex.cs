using Godot;
using System.Collections.Generic;

/// <summary>Une entrée de codex : image + nom + description (ennemi ou arme).</summary>
public sealed class CodexEntry
{
    public string  Id          { get; }
    public string  Name        { get; }
    public string  Tag         { get; }   // ex. "Fourrage", "Boss", "Arme active"
    public string  Description { get; }
    public string  ImagePath   { get; }
    public Color   Accent      { get; }
    /// <summary>Si non nul, l'entrée s'anime (SpriteFrames, anim "idle") au lieu d'une image figée.</summary>
    public string? FramesPath  { get; }

    public CodexEntry(string id, string name, string tag, string description, string imagePath,
                      Color accent, string? framesPath = null)
    {
        Id = id; Name = name; Tag = tag; Description = description; ImagePath = imagePath;
        Accent = accent; FramesPath = framesPath;
    }
}

/// <summary>
/// Données statiques du codex : bestiaire (ennemis) et arsenal (armes), avec descriptions
/// orientées joueur. Sert aussi de table de correspondance id → icône d'arme, réutilisée
/// partout où une arme est citée (cartes de level-up, arsenal).
/// </summary>
public static class Codex
{
    private const string EnemyDir = "res://assets/sprites/enemies/";
    private const string IconDir  = "res://assets/sprites/ui/";

    private static readonly Color Cyan   = new(0.267f, 1f,    0.933f);
    private static readonly Color Violet = new(0.667f, 0.267f, 1f);
    private static readonly Color Gold   = new(1f,     0.8f,   0.267f);
    private static readonly Color Orange = new(1f,     0.55f,  0.2f);
    private static readonly Color RustR  = new(0.85f,  0.35f,  0.25f);

    // Les champs Name/Tag/Description contiennent des CLÉS de traduction (voir localization/ui.csv) ;
    // les consommateurs (DisplayName, CodexScreenBase) les passent par Loc.T.

    // ── Bestiaire ────────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<CodexEntry> Enemies = new List<CodexEntry>
    {
        new("rust_swarm", "ENEMY_RUST_SWARM_NAME", "ENEMY_RUST_SWARM_TAG", "ENEMY_RUST_SWARM_DESC",
            EnemyDir + "rustswarm/enemy_rustswarm_idle_01.png", RustR,
            EnemyDir + "rustswarm/rustswarm_frames.tres"),

        new("corrupted_drone", "ENEMY_CORRUPTED_DRONE_NAME", "ENEMY_CORRUPTED_DRONE_TAG", "ENEMY_CORRUPTED_DRONE_DESC",
            EnemyDir + "drone/enemy_drone_idle_01.png", Violet,
            EnemyDir + "drone/drone_frames.tres"),

        new("corrupted_sentinel", "ENEMY_CORRUPTED_SENTINEL_NAME", "ENEMY_CORRUPTED_SENTINEL_TAG", "ENEMY_CORRUPTED_SENTINEL_DESC",
            EnemyDir + "sentinel/enemy_sentinel_idle_01.png", Orange,
            EnemyDir + "sentinel/sentinel_frames.tres"),

        new("aether_revenant", "ENEMY_AETHER_REVENANT_NAME", "ENEMY_AETHER_REVENANT_TAG", "ENEMY_AETHER_REVENANT_DESC",
            EnemyDir + "aether_revenant/aether_revenant_idle_01.png", Violet,
            EnemyDir + "aether_revenant/aether_revenant_frames.tres"),

        new("grafted_colossus", "ENEMY_GRAFTED_COLOSSUS_NAME", "ENEMY_GRAFTED_COLOSSUS_TAG", "ENEMY_GRAFTED_COLOSSUS_DESC",
            EnemyDir + "colossus/enemy_colossus_idle_01.png", RustR,
            EnemyDir + "colossus/colossus_frames.tres"),

        new("rust_stalker", "ENEMY_RUST_STALKER_NAME", "ENEMY_RUST_STALKER_TAG", "ENEMY_RUST_STALKER_DESC",
            EnemyDir + "rust_stalker/rust_stalker_idle_01.png", Gold,
            EnemyDir + "rust_stalker/rust_stalker_frames.tres"),

        new("rusted_core", "ENEMY_RUSTED_CORE_NAME", "ENEMY_RUSTED_CORE_TAG", "ENEMY_RUSTED_CORE_DESC",
            EnemyDir + "rusted_core/rusted_core_idle_01.png", Gold,
            EnemyDir + "rusted_core/rusted_core_frames.tres"),

        new("master_sentinel", "ENEMY_MASTER_SENTINEL_NAME", "ENEMY_MASTER_SENTINEL_TAG", "ENEMY_MASTER_SENTINEL_DESC",
            EnemyDir + "master_sentinel/master_sentinel_idle_01.png", Cyan,
            EnemyDir + "master_sentinel/master_sentinel_frames.tres"),
    };

    // ── Passifs (affichés dans l'Arsenal sous les armes) ──────────────────────
    public static readonly IReadOnlyList<CodexEntry> Passives = new List<CodexEntry>
    {
        new("thermal_core", "PAS_THERMAL_CORE_NAME", "TAG_PASSIVE", "PAS_THERMAL_CORE_DESC",
            IconDir + "ui_icon_thermal_core.png", Orange),

        new("reinforced_plating", "PAS_REINFORCED_PLATING_NAME", "TAG_PASSIVE", "PAS_REINFORCED_PLATING_DESC",
            IconDir + "ui_icon_reinforced_plate.png", Cyan),

        new("servo_motors", "PAS_SERVO_MOTORS_NAME", "TAG_PASSIVE", "PAS_SERVO_MOTORS_DESC",
            IconDir + "ui_icon_servomotors.png", Cyan),

        new("capacitor", "PAS_CAPACITOR_NAME", "TAG_PASSIVE", "PAS_CAPACITOR_DESC",
            IconDir + "ui_icon_capacitor.png", Violet),
    };

    // ── Arsenal (armes actives + fusions) ─────────────────────────────────────
    public static readonly IReadOnlyList<CodexEntry> Weapons = new List<CodexEntry>
    {
        new("impulse_cannon", "WPN_IMPULSE_CANNON_NAME", "TAG_ACTIVE", "WPN_IMPULSE_CANNON_DESC",
            IconDir + "ui_icon_impulse_cannon.png", Cyan),

        new("plasma_blade", "WPN_PLASMA_BLADE_NAME", "TAG_ACTIVE", "WPN_PLASMA_BLADE_DESC",
            IconDir + "ui_icon_plasmablade.png", Cyan),

        new("drone_swarm", "WPN_DRONE_SWARM_NAME", "TAG_ACTIVE", "WPN_DRONE_SWARM_DESC",
            IconDir + "ui_icon_droneswarm.png", Cyan),

        new("overload_field", "WPN_OVERLOAD_FIELD_NAME", "TAG_ACTIVE", "WPN_OVERLOAD_FIELD_DESC",
            IconDir + "ui_icon_overloadfield.png", Violet),

        new("tesla_coil", "WPN_TESLA_COIL_NAME", "TAG_ACTIVE", "WPN_TESLA_COIL_DESC",
            IconDir + "ui_icon_tesla.png", Cyan),

        new("scatter_volley", "WPN_SCATTER_VOLLEY_NAME", "TAG_ACTIVE", "WPN_SCATTER_VOLLEY_DESC",
            IconDir + "ui_icon_scatter.png", Cyan),

        new("glaive", "WPN_GLAIVE_NAME", "TAG_ACTIVE", "WPN_GLAIVE_DESC",
            IconDir + "ui_icon_glaive.png", Cyan),

        new("seeker_swarm", "WPN_SEEKER_SWARM_NAME", "TAG_ACTIVE", "WPN_SEEKER_SWARM_DESC",
            IconDir + "ui_icon_seeker.png", Violet),

        new("cryo_lance", "WPN_CRYO_LANCE_NAME", "TAG_ACTIVE", "WPN_CRYO_LANCE_DESC",
            IconDir + "ui_icon_cryo.png", Cyan),

        new("pyre_stream", "WPN_PYRE_STREAM_NAME", "TAG_ACTIVE", "WPN_PYRE_STREAM_DESC",
            IconDir + "ui_icon_pyre.png", Gold),

        new("singularity", "WPN_SINGULARITY_NAME", "TAG_ACTIVE", "WPN_SINGULARITY_DESC",
            IconDir + "ui_icon_singularity.png", Violet),

        new("fusion_blade", "WPN_FUSION_BLADE_NAME", "TAG_FUSION", "WPN_FUSION_BLADE_DESC",
            IconDir + "ui_icon_fusionblade.png", Gold),

        new("rail_overcharged", "WPN_RAIL_OVERCHARGED_NAME", "TAG_FUSION", "WPN_RAIL_OVERCHARGED_DESC",
            IconDir + "ui_icon_rail.png", Gold),

        new("orbital_swarm", "WPN_ORBITAL_SWARM_NAME", "TAG_FUSION", "WPN_ORBITAL_SWARM_DESC",
            IconDir + "ui_icon_orbital.png", Gold),

        new("overload_aegis", "WPN_OVERLOAD_AEGIS_NAME", "TAG_FUSION", "WPN_OVERLOAD_AEGIS_DESC",
            IconDir + "ui_icon_aegis.png", Gold),
    };

    // ── Correspondance id → icône (armes ET passifs) ──────────────────────────
    private static readonly Dictionary<string, string> IconById = new()
    {
        { "impulse_cannon",    IconDir + "ui_icon_impulse_cannon.png" },
        { "plasma_blade",      IconDir + "ui_icon_plasmablade.png"    },
        { "drone_swarm",       IconDir + "ui_icon_droneswarm.png"     },
        { "overload_field",    IconDir + "ui_icon_overloadfield.png"  },
        { "tesla_coil",        IconDir + "ui_icon_tesla.png"          },
        { "scatter_volley",    IconDir + "ui_icon_scatter.png"        },
        { "glaive",            IconDir + "ui_icon_glaive.png"         },
        { "seeker_swarm",      IconDir + "ui_icon_seeker.png"         },
        { "cryo_lance",        IconDir + "ui_icon_cryo.png"           },
        { "pyre_stream",       IconDir + "ui_icon_pyre.png"           },
        { "singularity",       IconDir + "ui_icon_singularity.png"    },
        { "fusion_blade",      IconDir + "ui_icon_fusionblade.png"    },
        { "rail_overcharged",  IconDir + "ui_icon_rail.png"           },
        { "orbital_swarm",     IconDir + "ui_icon_orbital.png"        },
        { "overload_aegis",    IconDir + "ui_icon_aegis.png"          },
        { "thermal_core",      IconDir + "ui_icon_thermal_core.png"   },
        { "reinforced_plating",IconDir + "ui_icon_reinforced_plate.png" },
        { "servo_motors",      IconDir + "ui_icon_servomotors.png"    },
        { "capacitor",         IconDir + "ui_icon_capacitor.png"      },
        { "xp_bonus",          IconDir + "ui_icon_noyau.png"          },
    };

    // ── Correspondance id → nom canonique (source unique d'affichage) ─────────
    // Construite à partir des entrées Weapons + Passives ci-dessus, plus l'entrée
    // spéciale xp_bonus. Tout le jeu (cartes de level-up, notifs HUD, arsenal)
    // doit citer une arme/passif via ce nom — évite les divergences d'orthographe.
    private static readonly Dictionary<string, string> NameById = BuildNameTable();

    private static Dictionary<string, string> BuildNameTable()
    {
        var d = new Dictionary<string, string>();
        foreach (var e in Weapons)  d[e.Id] = e.Name;   // valeurs = clés de traduction
        foreach (var e in Passives) d[e.Id] = e.Name;
        d["xp_bonus"] = "MISC_XP_BONUS_NAME";
        return d;
    }

    /// <summary>Nom traduit d'une arme/passif/fusion dans la langue courante, ou l'id si inconnu.</summary>
    public static string DisplayName(string id) => NameById.TryGetValue(id, out var key) ? Loc.T(key) : id;

    // Table id → clé de description (armes + passifs), pour les cartes de level-up.
    private static readonly Dictionary<string, string> DescById = BuildDescTable();

    private static Dictionary<string, string> BuildDescTable()
    {
        var d = new Dictionary<string, string>();
        foreach (var e in Weapons)  d[e.Id] = e.Description;
        foreach (var e in Passives) d[e.Id] = e.Description;
        return d;
    }

    /// <summary>Description traduite d'une arme/passif/fusion, ou chaîne vide si inconnu.</summary>
    public static string Description(string id) => DescById.TryGetValue(id, out var key) ? Loc.T(key) : "";

    /// <summary>Chemin de l'icône d'une arme/passif, ou null si inconnu.</summary>
    public static string? IconPath(string id) => IconById.TryGetValue(id, out var p) ? p : null;

    /// <summary>Charge l'icône d'une arme/passif (cache via le ResourceLoader Godot).</summary>
    public static Texture2D? LoadIcon(string id)
    {
        var path = IconPath(id);
        return path != null ? GD.Load<Texture2D>(path) : null;
    }
}
