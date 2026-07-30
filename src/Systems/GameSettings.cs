using Godot;
using System.Collections.Generic;

/// <summary>
/// Réglages du jeu (audio, affichage, accessibilité), persistés dans
/// <c>user://settings.cfg</c>. Autoload : charge et applique au démarrage.
/// L'écran Options lit/écrit via les setters (qui appliquent + sauvegardent).
/// </summary>
public partial class GameSettings : Node
{
    public static GameSettings Instance { get; private set; } = null!;

    private const string Path = "user://settings.cfg";

    /// <summary>
    /// Réglage d'<b>assistance</b>. Depuis la 1.25.0 le challenge passe par la saturation
    /// (<see cref="SaturationTable"/>) : seuls <c>Facile</c> et <c>Normal</c> sont proposés.
    /// <c>Difficile</c> est conservé pour <b>relire les anciens <c>settings.cfg</c></b> — au chargement
    /// il est converti en <i>Normal + saturation 1</i>, aux multiplicateurs identiques.
    /// </summary>
    public enum GameDifficulty { Facile, Normal, Difficile }

    /// <summary>Mode de fenêtre : fenêtré / plein écran fenêtré (sans bordure) / plein écran.</summary>
    public enum WindowMode { Windowed, Borderless, Fullscreen }

    public float          Master       { get; private set; } = 1.0f;
    public float          Music        { get; private set; } = 0.8f;
    public float          Sfx          { get; private set; } = 0.9f;
    public GameDifficulty Difficulty   { get; private set; } = GameDifficulty.Normal;

    // ── Affichage ─────────────────────────────────────────────────────────────
    public WindowMode DisplayMode { get; private set; } = WindowMode.Windowed;

    /// <summary>Taille de la fenêtre en mode fenêtré (ignorée dans les deux autres modes).</summary>
    public Vector2I WindowSize { get; private set; } = new(1280, 720);

