using Godot;

/// <summary>
/// Node ordinaire (pas AutoLoad) présent dans Game.tscn.
/// Tracke les statistiques de la run en cours : temps, kills, noyaux collectés.
/// À la fin de run, calcule les Échos et ouvre RunEndScreen.
/// </summary>
public partial class RunStatsTracker : Node
{
    public static RunStatsTracker? Instance { get; private set; }

    // Durée de run lue depuis meta_upgrades.json via MetaProgressionSystem (fallback 900 s)
    private int _runDurationSeconds = 900;

    public float ElapsedSeconds    { get; private set; } = 0f;
    public int   KillCount         { get; private set; } = 0;
    public int   CoresCollected    { get; private set; } = 0;
    public bool  RunEnded          { get; private set; } = false;
    public int   RunDurationSeconds => _runDurationSeconds;

    /// <summary>« Overtime » : le temps imparti est écoulé (décompte à 0 = arrivée du boss).
    /// Déclenche l'escalade brutale (EnemySpawner) — vagues massives + mini-boss/boss en boucle.</summary>
    public bool  Overtime          => !RunEnded && ElapsedSeconds >= _runDurationSeconds;

    /// <summary>Secondes écoulées depuis le début de l'overtime (0 avant).</summary>
    public float OvertimeSeconds   => Mathf.Max(0f, ElapsedSeconds - _runDurationSeconds);

    /// <summary>Le boss de fin de niveau a-t-il été vaincu durant cette run ? (= niveau terminé)</summary>
    public bool  LevelCompleted    { get; private set; }

    private bool _overtimeAnnounced = false;

    private static PackedScene? _runEndScreenScene;

    public override void _Ready()
    {
        Instance = this;

        // Récupère la durée de run depuis le JSON (via MetaProgressionSystem qui l'a déjà parsé)
        // On relit directement pour ne pas coupler les systèmes sur un champ non exposé
        LoadRunDuration();

        // Banc : --start-at=<minutes> démarre la run à une minute donnée. L'horloge du spawner est
        // avancée de son côté (EnemySpawner tient son propre compteur) — les deux doivent rester
        // cohérents, sinon la menace et le décompte d'overtime décrivent deux runs différentes.
        if (DebugHooks.StartAtMinutes > 0f)
            ElapsedSeconds = DebugHooks.StartAtMinutes * 60f;

        // Abonnement au signal EnemyKilled émis par GameManager
        GameManager.Instance.EnemyKilled += OnEnemyKilled;

        _runEndScreenScene ??= GD.Load<PackedScene>("res://scenes/ui/RunEndScreen.tscn");

        // Différé d'une frame : `GameManager.CurrentBiomeId` est posé par
        // GroundRenderer._Ready et l'ordre des _Ready entre nœuds frères n'est pas
        // garanti — sans ce report, le biome peut être encore vide ici et la
        // musique adaptative retomberait sur le fallback à chaque run.
        Callable.From(StartRunMusic).CallDeferred();

        // Journal de la courbe de puissance (flag --power-curve uniquement, cf. PowerTelemetry).
        // Différé comme la musique : le biome et le build de départ ne sont posés qu'après ce _Ready.
        Callable.From(PowerTelemetry.Begin).CallDeferred();
    }

    /// <summary>
    /// Démarre la musique adaptative du biome courant (<see cref="MusicDirector"/>) :
    /// 4 couches synchronisées dont seuls les volumes suivent l'action, au lieu des
    /// bascules de piste par paliers de temps d'avant la 1.17.
    /// </summary>
    private void StartRunMusic()
    {
        string biome = GameManager.Instance?.CurrentBiomeId ?? "";
        if (biome.Length == 0 || MusicDirector.Instance?.PlayBiome(biome, 2.0f) != true)
            GD.PrintErr($"[RunStatsTracker] Pas de musique de run : stems absents pour " +
                        $"le biome '{biome}' (régénérer via tools/generate_music_v3.py).");
    }

    private void LoadRunDuration()
    {
        const string path = "res://data/meta_upgrades.json";
        if (!Godot.FileAccess.FileExists(path)) return;

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;

        using var doc = System.Text.Json.JsonDocument.Parse(file.GetAsText());
        if (doc.RootElement.TryGetProperty("runDurationSeconds", out var prop))
            _runDurationSeconds = prop.GetInt32();

        // Cran III « Compte à rebours » : l'overtime arrive plus tôt (13 → ~10 min). Appliqué ici, sur
        // la valeur du JSON, pour rester compatible avec l'upgrade méta `overtime_stabilizer` qui module
        // la même durée — un seuil en dur ailleurs contredirait l'un des deux.
        //
        // Ce cran attaque le TEMPS DE CONSTRUCTION du build, pas la puissance : le relevé du 2026-07-29
        // a montré que l'état d'entrée en overtime explique à lui seul un facteur 2,4 sur la survie.
        float durationMult = GameSettings.Instance?.RunDurationMult ?? 1f;
        if (durationMult < 1f)
            _runDurationSeconds = Mathf.Max(60, Mathf.RoundToInt(_runDurationSeconds * durationMult));
    }

