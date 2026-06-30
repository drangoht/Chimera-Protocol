using Godot;

/// <summary>
/// Écran de choix du niveau (biome) après « Jouer ». Liste les biomes avec aperçu
/// (tuile en damier), nom, effet et description. Sélectionner un biome le force pour
/// la run ; « Aléatoire » laisse le tirage au sort. UI construite en code.
/// </summary>
public partial class LevelSelectScreen : Control
{
    private static readonly Color Bg     = new(0.06f, 0.06f, 0.11f);
    private static readonly Color Cyan   = new(0.267f, 1f, 0.933f);
    private static readonly Color Violet = new(0.667f, 0.267f, 1f);
    private static readonly Color Text   = new(0.85f, 0.85f, 0.95f);
    private static readonly Color Dim    = new(0.6f, 0.62f, 0.72f);

    private ColorRect _fade   = null!;
    private bool      _leaving = false;
    private Button?   _firstPlay;   // bouton « Jouer ici » du 1er biome (présélectionné)
    private ScrollContainer _scroll = null!;
    private readonly System.Collections.Generic.List<Button> _playButtons = new();

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 12);
        root.OffsetLeft = 80; root.OffsetRight = -80; root.OffsetTop = 30; root.OffsetBottom = -24;
        AddChild(root);

        var title = new Label { Text = Loc.T("LEVELSEL_TITLE"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", Cyan);
        root.AddChild(title);

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(list);

        foreach (var b in BiomeCatalog.All)
        {
            string k = b.Id.ToUpperInvariant();
            list.AddChild(BuildCard(b.Id, Loc.T($"BIOME_{k}_NAME"), Loc.T($"BIOME_{k}_EFFECT"),
                                    Loc.T($"BIOME_{k}_DESC"), b.Accent, b.PreviewPath));
        }

        // Boutons bas : Aléatoire + Retour
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        var rand = new Button { Text = Loc.T("LEVELSEL_RANDOM"), CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(rand, Cyan);
        rand.Pressed += StartRandomUnlocked;   // ne tire que parmi les niveaux débloqués
        var back = new Button { Text = Loc.T("COMMON_BACK"), CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(back, Violet);
        back.Pressed += GoBack;
        row.AddChild(rand);
        row.AddChild(back);
        root.AddChild(row);

        // Chaîne de focus explicite (le focus spatial ne traverse pas fiablement les PanelContainer
        // des cartes) + auto-scroll : sans ça, en bas le focus sautait directement à Random/Back et
        // la liste ne défilait pas pour suivre la carte sélectionnée (Néon hors écran).
        SetupFocusChain(rand, back);

        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        // Présélectionne le 1er niveau (fallback « Retour » s'il n'y a aucun biome).
        t.TweenCallback(Callable.From(() => (_firstPlay ?? back).GrabFocus()));
    }

    private Control BuildCard(string id, string name, string effect, string desc, Color accent, string preview)
    {
        bool unlocked  = GameSettings.Instance?.IsUnlocked(id) ?? true;
        bool completed = GameSettings.Instance?.HasCompletedAny(id) == true;

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.16f, 0.95f) };
        style.SetBorderWidthAll(2); style.BorderColor = unlocked ? accent : Dim; style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);
        if (!unlocked) panel.Modulate = new Color(1f, 1f, 1f, 0.45f);   // carte grisée si verrouillée

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hb);

        var prev = new TextureRect
        {
            Texture           = GD.Load<Texture2D>(preview),
            StretchMode       = TextureRect.StretchModeEnum.Tile,
            TextureFilter     = TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(96, 96),
        };
        // Cadre accent autour de l'aperçu
        var prevWrap = new PanelContainer();
        var ps = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        ps.SetBorderWidthAll(2); ps.BorderColor = accent;
        prevWrap.AddThemeStyleboxOverride("panel", ps);
        prevWrap.AddChild(prev);
        hb.AddChild(prevWrap);

        var vb = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vb.AddThemeConstantOverride("separation", 2);
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 10);
        var lblName = new Label { Text = name };
        lblName.AddThemeFontSizeOverride("font_size", 22);
        lblName.AddThemeColorOverride("font_color", accent);
        nameRow.AddChild(lblName);
        // Badge : « VAINCU » si complété, sinon « VERROUILLÉ » si non débloqué.
        if (completed || !unlocked)
        {
            var badge = new Label
            {
                Text = Loc.T(completed ? "LEVELSEL_DEFEATED" : "LEVELSEL_LOCKED"),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            badge.AddThemeFontSizeOverride("font_size", 13);
            badge.AddThemeColorOverride("font_color", completed ? new Color(1f, 0.8f, 0.27f) : Dim);
            nameRow.AddChild(badge);
        }
        // Record de temps survécu (high score du niveau) + difficulté du record.
        int best = GameSettings.Instance?.BestTime(id) ?? 0;
        if (best > 0)
        {
            string diff = Loc.T(GameSettings.DifficultyKey(GameSettings.Instance!.BestDifficulty(id)));
            var rec = new Label
            {
                Text = $"⏱ {best / 60:D2}:{best % 60:D2} · {diff}",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            rec.AddThemeFontSizeOverride("font_size", 13);
            rec.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
            nameRow.AddChild(rec);
        }
        var lblEffect = new Label { Text = effect };
        lblEffect.AddThemeFontSizeOverride("font_size", 14);
        lblEffect.AddThemeColorOverride("font_color", Cyan);
        var lblDesc = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lblDesc.AddThemeFontSizeOverride("font_size", 14);
        lblDesc.AddThemeColorOverride("font_color", Dim);
        vb.AddChild(nameRow); vb.AddChild(lblEffect); vb.AddChild(lblDesc);
        hb.AddChild(vb);

        var play = new Button
        {
            Text = unlocked ? Loc.T("LEVELSEL_PLAY_HERE") : "🔒",
            CustomMinimumSize = new Vector2(130, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Disabled = !unlocked,
        };
        StyleButton(play, accent);
        if (unlocked)
        {
            play.Pressed += () => StartRun(id);
            // Auto-scroll : quand ce bouton prend le focus (clavier/manette), défile pour le rendre visible.
            play.FocusEntered += () => _scroll.EnsureControlVisible(play);
            hb.AddChild(play);
            _playButtons.Add(play);          // seuls les débloqués entrent dans la chaîne de focus
            _firstPlay ??= play;             // 1er niveau jouable = présélection
        }
        else
        {
            hb.AddChild(play);               // visible mais désactivé (pas dans le focus)
        }

        return panel;
    }

    /// <summary>
    /// Câble la navigation verticale : carte[0] → … → carte[N] → (Aléatoire / Retour), et l'inverse.
    /// Indispensable car l'algo de focus spatial de Godot ne traverse pas fiablement les PanelContainer
    /// des cartes (même piège que HubScreen.SetupFocusChain).
    /// </summary>
    private void SetupFocusChain(Button rand, Button back)
    {
        for (int i = 0; i < _playButtons.Count; i++)
        {
            var cur = _playButtons[i];
            if (i > 0)
                cur.FocusNeighborTop = cur.GetPathTo(_playButtons[i - 1]);
            cur.FocusNeighborBottom = (i < _playButtons.Count - 1)
                ? cur.GetPathTo(_playButtons[i + 1])
                : cur.GetPathTo(rand);
        }

        if (_playButtons.Count > 0)
        {
            var last = _playButtons[^1];
            rand.FocusNeighborTop = rand.GetPathTo(last);
            back.FocusNeighborTop = back.GetPathTo(last);
        }
        rand.FocusNeighborRight = rand.GetPathTo(back);
        back.FocusNeighborLeft  = back.GetPathTo(rand);
    }

    private void StyleButton(Button btn, Color accent)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.2f, 0.95f) };
        normal.SetBorderWidthAll(2); normal.BorderColor = accent; normal.SetCornerRadiusAll(6);
        var hover = new StyleBoxFlat { BgColor = new Color(0.18f, 0.14f, 0.32f, 0.98f) };
        hover.SetBorderWidthAll(3); hover.BorderColor = Violet; hover.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Text);
    }

    private void StartRun(string? biomeId)
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        if (GameManager.Instance != null) GameManager.Instance.SelectedBiomeId = biomeId;
        Transition("res://scenes/Game.tscn");
    }

    /// <summary>« Aléatoire » : tire un biome parmi les niveaux débloqués uniquement.</summary>
    private void StartRandomUnlocked()
    {
        var unlocked = new System.Collections.Generic.List<string>();
        foreach (var b in BiomeCatalog.All)
            if (GameSettings.Instance?.IsUnlocked(b.Id) ?? true) unlocked.Add(b.Id);
        if (unlocked.Count == 0) { StartRun(null); return; }
        StartRun(unlocked[(int)(GD.Randi() % (uint)unlocked.Count)]);
    }

    private void GoBack()
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        // Étape précédente du flux « Jouer » : le choix du personnage.
        Transition("res://scenes/ui/CharacterSelectScreen.tscn");
    }

    private void Transition(string path)
    {
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 1f, 0.3);
        t.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(path)));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            GoBack();
        }
    }
}
