using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran Options, organisé en sections : Audio (master/musique/SFX), Affichage (mode de fenêtre,
/// résolution, VSync, limite et compteur d'IPS), Jeu (difficulté, secousses, flashs, vibration),
/// Interface (langue, tampon de version, Discord) et Contrôles (remap ZQSD + dash).
/// Lit/écrit via <see cref="GameSettings"/> (appliqué + persisté immédiatement).
/// UI construite en code ; la scène = root Control + script. Retour : Échap / bouton.
///
/// Deux usages :
/// - écran plein (depuis le menu principal) — le retour recharge <c>MainMenu.tscn</c> ;
/// - surcouche modale (depuis le menu pause, via <see cref="OpenOverlay"/>) — le retour libère
///   la surcouche et rend la main à l'appelant, sans toucher à la scène de run.
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

    // ── Mode surcouche (ouvert depuis le menu pause) ──────────────────────────
    private bool             _overlay  = false;
    private System.Action?   _onClosed;

    /// <summary>
    /// Ouvre les options en surcouche au-dessus de la scène courante (menu pause en run) :
    /// aucun changement de scène, l'arbre reste en pause. <paramref name="onClosed"/> est
    /// appelé après la fermeture — l'appelant y reprend la main (input, rafraîchissement).
    /// </summary>
    public static void OpenOverlay(Node context, System.Action? onClosed = null)
    {
        var scene  = GD.Load<PackedScene>("res://scenes/ui/OptionsScreen.tscn");
        var screen = scene.Instantiate<OptionsScreen>();
        // Champs posés AVANT l'entrée dans l'arbre : _Ready() en dépend.
        screen._overlay  = true;
        screen._onClosed = onClosed;

        // Layer au-dessus du PauseScreen (100). ProcessMode.Always : sans ça, rien ne
        // répondrait — l'arbre est en pause pendant tout l'affichage.
        var layer = new CanvasLayer { Layer = 110, ProcessMode = ProcessModeEnum.Always };
        layer.AddChild(screen);
        context.GetTree().Root.AddChild(layer);
    }

    public override void _Ready()
    {
        if (_overlay) ProcessMode = ProcessModeEnum.Always;
        Build();
    }

    /// <summary>Reconstruit l'écran à neuf (changement de langue en surcouche, où l'on ne peut
    /// pas recharger la scène courante sans tuer la run).</summary>
    private void Rebuild()
    {
        _listeningAction = null;
        _resetArmed      = false;
        _resetButton     = null;
        _rebindButtons.Clear();
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        Build();
    }

    private void Build()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Fond opaque en écran plein ; voile semi-transparent en surcouche (le menu pause
        // doit rester perceptible dessous).
        var bg = new ColorRect { Color = _overlay ? new Color(0f, 0f, 0f, 0.85f) : Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // Conteneur défilable (le contenu dépasse largement la hauteur en 720p) —
        // FollowFocus garde l'élément focalisé visible en nav clavier.
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
        // ShrinkBegin, pas ShrinkCenter : le contenu des options dépasse la hauteur de l'écran,
        // et un centrage vertical dans le ScrollContainer faisait démarrer la vue au milieu de la
        // liste — on ouvrait l'écran sur la section « Contrôles », le début hors champ.
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
        panel.AddThemeStyleboxOverride("panel", UiStyle.ScreenPanel());
        hcenter.AddChild(panel);

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(600, 0) };
        vbox.AddThemeConstantOverride("separation", 14);
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

        // ── Audio ─────────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "OPTIONS_SECTION_AUDIO");
        AddSlider(vbox, Loc.T("OPTIONS_MASTER"), s?.Master ?? 1f, v => GameSettings.Instance?.SetMaster(v));
        AddSlider(vbox, Loc.T("OPTIONS_MUSIC"),  s?.Music  ?? 0.8f, v => GameSettings.Instance?.SetMusic(v));
        AddSlider(vbox, Loc.T("OPTIONS_SFX"),    s?.Sfx    ?? 0.9f, v => GameSettings.Instance?.SetSfx(v));

        // ── Affichage ─────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "OPTIONS_SECTION_DISPLAY");
        AddDisplaySettings(vbox, s);

        // ── Jeu / confort ─────────────────────────────────────────────────────
        AddSectionHeader(vbox, "OPTIONS_SECTION_GAMEPLAY");
        AddDifficulty(vbox, s?.Difficulty ?? GameSettings.GameDifficulty.Normal);
        AddSlider(vbox, Loc.T("OPTIONS_SHAKE"), s?.ShakeIntensity ?? 1f,
                  v => GameSettings.Instance?.SetShakeIntensity(v));
        AddToggle(vbox, Loc.T("OPTIONS_REDUCE_FLASHES"), s?.ReduceFlashes ?? false,
                  v => GameSettings.Instance?.SetReduceFlashes(v));
        AddSlider(vbox, Loc.T("OPTIONS_RUMBLE"), s?.Rumble ?? 0.7f,
                  v => GameSettings.Instance?.SetRumble(v));

        // ── Interface ─────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "OPTIONS_SECTION_INTERFACE");
        AddLanguage(vbox, s?.Language ?? "en");
        AddToggle(vbox, Loc.T("OPTIONS_VERSION_STAMP"), s?.ShowVersionStamp ?? true,
                  v => GameSettings.Instance?.SetShowVersionStamp(v));
        AddToggle(vbox, Loc.T("OPTIONS_DISCORD"), s?.DiscordEnabled ?? true,
                  v => GameSettings.Instance?.SetDiscordEnabled(v));

        // ── Contrôles ─────────────────────────────────────────────────────────
        AddControls(vbox);

        // Réinitialisation totale : jamais proposée en pleine run (destructif, et la run en
        // cours écrirait par-dessus la remise à zéro à sa fin).
        if (!_overlay)
        {
            vbox.AddChild(UiStyle.Separator(UiPalette.Cyan));
            AddResetButton(vbox);
        }
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
        // En surcouche l'arbre est en pause : sans ce mode, le tween — donc le focus initial —
        // ne démarrerait jamais.
        if (_overlay) t.SetPauseMode(Tween.TweenPauseMode.Process);
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        // Le focus va au PREMIER réglage, pas au bouton Retour : avec FollowFocus, focaliser un
        // contrôle situé tout en bas fait défiler la liste dès l'ouverture et l'écran s'affichait
        // au milieu de la section « Contrôles », son début hors champ. Repli sur Retour si aucun
        // réglage n'est focalisable.
        t.TweenCallback(Callable.From(() => (FirstFocusable(vbox) ?? back).GrabFocus()));
    }

    /// <summary>Titre de section + séparateur (règle du brief §3.6).</summary>
    private static void AddSectionHeader(VBoxContainer parent, string labelKey)
    {
        var header = new Label
        {
            Text                = Loc.T(labelKey),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        header.AddThemeFontSizeOverride("font_size", 22);
        header.AddThemeColorOverride("font_color", Cyan);
        parent.AddChild(header);
        parent.AddChild(UiStyle.Separator(UiPalette.Cyan));
    }

    /// <summary>Premier contrôle réellement focalisable de l'arbre, en profondeur d'abord.</summary>
    private static Control? FirstFocusable(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control c && c.FocusMode != Control.FocusModeEnum.None && c.Visible)
                return c;
            if (child is Node node && FirstFocusable(node) is { } found)
                return found;
        }
        return null;
    }

    // ── Lignes de réglage ─────────────────────────────────────────────────────

    /// <summary>Ligne « libellé + contrôle » : gabarit commun à tous les réglages.</summary>
    private static HBoxContainer AddRow(VBoxContainer parent, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(240, 0) };
        lbl.AddThemeColorOverride("font_color", Text);
        row.AddChild(lbl);

        parent.AddChild(row);
        return row;
    }

    private void AddSlider(VBoxContainer parent, string label, float value, System.Action<float> onChange)
    {
        var row = AddRow(parent, label);

        var slider = new HSlider
        {
            MinValue = 0, MaxValue = 1, Step = 0.05,
            Value = value,
            CustomMinimumSize = new Vector2(240, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        UiStyle.ApplySliderStyles(slider);
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
    }

    /// <summary>Liste déroulante « libellé + choix », gabarit commun (difficulté, langue, affichage).</summary>
    private OptionButton AddDropdown(VBoxContainer parent, string label, string[] items,
                                     int selected, System.Action<int> onSelected)
    {
        var row = AddRow(parent, label);

        // 220 px : le plus long libellé (« Fenêtré » / « Sans bordure » / « Plein écran »)
        // doit rester en deçà de la flèche du dropdown.
        var opt = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        UiStyle.ApplyDropdownFrames(opt);   // liste déroulante : la flèche doit rester en deçà du liseré
        foreach (string item in items) opt.AddItem(item);
        opt.Selected = Mathf.Clamp(selected, 0, items.Length - 1);
        opt.ItemSelected += idx => onSelected((int)idx);
        row.AddChild(opt);
        return opt;
    }

    private void AddDifficulty(VBoxContainer parent, GameSettings.GameDifficulty value)
    {
        // Depuis la 1.25.0, ce réglage ne porte plus que l'ASSISTANCE : le challenge passe par
        // la saturation, choisie à l'écran de sélection de niveau (docs/ENDGAME_PLAN.md §7.1). « Difficile »
        // n'est donc plus proposé — il vaut désormais la saturation 1, et le laisser ici ferait cohabiter
        // deux axes de difficulté qui se cumuleraient en silence.
        //
        // Une sauvegarde d'avant la 1.25.0 est migrée au chargement (Normal + saturation 1), donc `value`
        // ne vaut jamais Difficile ici ; le clamp protège quand même l'index de la liste déroulante.
        int selected = Mathf.Min((int)value, (int)GameSettings.GameDifficulty.Normal);
        var opt = AddDropdown(parent, Loc.T("OPTIONS_DIFFICULTY"),
            new[] { Loc.T("DIFF_EASY"), Loc.T("DIFF_NORMAL") },
            selected,
            idx => GameSettings.Instance?.SetDifficulty((GameSettings.GameDifficulty)idx));

        // En pleine run, la difficulté est déjà engagée (scaling des ennemis, high score) :
        // la changer à chaud fausserait la partie en cours.
        if (_overlay) opt.Disabled = true;
    }

    private void AddLanguage(VBoxContainer parent, string current)
    {
        var codes = GameSettings.Languages;
        var names = new string[codes.Length];
        for (int i = 0; i < codes.Length; i++) names[i] = codes[i].ToUpper();

        AddDropdown(parent, Loc.T("OPTIONS_LANGUAGE"), names,
            System.Math.Max(0, System.Array.IndexOf(codes, current)),
            idx =>
            {
                GameSettings.Instance?.SetLanguage(codes[idx]);
                // Écran plein : rechargement de la scène. En surcouche, recharger la scène
                // courante tuerait la run — on se reconstruit sur place.
                if (_overlay) Rebuild();
                else          GetTree().ReloadCurrentScene();
            });
    }

    private void AddToggle(VBoxContainer parent, string label, bool value, System.Action<bool> onChange)
    {
        var row = AddRow(parent, label);

        var check = new CheckButton { ButtonPressed = value };
        UiStyle.ApplyToggleStyles(check);
        check.Toggled += pressed => onChange(pressed);
        row.AddChild(check);
    }

    // ── Affichage (fenêtre, résolution, VSync, IPS) ───────────────────────────

    private void AddDisplaySettings(VBoxContainer parent, GameSettings? s)
    {
        var mode = s?.DisplayMode ?? GameSettings.WindowMode.Windowed;

        // La résolution ne concerne QUE le mode fenêtré : elle est grisée dans les deux autres
        // (le plein écran fenêtré prend la taille de l'écran, le plein écran celle du moniteur).
        OptionButton? resolution = null;

        AddDropdown(parent, Loc.T("OPTIONS_DISPLAY_MODE"),
            new[] { Loc.T("OPTIONS_DISPLAY_WINDOWED"),
                    Loc.T("OPTIONS_DISPLAY_BORDERLESS"),
                    Loc.T("OPTIONS_DISPLAY_FULLSCREEN") },
            (int)mode,
            idx =>
            {
                var picked = (GameSettings.WindowMode)idx;
                GameSettings.Instance?.SetDisplayMode(picked);
                if (resolution != null) resolution.Disabled = picked != GameSettings.WindowMode.Windowed;
            });

        var sizes = GameSettings.Resolutions;
        var names = new string[sizes.Length];
        for (int i = 0; i < sizes.Length; i++) names[i] = $"{sizes[i].X} × {sizes[i].Y}";

        resolution = AddDropdown(parent, Loc.T("OPTIONS_RESOLUTION"), names,
            System.Math.Max(0, System.Array.IndexOf(sizes, s?.WindowSize ?? sizes[0])),
            idx => GameSettings.Instance?.SetWindowSize(sizes[idx]));
        resolution.Disabled = mode != GameSettings.WindowMode.Windowed;

        AddToggle(parent, Loc.T("OPTIONS_VSYNC"), s?.VSync ?? true,
                  v => GameSettings.Instance?.SetVSync(v));

        var limits = GameSettings.FpsLimits;
        var limitNames = new string[limits.Length];
        for (int i = 0; i < limits.Length; i++)
            limitNames[i] = limits[i] == 0 ? Loc.T("OPTIONS_FPS_UNLIMITED") : limits[i].ToString();

        AddDropdown(parent, Loc.T("OPTIONS_FPS_LIMIT"), limitNames,
            System.Math.Max(0, System.Array.IndexOf(limits, s?.MaxFps ?? 0)),
            idx => GameSettings.Instance?.SetMaxFps(limits[idx]));

        AddToggle(parent, Loc.T("OPTIONS_SHOW_FPS"), s?.ShowFps ?? false,
                  v => GameSettings.Instance?.SetShowFps(v));
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
        AddSectionHeader(parent, "OPTIONS_CONTROLS");

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
        var row = AddRow(parent, Loc.T(labelKey));

        var btn = new Button { CustomMinimumSize = new Vector2(200, 40) };
        StyleButton(btn);
        btn.Pressed += () => StartListening(action);
        _rebindButtons[action] = btn;
        row.AddChild(btn);

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
        if (_overlay) t.SetPauseMode(Tween.TweenPauseMode.Process);
        t.TweenProperty(_fade, "color:a", 1f, _overlay ? 0.15 : 0.3);
        t.TweenCallback(Callable.From(_overlay ? CloseOverlay : ReturnToMenu));
    }

    private void ReturnToMenu() => GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");

    /// <summary>Libère la surcouche (et son CanvasLayer porteur), puis rend la main à l'appelant.</summary>
    private void CloseOverlay()
    {
        var callback = _onClosed;
        _onClosed = null;
        // Le CanvasLayer a été créé par OpenOverlay pour ce seul écran : il part avec lui.
        Node carrier = GetParent() is CanvasLayer layer ? layer : this;
        carrier.QueueFree();
        callback?.Invoke();
    }
}
