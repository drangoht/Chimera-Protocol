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

        // Plus d'auto-victoire au timer : la run se gagne en VAINQUANT le boss final
        // (RustedCore.FinishDeath -> EndRun("extraction_success")). Le timer ne fait
        // qu'indiquer le compte a rebours avant l'apparition du boss.
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
        int echoes   = CalculateEchoes(timeSecs, KillCount, CoresCollected);

        MetaProgressionSystem.Instance?.AddEchoes(echoes);

        GD.Print($"[RunStatsTracker] Fin de run — outcome={outcome}, T={timeSecs}s, K={KillCount}, N={CoresCollected}, Échos={echoes}");

        // Arret musique de run lors d'une extraction reussie
        // (en cas de mort, Player.HandleDeath() gere l'arret avec fondu)
        if (outcome == "extraction_success")
        {
            AudioSystem.Instance?.StopMusic(fadeOutSec: 1.0f);
            // Enregistre la complétion du biome à la difficulté courante (badge sélection niveau).
            string biome = GameManager.Instance?.CurrentBiomeId ?? "";
            if (biome.Length > 0 && GameSettings.Instance != null)
                GameSettings.Instance.RecordCompletion(biome, GameSettings.Instance.Difficulty);
        }

        OpenEndScreen(outcome, timeSecs, echoes);
    }

    // ---------------------------------------------------------------------------
    // Calcul des Échos
    // ---------------------------------------------------------------------------

    private int CalculateEchoes(int timeSecs, int kills, int cores)
    {
        var meta = MetaProgressionSystem.Instance;
        if (meta == null) return 10;

        return (timeSecs / meta.EchoTimeDiv)
             + (kills    / meta.EchoKillDiv)
             + (cores    * meta.EchoCoreMult)
             + meta.EchoBaseBonus;
    }

    // ---------------------------------------------------------------------------
    // Écran de fin
    // ---------------------------------------------------------------------------

    private void OpenEndScreen(string outcome, int timeSecs, int echoesEarned)
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
