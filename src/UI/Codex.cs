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

    // ── Bestiaire ────────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<CodexEntry> Enemies = new List<CodexEntry>
    {
        new("rust_swarm", "Essaim de Rouille", "Fourrage — dès 0:00",
            "Carcasse mécanique rongée par la Rouille Vivante. Fonce en ligne droite, sans ruse. "
            + "Faible isolément, mortelle en nombre : c'est la marée du Sanctuaire.",
            EnemyDir + "rustswarm/enemy_rustswarm_idle_01.png", RustR,
            EnemyDir + "rustswarm/rustswarm_frames.tres"),

        new("corrupted_drone", "Drone Corrompu", "Harceleur — dès 2:00",
            "Drone d'observation devenu fou. Trajectoire erratique (±45°), très rapide et fragile. "
            + "Difficile à toucher, négligeable si on l'ignore… jusqu'à ce qu'ils soient dix.",
            EnemyDir + "drone/enemy_drone_idle_01.png", Violet,
            EnemyDir + "drone/drone_frames.tres"),

        new("corrupted_sentinel", "Sentinelle Corrompue", "Tireur — dès 5:00",
            "Tourelle ambulante qui maintient ses distances et te canarde. Recule si tu approches. "
            + "Force à bouger en permanence et à gérer les projectiles.",
            EnemyDir + "sentinel/enemy_sentinel_idle_01.png", Orange,
            EnemyDir + "sentinel/sentinel_frames.tres"),

        new("aether_revenant", "Revenant d'Aether", "MINI-BOSS — mi-temps 7:00",
            "Spectre cyborg réanimé par un Noyau d'Aether instable. Te pourchasse vite et fond sur toi "
            + "par ruades fulgurantes. À sa mort, libère un butin d'arme.",
            EnemyDir + "aether_revenant/aether_revenant_idle_01.png", Violet,
            EnemyDir + "aether_revenant/aether_revenant_frames.tres"),

        new("grafted_colossus", "Colosse Greffé", "Bruiser — dès 9:00",
            "Titan d'acier et de chair greffée. Lent mais effroyablement résistant et dévastateur au contact. "
            + "À sa mort, relâche un Noyau d'Aether pur.",
            EnemyDir + "colossus/enemy_colossus_idle_01.png", RustR,
            EnemyDir + "colossus/colossus_frames.tres"),

        new("rust_stalker", "Rôdeur de Rouille", "MINI-BOSS — dès 12:00",
            "Araignée mécanique corrodée, blindée et tenace. Nécessite un vrai build offensif pour tomber. "
            + "Récompense : orbe d'XP or et choix d'arme.",
            EnemyDir + "rust_stalker/rust_stalker_idle_01.png", Gold,
            EnemyDir + "rust_stalker/rust_stalker_frames.tres"),

        new("rusted_core", "Le Noyau Rouillé", "BOSS DE FIN — dès 13:00",
            "Le cœur corrompu du Sanctuaire. Colosse-gardien au noyau en fusion : salves radiales, ondes de choc, "
            + "1600 PV. Le vaincre relâche 500 d'XP, 3 Noyaux d'Aether et un choix d'arme dans une explosion finale.",
            EnemyDir + "rusted_core/rusted_core_idle_01.png", Gold,
            EnemyDir + "rusted_core/rusted_core_frames.tres"),

        new("master_sentinel", "Sentinelle Maîtresse", "MINI-BOSS — dès 16:00",
            "Version d'élite de la Sentinelle. Tire en éventail et kite sans répit. "
            + "Récompense : gros orbe d'XP et choix d'arme.",
            EnemyDir + "master_sentinel/master_sentinel_idle_01.png", Cyan,
            EnemyDir + "master_sentinel/master_sentinel_frames.tres"),
    };

    // ── Passifs (affichés dans l'Arsenal sous les armes) ──────────────────────
    public static readonly IReadOnlyList<CodexEntry> Passives = new List<CodexEntry>
    {
        new("thermal_core", "Noyau Thermique", "Passif",
            "Augmente les dégâts de toutes les armes actives. Prérequis de la fusion Lame à Fusion.",
            IconDir + "ui_icon_thermal_core.png", Orange),

        new("reinforced_plating", "Plaque Renforcée", "Passif",
            "Augmente les PV max et réduit les dégâts reçus (jusqu'à -40%). Le pilier d'un build défensif.",
            IconDir + "ui_icon_reinforced_plate.png", Cyan),

        new("servo_motors", "Servo-Moteurs", "Passif",
            "Augmente la vitesse de déplacement (plafond 380). Plus de mobilité pour kiter les nuées.",
            IconDir + "ui_icon_servomotors.png", Cyan),

        new("capacitor", "Capaciteur", "Passif",
            "Réduit le cooldown de toutes les armes actives. Prérequis de la fusion Rail Surchargé.",
            IconDir + "ui_icon_capacitor.png", Violet),
    };

    // ── Arsenal (armes actives + fusions) ─────────────────────────────────────
    public static readonly IReadOnlyList<CodexEntry> Weapons = new List<CodexEntry>
    {
        new("impulse_cannon", "Canon à Impulsions", "Arme active",
            "Tir automatique sur l'ennemi le plus proche. Perfore dès le niveau 3, puis double projectile. "
            + "L'arme de départ fiable et polyvalente.",
            IconDir + "ui_icon_impulse_cannon.png", Cyan),

        new("plasma_blade", "Lame Plasma", "Arme active",
            "Arc de mêlée tranchant dans un cône devant le joueur. L'angle et la portée s'élargissent à chaque niveau. "
            + "Excellente contre les nuées au corps-à-corps.",
            IconDir + "ui_icon_plasmablade.png", Cyan),

        new("drone_swarm", "Essaim de Drones", "Arme active",
            "Drones en orbite qui infligent des dégâts de contact en continu. Jusqu'à 4 drones tournoyants. "
            + "Protection passive permanente autour de toi.",
            IconDir + "ui_icon_droneswarm.png", Cyan),

        new("overload_field", "Champ de Surcharge", "Arme active",
            "Pulse de zone centré sur le joueur : dégâts + repoussée (knockback). Le rayon grandit avec le niveau. "
            + "Crée de l'espace quand tu es submergé.",
            IconDir + "ui_icon_overloadfield.png", Violet),

        new("tesla_coil", "Bobine Tesla", "Arme active",
            "Éclair en chaîne qui rebondit d'ennemi en ennemi (jusqu'à 7 sauts). Foudre cyan éblouissante. "
            + "Ravage les groupes denses en un éclair.",
            IconDir + "ui_icon_tesla.png", Cyan),

        new("scatter_volley", "Volée Multiple", "Arme active",
            "Tir multi-cible : envoie plusieurs projectiles vers les ennemis les plus proches, un par cible. "
            + "2 projectiles au niveau 1, +1 à chaque niveau (jusqu'à 6). Idéale contre les groupes éparpillés.",
            IconDir + "ui_icon_scatter.png", Cyan),

        new("fusion_blade", "Lame à Fusion", "FUSION (épique)",
            "Évolution de la Lame Plasma + Noyau Thermique : l'arc devient un anneau de flammes d'Aether actif "
            + "en continu autour du joueur. Plus aucun cooldown.",
            IconDir + "ui_icon_fusionblade.png", Gold),

        new("rail_overcharged", "Rail Surchargé", "FUSION (épique)",
            "Évolution du Canon + Capaciteur : rafale automatique de 3 projectiles perforants qui traversent "
            + "toute la ligne d'ennemis.",
            IconDir + "ui_icon_rail.png", Gold),

        new("orbital_swarm", "Essaim Orbital", "FUSION (épique)",
            "Évolution de l'Essaim de Drones + Servo-Moteurs : 6 drones ultra-rapides en orbite large. "
            + "Un bouclier offensif qui broie tout au contact.",
            IconDir + "ui_icon_orbital.png", Gold),

        new("overload_aegis", "Égide de Surcharge", "FUSION (épique)",
            "Évolution du Champ de Surcharge + Plaque Renforcée : pulse massif à knockback brutal qui "
            + "régénère tes PV à chaque onde. La forteresse mobile.",
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
        foreach (var e in Weapons)  d[e.Id] = e.Name;
        foreach (var e in Passives) d[e.Id] = e.Name;
        d["xp_bonus"] = "Écho d'Aether";
        return d;
    }

    /// <summary>Nom canonique accentué d'une arme/passif/fusion, ou l'id si inconnu.</summary>
    public static string DisplayName(string id) => NameById.TryGetValue(id, out var n) ? n : id;

    /// <summary>Chemin de l'icône d'une arme/passif, ou null si inconnu.</summary>
    public static string? IconPath(string id) => IconById.TryGetValue(id, out var p) ? p : null;

    /// <summary>Charge l'icône d'une arme/passif (cache via le ResourceLoader Godot).</summary>
    public static Texture2D? LoadIcon(string id)
    {
        var path = IconPath(id);
        return path != null ? GD.Load<Texture2D>(path) : null;
    }
}
