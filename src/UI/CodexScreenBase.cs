using Godot;
using System.Collections.Generic;

/// <summary>
/// Base commune aux écrans Bestiaire et Arsenal : liste scrollable d'entrées
/// (image + nom + tag + description), titre, bouton retour, fondu d'entrée/sortie.
/// Les sous-classes fournissent le titre, la couleur d'accent et les entrées.
/// </summary>
public abstract partial class CodexScreenBase : Control
{
    protected abstract string ScreenTitle { get; }
    protected abstract Color  TitleAccent { get; }
    protected abstract IReadOnlyList<CodexEntry> Entries { get; }

    private ColorRect _fade = null!;
    private Button    _backButton = null!;

    private static readonly Color BgColor   = new(0.06f, 0.06f, 0.11f, 1f);
    private static readonly Color PanelBg   = new(0.10f, 0.10f, 0.18f, 0.92f);
    private static readonly Color TextColor = new(0.85f, 0.85f, 0.95f, 1f);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUI();
        AudioSystem.Instance?.PlayMusic("music_menu");

        // Fondu d'entrée
        _fade.Color = new Color(0, 0, 0, 1);
        var tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 0f, 0.5);

        _backButton.GrabFocus();
    }

    private void BuildUI()
    {
        var bg = new ColorRect { Color = BgColor };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 60);
        margin.AddThemeConstantOverride("margin_right", 60);
        margin.AddThemeConstantOverride("margin_top", 36);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 16);
        margin.AddChild(root);

        // Titre
        var title = new Label
        {
            Text = ScreenTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", TitleAccent);
        root.AddChild(title);

        var sep = new ColorRect
        {
            Color = new Color(TitleAccent.R, TitleAccent.G, TitleAccent.B, 0.4f),
            CustomMinimumSize = new Vector2(0, 2),
        };
        root.AddChild(sep);

        // Liste scrollable
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddThemeConstantOverride("margin_top", 4);
        root.AddChild(scroll);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);

        foreach (var entry in Entries)
            list.AddChild(BuildRow(entry));

        // Bouton retour
        _backButton = MakeButton("◄  " + Loc.T("COMMON_BACK"));
        _backButton.Pressed += OnBackPressed;
        ConnectHover(_backButton);
        var backWrap = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        backWrap.AddChild(_backButton);
        root.AddChild(backWrap);

        // Fondu plein écran
        _fade = new ColorRect
        {
            Color       = new Color(0, 0, 0, 1),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex      = 100,
        };
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_fade);
    }

    private Control BuildRow(CodexEntry e)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat { BgColor = PanelBg };
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(e.Accent.R, e.Accent.G, e.Accent.B, 0.6f);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 18);
        panel.AddChild(hbox);

        // Image : animée (SpriteFrames "idle") si disponible, sinon figée
        TextureRect img;
        if (e.FramesPath != null)
        {
            var anim = new CodexAnimImage();
            anim.Setup(e.FramesPath, e.ImagePath);
            img = anim;
        }
        else
        {
            img = new TextureRect { Texture = GD.Load<Texture2D>(e.ImagePath) };
        }
        img.CustomMinimumSize = new Vector2(96, 96);
        img.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
        img.StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered;
        img.TextureFilter     = TextureFilterEnum.Nearest;
        img.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        hbox.AddChild(img);

        // Texte
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        var name = new Label { Text = Loc.T(e.Name) };
        name.AddThemeFontSizeOverride("font_size", 20);
        name.AddThemeColorOverride("font_color", e.Accent);
        header.AddChild(name);

        var tag = new Label { Text = Loc.T(e.Tag), VerticalAlignment = VerticalAlignment.Center };
        tag.AddThemeFontSizeOverride("font_size", 13);
        tag.AddThemeColorOverride("font_color", new Color(e.Accent.R, e.Accent.G, e.Accent.B, 0.7f));
        header.AddChild(tag);
        vbox.AddChild(header);

        var desc = new Label
        {
            Text         = Loc.T(e.Description),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        desc.AddThemeFontSizeOverride("font_size", 15);
        desc.AddThemeColorOverride("font_color", TextColor);
        desc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddChild(desc);

        return panel;
    }

    // ── Bouton stylé (cohérent avec le menu) ──────────────────────────────────
    private Button MakeButton(string text)
    {
        var btn = new Button
        {
            Text              = text,
            CustomMinimumSize = new Vector2(280, 52),
        };
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
        btn.AddThemeStyleboxOverride("normal",  MakeBtnStyle(2, new Color(0.267f, 1f, 0.933f, 0.8f), 0.85f));
        btn.AddThemeStyleboxOverride("hover",   MakeBtnStyle(3, new Color(0.667f, 0.267f, 1f), 0.95f));
        btn.AddThemeStyleboxOverride("pressed", MakeBtnStyle(3, new Color(0.667f, 0.267f, 1f), 1f));
        btn.AddThemeStyleboxOverride("focus",   MakeBtnStyle(3, new Color(0.667f, 0.267f, 1f), 0.95f));
        return btn;
    }

    private static StyleBoxFlat MakeBtnStyle(int border, Color borderCol, float bgA)
    {
        var s = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.16f, bgA) };
        s.SetBorderWidthAll(border);
        s.BorderColor = borderCol;
        s.SetCornerRadiusAll(4);
        return s;
    }

    private void ConnectHover(Button btn)
    {
        btn.MouseEntered += () =>
        {
            AudioSystem.Instance?.PlaySfx("sfx_ui_button");
            btn.PivotOffset = btn.Size / 2f;
            CreateTween().TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.1);
        };
        btn.FocusEntered += () =>
        {
            btn.PivotOffset = btn.Size / 2f;
            CreateTween().TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.1);
        };
        btn.MouseExited  += () => CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.1);
        btn.FocusExited  += () => CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.1);
    }

    private void OnBackPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        _backButton.Disabled = true;
        var tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 1f, 0.35);
        tw.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            OnBackPressed();
        }
    }
}

/// <summary>
/// TextureRect qui cycle les frames d'une animation "idle" d'un SpriteFrames — anime
/// les entrées du bestiaire tout en restant dans le layout Control (pas de Node2D).
/// </summary>
public partial class CodexAnimImage : TextureRect
{
    private Texture2D[] _frames = System.Array.Empty<Texture2D>();
    private float _t;
    private int   _i;
    private float _fps = 6f;

    public void Setup(string framesPath, string fallbackImage)
    {
        var sf = GD.Load<SpriteFrames>(framesPath);
        string anim = "";
        if (sf != null)
        {
            if (sf.HasAnimation("idle")) anim = "idle";
            else { var names = sf.GetAnimationNames(); if (names.Length > 0) anim = names[0]; }
        }

        if (sf == null || anim == "")
        {
            Texture = GD.Load<Texture2D>(fallbackImage);
            return;
        }

        int n = sf.GetFrameCount(anim);
        _frames = new Texture2D[n];
        for (int k = 0; k < n; k++) _frames[k] = sf.GetFrameTexture(anim, k);
        _fps = (float)sf.GetAnimationSpeed(anim);
        if (_fps <= 0.1f) _fps = 6f;
        if (n > 0) Texture = _frames[0];
    }

    public override void _Process(double delta)
    {
        if (_frames.Length < 2) return;
        _t += (float)delta;
        if (_t >= 1f / _fps)
        {
            _t = 0f;
            _i = (_i + 1) % _frames.Length;
            Texture = _frames[_i];
        }
    }
}
