using Godot;

/// <summary>
/// Écran de choix du personnage, affiché après « Jouer » et AVANT le choix du niveau.
/// Cartes avec image (frame idle du SpriteFrames), nom, stats et description. Choisir un
/// personnage le fixe (GameManager.SelectedCharacterId + arme de signature) puis mène à
/// LevelSelectScreen. UI construite en code (modèle : LevelSelectScreen).
/// </summary>
public partial class CharacterSelectScreen : Control
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

        var title = new Label { Text = "CHOISIR LE PERSONNAGE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", Cyan);
        root.AddChild(title);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);

        Button? first = null;
        foreach (var c in Characters.All)
        {
            var (card, choose) = BuildCard(c);
            list.AddChild(card);
            first ??= choose;
        }

        // Bouton bas : Retour (vers le menu principal)
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        var back = new Button { Text = "Retour", CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(back, Violet);
        back.Pressed += GoBack;
        row.AddChild(back);
        root.AddChild(row);

        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        var firstFocus = first ?? back;
        t.TweenCallback(Callable.From(() => firstFocus.GrabFocus()));
    }

    private (Control, Button) BuildCard(CharacterDef c)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.09f, 0.16f, 0.95f) };
        style.SetBorderWidthAll(2); style.BorderColor = c.Tint; style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hb);

        // Image = frame idle du SpriteFrames du personnage
        var img = new TextureRect
        {
            Texture           = LoadIdle(c.FramesPath),
            StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter     = TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(96, 96),
        };
        var imgWrap = new PanelContainer();
        var ps = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        ps.SetBorderWidthAll(2); ps.BorderColor = c.Tint;
        imgWrap.AddThemeStyleboxOverride("panel", ps);
        imgWrap.AddChild(img);
        hb.AddChild(imgWrap);

        var vb = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vb.AddThemeConstantOverride("separation", 2);
        var lblName = new Label { Text = $"{c.Name} — {c.Tag}" };
        lblName.AddThemeFontSizeOverride("font_size", 22);
        lblName.AddThemeColorOverride("font_color", c.Tint);
        var lblStats = new Label
        {
            Text = $"PV {c.MaxHp:0}  ·  Vitesse {c.Speed:0}  ·  Arme : {Codex.DisplayName(c.StartingWeaponId)}",
        };
        lblStats.AddThemeFontSizeOverride("font_size", 14);
        lblStats.AddThemeColorOverride("font_color", Cyan);
        var lblDesc = new Label { Text = c.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lblDesc.AddThemeFontSizeOverride("font_size", 14);
        lblDesc.AddThemeColorOverride("font_color", Dim);
        vb.AddChild(lblName); vb.AddChild(lblStats); vb.AddChild(lblDesc);
        hb.AddChild(vb);

        var choose = new Button { Text = "Choisir", CustomMinimumSize = new Vector2(130, 44),
                                  SizeFlagsVertical = SizeFlags.ShrinkCenter };
        StyleButton(choose, c.Tint);
        string id = c.Id;
        choose.Pressed += () => Select(id);
        hb.AddChild(choose);

        return (panel, choose);
    }

    private static Texture2D? LoadIdle(string framesPath)
    {
        var frames = GD.Load<SpriteFrames>(framesPath);
        if (frames == null) return null;
        var names = frames.GetAnimationNames();
        string anim = frames.HasAnimation("idle") ? "idle" : (names.Length > 0 ? names[0] : "");
        if (anim == "" || frames.GetFrameCount(anim) == 0) return null;
        return frames.GetFrameTexture(anim, 0);
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

    private void Select(string charId)
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.SelectedCharacterId = charId;
            // Le personnage garde TOUJOURS son arme de signature (décision design 2026-06-27).
            gm.StartingWeaponId = Characters.Get(charId).StartingWeaponId;
        }
        Transition("res://scenes/ui/LevelSelectScreen.tscn");
    }

    private void GoBack()
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        Transition("res://scenes/MainMenu.tscn");
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
