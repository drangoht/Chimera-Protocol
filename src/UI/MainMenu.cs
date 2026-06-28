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

    public override void _Ready()
    {
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

        // --- Musique ---
        AudioSystem.Instance?.PlayMusic("music_menu");

        // --- Fade-in d'entrée : noir → transparent en 0.8 s ---
        var tween = CreateTween();
        tween.TweenProperty(_fadeOverlay, "color:a", 0.0f, 0.8)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);

        _playButton.GrabFocus();
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
        TransitionTo("res://scenes/ui/LevelSelectScreen.tscn");
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
