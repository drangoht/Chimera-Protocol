using Godot;

/// <summary>
/// Écran de fin de run — instancié et configuré par RunStatsTracker.EndRun().
/// Les données sont préremplies via les propriétés Pending* avant AddChild,
/// puis ShowEndScreen est appelé dans _Ready() une fois dans le scene tree.
/// Anime séquentiellement les 4 composantes d'Échos puis affiche le total.
/// Deux boutons : "Retour au Hub" et "Rejouer".
/// </summary>
public partial class RunEndScreen : CanvasLayer
{
    // Données préremplies par RunStatsTracker avant AddChild
    public string PendingOutcome      { get; set; } = "death";
    public int    PendingTimeSecs     { get; set; } = 0;
    public int    PendingKills        { get; set; } = 0;
    public int    PendingCores        { get; set; } = 0;
    public int    PendingEchoesEarned { get; set; } = 0;
    public int    PendingOvertimeBonus { get; set; } = 0;
    public int    PendingBestTime       { get; set; } = 0;
    public bool   PendingNewRecord      { get; set; } = false;
    public bool   PendingLevelCompleted { get; set; } = false;
    public string PendingDifficultyKey  { get; set; } = "DIFF_NORMAL";
    /// <summary>Ids des défis nouvellement accomplis lors de cette run (affichés en fin d'écran).</summary>
    public System.Collections.Generic.List<string> PendingNewChallenges { get; set; } = new();

    private Label     _outcomeLabel  = null!;
    private Label     _timeLabel     = null!;
    private Label     _killLabel     = null!;
    private Label     _coreLabel     = null!;
    private Label     _overtimeBonusLabel = null!;
    private Label     _bonusLabel    = null!;
    private Label     _totalLabel    = null!;
    private Button    _hubButton     = null!;
    private Button    _replayButton  = null!;
    private ColorRect _fadeOverlay   = null!;

    // Durée d'animation countup par composante (secondes)
    private const double CountupDuration = 0.8;

    // Couleurs outcome
    private static readonly Color ColorVictory = new(0.2f, 1.0f, 0.9f);  // cyan
    private static readonly Color ColorDeath   = new(0.8f, 0.3f, 0.15f); // rouge-rouille

    public override void _Ready()
    {
        // Reste actif même si le tree est pausé (mort pendant level-up, ou pause générale)
        ProcessMode = ProcessModeEnum.Always;

        _outcomeLabel = GetNode<Label>("OutcomeLabel");
        _timeLabel    = GetNode<Label>("TimeLabel");
        _killLabel    = GetNode<Label>("KillLabel");
        _coreLabel    = GetNode<Label>("CoreLabel");
        _overtimeBonusLabel = GetNode<Label>("OvertimeBonusLabel");
        _bonusLabel   = GetNode<Label>("BonusLabel");
        _totalLabel   = GetNode<Label>("TotalLabel");
        _hubButton    = GetNode<Button>("HubButton");
        _replayButton = GetNode<Button>("ReplayButton");
        _fadeOverlay  = GetNode<ColorRect>("FadeOverlay");

        _hubButton.Text    = Loc.T("RUNEND_HUB");
        _replayButton.Text = Loc.T("RUNEND_REPLAY");

        _hubButton.Pressed    += OnHubPressed;
        _replayButton.Pressed += OnReplayPressed;

        ConnectHoverEffects(_hubButton);
        ConnectHoverEffects(_replayButton);

        _hubButton.FocusNeighborRight   = _hubButton.GetPathTo(_replayButton);
        _replayButton.FocusNeighborLeft = _replayButton.GetPathTo(_hubButton);

        Visible = false;

        // Lance l'affichage avec les données préremplies
        ShowEndScreen(PendingOutcome, PendingTimeSecs, PendingKills, PendingCores, PendingEchoesEarned, PendingOvertimeBonus);
    }

    private static string Fmt(int secs) => $"{secs / 60:D2}:{secs % 60:D2}";

