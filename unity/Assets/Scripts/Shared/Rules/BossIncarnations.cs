using System;

/// <summary>Mécanique signature d'une incarnation du boss de fin (cf. <see cref="BossIncarnations"/>).</summary>
public enum BossSignature
{
    /// <summary>Sanctuaire — éventail de projectiles resserré, dirigé vers le joueur.</summary>
    DirectedFan,
    /// <summary>Aether — translocation près du joueur, suivie d'une salve spiralée.</summary>
    Blink,
    /// <summary>Givre — nova cryogénique qui ralentit et laisse des plaques de givre.</summary>
    FrostNova,
    /// <summary>Fournaise — projectiles en cloche laissant des flaques de magma persistantes.</summary>
    MagmaPools,
    /// <summary>Néon — faisceaux laser balayant en rotation autour du boss.</summary>
    RotatingBeams,
}

/// <summary>Description d'une incarnation du Noyau Rouillé, résolue depuis le biome joué.</summary>
public readonly struct BossIncarnation
{
    /// <summary>Identifiant technique (clés de loc, sprites, Codex).</summary>
    public string Id { get; }
    /// <summary>Biome dans lequel cette incarnation apparaît.</summary>
    public string BiomeId { get; }
    /// <summary>Clé de localisation du nom affiché sur la barre de boss.</summary>
    public string NameKey { get; }
    /// <summary>Mécanique signature ajoutée au socle commun.</summary>
    public BossSignature Signature { get; }
    /// <summary>Période de base de la signature en phase I, en secondes (raccourcie par les phases).</summary>
    public float BaseIntervalSec { get; }
    /// <summary>Teinte multiplicative appliquée au sprite (composantes pouvant dépasser 1 = surbrillance).</summary>
    public float TintR { get; }
    public float TintG { get; }
    public float TintB { get; }
    /// <summary>Chemin des SpriteFrames dédiés ; vide = souche (sprite d'origine).</summary>
    public string FramesPath { get; }

    public BossIncarnation(string id, string biomeId, string nameKey, BossSignature signature,
                           float baseIntervalSec, float tintR, float tintG, float tintB, string framesPath)
    {
        Id = id; BiomeId = biomeId; NameKey = nameKey; Signature = signature;
        BaseIntervalSec = baseIntervalSec;
        TintR = tintR; TintG = tintG; TintB = tintB;
        FramesPath = framesPath;
    }
}

/// <summary>
/// Les cinq incarnations du boss de fin — logique pure, testable.
///
/// Le Noyau Rouillé reste l'unique condition de victoire des cinq niveaux (groupe `rusted_core`,
/// `onDeath.endsRunVictory`) : ce qui change d'un biome à l'autre est **ce qu'il a assimilé sur
/// place**. Chaque incarnation ajoute UNE mécanique signature au socle commun (salves radiales,
/// ondes de choc, contact lourd, phases) — un joueur qui a appris le boss du Sanctuaire n'est
/// jamais dépaysé, il a une chose de plus à gérer.
///
/// L'ordre suit <see cref="LevelThreat.Order"/> : la souche est un pattern d'apprentissage
/// lisible, les faisceaux rotatifs du Néon (dernier palier) imposent un déplacement constant.
///
/// Cf. docs/GDD.md §29.
/// </summary>
public static class BossIncarnations
{
    private const string FramesDir = "res://assets/sprites/enemies/rusted_core/";

    /// <summary>Table indexée dans l'ordre de déblocage des niveaux.</summary>
    public static readonly BossIncarnation[] All =
    {
        new("core_root",     "sanctuaire", "BOSS_CORE_ROOT_NAME",     BossSignature.DirectedFan,   4.0f,
            1.00f, 1.00f, 1.00f, ""),
        new("core_spectral", "aether",     "BOSS_CORE_SPECTRAL_NAME", BossSignature.Blink,         7.0f,
            0.72f, 0.55f, 1.25f, FramesDir + "rusted_core_spectral_frames.tres"),
        new("core_frost",    "givre",      "BOSS_CORE_FROST_NAME",    BossSignature.FrostNova,     6.0f,
            0.62f, 0.92f, 1.35f, FramesDir + "rusted_core_frost_frames.tres"),
        new("core_molten",   "fournaise",  "BOSS_CORE_MOLTEN_NAME",   BossSignature.MagmaPools,    5.0f,
            1.30f, 0.72f, 0.45f, FramesDir + "rusted_core_molten_frames.tres"),
        new("core_prism",    "neon",       "BOSS_CORE_PRISM_NAME",    BossSignature.RotatingBeams, 8.0f,
            1.15f, 0.60f, 1.30f, FramesDir + "rusted_core_prism_frames.tres"),
    };

    /// <summary>Incarnation de la souche (Sanctuaire) — repli pour tout biome inconnu.</summary>
    public static BossIncarnation Root => All[0];

    /// <summary>
    /// Incarnation jouée dans ce biome. Un biome inconnu, vide ou null retombe sur la souche :
    /// un test headless ou une scène lancée hors run doit toujours obtenir un boss jouable.
    /// </summary>
    public static BossIncarnation For(string? biomeId)
    {
        if (string.IsNullOrEmpty(biomeId)) return Root;
        foreach (var inc in All)
            if (string.Equals(inc.BiomeId, biomeId, StringComparison.OrdinalIgnoreCase))
                return inc;
        return Root;
    }

    /// <summary>Incarnation par identifiant technique (Codex, outils de debug). Repli sur la souche.</summary>
    public static BossIncarnation ById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return Root;
        foreach (var inc in All)
            if (string.Equals(inc.Id, id, StringComparison.OrdinalIgnoreCase))
                return inc;
        return Root;
    }
}
