using Godot;

/// <summary>
/// Menu principal — fade-in d'entrée, hover effects, transition de sortie.
/// </summary>
public partial class MainMenu : Control
{
    private Button    _playButton     = null!;
    private Button    _hubButton      = null!;
    private Button    _bestiaryButton = null!;
    private Button    _arsenalButton  = null!;
    private Button    _optionsButton  = null!;
    private Button    _quitButton     = null!;
    private ColorRect _fadeOverlay    = null!;

    private readonly System.Collections.Generic.List<Button> _langButtons = new();

    /// <summary>Source de vérité de la dernière version publiée (fichier poussé à chaque release).</summary>
    private const string VersionManifestUrl =
        "https://raw.githubusercontent.com/drangoht/Chimera-Protocol/main/version.json";

    private string _updateUrl = "https://drangoht.itch.io/chimera-protocol";

    public override void _Ready()
    {
        DiscordPresence.Instance?.SetInMenus();   // retour au menu → statut « in the menus »

        _playButton     = GetNode<Button>("VBox/PlayButton");
        _hubButton      = GetNode<Button>("VBox/HubButton");
        _bestiaryButton = GetNode<Button>("VBox/BestiaryButton");
        _arsenalButton  = GetNode<Button>("VBox/ArsenalButton");
        _optionsButton  = GetNode<Button>("VBox/OptionsButton");
        _quitButton     = GetNode<Button>("VBox/QuitButton");
        _fadeOverlay    = GetNode<ColorRect>("FadeOverlay");

        // Les boutons Bestiaire/Arsenal/Options sont ajoutés sans styleboxes dans le .tscn — stylés ici.
        StyleMenuButton(_bestiaryButton);
        StyleMenuButton(_arsenalButton);
        StyleMenuButton(_optionsButton);

        // --- Signaux boutons ---
        _playButton.Pressed     += OnPlayPressed;
        _hubButton.Pressed      += OnHubPressed;
        _bestiaryButton.Pressed += OnBestiaryPressed;
        _arsenalButton.Pressed  += OnArsenalPressed;
        _optionsButton.Pressed  += OnOptionsPressed;
        _quitButton.Pressed     += OnQuitPressed;

        // --- Hover effects ---
        ConnectHoverEffects(_playButton);
        ConnectHoverEffects(_hubButton);
        ConnectHoverEffects(_bestiaryButton);
        ConnectHoverEffects(_arsenalButton);
        ConnectHoverEffects(_optionsButton);
        ConnectHoverEffects(_quitButton);

        ApplyTexts();
        BuildLanguageSelector();

        // --- Musique ---
        AudioSystem.Instance?.PlayMusic("music_menu");

        // --- Fade-in d'entrée : noir → transparent en 0.8 s ---
        var tween = CreateTween();
        tween.TweenProperty(_fadeOverlay, "color:a", 0.0f, 0.8)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);

        _playButton.GrabFocus();