    /// <summary>Résolutions de fenêtre proposées par l'écran Options (mode fenêtré).</summary>
    public static readonly Vector2I[] Resolutions =
        { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440) };

    public bool VSync   { get; private set; } = true;

    /// <summary>Limite d'images par seconde ; 0 = illimitée (<see cref="Engine.MaxFps"/>).</summary>
    public int  MaxFps  { get; private set; } = 0;
    public static readonly int[] FpsLimits = { 0, 60, 120, 144, 240 };

    /// <summary>Compteur d'images/s affiché en surimpression (au-dessus du tampon de version).</summary>
    public bool ShowFps { get; private set; } = false;

    // ── Confort / accessibilité ───────────────────────────────────────────────
    /// <summary>Intensité des secousses de caméra (0 = désactivées, 1 = nominal).</summary>
    public float ShakeIntensity { get; private set; } = 1f;

    /// <summary>Compat : les secousses sont-elles actives ? (intensité non nulle)</summary>
    public bool ShakeEnabled => ShakeIntensity > 0.001f;

    /// <summary>Photosensibilité : atténue le flash de fusion et coupe l'aberration chromatique.</summary>
    public bool ReduceFlashes { get; private set; } = false;

    /// <summary>Intensité des vibrations manette (0 = coupées, 1 = nominal).</summary>
    public float Rumble { get; private set; } = 0.7f;

    // ── Interface ─────────────────────────────────────────────────────────────
    /// <summary>Affiche le tampon <c>v&lt;version&gt;-&lt;sha&gt;</c> en bas à droite.</summary>
    public bool ShowVersionStamp { get; private set; } = true;

    /// <summary>Publie le statut Discord Rich Presence (« joue à Chimera Protocol »).</summary>
    public bool DiscordEnabled { get; private set; } = true;

    /// <summary>Code de langue de l'UI : "en" (défaut), "fr", "es". Persisté.</summary>
    public string Language { get; private set; } = "en";
    public static readonly string[] Languages = { "en", "fr", "es" };

    // Langue réellement écrite dans settings.cfg. Diffère de Language quand la session tourne sous
    // --lang=<code> (capture du trailer) : la surcharge ne doit JAMAIS écraser la préférence du
    // joueur, même si un Save() est déclenché en cours de session (high score, complétion…).
    private string _persistedLanguage = "en";

    // ── Saturation (challenge de fin de partie, cf. docs/ENDGAME_PLAN.md) ──────
    /// <summary>Cran de saturation choisi pour la prochaine run (0 = aucun). Persisté.</summary>
    public int Saturation { get; private set; } = 0;

    // Cran réellement écrit dans settings.cfg. Diffère de Saturation quand la session tourne sous
    // --saturation=<n> (banc de mesure) : la surcharge ne doit JAMAIS écraser le choix du joueur, même
    // si un Save() est déclenché en cours de session (high score, complétion, découverte d'arme…).
    // Même parade que _persistedLanguage pour --lang, et même raison : ces Save() sont fréquents.
    private int _persistedSaturation = 0;

    /// <summary>
    /// Cran le plus élevé auquel un boss de fin a été battu, <b>tous biomes confondus</b> — c'est lui
    /// qui débloque le cran suivant. Le déblocage est global à dessein : par biome, cinq niveaux ×
    /// dix crans deviendrait une corvée (`docs/ENDGAME_PLAN.md` §7.3).
    /// </summary>
    public int HighestSaturationBeaten { get; private set; } = 0;

    /// <summary>Cran maximum sélectionnable en l'état de la progression.</summary>
    public int MaxSelectableSaturation => SaturationTable.MaxSelectable(HighestSaturationBeaten);

    /// <summary>true si le joueur joue en mode assistance (aucune saturation possible).</summary>
    public bool IsAssisted => Difficulty == GameDifficulty.Facile;

    // Multiplicateurs de menace lus par EnemySpawner (ennemis).
    //
    // La saturation ABSORBE l'ancien axe de difficulté : les deux ne se cumulent jamais. Sans cela,
    // quatre axes multiplicatifs se superposeraient en silence (assistance × saturation × palier de
    // niveau × overtime) et plus aucun diagnostic ne serait possible — le chantier du GDD §31 a mis
    // trois sessions jouées à isoler une cause pour cette raison précise.
    //
    // « Facile » reste hors de l'échelle : c'est de l'accessibilité (ennemis affaiblis), pas une
    // saturation négative, et la saturation y est forcée à 0.
    public float EnemyDamageMult => IsAssisted
        ? DifficultyTuning.EnemyDamage((int)GameDifficulty.Facile)
        : SaturationTable.EnemyDamageMult(Saturation);

    public float EnemyHpMult => IsAssisted
        ? DifficultyTuning.EnemyHp((int)GameDifficulty.Facile)
        : SaturationTable.EnemyHpMult(Saturation);

    public float SpawnMult => IsAssisted
        ? DifficultyTuning.Spawn((int)GameDifficulty.Facile)
        : SaturationTable.SpawnMult(Saturation);

    /// <summary>
    /// Multiplicateur des soins <b>reçus</b> (cran II « Hémorragie »). Vise le canal de soin dominant
    /// mesuré — 86,4 PV/s de soins ponctuels contre 8,2 de régénération.
    /// </summary>
    public float HealingMult => IsAssisted ? 1f : SaturationTable.HealingMult(Saturation);

    /// <summary>Multiplicateur de la durée de run avant overtime (cran III « Compte à rebours »).</summary>
    public float RunDurationMult => IsAssisted ? 1f : SaturationTable.RunDurationMult(Saturation);

    /// <summary>
    /// Les filets de survie de la méta (Noyau de Secours, Plaque Adaptative) sont-ils actifs ?
    /// Faux à partir du cran IV « Sans filet ».
    /// </summary>
    public bool SafetyNetsEnabled => IsAssisted || SaturationTable.SafetyNetsEnabled(Saturation);

    /// <summary>Multiplicateur de fréquence des élites (cran V « Élite ordinaire »).</summary>
    public float EliteFrequencyMult => IsAssisted ? 1f : SaturationTable.EliteFrequencyMult(Saturation);

    /// <summary>Multiplicateur d'Échos apporté par la saturation (branché dans <c>EchoFormula</c>).</summary>
    public double SaturationEchoMult => IsAssisted ? 1.0 : SaturationTable.EchoMult(Saturation);

    /// <summary>
    /// Multiplicateur d'Échos <b>total</b> d'une run : palier du niveau × saturation.
    ///
    /// <para>Source unique à dessein. <c>EchoFormula</c> applique ce facteur <b>composante par
    /// composante</b> et <c>RunEndScreen</c> refait le même calcul pour animer l'écran de fin : si les
    /// deux sites combinaient les facteurs chacun de leur côté, la somme affichée finirait par diverger
    /// du total crédité — exactement ce que le commentaire d'en-tête d'<c>EchoFormula</c> met en garde
    /// de ne pas faire.</para>
    /// </summary>
    public double TotalEchoMult(int threatTier) => LevelThreat.EchoMult(threatTier) * SaturationEchoMult;

    // Touches de déplacement personnalisées (move_up/down/left/right → keycode). Absente = défaut ZQSD.
    private readonly Dictionary<string, Key> _moveKeys = new();

    // Touche de dash personnalisée (greffe Servos Erratiques). Null = défaut (Maj).
    private Key? _dashKey;

    // Biomes vaincus (boss final battu), clés "biomeId:difficulté". Sert au badge de l'écran de sélection.
    private readonly HashSet<string> _completions = new();

    public override void _Ready()
    {
        Instance = this;
        Load();
        ApplyLanguageOverride();
        Apply();
    }

    // ── Complétion des biomes (badge sélection de niveau) ──────────────────────
    private static string CompletionKey(string biomeId, GameDifficulty d) => $"{biomeId}:{(int)d}";

    /// <summary>Marque un biome comme vaincu à la difficulté donnée (boss final battu) et persiste.</summary>
    public void RecordCompletion(string biomeId, GameDifficulty d)
    {
        if (biomeId.Length == 0) return;
        if (_completions.Add(CompletionKey(biomeId, d))) Save();
    }

    /// <summary>Le biome a-t-il été vaincu à cette difficulté précise ?</summary>
    public bool HasCompleted(string biomeId, GameDifficulty d) => _completions.Contains(CompletionKey(biomeId, d));

    /// <summary>Le biome a-t-il été vaincu à n'importe quelle difficulté ?</summary>
    public bool HasCompletedAny(string biomeId)
    {
        foreach (GameDifficulty d in System.Enum.GetValues<GameDifficulty>())
            if (HasCompleted(biomeId, d)) return true;
        return false;
    }

    // ── Déblocage progressif des niveaux ──────────────────────────────────────
    /// <summary>Ordre de déblocage des niveaux (biomes). Le 1er est jouable d'office ;
    /// chacun se débloque quand le précédent est complété (boss de fin de niveau battu).
    /// Source de vérité : <see cref="LevelThreat.Order"/> (l'index y est aussi le palier de menace).</summary>
    public static string[] LevelOrder => LevelThreat.Order;

    /// <summary>Le niveau est-il débloqué ? (1er niveau ou id inconnu = oui ; sinon précédent complété)</summary>
    public bool IsUnlocked(string biomeId)
    {
        int idx = System.Array.IndexOf(LevelOrder, biomeId);
        if (idx <= 0) return true;
        return HasCompletedAny(LevelOrder[idx - 1]);
    }

    // ── High scores (temps survécu max par niveau + difficulté du record) ─────
    private readonly Dictionary<string, int> _bestTimes = new();
    private readonly Dictionary<string, int> _bestDiff  = new();   // biome → (int)GameDifficulty du record

    /// <summary>Meilleur temps survécu (secondes) sur ce niveau, ou 0 si jamais joué.</summary>
    public int BestTime(string biomeId) => _bestTimes.GetValueOrDefault(biomeId, 0);

    /// <summary>Difficulté à laquelle le meilleur temps a été réalisé (Normal par défaut).</summary>
    public GameDifficulty BestDifficulty(string biomeId)
        => (GameDifficulty)_bestDiff.GetValueOrDefault(biomeId, (int)GameDifficulty.Normal);

    /// <summary>Enregistre un temps survécu + la difficulté ; garde le max. True si nouveau record.</summary>
    public bool RecordTime(string biomeId, int secs, GameDifficulty diff)
    {
        if (biomeId.Length == 0 || secs <= _bestTimes.GetValueOrDefault(biomeId, 0)) return false;
        _bestTimes[biomeId] = secs;
        _bestDiff[biomeId]  = (int)diff;
        Save();
        return true;
    }

    /// <summary>Clé de localisation du nom court d'une difficulté (DIFF_EASY/NORMAL/HARD).</summary>
    public static string DifficultyKey(GameDifficulty d) => d switch
    {
        GameDifficulty.Facile    => "DIFF_EASY",
        GameDifficulty.Difficile => "DIFF_HARD",
        _                        => "DIFF_NORMAL",
    };

    // ── Armes découvertes (arsenal) ──────────────────────────────────────────
    private readonly HashSet<string> _discovered = new();

    /// <summary>Armes de signature des personnages : toujours considérées découvertes.</summary>
    public static readonly string[] SignatureWeapons = { "impulse_cannon", "drone_swarm", "plasma_blade", "vector_lance" };

    /// <summary>L'arme a-t-elle été découverte (équipée au moins une fois) ou est-elle une arme de signature ?</summary>
    public bool IsDiscovered(string weaponId)
        => System.Array.IndexOf(SignatureWeapons, weaponId) >= 0 || _discovered.Contains(weaponId);

    /// <summary>Marque une arme comme découverte (1re acquisition) et persiste.</summary>
    public void Discover(string weaponId)
    {
        if (weaponId.Length == 0) return;
        if (_discovered.Add(weaponId)) Save();
    }

    // ── Greffes découvertes (Assimilation) ────────────────────────────────────
    private readonly HashSet<string> _discoveredGrafts = new();

    /// <summary>La greffe a-t-elle déjà été assimilée au moins une fois ?</summary>
    public bool IsGraftDiscovered(string graftId) => _discoveredGrafts.Contains(graftId);

    /// <summary>Marque une greffe comme découverte (1re assimilation) et persiste.</summary>
    public void DiscoverGraft(string graftId)
    {
        if (graftId.Length == 0) return;
        if (_discoveredGrafts.Add(graftId)) Save();
    }

    // ── Touches de déplacement (remap clavier) ────────────────────────────────
    /// <summary>Touche clavier principale d'une action de déplacement (perso ou défaut ZQSD).</summary>
    public Key MoveKey(string action) => _moveKeys.GetValueOrDefault(action, InputRemap.DefaultKeys[action]);

    /// <summary>Réaffecte la touche principale d'une action de déplacement, l'applique et persiste.</summary>
    public void SetMoveKey(string action, Key key)
    {
        _moveKeys[action] = key;
        InputRemap.SetKey(action, key);
        Save();
    }

    /// <summary>Touche clavier de dash (perso ou défaut Maj).</summary>
    public Key DashKey => _dashKey ?? InputRemap.DefaultDashKey;

    /// <summary>Réaffecte la touche de dash, l'applique et persiste.</summary>
    public void SetDashKey(Key key)
    {
        _dashKey = key;
        InputRemap.SetDashKey(key);
        Save();
    }

    /// <summary>Restaure les touches de déplacement par défaut (ZQSD) + dash (Maj), applique et persiste.</summary>
    public void ResetMoveKeys()
    {
        _moveKeys.Clear();
        _dashKey = null;
        InputRemap.ApplyAll(this);
        InputRemap.SetDashKey(DashKey);
        Save();
    }

    // ── Setters (appliquent + sauvegardent) ───────────────────────────────────
    public void SetMaster(float v)     { Master = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetMusic(float v)      { Music  = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetSfx(float v)        { Sfx    = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetDifficulty(GameDifficulty d)
    {
        Difficulty = d;
        // Passer en assistance remet la saturation à zéro : garder un cran actif sous « Facile » ferait
        // coexister les deux axes que la 1.25.0 vient précisément de fusionner.
        if (d == GameDifficulty.Facile) Saturation = 0;
        Save();
    }

    /// <summary>Choisit le cran de saturation de la prochaine run (borné par la progression).</summary>
    public void SetSaturation(int rank)
    {
        if (IsAssisted) { Saturation = 0; _persistedSaturation = 0; Save(); return; }
        Saturation = Mathf.Clamp(rank, 0, MaxSelectableSaturation);
        _persistedSaturation = Saturation;   // choix explicite du joueur : il devient la valeur persistée
        Save();
    }

    /// <summary>
    /// À appeler quand le boss de fin est battu : mémorise le cran atteint, ce qui débloque le suivant.
    /// Ne redescend jamais (un cran validé reste validé, même après une run à cran plus bas).
    /// </summary>
    public void RecordSaturationBeaten(int rank)
    {
        if (IsAssisted || rank <= HighestSaturationBeaten) return;
        // ⚠ Sous --saturation=<n>, le cran n'a pas été GAGNÉ par le joueur : il a été imposé au banc.
        // Sans ce garde-fou, une campagne de mesure débloque l'échelle dans la sauvegarde réelle — c'est
        // arrivé le 2026-07-30 (`saturation_beaten=5` sans aucune victoire à ce cran), et le joueur s'est
        // retrouvé à pouvoir choisir n'importe quel cran. Protéger `saturation` contre la persistance ne
        // suffisait pas : le DÉBLOCAGE est une seconde voie d'écriture.
        if (DebugHooks.Saturation.HasValue) return;
        HighestSaturationBeaten = Mathf.Min(rank, SaturationTable.MaxRank);
        Save();
    }

    public void SetDisplayMode(WindowMode m)  { DisplayMode = m; ApplyDisplay(); Save(); }
    public void SetWindowSize(Vector2I size)  { WindowSize  = size; ApplyDisplay(); Save(); }
    public void SetVSync(bool v)              { VSync  = v; ApplyDisplay(); Save(); }
    public void SetMaxFps(int fps)            { MaxFps = Mathf.Max(0, fps); Engine.MaxFps = MaxFps; Save(); }
    public void SetShowFps(bool v)            { ShowFps = v; VersionStamp.Instance?.SetFpsVisible(v); Save(); }

    /// <summary>Intensité des secousses ; 0 les désactive complètement (accessibilité).</summary>
    public void SetShakeIntensity(float v)
    {
        ShakeIntensity = Mathf.Clamp(v, 0f, 1f);
        ScreenShake.Intensity = ShakeIntensity;
        ScreenShake.Enabled   = ShakeEnabled;
        Save();
    }

    public void SetReduceFlashes(bool v) { ReduceFlashes = v; Save(); }
    public void SetRumble(float v)       { Rumble = Mathf.Clamp(v, 0f, 1f); Save(); }

    public void SetShowVersionStamp(bool v) { ShowVersionStamp = v; VersionStamp.Instance?.SetStampVisible(v); Save(); }
    public void SetDiscordEnabled(bool v)   { DiscordEnabled = v; DiscordPresence.Instance?.SetEnabled(v); Save(); }

    /// <summary>Change la langue de l'UI, l'applique au TranslationServer et persiste.</summary>
    public void SetLanguage(string lang)
    {
        if (System.Array.IndexOf(Languages, lang) < 0) lang = "en";
        Language = _persistedLanguage = lang;
        TranslationServer.SetLocale(lang);
        Save();
    }

    /// <summary>Applique <c>--lang=&lt;code&gt;</c> pour la session, sans toucher à settings.cfg
    /// (cf. DebugHooks.ForcedLanguage). Sans le flag : sans effet.</summary>
    private void ApplyLanguageOverride()
    {
        string? forced = DebugHooks.ForcedLanguage;
        if (forced == null) return;
        if (System.Array.IndexOf(Languages, forced) < 0)
        {
            GD.PushWarning($"--lang={forced} ignoré (langues : {string.Join(", ", Languages)})");
            return;
        }
        Language = forced;   // _persistedLanguage reste celle du joueur
    }

    // ── Application ────────────────────────────────────────────────────────────
    public void Apply()
    {
        ApplyAudio();
        ApplyDisplay();
        ScreenShake.Intensity = ShakeIntensity;
        ScreenShake.Enabled   = ShakeEnabled;
        TranslationServer.SetLocale(Language);
        InputRemap.ApplyAll(this);
        InputRemap.SetDashKey(DashKey);
    }

    private void ApplyAudio()
    {
        int master = AudioServer.GetBusIndex("Master");
        if (master >= 0) AudioServer.SetBusVolumeDb(master, Db(Master));
        if (AudioSystem.Instance != null)
        {
            AudioSystem.Instance.MusicVolume = Music;
            AudioSystem.Instance.SfxVolume   = Sfx;
        }
    }

    /// <summary>Applique mode de fenêtre, taille (mode fenêtré), VSync et limite d'IPS.
    /// On s'appuie EXCLUSIVEMENT sur les modes natifs de Godot : `Fullscreen` y désigne le
    /// plein écran FENÊTRÉ (sans bordure) et `ExclusiveFullscreen` le plein écran exclusif.
    /// Fabriquer soi-même un « sans bordure » (flag Borderless + fenêtre à la taille de l'écran)
    /// fonctionne à l'aller mais pas au retour : Godot relit alors le mode depuis la géométrie,
    /// se croit en plein écran, et refuse de repasser en fenêtré — le joueur reste coincé.</summary>
    private void ApplyDisplay()
    {
        switch (DisplayMode)
        {
            case WindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;

            case WindowMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;

            default:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(WindowSize);
                // Recentrage : sans ça, changer de résolution laisse la fenêtre collée en haut
                // à gauche de sa position précédente, parfois à cheval hors de l'écran.
                int screen = DisplayServer.WindowGetCurrentScreen();
                DisplayServer.WindowSetPosition(
                    DisplayServer.ScreenGetPosition(screen)
                    + (DisplayServer.ScreenGetSize(screen) - WindowSize) / 2);
                break;
        }

        DisplayServer.WindowSetVsyncMode(VSync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = MaxFps;
    }

    private static float Db(float linear) => linear <= 0.001f ? -80f : Mathf.LinearToDb(linear);

    /// <summary>
    /// Lit la saturation, ou <b>migre</b> une sauvegarde d'avant la 1.25.0.
    ///
    /// <para>L'absence de la clé <c>gameplay/saturation</c> signe un ancien fichier. « Difficile » y
    /// devient <i>Normal + saturation 1</i>, aux multiplicateurs identiques
    /// (<see cref="SaturationTable.MigrateLegacyDifficulty"/>) : les records gagnés à cette difficulté
    /// restent donc <b>exacts</b> — ni effacés, ni réinterprétés à la hausse. Et comme ce joueur jouait
    /// effectivement au cran 1, on le lui crédite s'il a déjà terminé un niveau, plutôt que de le
    /// renvoyer au bas de l'échelle.</para>
    ///
    /// <para>Doit être appelée <b>après</b> le chargement des complétions (cf. l'appel dans
    /// <c>Load</c>) : sans elles, le crédit du cran est perdu.</para>
    /// </summary>
    private void LoadOrMigrateSaturation(ConfigFile cfg)
    {
        if (cfg.HasSectionKey("gameplay", "saturation"))
        {
            Saturation = cfg.GetValue("gameplay", "saturation", 0).AsInt32();
            HighestSaturationBeaten = cfg.GetValue("gameplay", "saturation_beaten", 0).AsInt32();
        }
        else
        {
            var (migratedDiff, migratedSat, migratedBeaten) =
                SaturationTable.MigrateLegacyDifficulty((int)Difficulty);
            Difficulty = (GameDifficulty)migratedDiff;
            Saturation = migratedSat;
            // Le cran n'est crédité que si le joueur a réellement terminé un niveau : jouer en
            // « Difficile » sans jamais gagner ne débloque rien.
            if (migratedBeaten > 0 && HasCompletedAny(LevelOrder[0]))
                HighestSaturationBeaten = migratedBeaten;
        }

        HighestSaturationBeaten = Mathf.Clamp(HighestSaturationBeaten, 0, SaturationTable.MaxRank);
        Saturation = Mathf.Clamp(Saturation, 0, MaxSelectableSaturation);
        if (Difficulty == GameDifficulty.Facile) Saturation = 0;

        // Banc de mesure : --saturation=<n> force le cran sans passer par l'écran de sélection (que le
        // bot ne traverse jamais) et SANS persister — une campagne ne doit pas laisser le joueur avec
        // un cran qu'il n'a pas choisi. Le déblocage est volontairement ignoré : on mesure un cran
        // avant de l'avoir gagné. Appliqué en dernier pour écraser le clamp ci-dessus.
        _persistedSaturation = Saturation;
        if (DebugHooks.Saturation.HasValue)
        {
            Saturation = Mathf.Clamp(DebugHooks.Saturation.Value, 0, SaturationTable.MaxRank);
            Difficulty = GameDifficulty.Normal;   // l'assistance annulerait tous les crans
            GD.Print($"[GameSettings] --saturation : cran forcé à {Saturation} (non persisté)");
        }
    }

    // ── Persistance ────────────────────────────────────────────────────────────
    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return; // pas de fichier → défauts
        Master       = (float)cfg.GetValue("audio",   "master",     Master).AsSingle();
        Music        = (float)cfg.GetValue("audio",   "music",      Music).AsSingle();
        Sfx          = (float)cfg.GetValue("audio",   "sfx",        Sfx).AsSingle();
        Difficulty   = (GameDifficulty)cfg.GetValue("gameplay", "difficulty", (int)Difficulty).AsInt32();
        Language     = cfg.GetValue("display", "language", Language).AsString();

        // Affichage — migration des anciennes clés : le booléen `fullscreen` devient le défaut
        // du nouveau mode à trois valeurs, et `shake` (bool) devient l'intensité 0/1.
        bool legacyFullscreen = cfg.GetValue("display", "fullscreen", false).AsBool();
        var  defaultMode      = legacyFullscreen ? WindowMode.Fullscreen : WindowMode.Windowed;
        DisplayMode = (WindowMode)cfg.GetValue("display", "mode", (int)defaultMode).AsInt32();
        if (!System.Enum.IsDefined(DisplayMode)) DisplayMode = WindowMode.Windowed;

        WindowSize = new Vector2I(
            cfg.GetValue("display", "width",  WindowSize.X).AsInt32(),
            cfg.GetValue("display", "height", WindowSize.Y).AsInt32());
        VSync   = cfg.GetValue("display", "vsync",    VSync).AsBool();
        MaxFps  = Mathf.Max(0, cfg.GetValue("display", "max_fps", MaxFps).AsInt32());
        ShowFps = cfg.GetValue("display", "show_fps", ShowFps).AsBool();

        bool legacyShake = cfg.GetValue("gameplay", "shake", true).AsBool();
        ShakeIntensity = Mathf.Clamp(
            cfg.GetValue("gameplay", "shake_intensity", legacyShake ? 1f : 0f).AsSingle(), 0f, 1f);
        ReduceFlashes = cfg.GetValue("gameplay", "reduce_flashes", ReduceFlashes).AsBool();
        Rumble        = Mathf.Clamp(cfg.GetValue("gameplay", "rumble", Rumble).AsSingle(), 0f, 1f);

        ShowVersionStamp = cfg.GetValue("interface", "version_stamp", ShowVersionStamp).AsBool();
        DiscordEnabled   = cfg.GetValue("interface", "discord",       DiscordEnabled).AsBool();

        if (System.Array.IndexOf(Languages, Language) < 0) Language = "en";
        _persistedLanguage = Language;

        _completions.Clear();
        foreach (string key in cfg.GetValue("progress", "completions", new string[0]).AsStringArray())
            _completions.Add(key);

        // ⚠ APRÈS les complétions, et pas plus haut avec les autres réglages de gameplay : la
        // migration a besoin de savoir si le joueur a déjà battu un niveau pour lui créditer le cran 1.
        LoadOrMigrateSaturation(cfg);

        _bestTimes.Clear();
        if (cfg.HasSection("highscores"))
            foreach (string biome in cfg.GetSectionKeys("highscores"))
                _bestTimes[biome] = cfg.GetValue("highscores", biome, 0).AsInt32();

        _bestDiff.Clear();
        if (cfg.HasSection("highscores_diff"))
            foreach (string biome in cfg.GetSectionKeys("highscores_diff"))
                _bestDiff[biome] = cfg.GetValue("highscores_diff", biome, (int)GameDifficulty.Normal).AsInt32();

        _discovered.Clear();
        foreach (string id in cfg.GetValue("discovered", "weapons", new string[0]).AsStringArray())
            _discovered.Add(id);

        _discoveredGrafts.Clear();
        foreach (string id in cfg.GetValue("discovered", "grafts", new string[0]).AsStringArray())
            _discoveredGrafts.Add(id);

        _moveKeys.Clear();
        foreach (string action in InputRemap.Actions)
        {
            int code = cfg.GetValue("input", action, 0).AsInt32();
            if (code != 0) _moveKeys[action] = (Key)code;
        }
        int dashCode = cfg.GetValue("input", InputRemap.Dash, 0).AsInt32();
        _dashKey = dashCode != 0 ? (Key)dashCode : null;
    }

    /// <summary>Réinitialise TOUTE la progression (complétions, high scores, armes découvertes) et
    /// persiste. Les Échos/améliorations méta sont réinitialisés séparément (MetaProgressionSystem).
    /// Les préférences (audio, langue, difficulté, plein écran) sont conservées.</summary>
    public void ResetProgress()
    {
        _completions.Clear();
        _bestTimes.Clear();
        _bestDiff.Clear();
        _discovered.Clear();
        _discoveredGrafts.Clear();
        // La saturation est de la PROGRESSION, pas une préférence : la laisser survivre à une remise à
        // zéro donnerait un cran 5 débloqué à un joueur qui n'a plus aucune victoire à son compte.
        Saturation = 0;
        _persistedSaturation = 0;
        HighestSaturationBeaten = 0;
        Save();
    }

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio",    "master",     Master);
        cfg.SetValue("audio",    "music",      Music);
        cfg.SetValue("audio",    "sfx",        Sfx);
        cfg.SetValue("display",  "language",   _persistedLanguage);
        cfg.SetValue("display",  "mode",       (int)DisplayMode);
        cfg.SetValue("display",  "width",      WindowSize.X);
        cfg.SetValue("display",  "height",     WindowSize.Y);
        cfg.SetValue("display",  "vsync",      VSync);
        cfg.SetValue("display",  "max_fps",    MaxFps);
        cfg.SetValue("display",  "show_fps",   ShowFps);
        cfg.SetValue("gameplay", "difficulty", (int)Difficulty);
        // La présence de ces deux clés signale à LoadOrMigrateSaturation que le fichier est au format
        // 1.25.0 : ne jamais les rendre conditionnelles, sinon la migration se rejouerait à chaque
        // démarrage et écraserait un choix d'assistance par « Normal + saturation 1 ».
        cfg.SetValue("gameplay", "saturation",        _persistedSaturation);
        cfg.SetValue("gameplay", "saturation_beaten", HighestSaturationBeaten);
        cfg.SetValue("gameplay", "shake_intensity", ShakeIntensity);
        cfg.SetValue("gameplay", "reduce_flashes",  ReduceFlashes);
        cfg.SetValue("gameplay", "rumble",          Rumble);
        cfg.SetValue("interface","version_stamp",   ShowVersionStamp);
        cfg.SetValue("interface","discord",         DiscordEnabled);

        var keys = new string[_completions.Count];
        _completions.CopyTo(keys);
        cfg.SetValue("progress", "completions", keys);

        foreach (var (biome, secs) in _bestTimes)
            cfg.SetValue("highscores", biome, secs);
        foreach (var (biome, diff) in _bestDiff)
            cfg.SetValue("highscores_diff", biome, diff);

        var disc = new string[_discovered.Count];
        _discovered.CopyTo(disc);
        cfg.SetValue("discovered", "weapons", disc);

        var discGrafts = new string[_discoveredGrafts.Count];
        _discoveredGrafts.CopyTo(discGrafts);
        cfg.SetValue("discovered", "grafts", discGrafts);

        foreach (var (action, key) in _moveKeys)
            cfg.SetValue("input", action, (int)key);
        if (_dashKey.HasValue)
            cfg.SetValue("input", InputRemap.Dash, (int)_dashKey.Value);
        cfg.Save(Path);
    }
}