    public override void _Process(double delta)
    {
        if (RunEnded) return;

        ElapsedSeconds += (float)delta;

        PowerTelemetry.Tick((float)delta);

        // (L'intensité musicale n'est plus pilotée ici : MusicDirector échantillonne
        // lui-même l'état de la run — ennemis à l'écran, temps, PV, boss.)

        // Le timer ne termine plus la run : à 0 (fin du temps imparti), on entre en OVERTIME
        // (escalade brutale gérée par EnemySpawner). La run se termine à la mort du joueur ;
        // battre le boss de fin de niveau marque la complétion (OnLevelBossDefeated).
        if (!_overtimeAnnounced && Overtime)
        {
            _overtimeAnnounced = true;
            Banner.Show(GetTree(), Loc.T("OVERTIME"), new Color(1f, 0.3f, 0.3f));
        }

        // Banc automatisé (--run-limit) : la survie étant sans fin, seule une borne de temps permet
        // de terminer proprement une mesure d'overtime. Jamais actif en build normal.
        if (DebugHooks.RunLimit > 0f && ElapsedSeconds >= DebugHooks.RunLimit)
            EndRun("bench_limit");
    }

    /// <summary>
    /// Boss de fin de niveau vaincu : marque le NIVEAU TERMINÉ (enregistre la complétion → débloque
    /// le suivant + bannière), une seule fois. **N'arrête PAS la run** (survie sans fin jusqu'à la mort).
    /// </summary>
    public void OnLevelBossDefeated()
    {
        if (RunEnded || LevelCompleted) return;
        LevelCompleted = true;

        string biome = GameManager.Instance?.CurrentBiomeId ?? "";
        if (biome.Length > 0 && GameSettings.Instance != null)
        {
            GameSettings.Instance.RecordCompletion(biome, GameSettings.Instance.Difficulty);
            // Le cran de saturation est validé par la MORT DU BOSS, pas par la durée de survie : c'est
            // ce qui débloque le cran suivant (déblocage global, tous biomes confondus).
            GameSettings.Instance.RecordSaturationBeaten(GameSettings.Instance.Saturation);
        }

        Banner.Show(GetTree(), Loc.T("LEVEL_COMPLETE"), new Color(1f, 0.85f, 0.3f));
        AudioSystem.Instance?.PlaySfx("sfx_core_collect");
    }

    // ---------------------------------------------------------------------------
    // API publique
    // ---------------------------------------------------------------------------

    public void RegisterKill()
    {
        if (RunEnded) return;
        KillCount++;
    }

    public void RegisterCoreCollected()
    {
        if (RunEnded) return;
        CoresCollected++;
    }

    /// <summary>
    /// Termine la run, calcule les Échos et ouvre l'écran de fin.
    /// <paramref name="outcome"/> : "extraction_success" ou "death".
    /// </summary>
    public void EndRun(string outcome)
    {
        if (RunEnded) return;
        RunEnded = true;

        // L'écran de fin met l'arbre en pause : le HUD cesse de traiter ses frames et sa barre de
        // boss resterait figée par-dessus le titre de l'écran de fin. On la retire ici, tant que
        // le HUD peut encore réagir.
        HUD.Instance?.HideBossBar();

        // Un combat de boss en cours au moment de la mort du joueur est écrit quand même : savoir
        // qu'il restait 40 % de PV au boss vaut autant qu'un TTK pour l'équilibrage.
        BossTelemetry.NotifyRunEnd(outcome);

        // Courbe de puissance : clôt le journal (dernier échantillon partiel + écriture disque).
        PowerTelemetry.End(outcome);

        int timeSecs = (int)ElapsedSeconds;
        var (echoes, overtimeBonus) = CalculateEchoesDetailed(timeSecs, KillCount, CoresCollected);

        MetaProgressionSystem.Instance?.AddEchoes(echoes);

        GD.Print($"[RunStatsTracker] Fin de run — outcome={outcome}, T={timeSecs}s, K={KillCount}, N={CoresCollected}, Échos={echoes} (dont overtime={overtimeBonus})");

        // High score : enregistre le temps survécu + la difficulté du niveau (garde le max).
        string biome = GameManager.Instance?.CurrentBiomeId ?? "";
        bool newRecord = GameSettings.Instance?.RecordTime(biome, timeSecs,
            GameSettings.Instance.Difficulty) ?? false;

        // Défis / Succès : évalue la run, octroie les récompenses (Échos immédiats, perks/cosmétiques
        // débloqués) et persiste. Doit passer APRÈS RecordCompletion (OnLevelBossDefeated) pour que les
        // défis de complétion voient la complétion à jour. Tolérant à l'absence du système.
        int difficultyRank = (int)(GameSettings.Instance?.Difficulty ?? GameSettings.GameDifficulty.Normal);
        var newChallenges = ChallengeSystem.Instance?.EvaluateRunEnd(
            timeSecs, KillCount, CoresCollected, LevelCompleted, biome, difficultyRank) ?? new();

        // Banc headless : l'écran de fin n'a personne pour le lire et la mesure est déjà écrite.
        // Sans cette sortie, le processus reste vivant sur un arbre en pause et il faut le tuer.
        if (DisplayServer.GetName() == "headless" &&
            (DebugHooks.AutoPlay || DebugHooks.PowerCurve || DebugHooks.RunLimit > 0f))
        {
            GetTree().Quit();
            return;
        }

        OpenEndScreen(outcome, timeSecs, echoes, overtimeBonus, newRecord, GameSettings.Instance?.BestTime(biome) ?? timeSecs, newChallenges);
    }

