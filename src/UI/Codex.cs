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

    // Accents supplémentaires pour la faune par biome (Fournaise/Givre/Néon, cf. docs/GDD.md §21).
    private static readonly Color Ember  = new(1f,     0.42f,  0.1f);   // Fournaise
    private static readonly Color IceB   = new(0.6f,   0.85f,  1f);     // Givre
    private static readonly Color Magenta= new(1f,     0.25f,  0.8f);   // Néon

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

        // ── Faune par biome (20 ennemis basiques, cf. docs/GDD.md §21) ────────────
        // Matrice 5 biomes x 4 archétypes (fourrage/harceleur/pression_distance/bruiser),
        // réutilisent les scènes archétype existantes (RustSwarm/CorruptedDrone/CorruptedSentinel/
        // GraftedColossus) avec un sprite dédié chargé au runtime (EnemyBase.SetSpriteFrames).

        // Sanctuaire
        new("sanctuary_marked_walker", "ENEMY_SANCTUARY_MARKED_WALKER_NAME", "ENEMY_SANCTUARY_MARKED_WALKER_TAG", "ENEMY_SANCTUARY_MARKED_WALKER_DESC",
            EnemyDir + "sanctuary_marked_walker/sanctuary_marked_walker_idle_01.png", RustR,
            EnemyDir + "sanctuary_marked_walker/sanctuary_marked_walker_frames.tres"),

        new("sanctuary_scout_drone", "ENEMY_SANCTUARY_SCOUT_DRONE_NAME", "ENEMY_SANCTUARY_SCOUT_DRONE_TAG", "ENEMY_SANCTUARY_SCOUT_DRONE_DESC",
            EnemyDir + "sanctuary_scout_drone/sanctuary_scout_drone_idle_01.png", Cyan,
            EnemyDir + "sanctuary_scout_drone/sanctuary_scout_drone_frames.tres"),

        new("sanctuary_walker_turret", "ENEMY_SANCTUARY_WALKER_TURRET_NAME", "ENEMY_SANCTUARY_WALKER_TURRET_TAG", "ENEMY_SANCTUARY_WALKER_TURRET_DESC",
            EnemyDir + "sanctuary_walker_turret/sanctuary_walker_turret_idle_01.png", Orange,
            EnemyDir + "sanctuary_walker_turret/sanctuary_walker_turret_frames.tres"),

        new("sanctuary_maintenance_golem", "ENEMY_SANCTUARY_MAINTENANCE_GOLEM_NAME", "ENEMY_SANCTUARY_MAINTENANCE_GOLEM_TAG", "ENEMY_SANCTUARY_MAINTENANCE_GOLEM_DESC",
            EnemyDir + "sanctuary_maintenance_golem/sanctuary_maintenance_golem_idle_01.png", RustR,
            EnemyDir + "sanctuary_maintenance_golem/sanctuary_maintenance_golem_frames.tres"),

        // Aether
        new("aether_shard", "ENEMY_AETHER_SHARD_NAME", "ENEMY_AETHER_SHARD_TAG", "ENEMY_AETHER_SHARD_DESC",
            EnemyDir + "aether_shard/aether_shard_idle_01.png", Violet,
            EnemyDir + "aether_shard/aether_shard_frames.tres"),

        new("aether_drifting_wraith", "ENEMY_AETHER_DRIFTING_WRAITH_NAME", "ENEMY_AETHER_DRIFTING_WRAITH_TAG", "ENEMY_AETHER_DRIFTING_WRAITH_DESC",
            EnemyDir + "aether_drifting_wraith/aether_drifting_wraith_idle_01.png", Cyan,
            EnemyDir + "aether_drifting_wraith/aether_drifting_wraith_frames.tres"),

        new("aether_spectral_watcher", "ENEMY_AETHER_SPECTRAL_WATCHER_NAME", "ENEMY_AETHER_SPECTRAL_WATCHER_TAG", "ENEMY_AETHER_SPECTRAL_WATCHER_DESC",
            EnemyDir + "aether_spectral_watcher/aether_spectral_watcher_idle_01.png", Violet,
            EnemyDir + "aether_spectral_watcher/aether_spectral_watcher_frames.tres"),

        new("aether_golem", "ENEMY_AETHER_GOLEM_NAME", "ENEMY_AETHER_GOLEM_TAG", "ENEMY_AETHER_GOLEM_DESC",
            EnemyDir + "aether_golem/aether_golem_idle_01.png", Cyan,
            EnemyDir + "aether_golem/aether_golem_frames.tres"),

        // Fournaise
        new("cinder_crawler", "ENEMY_CINDER_CRAWLER_NAME", "ENEMY_CINDER_CRAWLER_TAG", "ENEMY_CINDER_CRAWLER_DESC",
            EnemyDir + "cinder_crawler/cinder_crawler_idle_01.png", Ember,
            EnemyDir + "cinder_crawler/cinder_crawler_frames.tres"),

        new("volatile_spark", "ENEMY_VOLATILE_SPARK_NAME", "ENEMY_VOLATILE_SPARK_TAG", "ENEMY_VOLATILE_SPARK_DESC",
            EnemyDir + "volatile_spark/volatile_spark_idle_01.png", Gold,
            EnemyDir + "volatile_spark/volatile_spark_frames.tres"),

        new("lava_spitter", "ENEMY_LAVA_SPITTER_NAME", "ENEMY_LAVA_SPITTER_TAG", "ENEMY_LAVA_SPITTER_DESC",
            EnemyDir + "lava_spitter/lava_spitter_idle_01.png", Ember,
            EnemyDir + "lava_spitter/lava_spitter_frames.tres"),

        new("magma_colossus", "ENEMY_MAGMA_COLOSSUS_NAME", "ENEMY_MAGMA_COLOSSUS_TAG", "ENEMY_MAGMA_COLOSSUS_DESC",
            EnemyDir + "magma_colossus/magma_colossus_idle_01.png", Ember,
            EnemyDir + "magma_colossus/magma_colossus_frames.tres"),

        // Givre
        new("frost_crawler", "ENEMY_FROST_CRAWLER_NAME", "ENEMY_FROST_CRAWLER_TAG", "ENEMY_FROST_CRAWLER_DESC",
            EnemyDir + "frost_crawler/frost_crawler_idle_01.png", IceB,
            EnemyDir + "frost_crawler/frost_crawler_frames.tres"),

        new("wandering_ice_shard", "ENEMY_WANDERING_ICE_SHARD_NAME", "ENEMY_WANDERING_ICE_SHARD_TAG", "ENEMY_WANDERING_ICE_SHARD_DESC",
            EnemyDir + "wandering_ice_shard/wandering_ice_shard_idle_01.png", IceB,
            EnemyDir + "wandering_ice_shard/wandering_ice_shard_frames.tres"),

        new("cryo_marksman", "ENEMY_CRYO_MARKSMAN_NAME", "ENEMY_CRYO_MARKSMAN_TAG", "ENEMY_CRYO_MARKSMAN_DESC",
            EnemyDir + "cryo_marksman/cryo_marksman_idle_01.png", Cyan,
            EnemyDir + "cryo_marksman/cryo_marksman_frames.tres"),

        new("ice_titan", "ENEMY_ICE_TITAN_NAME", "ENEMY_ICE_TITAN_TAG", "ENEMY_ICE_TITAN_DESC",
            EnemyDir + "ice_titan/ice_titan_idle_01.png", IceB,
            EnemyDir + "ice_titan/ice_titan_frames.tres"),

        // Néon
        new("neon_security_drone", "ENEMY_NEON_SECURITY_DRONE_NAME", "ENEMY_NEON_SECURITY_DRONE_TAG", "ENEMY_NEON_SECURITY_DRONE_DESC",
            EnemyDir + "neon_security_drone/neon_security_drone_idle_01.png", Magenta,
            EnemyDir + "neon_security_drone/neon_security_drone_frames.tres"),

        new("holographic_glitch", "ENEMY_HOLOGRAPHIC_GLITCH_NAME", "ENEMY_HOLOGRAPHIC_GLITCH_TAG", "ENEMY_HOLOGRAPHIC_GLITCH_DESC",
            EnemyDir + "holographic_glitch/holographic_glitch_idle_01.png", Magenta,
            EnemyDir + "holographic_glitch/holographic_glitch_frames.tres"),

        new("neon_laser_turret", "ENEMY_NEON_LASER_TURRET_NAME", "ENEMY_NEON_LASER_TURRET_TAG", "ENEMY_NEON_LASER_TURRET_DESC",
            EnemyDir + "neon_laser_turret/neon_laser_turret_idle_01.png", Cyan,
            EnemyDir + "neon_laser_turret/neon_laser_turret_frames.tres"),

        new("synthetic_golem", "ENEMY_SYNTHETIC_GOLEM_NAME", "ENEMY_SYNTHETIC_GOLEM_TAG", "ENEMY_SYNTHETIC_GOLEM_DESC",
            EnemyDir + "synthetic_golem/synthetic_golem_idle_01.png", Violet,
            EnemyDir + "synthetic_golem/synthetic_golem_frames.tres"),
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

        new("vector_lance", "WPN_VECTOR_LANCE_NAME", "TAG_ACTIVE", "WPN_VECTOR_LANCE_DESC",
            IconDir + "ui_icon_vector_lance.png", Cyan),

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

        new("ionic_storm", "WPN_IONIC_STORM_NAME", "TAG_FUSION", "WPN_IONIC_STORM_DESC",
            IconDir + "ui_icon_ionic_storm.png", Gold),

        new("solar_column", "WPN_SOLAR_COLUMN_NAME", "TAG_FUSION", "WPN_SOLAR_COLUMN_DESC",
            IconDir + "ui_icon_solar_column.png", Gold),

        new("hornet_swarm", "WPN_HORNET_SWARM_NAME", "TAG_FUSION", "WPN_HORNET_SWARM_DESC",
            IconDir + "ui_icon_hornet_swarm.png", Gold),
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
        { "vector_lance",      IconDir + "ui_icon_vector_lance.png"   },
        { "singularity",       IconDir + "ui_icon_singularity.png"    },
        { "fusion_blade",      IconDir + "ui_icon_fusionblade.png"    },
        { "rail_overcharged",  IconDir + "ui_icon_rail.png"           },
        { "orbital_swarm",     IconDir + "ui_icon_orbital.png"        },
        { "overload_aegis",    IconDir + "ui_icon_aegis.png"          },
        { "ionic_storm",       IconDir + "ui_icon_ionic_storm.png"    },
        { "solar_column",      IconDir + "ui_icon_solar_column.png"   },
        { "hornet_swarm",      IconDir + "ui_icon_hornet_swarm.png"   },
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
