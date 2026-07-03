using Godot;

/// <summary>
/// Détection des flags de debug en ligne de commande.
/// N'a AUCUN effet en build normal : tout est gardé derrière la présence explicite
/// d'un flag. Utilisé par GameManager.RegisterPlayer pour le hook --debug-boss
/// (loadout de test + spawn immédiat du boss final), demandé par le game-tester
/// pour mesurer le TTK du boss sans éditer data/enemies.json.
/// </summary>
public static class DebugHooks
{
    private static bool? _bossDebug;

    /// <summary>
    /// Vrai si le jeu a été lancé avec l'argument <c>--debug-boss</c>.
    /// Cherche dans les args « moteur » (GetCmdlineArgs) ET les args utilisateur
    /// passés après <c>--</c> (GetCmdlineUserArgs). Résultat mis en cache.
    /// </summary>
    public static bool BossDebug
    {
        get
        {
            if (_bossDebug == null)
                _bossDebug = HasFlag("--debug-boss");
            return _bossDebug.Value;
        }
    }

    private static bool? _fusionDebug;

    /// <summary>
    /// TEMPORAIRE (test visuel Lame à Fusion) : vrai si lancé avec <c>--debug-fusion</c>.
    /// Équipe les prérequis de fusion_blade et applique la fusion, ennemis ambiants actifs.
    /// À retirer après validation du VFX.
    /// </summary>
    public static bool FusionDebug
    {
        get
        {
            if (_fusionDebug == null)
                _fusionDebug = HasFlag("--debug-fusion");
            return _fusionDebug.Value;
        }
    }

    private static bool _forcedBiomeRead;
    private static string? _forcedBiome;

    /// <summary>
    /// Id de biome forcé via <c>--biome=&lt;id&gt;</c> (ex. <c>--biome=givre</c>), ou null.
    /// Utilisé par GroundRenderer pour les captures d'écran et le game-tester —
    /// permet de valider chaque biome sans passer par l'écran de sélection.
    /// </summary>
    public static string? ForcedBiome
    {
        get
        {
            if (!_forcedBiomeRead)
            {
                _forcedBiome     = ValueFlag("--biome=");
                _forcedBiomeRead = true;
            }
            return _forcedBiome;
        }
    }

    private static bool HasFlag(string flag)
    {
        foreach (var arg in OS.GetCmdlineArgs())
            if (arg == flag) return true;
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg == flag) return true;
        return false;
    }

    private static string? ValueFlag(string prefix)
    {
        foreach (var arg in OS.GetCmdlineArgs())
            if (arg.StartsWith(prefix)) return arg[prefix.Length..];
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith(prefix)) return arg[prefix.Length..];
        return null;
    }
}