    // ---------------------------------------------------------------------------
    // Calcul des Échos
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Calcule le total d'Échos ainsi que le détail du bonus de surcharge (overtime) séparément,
    /// pour l'affichage dédié dans RunEndScreen. capTimeSecs == RunDurationSeconds par construction.
    /// </summary>
    private (int Total, int OvertimeBonus) CalculateEchoesDetailed(int timeSecs, int kills, int cores)
    {
        var meta = MetaProgressionSystem.Instance;
        if (meta == null) return (10, 0);

        return EchoFormula.CalculateDetailed(timeSecs, kills, cores,
            meta.EchoTimeDiv, meta.EchoKillDiv, meta.EchoCoreMult, meta.EchoBaseBonus,
            RunDurationSeconds, meta.EchoCapKills, meta.EchoCapCores,
            meta.EchoOvertimeDampening, meta.EchoOvertimeBonusCap,
            // Palier du niveau × saturation, via la source unique (cf. GameSettings.TotalEchoMult) :
            // RunEndScreen refait le même calcul pour animer les composantes.
            GameSettings.Instance?.TotalEchoMult(ThreatTier) ?? LevelThreat.EchoMult(ThreatTier));
    }

    /// <summary>Palier de menace du niveau joué (cf. <see cref="LevelThreat"/>, GDD §28).</summary>
    public int ThreatTier => LevelThreat.TierOf(GameManager.Instance?.CurrentBiomeId);

    // ---------------------------------------------------------------------------
    // Écran de fin
    // ---------------------------------------------------------------------------

    private void OpenEndScreen(string outcome, int timeSecs, int echoesEarned, int overtimeBonus, bool newRecord, int bestTime,
        System.Collections.Generic.List<string> newChallenges)
    {
        if (_runEndScreenScene == null)
        {
            GD.PrintErr("[RunStatsTracker] RunEndScreen.tscn introuvable.");
            return;
        }

        var screen = _runEndScreenScene.Instantiate<RunEndScreen>();
        // Pré-initialise les données avant AddChild — ShowEndScreen sera appelé dans _Ready() de RunEndScreen
        screen.PendingOutcome      = outcome;
        screen.PendingTimeSecs     = timeSecs;
        screen.PendingKills        = KillCount;
        screen.PendingCores        = CoresCollected;
        screen.PendingEchoesEarned = echoesEarned;
        screen.PendingOvertimeBonus  = overtimeBonus;
        screen.PendingBestTime       = bestTime;
        screen.PendingNewRecord      = newRecord;
        screen.PendingLevelCompleted = LevelCompleted;
        screen.PendingDifficultyKey  = GameSettings.DifficultyKey(
            GameSettings.Instance?.Difficulty ?? GameSettings.GameDifficulty.Normal);
        screen.PendingThreatTier     = ThreatTier;
        screen.PendingNewChallenges  = newChallenges;

        // Ajout différé à la racine pour éviter les conflits avec le scene tree en cours de flush
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, screen);
        // Gèle le jeu après l'ajout (RunEndScreen a ProcessMode=Always donc ses boutons restent actifs)
        CallDeferred(MethodName.PauseTree);
    }

    private void PauseTree() => GetTree().Paused = true;

    // ---------------------------------------------------------------------------
    // Callbacks
    // ---------------------------------------------------------------------------

    private void OnEnemyKilled()
    {
        RegisterKill();
    }

    public override void _ExitTree()
    {
        Instance = null;
        if (GameManager.Instance != null)
            GameManager.Instance.EnemyKilled -= OnEnemyKilled;
    }
}
