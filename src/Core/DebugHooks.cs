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

    private static bool? _forceElites;

    /// <summary>
    /// Vrai si lancé avec <c>--force-elites</c> : chaque ennemi basique devient élite (affixe tiré
    /// au hasard), sans attendre la montée de fréquence. Sert au game-tester à valider les 5 affixes
    /// (rendu + comportement) en une session, et au tuning. Aucun effet en build normal.
    /// </summary>
    public static bool ForceElites
    {
        get
        {
            if (_forceElites == null)
                _forceElites = HasFlag("--force-elites");
            return _forceElites.Value;
        }
    }

    private static bool _forcedFusionRead;
    private static string? _forcedFusion;

    /// <summary>
    /// Id de fusion à équiper d'office via <c>--force-fusion=&lt;id&gt;</c> (ex.
    /// <c>--force-fusion=fusion_charge_blindee</c>) ou <c>--force-fusion=all</c> pour les deux, ou null.
    /// Sert au game-tester à valider le ressenti/l'équilibrage des fusions sans grinder les jauges.
    /// Aucun effet en build normal. Voir GameManager.ApplyFusionDebugHook.
    /// </summary>
    public static string? ForcedFusion
    {
        get
        {
            if (!_forcedFusionRead)
            {
                _forcedFusion     = ValueFlag("--force-fusion=");
                _forcedFusionRead = true;
            }
            return _forcedFusion;
        }
    }

    private static bool _forcedGraftRead;
    private static string? _forcedGraft;

    /// <summary>
    /// Id de greffe à équiper d'office via <c>--force-graft=&lt;id&gt;</c> (ex.
    /// <c>--force-graft=grafted_carapace</c>) ou <c>--force-graft=all</c> pour les 5 greffes de base, ou
    /// null. Sert à valider visuellement les props de silhouette (Phase B) sans grinder les jauges.
    /// Aucun effet en build normal. Voir GameManager.ApplyGraftDebugHook.
    /// </summary>
    public static string? ForcedGraft
    {
        get
        {
            if (!_forcedGraftRead)
            {
                _forcedGraft     = ValueFlag("--force-graft=");
                _forcedGraftRead = true;
            }
            return _forcedGraft;
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