    /// <summary>Ligne « temps survécu / record du niveau » sous le titre (or si nouveau record).</summary>
    private void ShowSurvivalLine(int timeSecs)
    {
        var line = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = $"{Loc.T("RUNEND_SURVIVED")} : {Fmt(timeSecs)} ({Loc.T(PendingDifficultyKey)})   •   {Loc.T("RUNEND_BEST")} : {Fmt(PendingBestTime)}"
                 + (PendingNewRecord ? $"   ★ {Loc.T("RUNEND_NEW_RECORD")}" : ""),
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -400f, OffsetRight = 400f, OffsetTop = 145f, OffsetBottom = 175f,
        };
        line.AddThemeFontSizeOverride("font_size", 18);
        line.AddThemeColorOverride("font_color",
            PendingNewRecord ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.85f, 0.95f));
        _outcomeLabel.GetParent().AddChild(line);
    }

    /// <summary>
    /// Affiche l'écran et lance l'animation countup précédée d'un fade-in depuis le noir.
    /// </summary>
    public void ShowEndScreen(string outcome, int timeSecs, int kills, int cores, int echoesEarned, int overtimeBonus)
    {
        Visible = true;

        // Titre : si le boss de fin de niveau a été vaincu → « NIVEAU TERMINÉ » (positif),
        // sinon « MORT EN SERVICE ». La run se termine toujours à la mort (survie sans fin).
        bool completed = PendingLevelCompleted || outcome == "extraction_success";
        _outcomeLabel.Text = completed ? Loc.T("RUNEND_LEVEL_DONE") : Loc.T("RUNEND_DEATH");
        _outcomeLabel.AddThemeColorOverride("font_color", completed ? ColorVictory : ColorDeath);
        bool isVictory = completed;

        ShowSurvivalLine(timeSecs);

        // Stinger sonore selon le resultat (la musique de run a deja ete arretee par Player.HandleDeath ou RunStatsTracker)
        if (isVictory)
            AudioSystem.Instance?.PlaySfx("sfx_ui_victory");
        else
            AudioSystem.Instance?.PlaySfx("sfx_ui_death");

        // Calcul des composantes STANDARD (plafonnées) — le bonus de surcharge (overtime) est
        // déjà calculé séparément par RunStatsTracker et transmis via overtimeBonus.
        var meta      = MetaProgressionSystem.Instance;
        int timeDiv   = meta?.EchoTimeDiv   ?? 20;
        int killDiv   = meta?.EchoKillDiv   ?? 10;
        int coreMult  = meta?.EchoCoreMult  ?? 5;
        int baseBonus = meta?.EchoBaseBonus ?? 10;
        int capTimeSecs = RunStatsTracker.Instance?.RunDurationSeconds ?? timeSecs;
        int capKills    = meta?.EchoCapKills ?? kills;
        int capCores    = meta?.EchoCapCores ?? cores;

        int timeEchoes = Mathf.Min(timeSecs, capTimeSecs) / timeDiv;
        int killEchoes = Mathf.Min(kills,    capKills)    / killDiv;
        int coreEchoes = Mathf.Min(cores,    capCores)    * coreMult;

        // Cache les labels pendant l'animation
        _timeLabel.Visible          = false;
        _killLabel.Visible          = false;
        _coreLabel.Visible          = false;
        _overtimeBonusLabel.Visible = false;
        _bonusLabel.Visible         = false;
        _totalLabel.Visible         = false;
        _hubButton.Visible          = false;
        _replayButton.Visible       = false;

        // Fade-in depuis le noir — démarre opaque, fade vers transparent en 0.5s
        _fadeOverlay.Color = new Color(0, 0, 0, 1f);
        var fadeTween = CreateTween();
        fadeTween.SetPauseMode(Tween.TweenPauseMode.Process);
        fadeTween.TweenProperty(_fadeOverlay, "color:a", 0f, 0.5).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);

        // Animation séquentielle via Tween chaîné
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);

        // Composante 1 — Temps
        tween.TweenCallback(Callable.From(() => _timeLabel.Visible = true));
        AnimateCountup(tween, _timeLabel, Loc.T("RUNEND_TIME"), timeEchoes, CountupDuration);

        // Composante 2 — Kills
        tween.TweenCallback(Callable.From(() => _killLabel.Visible = true));
        AnimateCountup(tween, _killLabel, Loc.T("RUNEND_KILLS"), killEchoes, CountupDuration);

        // Composante 3 — Noyaux
        tween.TweenCallback(Callable.From(() => _coreLabel.Visible = true));
        AnimateCountup(tween, _coreLabel, Loc.T("RUNEND_CORES"), coreEchoes, CountupDuration);

        // Composante 4 — Bonus de Surcharge (overtime) : sautée si 0 (run standard sans dépassement
        // des caps) pour ne pas casser le rythme du countup avec une ligne "+0 Échos".
        if (overtimeBonus > 0)
        {
            tween.TweenCallback(Callable.From(() => _overtimeBonusLabel.Visible = true));
            AnimateCountup(tween, _overtimeBonusLabel, Loc.T("RUNEND_OVERTIME_BONUS"), overtimeBonus, CountupDuration);
        }

        // Composante 5 — Bonus de base
        tween.TweenCallback(Callable.From(() => _bonusLabel.Visible = true));
        AnimateCountup(tween, _bonusLabel, Loc.T("RUNEND_BONUS"), baseBonus, CountupDuration);

        // Total + boutons
        tween.TweenCallback(Callable.From(() =>
        {
            _totalLabel.Text      = Loc.T("RUNEND_TOTAL", echoesEarned);
            _totalLabel.Visible   = true;
            ShowChallengeLines();
            _hubButton.Visible    = true;
            _replayButton.Visible = true;
            _replayButton.GrabFocus();
        }));
    }

    /// <summary>
    /// Affiche une ligne dorée résumant les défis nouvellement accomplis (dans le créneau entre le
    /// total et les boutons). Détail complet sur l'écran Défis. Rien si aucun défi débloqué.
    /// </summary>
    private void ShowChallengeLines()
    {
        if (PendingNewChallenges == null || PendingNewChallenges.Count == 0) return;

        var names = new System.Collections.Generic.List<string>();
        foreach (var id in PendingNewChallenges)
        {
            var def = ChallengeSystem.Instance?.FindDef(id);
            names.Add(def != null ? Loc.T(def.NameKey) : id);
        }

        string shown;
        if (names.Count <= 2)
            shown = string.Join("  ·  ", names);
        else
            shown = $"{names[0]}  ·  {names[1]}  {Loc.T("CHAL_AND_MORE", names.Count - 2)}";

        var line = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = $"★ {Loc.T("CHAL_UNLOCKED_BANNER")} : {shown}",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -450f, OffsetRight = 450f, OffsetTop = 485f, OffsetBottom = 513f,
        };
        line.AddThemeFontSizeOverride("font_size", 16);
        line.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.27f));  // or
        _totalLabel.GetParent().AddChild(line);
        // Son valorisant distinct de la collecte d'orbe : un défi débloqué est un accomplissement.
        AudioSystem.Instance?.PlaySfx("sfx_fusion_evolve");
    }

    private static void AnimateCountup(Tween tween, Label label, string labelText, int targetValue, double duration)
    {
        tween.TweenMethod(
            Callable.From((float v) => label.Text = $"{labelText} : +{(int)v} Échos"),
            0f,
            (float)targetValue,
            duration
        );
    }

    // ---------------------------------------------------------------------------
    // Hover effects (souris + focus clavier/manette)
    // ---------------------------------------------------------------------------

    private void ConnectHoverEffects(Button btn)
    {
        btn.PivotOffset = btn.CustomMinimumSize / 2f;

        var focusStyle = new StyleBoxFlat();
        focusStyle.BgColor = new Color(0.1f, 0.1f, 0.25f, 0.95f);
        focusStyle.SetBorderWidthAll(3);
        focusStyle.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
        focusStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("focus", focusStyle);

        btn.MouseEntered += () => OnBtnEntered(btn);
        btn.MouseExited  += () => OnBtnExited(btn);
        btn.FocusEntered += () => OnBtnEntered(btn);
        btn.FocusExited  += () => OnBtnExited(btn);
    }

    private void OnBtnEntered(Button btn)
    {
        btn.PivotOffset = btn.Size.X > 0 ? btn.Size / 2f : btn.CustomMinimumSize / 2f;
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    private void OnBtnExited(Button btn)
    {
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", Vector2.One, 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    // ---------------------------------------------------------------------------
    // Navigation clavier/manette
    // ---------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            OnHubPressed();
        }
    }

    // ---------------------------------------------------------------------------
    // Boutons
    // ---------------------------------------------------------------------------

    private void OnHubPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        GetTree().Paused = false;
        string hubPath  = "res://scenes/ui/HubScreen.tscn";
        string menuPath = "res://scenes/MainMenu.tscn";
        // ResourceLoader.Exists (pas FileAccess.FileExists) : a l'export, les .tscn sont
        // remappes (.remap/.scn) et le fichier litteral est absent du PCK — FileExists
        // renverrait toujours false et enverrait vers le menu au lieu du Hub.
        string target   = ResourceLoader.Exists(hubPath) ? hubPath : menuPath;
        // Purge les VFX monde figés par la pause (parentés à la racine) avant de changer de scène,
        // sinon ils réapparaissent par-dessus le Hub/menu.
        SceneCleanup.ClearWorldVfx(GetTree());
        GetTree().ChangeSceneToFile(target);
        GetParent().RemoveChild(this);
        QueueFree();
    }

    private void OnReplayPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        GetTree().Paused = false;
        // Purge les VFX monde figés par la pause avant de relancer une run (sinon ils polluent le
        // début de la nouvelle partie).
        SceneCleanup.ClearWorldVfx(GetTree());
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
        GetParent().RemoveChild(this);
        QueueFree();
    }
}
