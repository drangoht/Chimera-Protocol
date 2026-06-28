using Godot;

/// <summary>
/// Écran Options : volumes (master/musique/SFX), plein écran, screen shake.
/// Lit/écrit via <see cref="GameSettings"/> (appliqué + persisté immédiatement).
/// UI construite en code ; la scène = root Control + script. Retour : Échap / bouton.
/// </summary>
public partial class OptionsScreen : Control
{
    private static readonly Color Bg     = new(0.06f, 0.06f, 0.11f);
    private static readonly Color Cyan   = new(0.267f, 1f, 0.933f);
    private static readonly Color Violet = new(0.667f, 0.267f, 1f);
    private static readonly Color Text   = new(0.85f, 0.85f, 0.95f);

    private ColorRect _fade  = null!;
    private bool      _leaving = false;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // Conteneur central
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        vbox.AddThemeConstantOverride("separation", 18);
        center.AddChild(vbox);

        var title = new Label
        {
            Text                = "OPTIONS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", Cyan);
        vbox.AddChild(title);
        vbox.AddChild(new HSeparator());

        var s = GameSettings.Instance;
        AddSlider(vbox, "Volume général", s?.Master ?? 1f, v => GameSettings.Instance?.SetMaster(v));
        AddSlider(vbox, "Musique",        s?.Music  ?? 0.8f, v => GameSettings.Instance?.SetMusic(v));
        AddSlider(vbox, "Effets (SFX)",   s?.Sfx    ?? 0.9f, v => GameSettings.Instance?.SetSfx(v));
        AddToggle(vbox, "Plein écran",    s?.Fullscreen ?? false, v => GameSettings.Instance?.SetFullscreen(v));
        AddToggle(vbox, "Secousses écran", s?.ShakeEnabled ?? true, v => GameSettings.Instance?.SetShake(v));
        AddDifficulty(vbox, s?.Difficulty ?? GameSettings.GameDifficulty.Normal);

        vbox.AddChild(new HSeparator());

        var back = new Button { Text = "Retour", CustomMinimumSize = new Vector2(200, 48) };
        StyleButton(back);
        back.Pressed += GoBack;
        var backWrap = new CenterContainer();
        backWrap.AddChild(back);
        vbox.AddChild(backWrap);

        // Fondu d'entrée
        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        t.TweenCallback(Callable.From(() => back.GrabFocus()));
    }

    private void AddSlider(VBoxContainer parent, string label, float value, System.Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = 0, MaxValue = 1, Step = 0.05,
            Value = value,
            CustomMinimumSize = new Vector2(240, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(slider);

        var val = new Label { Text = $"{value * 100:0} %", CustomMinimumSize = new Vector2(56, 0),
                              HorizontalAlignment = HorizontalAlignment.Right };
        val.AddThemeColorOverride("font_color", Cyan);
        row.AddChild(val);

        slider.ValueChanged += d =>
        {
            val.Text = $"{d * 100:0} %";
            onChange((float)d);
        };
        parent.AddChild(row);
    }

    private void AddDifficulty(VBoxContainer parent, GameSettings.GameDifficulty value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = "Difficulté", CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var opt = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        opt.AddItem("Facile");
        opt.AddItem("Normal");
        opt.AddItem("Difficile");
        opt.Selected = (int)value;
        opt.ItemSelected += idx =>
            GameSettings.Instance?.SetDifficulty((GameSettings.GameDifficulty)idx);
        row.AddChild(opt);
        parent.AddChild(row);
    }

    private void AddToggle(VBoxContainer parent, string label, bool value, System.Action<bool> onChange)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var check = new CheckButton { ButtonPressed = value };
        check.Toggled += pressed => onChange(pressed);
        row.AddChild(check);
        parent.AddChild(row);
    }

    private void StyleButton(Button btn)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.2f, 0.9f) };
        normal.SetBorderWidthAll(2); normal.BorderColor = Cyan; normal.SetCornerRadiusAll(6);
        var hover = new StyleBoxFlat { BgColor = new Color(0.16f, 0.12f, 0.3f, 0.95f) };
        hover.SetBorderWidthAll(3); hover.BorderColor = Violet; hover.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Text);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            GoBack();
        }
    }

    private void GoBack()
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 1f, 0.3);
        t.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")));
    }
}
