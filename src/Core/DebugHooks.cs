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

    private static bool? _invulnerable;

    /// <summary>
    /// Vrai si lancé avec <c>--invuln</c> : le joueur ne subit plus aucun dégât. Sert à observer un
    /// combat long jusqu'au bout — typiquement les trois phases du boss de fin (GDD §29), qu'un
    /// testeur automatisé n'atteint jamais parce qu'il meurt avant. Ne touche à rien d'autre : le
    /// DPS, le scaling et le TTK restent ceux d'une vraie run. Aucun effet en build normal.
    /// </summary>
    public static bool Invulnerable
    {
        get
        {
            if (_invulnerable == null)
                _invulnerable = HasFlag("--invuln");
            return _invulnerable.Value;
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

    private static bool? _forceBuff;

    /// <summary>
    /// Vrai si lancé avec <c>--force-buff</c> : équipe d'office une 2e arme et applique deux power-ups
    /// temporaires (Overclock + Berserk) de durée quasi-infinie dès le début de la run, pour valider
    /// visuellement la BuffBar HUD (position sous le loadout d'armes, absence de chevauchement) sans
    /// attendre les fenêtres de spawn naturelles. Aucun effet en build normal.
    /// </summary>
    public static bool ForceBuff
    {
        get
        {
            if (_forceBuff == null)
                _forceBuff = HasFlag("--force-buff");
            return _forceBuff.Value;
        }
    }

    private static bool _forcedLanguageRead;
    private static string? _forcedLanguage;

    /// <summary>
    /// Code de langue forcé via <c>--lang=&lt;code&gt;</c> (ex. <c>--lang=en</c>), ou null.
    /// Surcharge la langue de <c>user://settings.cfg</c> pour la session SANS l'y persister —
    /// indispensable pour capturer les rushes du trailer dans une autre langue que celle du poste
    /// (cf. <c>tools/record_trailer.py</c>) sans écraser la préférence du joueur.
    /// Voir GameSettings._Ready.
    /// </summary>
    public static string? ForcedLanguage
    {
        get
        {
            if (!_forcedLanguageRead)
            {
                _forcedLanguage     = ValueFlag("--lang=");
                _forcedLanguageRead = true;
            }
            return _forcedLanguage;
        }
    }

    private static bool? _trailerMode;

    /// <summary>
    /// Vrai si lancé avec <c>--trailer</c> : masque les surcouches de développement qui pollueraient
    /// une capture vidéo promotionnelle — tampon de version (<see cref="VersionStamp"/>) et invite
    /// « appuyer pour passer » de la cinématique d'intro. N'affecte QUE de l'affichage annexe :
    /// aucun impact sur le gameplay. Utilisé par <c>tools/record_trailer.py</c>.
    /// </summary>
    public static bool TrailerMode
    {
        get
        {
            if (_trailerMode == null)
                _trailerMode = HasFlag("--trailer");
            return _trailerMode.Value;
        }
    }

    private static bool? _autoPlay;

    /// <summary>
    /// Vrai si lancé avec <c>--auto-play</c> : les écrans modaux qui attendent un choix du joueur
    /// (level-up, assimilation) se résolvent **tout seuls** — carte tirée au hasard parmi celles
    /// proposées, greffe acceptée. Sans cela, une run headless se fige au premier level-up et il est
    /// impossible de mesurer quoi que ce soit sur une run complète : le build d'un joueur au boss
    /// (niveau atteint, armes, fusions, greffes) ne peut pas être simulé par un loadout figé.
    /// Aucun effet en build normal.
    /// </summary>
    public static bool AutoPlay
    {
        get
        {
            if (_autoPlay == null)
                _autoPlay = HasFlag("--auto-play");
            return _autoPlay.Value;
        }
    }

    private static bool _timeScaleRead;
    private static float _timeScale;

    /// <summary>
    /// Facteur d'accélération du temps via <c>--timescale=&lt;x&gt;</c> (ex. <c>--timescale=3</c>),
    /// 0 si absent. Permet de jouer les 13 minutes qui précèdent le boss en quelques minutes réelles
    /// sur le banc. **Au-delà de ~4, les projectiles commencent à traverser leurs cibles** (deltas
    /// trop grands) et toute mesure de DPS devient fausse.
    /// </summary>
    public static float TimeScale
    {
        get
        {
            if (!_timeScaleRead)
            {
                var raw = ValueFlag("--timescale=");
                _timeScale = raw != null && float.TryParse(raw,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
                _timeScaleRead = true;
            }
            return _timeScale;
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
