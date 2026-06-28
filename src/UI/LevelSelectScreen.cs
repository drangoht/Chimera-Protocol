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

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);

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
        rand.Pressed += () => StartRun(null);
        var back = new Button { Text = Loc.T("COMMON_BACK"), CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(back, Violet);
        back.Pressed += GoBack;
        row.AddChild(rand);
        row.AddChild(back);
        root.AddChild(row);

        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        t.TweenCallback(Callable.From(() => back.GrabFocus()));
    }

    private Control BuildCard(string id, string name, string effect, string desc, Color accent, string preview)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.16f, 0.95f) };
        style.SetBorderWidthAll(2); style.BorderColor = accent; style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);

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
        // Badge de complétion : affiché si le biome a déjà été vaincu (boss final battu).
        if (GameSettings.Instance?.HasCompletedAny(id) == true)
        {
            var badge = new Label { Text = Loc.T("LEVELSEL_DEFEATED"), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            badge.AddThemeFontSizeOverride("font_size", 13);
            badge.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.27f));
            nameRow.AddChild(badge);
        }
        var lblEffect = new Label { Text = effect };
        lblEffect.AddThemeFontSizeOverride("font_size", 14);
        lblEffect.AddThemeColorOverride("font_color", Cyan);
        var lblDesc = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lblDesc.AddThemeFontSizeOverride("font_size", 14);
        lblDesc.AddThemeColorOverride("font_color", Dim);
        vb.AddChild(nameRow); vb.AddChild(lblEffect); vb.AddChild(lblDesc);
        hb.AddChild(vb);

        var play = new Button { Text = Loc.T("LEVELSEL_PLAY_HERE"), CustomMinimumSize = new Vector2(130, 44),
                                SizeFlagsVertical = SizeFlags.ShrinkCenter };
        StyleButton(play, accent);
        play.Pressed += () => StartRun(id);
        hb.AddChild(play);

        return panel;
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
