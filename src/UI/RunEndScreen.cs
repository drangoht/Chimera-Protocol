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

    private Label     _outcomeLabel  = null!;
    private Label     _timeLabel     = null!;
    private Label     _killLabel     = null!;
    private Label     _coreLabel     = null!;
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
        _bonusLabel   = GetNode<Label>("BonusLabel");
        _totalLabel   = GetNode<Label>("TotalLabel");
        _hubButton    = GetNode<Button>("HubButton");
        _replayButton = GetNode<Button>("ReplayButton");
        _fadeOverlay  = GetNode<ColorRect>("FadeOverlay");

        _hubButton.Pressed    += OnHubPressed;
        _replayButton.Pressed += OnReplayPressed;

        ConnectHoverEffects(_hubButton);
        ConnectHoverEffects(_replayButton);

        _hubButton.FocusNeighborRight   = _hubButton.GetPathTo(_replayButton);
        _replayButton.FocusNeighborLeft = _replayButton.GetPathTo(_hubButton);

        Visible = false;

        // Lance l'affichage avec les données préremplies
        ShowEndScreen(PendingOutcome, PendingTimeSecs, PendingKills, PendingCores, PendingEchoesEarned);
    }

    /// <summary>
    /// Affiche l'écran et lance l'animation countup précédée d'un fade-in depuis le noir.
    /// </summary>
    public void ShowEndScreen(string outcome, int timeSecs, int kills, int cores, int echoesEarned)
    {
        Visible = true;

        bool isVictory = outcome == "extraction_success";
        _outcomeLabel.Text = isVictory ? "EXTRACTION REUSSIE" : "MORT EN SERVICE";
        _outcomeLabel.AddThemeColorOverride("font_color", isVictory ? ColorVictory : ColorDeath);

        // Stinger sonore selon le resultat (la musique de run a deja ete arretee par Player.HandleDeath ou RunStatsTracker)
        if (isVictory)
            AudioSystem.Instance?.PlaySfx("sfx_ui_victory");
        else
            AudioSystem.Instance?.PlaySfx("sfx_ui_death");

        // Calcul des composantes
        var meta      = MetaProgressionSystem.Instance;
        int timeDiv   = meta?.EchoTimeDiv   ?? 20;
        int killDiv   = meta?.EchoKillDiv   ?? 10;
        int coreMult  = meta?.EchoCoreMult  ?? 5;
        int baseBonus = meta?.EchoBaseBonus ?? 10;

        int timeEchoes = timeSecs / timeDiv;
        int killEchoes = kills    / killDiv;
        int coreEchoes = cores    * coreMult;

        // Cache les labels pendant l'animation
        _timeLabel.Visible    = false;
        _killLabel.Visible    = false;
        _coreLabel.Visible    = false;
        _bonusLabel.Visible   = false;
        _totalLabel.Visible   = false;
        _hubButton.Visible    = false;
        _replayButton.Visible = false;

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
        AnimateCountup(tween, _timeLabel, "Temps survécu", timeEchoes, CountupDuration);

        // Composante 2 — Kills
        tween.TweenCallback(Callable.From(() => _killLabel.Visible = true));
        AnimateCountup(tween, _killLabel, "Ennemis éliminés", killEchoes, CountupDuration);

        // Composante 3 — Noyaux
        tween.TweenCallback(Callable.From(() => _coreLabel.Visible = true));
        AnimateCountup(tween, _coreLabel, "Noyaux récupérés", coreEchoes, CountupDuration);

        // Composante 4 — Bonus de base
        tween.TweenCallback(Callable.From(() => _bonusLabel.Visible = true));
        AnimateCountup(tween, _bonusLabel, "Bonus de run", baseBonus, CountupDuration);

        // Total + boutons
        tween.TweenCallback(Callable.From(() =>
        {
            _totalLabel.Text      = $"TOTAL : {echoesEarned} ÉCHOS";
            _totalLabel.Visible   = true;
            _hubButton.Visible    = true;
            _replayButton.Visible = true;
            _replayButton.GrabFocus();
        }));
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
        GetTree().ChangeSceneToFile(target);
        GetParent().RemoveChild(this);
        QueueFree();
    }

    private void OnReplayPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://scenes/Game.tscn");
        GetParent().RemoveChild(this);
        QueueFree();
    }
}
