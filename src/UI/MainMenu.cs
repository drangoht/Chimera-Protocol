using Godot;

/// <summary>
/// Menu principal — fade-in d'entrée, hover effects, transition de sortie.
/// </summary>
public partial class MainMenu : Control
{
    private Button    _playButton     = null!;
    private Button    _hubButton      = null!;
    private Button    _codexButton    = null!;
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
        _codexButton    = GetNode<Button>("VBox/CodexButton");
        _optionsButton  = GetNode<Button>("VBox/OptionsButton");
        _quitButton     = GetNode<Button>("VBox/QuitButton");
        _fadeOverlay    = GetNode<ColorRect>("FadeOverlay");

        // Les boutons Codex/Options sont ajoutés sans styleboxes dans le .tscn — stylés ici.
        StyleMenuButton(_codexButton);
        StyleMenuButton(_optionsButton);

        // --- Signaux boutons ---
        _playButton.Pressed     += OnPlayPressed;
        _hubButton.Pressed      += OnHubPressed;
        _codexButton.Pressed    += OnCodexPressed;
        _optionsButton.Pressed  += OnOptionsPressed;
        _quitButton.Pressed     += OnQuitPressed;

        // --- Hover effects ---
        ConnectHoverEffects(_playButton);
        ConnectHoverEffects(_hubButton);
        ConnectHoverEffects(_codexButton);
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
        _codexButton.Text    = TextOr("MENU_CODEX", "Codex");
        _optionsButton.Text  = Loc.T("MENU_OPTIONS");
        ApplyTitleFlair();
        _quitButton.Text     = Loc.T("MENU_QUIT");
    }

    /// <summary>Rangée de drapeaux EN/FR/ES en haut à droite de l'écran ; change la langue et
    /// ré-applique les textes. La langue active est surlignée (liseré or).</summary>
    private void BuildLanguageSelector()
    {
        var row = new HBoxContainer
        {
            AnchorLeft     = 1f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft     = -172f, OffsetRight = -16f, OffsetTop = 16f, OffsetBottom = 52f,
            GrowHorizontal = GrowDirection.Begin, GrowVertical = GrowDirection.End,
        };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);

        foreach (var lang in GameSettings.Languages)
        {
            var b = new Button
            {
                CustomMinimumSize = new Vector2(44, 30),
                TooltipText       = lang.ToUpper(),
                ExpandIcon        = true,
                IconAlignment     = HorizontalAlignment.Center,
            };
            string flagPath = $"res://assets/sprites/ui/flag_{lang}.png";
            if (ResourceLoader.Exists(flagPath)) b.Icon = GD.Load<Texture2D>(flagPath);
            else b.Text = lang.ToUpper();   // repli si le drapeau manque
            b.TextureFilter = TextureFilterEnum.Nearest;
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

    /// <summary>Surligne le drapeau de la langue active (liseré or) ; les autres restent neutres.</summary>
    private void RefreshLanguageButtons()
    {
        string current = GameSettings.Instance?.Language ?? "en";
        for (int i = 0; i < _langButtons.Count && i < GameSettings.Languages.Length; i++)
        {
            bool active = string.Equals(GameSettings.Languages[i], current, System.StringComparison.OrdinalIgnoreCase);
            var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.16f, active ? 0.95f : 0.5f) };
            style.SetBorderWidthAll(active ? 3 : 1);
            style.BorderColor = active ? new Color(1f, 0.8f, 0.267f) : new Color(0.5f, 0.5f, 0.6f, 0.7f);
            style.SetCornerRadiusAll(3); style.SetContentMarginAll(3);
            _langButtons[i].AddThemeStyleboxOverride("normal", style);
        }
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

    private void OnCodexPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/ui/CodexMenuScreen.tscn");
    }

    /// <summary>Affiche le titre cosmétique équipé (débloqué via les Défis, choisi au Hub) sous le logo.
    /// Masqué si aucun titre équipé ou si le nœud est absent (robustesse).</summary>
    private void ApplyTitleFlair()
    {
        var flair = GetNodeOrNull<Label>("TitleFlair");
        if (flair == null) return;

        string id = MetaProgressionSystem.Instance?.Meta.EquippedCosmetic ?? "";
        var def = id.Length > 0 ? Titles.ById(id) : null;
        if (def == null) { flair.Visible = false; return; }

        flair.Text    = $"— {Loc.T(def.NameKey)} —";
        flair.Visible = true;
    }

    /// <summary>Loc.T avec repli si la clé n'est pas encore traduite (nouvelle clé de menu).</summary>
    private static string TextOr(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
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
        _codexButton.Disabled    = true;
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
