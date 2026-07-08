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

        // Abonnement au signal EnemyKilled émis par GameManager
        GameManager.Instance.EnemyKilled += OnEnemyKilled;

        _runEndScreenScene ??= GD.Load<PackedScene>("res://scenes/ui/RunEndScreen.tscn");

        // Demarre la musique de run (debut de run = piste legere)
        AudioSystem.Instance?.PlayMusic("music_run_intro");
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
    }

    // Seuils de changement de piste musicale (en secondes ecoules depuis le debut)
    // GDD §7 : difficulte montante => intensite musicale croissante
    private const float MidThresholdSec     = 300f; // 5:00 — Sentinelles entrent, tension montante
    private const float IntenseThresholdSec = 600f; // 10:00 — Colosses presents, chaos maximal

    private bool _musicMidPlaying     = false;
    private bool _musicIntensePlaying = false;

    public override void _Process(double delta)
    {
        if (RunEnded) return;

        ElapsedSeconds += (float)delta;

        // Progression de l'intensite musicale selon le temps ecoule
        UpdateRunMusicIntensity();

        // Le timer ne termine plus la run : à 0 (fin du temps imparti), on entre en OVERTIME
        // (escalade brutale gérée par EnemySpawner). La run se termine à la mort du joueur ;
        // battre le boss de fin de niveau marque la complétion (OnLevelBossDefeated).
        if (!_overtimeAnnounced && Overtime)
        {
            _overtimeAnnounced = true;
            Banner.Show(GetTree(), Loc.T("OVERTIME"), new Color(1f, 0.3f, 0.3f));
        }
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
            GameSettings.Instance.RecordCompletion(biome, GameSettings.Instance.Difficulty);

        Banner.Show(GetTree(), Loc.T("LEVEL_COMPLETE"), new Color(1f, 0.85f, 0.3f));
        AudioSystem.Instance?.PlaySfx("sfx_core_collect");
    }

    private void UpdateRunMusicIntensity()
    {
        if (!_musicIntensePlaying && ElapsedSeconds >= IntenseThresholdSec)
        {
            _musicIntensePlaying = true;
            AudioSystem.Instance?.PlayMusic("music_run_intense", fadeInSec: 2.0f);
        }
        else if (!_musicMidPlaying && !_musicIntensePlaying && ElapsedSeconds >= MidThresholdSec)
        {
            _musicMidPlaying = true;
            AudioSystem.Instance?.PlayMusic("music_run_mid", fadeInSec: 2.0f);
        }
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
            meta.EchoOvertimeDampening, meta.EchoOvertimeBonusCap);
    }

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
