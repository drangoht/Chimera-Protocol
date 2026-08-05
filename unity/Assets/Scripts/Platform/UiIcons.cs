using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Icônes des armes, passifs et greffes — <b>source unique</b> de la correspondance identifiant →
/// pictogramme. Portage de la table <c>Codex.IconById</c> du jeu publié.
///
/// <para><b>Le défaut qu'elle corrige</b> : les 43 icônes existaient dans le dépôt depuis le début du
/// portage, sous <c>Art/sprites/ui/</c> — c'est-à-dire <b>hors de portée d'un
/// <c>Resources.Load</c></b> — et aucun écran ne les demandait. Cartes de montée de niveau, Codex et
/// arsenal du HUD n'affichaient donc que du texte. Sur l'écran de montée de niveau en particulier,
/// c'est coûteux : le jeu est en pause au milieu d'une nuée, le joueur doit arbitrer entre trois
/// options en quelques secondes, et une icône se reconnaît sans être lue.</para>
///
/// <para>Les échecs sont mis en cache comme les succès : une icône manquante serait sinon redemandée
/// au disque à chaque reconstruction de liste, et le Codex en reconstruit trente d'un coup.</para>
///
/// <para>Elle vit dans <c>Platform</c> parce que le HUD (assemblage <c>Gameplay</c>) et les écrans
/// (assemblage <c>UI</c>) en ont tous deux besoin, et que <c>UI</c> référence déjà <c>Gameplay</c>.
/// La placer côté interface obligerait le HUD à tenir sa propre table — deux tables qui divergent
/// sont pires qu'une seule imparfaite.</para>
/// </summary>
public static class UiIcons
{
    private const string Dir = "Ui/";

    private static readonly Dictionary<string, Sprite?> Cache = new();

    /// <summary>
    /// Fichier associé à un identifiant. Les <b>cartes de surcharge</b> réemploient volontairement
    /// l'icône du passif dont elles prolongent l'effet : le joueur qui les découvre en overtime n'a
    /// pas à apprendre trois nouveaux pictogrammes pour comprendre ce qu'elles font.
    /// </summary>
    private static readonly Dictionary<string, string> FileById = new()
    {
        // ─── Armes de base ────────────────────────────────────────────────────
        { "impulse_cannon",     "ui_icon_impulse_cannon" },
        { "plasma_blade",       "ui_icon_plasmablade"    },
        { "drone_swarm",        "ui_icon_droneswarm"     },
        { "overload_field",     "ui_icon_overloadfield"  },
        { "tesla_coil",         "ui_icon_tesla"          },
        { "scatter_volley",     "ui_icon_scatter"        },
        { "glaive",             "ui_icon_glaive"         },
        { "seeker_swarm",       "ui_icon_seeker"         },
        { "cryo_lance",         "ui_icon_cryo"           },
        { "pyre_stream",        "ui_icon_pyre"           },
        { "vector_lance",       "ui_icon_vector_lance"   },
        { "singularity",        "ui_icon_singularity"    },

        // ─── Fusions ──────────────────────────────────────────────────────────
        { "fusion_blade",       "ui_icon_fusionblade"    },
        { "rail_overcharged",   "ui_icon_rail"           },
        { "orbital_swarm",      "ui_icon_orbital"        },
        { "overload_aegis",     "ui_icon_aegis"          },
        { "ionic_storm",        "ui_icon_ionic_storm"    },
        { "solar_column",       "ui_icon_solar_column"   },
        { "hornet_swarm",       "ui_icon_hornet_swarm"   },
        { "vector_beam",        "ui_icon_vector_beam"    },
        { "frost_veil",         "ui_icon_frost_veil"     },

        // ─── Passifs ──────────────────────────────────────────────────────────
        { "thermal_core",       "ui_icon_thermal_core"   },
        { "reinforced_plating", "ui_icon_reinforced_plate" },
        { "servo_motors",       "ui_icon_servomotors"    },
        { "capacitor",          "ui_icon_capacitor"      },
        { "xp_bonus",           "ui_icon_noyau"          },

        // ─── Cartes de surcharge (fin de partie) ──────────────────────────────
        { "overload_plating",   "ui_icon_reinforced_plate" },
        { "overload_regen",     "ui_icon_noyau"          },
        { "overload_damage",    "ui_icon_thermal_core"   },

        // ─── Récompenses (défis, Hub) ─────────────────────────────────────────
        { "echo",               "ui_icon_echo"           },
        { "extra_slot",         "ui_icon_extra_slot"     },
        { "title",              "ui_icon_title"          },
        { "hp",                 "ui_icon_hp"             },
        { "nova",               "ui_icon_nova"           },

        // ─── Greffes et fusions de chimère ────────────────────────────────────
        { "aiming_eye",             "aiming_eye_icon"             },
        { "erratic_servos",         "erratic_servos_icon"         },
        { "grafted_carapace",       "grafted_carapace_icon"       },
        { "stalker_wave",           "stalker_wave_icon"           },
        { "swarm_symbiote",         "swarm_symbiote_icon"         },
        { "fusion_charge_blindee",  "fusion_charge_blindee_icon"  },
        { "fusion_nova_rodeur",     "fusion_nova_rodeur_icon"     },
        { "fusion_ruche_tourelles", "fusion_ruche_tourelles_icon" },
    };

    /// <summary>Icône d'un identifiant, ou <c>null</c> s'il n'en a pas.</summary>
    public static Sprite? For(string id)
    {
        if (Cache.TryGetValue(id, out var cached)) return cached;

        Sprite? sprite = null;
        if (FileById.TryGetValue(id, out string? file))
        {
            sprite = Resources.Load<Sprite>(Dir + file);
            if (sprite == null)
                Debug.LogWarning($"[UiIcons] icone introuvable : {Dir}{file} (id « {id} »).");
        }

        Cache[id] = sprite;
        return sprite;
    }

    /// <summary>Identifiants pourvus d'une icône — observable pour les vérifications de banc.</summary>
    public static IReadOnlyCollection<string> KnownIds => FileById.Keys;

    /// <summary>Oublie les icônes chargées — réservé aux bancs.</summary>
    public static void Reset() => Cache.Clear();
}