        StartUpdateCheck();
    }

    // -------------------------------------------------------------------------
    // Contrôle de mise à jour — pour les joueurs qui téléchargent le ZIP via le web
    // (ceux de l'app itch.io reçoivent déjà l'auto-update natif de Butler).
    // -------------------------------------------------------------------------

    private void StartUpdateCheck()
    {
        // Lancé via l'app itch.io ? L'auto-update s'en charge déjà — pas de bandeau.
        if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ITCHIO_API_KEY")))
            return;

        var http = new HttpRequest { Timeout = 5 };
        AddChild(http);
        http.RequestCompleted += OnUpdateCheckCompleted;
        // En cas d'échec réseau : on ignore silencieusement (jeu jouable hors-ligne).
        if (http.Request(VersionManifestUrl) != Error.Ok)
            http.QueueFree();
    }

    private void OnUpdateCheckCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        if (result != (long)HttpRequest.Result.Success || responseCode != 200)
            return;

        var json = Json.ParseString(System.Text.Encoding.UTF8.GetString(body));
        if (json.VariantType != Variant.Type.Dictionary)
            return;

        var dict = json.AsGodotDictionary();
        string remote = dict.TryGetValue("version", out var v) ? v.AsString() : "";
        if (string.IsNullOrEmpty(remote))
            return;

        if (dict.TryGetValue("url", out var u) && !string.IsNullOrEmpty(u.AsString()))
            _updateUrl = u.AsString();

        string local = ProjectSettings.GetSetting("application/config/version").AsString();
        if (VersionCompare.IsNewer(remote, local))
            ShowUpdateBanner(remote);
    }

    /// <summary>Bandeau discret en haut de l'écran : « Nouvelle version dispo » + bouton itch.io.</summary>
    private void ShowUpdateBanner(string remoteVersion)
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 0f,
            GrowHorizontal = GrowDirection.Both, GrowVertical = GrowDirection.End,
            OffsetTop = 12f,
            MouseFilter = MouseFilterEnum.Pass,
        };
        var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.12f, 0.95f) };
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(1f, 0.8f, 0.267f); // or #FFCC44
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);

        var label = new Label
        {
            Text = Loc.T("UPDATE_AVAILABLE", remoteVersion),
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.267f));
        row.AddChild(label);

        var btn = new Button { Text = Loc.T("UPDATE_DOWNLOAD") };
        StyleMenuButton(btn);
        ConnectHoverEffects(btn);
        btn.Pressed += () =>
        {
            AudioSystem.Instance?.PlaySfx("sfx_ui_button");
            OS.ShellOpen(_updateUrl);
        };
        row.AddChild(btn);

        AddChild(panel);

        // Apparition en fondu.
        panel.Modulate = new Color(1f, 1f, 1f, 0f);
        var tween = CreateTween();
        tween.TweenProperty(panel, "modulate:a", 1.0f, 0.5)
             .SetEase(Tween.EaseType.Out);
    }

    /// <summary>Applique les libellés traduits aux boutons du menu.</summary>
    private void ApplyTexts()
    {
        _playButton.Text     = Loc.T("MENU_PLAY");
        _hubButton.Text      = Loc.T("MENU_HUB");
        _bestiaryButton.Text = Loc.T("MENU_BESTIARY");
        _arsenalButton.Text  = Loc.T("MENU_ARSENAL");
        _optionsButton.Text  = Loc.T("MENU_OPTIONS");
        _quitButton.Text     = Loc.T("MENU_QUIT");
    }

    /// <summary>Rangée EN/FR/ES en bas de l'écran ; change la langue et ré-applique les textes.</summary>
    private void BuildLanguageSelector()
    {
        var row = new HBoxContainer
        {
            Alignment      = BoxContainer.AlignmentMode.Center,
            AnchorLeft     = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft     = -160f, OffsetRight = 160f, OffsetTop = -52f, OffsetBottom = -14f,
            GrowHorizontal = GrowDirection.Both, GrowVertical = GrowDirection.Begin,
        };
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);

        foreach (var lang in GameSettings.Languages)
        {
            var b = new Button { Text = lang.ToUpper(), CustomMinimumSize = new Vector2(64, 34) };
            string code = lang;
            b.Pressed += () => OnLanguageChosen(code);
            ConnectHoverEffects(b);
            row.AddChild(b);
            _langButtons.Add(b);
        }
        RefreshLanguageButtons();
    }

    private void OnLanguageChosen(string lang)
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        GameSettings.Instance?.SetLanguage(lang);
        ApplyTexts();              // ré-applique les libellés des boutons du menu
        RefreshLanguageButtons();
    }

    /// <summary>Le bouton de la langue active est désactivé (= sélectionné).</summary>
    private void RefreshLanguageButtons()
    {
        string current = GameSettings.Instance?.Language ?? "en";
        foreach (var b in _langButtons)
            b.Disabled = string.Equals(b.Text, current, System.StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Hover effects
    // -------------------------------------------------------------------------

    private void ConnectHoverEffects(Button btn)
    {
        btn.PivotOffset = btn.CustomMinimumSize / 2f;

        var focusStyle = new StyleBoxFlat();
        focusStyle.BgColor = new Color(0.1f, 0.1f, 0.25f, 0.95f);
        focusStyle.SetBorderWidthAll(3);
        focusStyle.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
        focusStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("focus", focusStyle);

        btn.MouseEntered += () => OnButtonMouseEntered(btn);
        btn.MouseExited  += () => OnButtonMouseExited(btn);
        btn.FocusEntered += () => OnButtonFocusEntered(btn);
        btn.FocusExited  += () => OnButtonMouseExited(btn);
    }

    private void OnButtonMouseEntered(Button btn)
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");

        btn.PivotOffset = btn.Size / 2f;

        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    private void OnButtonFocusEntered(Button btn)
    {
        btn.PivotOffset = btn.Size / 2f;
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    private void OnButtonMouseExited(Button btn)
    {
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", Vector2.One, 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private void OnPlayPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        // « Jouer » mène d'abord au choix du personnage, puis au choix du niveau.
        TransitionTo("res://scenes/ui/CharacterSelectScreen.tscn");
    }

    private void OnHubPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/ui/HubScreen.tscn");
    }

    private void OnBestiaryPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/ui/BestiaryScreen.tscn");
    }

    private void OnArsenalPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/ui/ArsenalScreen.tscn");
    }

    private void OnOptionsPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/ui/OptionsScreen.tscn");
    }

    /// <summary>Applique les 3 styleboxes (normal/hover/pressed) cohérentes avec les autres boutons.</summary>
    private static void StyleMenuButton(Button btn)
    {
        btn.AddThemeStyleboxOverride("normal",  BtnStyle(2, new Color(0.267f, 1f, 0.933f, 0.8f), 0.85f));
        btn.AddThemeStyleboxOverride("hover",   BtnStyle(3, new Color(0.667f, 0.267f, 1f), 0.95f));
        btn.AddThemeStyleboxOverride("pressed", BtnStyle(3, new Color(0.667f, 0.267f, 1f), 1f));
    }

    private static StyleBoxFlat BtnStyle(int border, Color borderCol, float bgA)
    {
        var s = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.12f, bgA) };
        s.SetBorderWidthAll(border);
        s.BorderColor = borderCol;
        s.SetCornerRadiusAll(4);
        return s;
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            OnQuitPressed();
        }
    }

    // -------------------------------------------------------------------------
    // Transition de sortie : fade noir → changement de scène
    // -------------------------------------------------------------------------

    private void TransitionTo(string scenePath)
    {
        // Bloque les interactions pendant le fade
        _playButton.Disabled     = true;
        _hubButton.Disabled      = true;
        _bestiaryButton.Disabled = true;
        _arsenalButton.Disabled  = true;
        _optionsButton.Disabled  = true;
        _quitButton.Disabled     = true;

        var tween = CreateTween();
        tween.TweenProperty(_fadeOverlay, "color:a", 1.0f, 0.4)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile(scenePath)));
    }
}
