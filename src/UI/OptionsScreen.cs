using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran Options : volumes (master/musique/SFX), plein écran, screen shake, langue,
/// difficulté et remap des touches de déplacement (ZQSD par défaut, rebindables).
/// Lit/écrit via <see cref="GameSettings"/> (appliqué + persisté immédiatement).
/// UI construite en code ; la scène = root Control + script. Retour : Échap / bouton.
/// </summary>
public partial class OptionsScreen : Control
{
    private static readonly Color Bg   = UiPalette.BgDeep;
    private static readonly Color Cyan = UiPalette.Cyan;
    private static readonly Color Text = UiPalette.OffWhite;

    private ColorRect _fade  = null!;
    private bool      _leaving = false;

    private Button? _resetButton;
    private bool    _resetArmed = false;

    // Remap clavier : action en cours d'écoute (null = aucune) + boutons par action.
    private string? _listeningAction;
    private readonly Dictionary<string, Button> _rebindButtons = new();

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // Conteneur défilable (le contenu dépasse la hauteur en 720p depuis l'ajout
        // de la section Contrôles) — FollowFocus garde l'élément focalisé visible en nav clavier.
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.FollowFocus = true;
        AddChild(scroll);

        // Centrage horizontal du panneau à largeur fixe, tout en laissant la hauteur défiler.
        var hcenter = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
                                          Alignment = BoxContainer.AlignmentMode.Center };
        scroll.AddChild(hcenter);

        // Panneau de fond (ART_BRIEF_UI_FRAMES §3.3) : l'écran n'avait aucun contenant, les
        // réglages flottaient directement sur le fond. Le panneau leur donne une matière.
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", UiStyle.ScreenPanel());
        hcenter.AddChild(panel);

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        vbox.AddThemeConstantOverride("separation", 18);
        panel.AddChild(vbox);

        var title = new Label
        {
            Text                = Loc.T("OPTIONS_TITLE"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", Cyan);
        vbox.AddChild(title);
        vbox.AddChild(UiStyle.ScreenTitleUnderline(UiPalette.Cyan));

        var s = GameSettings.Instance;
        AddSlider(vbox, Loc.T("OPTIONS_MASTER"), s?.Master ?? 1f, v => GameSettings.Instance?.SetMaster(v));
        AddSlider(vbox, Loc.T("OPTIONS_MUSIC"),  s?.Music  ?? 0.8f, v => GameSettings.Instance?.SetMusic(v));
        AddSlider(vbox, Loc.T("OPTIONS_SFX"),    s?.Sfx    ?? 0.9f, v => GameSettings.Instance?.SetSfx(v));
        AddToggle(vbox, Loc.T("OPTIONS_FULLSCREEN"), s?.Fullscreen ?? false, v => GameSettings.Instance?.SetFullscreen(v));
        AddToggle(vbox, Loc.T("OPTIONS_SHAKE"), s?.ShakeEnabled ?? true, v => GameSettings.Instance?.SetShake(v));
        AddDifficulty(vbox, s?.Difficulty ?? GameSettings.GameDifficulty.Normal);
        AddLanguage(vbox, s?.Language ?? "en");

        vbox.AddChild(UiStyle.Separator(UiPalette.Cyan));
        AddControls(vbox);

        vbox.AddChild(UiStyle.Separator(UiPalette.Cyan));
        AddResetButton(vbox);
        vbox.AddChild(UiStyle.Separator(UiPalette.Cyan));

        var back = new Button { Text = Loc.T("COMMON_BACK"), CustomMinimumSize = new Vector2(200, 48) };
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

        var lbl = new Label { Text = Loc.T("OPTIONS_DIFFICULTY"), CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var opt = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        StyleButton(opt);          // sans ça, le seul contrôle de l'écran resté au thème Godot par défaut
        opt.AddItem(Loc.T("DIFF_EASY"));
        opt.AddItem(Loc.T("DIFF_NORMAL"));
        opt.AddItem(Loc.T("DIFF_HARD"));
        opt.Selected = (int)value;
        opt.ItemSelected += idx =>
            GameSettings.Instance?.SetDifficulty((GameSettings.GameDifficulty)idx);
        row.AddChild(opt);
        parent.AddChild(row);
    }

    private void AddLanguage(VBoxContainer parent, string current)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = Loc.T("OPTIONS_LANGUAGE"), CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var opt = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        StyleButton(opt);
        foreach (var l in GameSettings.Languages) opt.AddItem(l.ToUpper());
        opt.Selected = System.Math.Max(0, System.Array.IndexOf(GameSettings.Languages, current));
        opt.ItemSelected += idx =>
        {
            GameSettings.Instance?.SetLanguage(GameSettings.Languages[idx]);
            GetTree().ReloadCurrentScene(); // recharge l'écran pour appliquer la langue
        };
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

    // ── Remap des touches (déplacement ZQSD + dash) ───────────────────────────
    private static readonly (string Action, string LabelKey)[] MoveRows =
    {
        (InputRemap.Up,    "OPTIONS_MOVE_UP"),
        (InputRemap.Down,  "OPTIONS_MOVE_DOWN"),
        (InputRemap.Left,  "OPTIONS_MOVE_LEFT"),
        (InputRemap.Right, "OPTIONS_MOVE_RIGHT"),
        (InputRemap.Dash,  "OPTIONS_DASH"),
    };

    /// <summary>Touche clavier actuelle d'une action rebindable (déplacement ou dash).</summary>
    private static Key KeyForAction(string action) => action == InputRemap.Dash
        ? (GameSettings.Instance?.DashKey ?? InputRemap.DefaultDashKey)
        : (GameSettings.Instance?.MoveKey(action) ?? InputRemap.DefaultKeys[action]);

    /// <summary>Réaffecte la touche d'une action rebindable (déplacement ou dash).</summary>
    private static void AssignKey(string action, Key key)
    {
        if (action == InputRemap.Dash) GameSettings.Instance?.SetDashKey(key);
        else                           GameSettings.Instance?.SetMoveKey(action, key);
    }

    private void AddControls(VBoxContainer parent)
    {
        var header = new Label
        {
            Text                = Loc.T("OPTIONS_CONTROLS"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        header.AddThemeFontSizeOverride("font_size", 22);
        header.AddThemeColorOverride("font_color", Cyan);
        parent.AddChild(header);
        // Règle du brief §3.6 : tout titre de section est systématiquement suivi du séparateur.
        parent.AddChild(UiStyle.Separator(UiPalette.Cyan));

        foreach (var (action, labelKey) in MoveRows)
            AddRebindRow(parent, action, labelKey);

        var reset = new Button { Text = Loc.T("OPTIONS_CONTROLS_RESET"), CustomMinimumSize = new Vector2(280, 40) };
        StyleButton(reset);
        reset.Pressed += () =>
        {
            AudioSystem.Instance?.PlaySfx("sfx_ui_button");
            GameSettings.Instance?.ResetMoveKeys();
            RefreshRebindLabels();
        };
        var wrap = new CenterContainer();
        wrap.AddChild(reset);
        parent.AddChild(wrap);
    }

    private void AddRebindRow(VBoxContainer parent, string action, string labelKey)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = Loc.T(labelKey), CustomMinimumSize = new Vector2(220, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        var btn = new Button { CustomMinimumSize = new Vector2(180, 40) };
        StyleButton(btn);
        btn.Pressed += () => StartListening(action);
        _rebindButtons[action] = btn;
        row.AddChild(btn);

        parent.AddChild(row);
        RefreshRebindLabel(action);
    }

    /// <summary>Passe le bouton d'une action en attente de la prochaine touche pressée.</summary>
    private void StartListening(string action)
    {
        if (_listeningAction != null) return;   // déjà en écoute sur une autre action
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        _listeningAction = action;
        if (_rebindButtons.TryGetValue(action, out var btn))
            btn.Text = Loc.T("OPTIONS_CONTROLS_PRESS");
    }

    private void RefreshRebindLabels()
    {
        foreach (var action in _rebindButtons.Keys) RefreshRebindLabel(action);
    }

    private void RefreshRebindLabel(string action)
    {
        if (!_rebindButtons.TryGetValue(action, out var btn)) return;
        btn.Text = InputRemap.KeyName(KeyForAction(action));
    }

    // ── Réinitialisation totale (état initial du jeu, Échos inclus) ───────────
    private static readonly Color Danger = UiPalette.Danger;

    private void AddResetButton(VBoxContainer parent)
    {
        _resetButton = new Button { Text = Loc.T("OPTIONS_RESET"), CustomMinimumSize = new Vector2(380, 44) };
        // Accent ambre sourd (§3.0) : l'action destructrice se distingue du reste par le liseré,
        // pas seulement par la couleur du texte.
        StyleButton(_resetButton, UiStyle.FrameAccent.Danger);
        _resetButton.AddThemeColorOverride("font_color", Danger);   // action destructrice
        _resetButton.Pressed += OnResetPressed;
        var wrap = new CenterContainer();
        wrap.AddChild(_resetButton);
        parent.AddChild(wrap);
    }

    private void OnResetPressed()
    {
        if (_resetButton == null) return;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");

        if (!_resetArmed)
        {
            // 1er clic : armement (confirmation requise — action irréversible).
            _resetArmed = true;
            _resetButton.Text = Loc.T("OPTIONS_RESET_CONFIRM");
            return;
        }

        // 2e clic : réinitialisation TOTALE (Échos + améliorations + progression).
        MetaProgressionSystem.Instance?.HardReset();
        GameSettings.Instance?.ResetProgress();
        _resetArmed = false;
        _resetButton.Text     = Loc.T("OPTIONS_RESET_DONE");
        _resetButton.Disabled = true;
    }

    /// <summary>Cadre « plaque blindée » du §3.2 + couleur de texte de l'écran. Toute la recette de
    /// StyleBox vit dans <see cref="UiStyle.ApplyButtonFrames(Button, UiStyle.FrameAccent)"/> —
    /// ici on ne fait que choisir l'accent de catégorie.</summary>
    private static void StyleButton(Button btn, UiStyle.FrameAccent accent = UiStyle.FrameAccent.Cyan)
    {
        UiStyle.ApplyButtonFrames(btn, accent);
        btn.AddThemeColorOverride("font_color", Text);
    }

    public override void _Input(InputEvent @event)
    {
        // Capture de touche pour le remap : intercepte AVANT la nav UI (_Input passe en premier).
        if (_listeningAction == null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        GetViewport().SetInputAsHandled();
        string action = _listeningAction;
        _listeningAction = null;

        // Échap = annuler l'assignation (on garde la touche actuelle).
        if (key.Keycode != Key.Escape)
        {
            Key chosen = key.Keycode != Key.None ? key.Keycode : (Key)key.PhysicalKeycode;
            AssignKey(action, chosen);
        }
        RefreshRebindLabel(action);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_listeningAction != null) return;   // en écoute de remap : ignorer le retour
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
