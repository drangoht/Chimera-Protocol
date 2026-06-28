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

    private static bool HasFlag(string flag)
    {
        foreach (var arg in OS.GetCmdlineArgs())
            if (arg == flag) return true;
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg == flag) return true;
        return false;
    }
}
