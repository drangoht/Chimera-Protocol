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

    /// <summary>Paragraphe d'introduction optionnel affiché sous le titre, avant la liste
    /// (null = aucun, cas Bestiaire/Arsenal). Sert aux écrans qui expliquent un système.</summary>
    protected virtual string? IntroText => null;

    /// <summary>Une entrée doit-elle être affichée « verrouillée » (non découverte) ? Par défaut non
    /// (Bestiaire). L'Arsenal et la Chimère surchargent pour masquer ce qui n'a pas encore été
    /// rencontré.</summary>
    protected virtual bool IsEntryLocked(CodexEntry e) => false;

    /// <summary>
    /// Clé de la description affichée à la place d'une entrée verrouillée. Surchargeable parce
    /// qu'elle doit dire <b>comment débloquer</b> — et que la réponse dépend de l'écran : une arme se
    /// trouve sur une carte de montée de niveau, une greffe en remplissant une jauge d'assimilation.
    /// Un texte générique n'aiderait personne, et le texte de l'Arsenal appliqué aux greffes serait
    /// simplement faux.
    /// </summary>
    protected virtual string LockedDescKey => "ARSENAL_LOCKED_DESC";

    private ColorRect      _fade = null!;
    private Button         _backButton = null!;
    private ScrollContainer _scroll = null!;

    private static readonly Color BgColor   = UiPalette.BgDeep;
    private static readonly Color TextColor = UiPalette.OffWhite;

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

        // Soulignement « gravé » du titre d'écran (ART_BRIEF_UI_FRAMES §3.6)
        root.AddChild(UiStyle.ScreenTitleUnderline(TitleAccent));

        // Paragraphe d'introduction (écrans « système » : Chimère…). Absent pour Bestiaire/Arsenal.
        if (IntroText is { Length: > 0 } intro)
        {
            var introLbl = new Label { Text = intro, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            introLbl.AddThemeFontSizeOverride("font_size", 15);
            introLbl.AddThemeColorOverride("font_color", new Color(0.78f, 0.78f, 0.88f));
            root.AddChild(introLbl);
        }

        // Liste scrollable
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _scroll.AddThemeConstantOverride("margin_top", 4);
        root.AddChild(_scroll);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        list.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(list);

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

    private static readonly Color LockGrey = UiPalette.Dim;

    private Control BuildRow(CodexEntry e)
    {
        bool locked = IsEntryLocked(e);
        Color accent = locked ? LockGrey : e.Accent;

        // Rangée = carte du §3.5 (plaque chanfreinée 9-slice, bord soudé en bas). Le liseré porte
        // l'accent de catégorie de l'entrée — c'est lui qui rend la famille lisible d'un coup d'œil
        // dans une liste de 28 lignes. Entrée non découverte : plaque d'acier éteinte, aucun accent
        // à révéler (le nom, le tag et la vignette sont déjà masqués plus bas).
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", locked
            ? UiStyle.CardFrameDisabled()
            : UiStyle.CardFrame(accent));

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 18);
        panel.AddChild(hbox);

        // Image : animée (SpriteFrames "idle") si disponible, sinon figée. Si verrouillée :
        // silhouette grisée (icône figée + Modulate très sombre).
        TextureRect img;
        if (e.FramesPath != null && !locked)
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
        if (locked) img.Modulate = new Color(0.12f, 0.12f, 0.15f);   // silhouette
        hbox.AddChild(img);

        // Texte
        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        var name = new Label { Text = locked ? "???" : Loc.T(e.Name) };
        name.AddThemeFontSizeOverride("font_size", 20);
        name.AddThemeColorOverride("font_color", accent);
        header.AddChild(name);

        var tag = new Label { Text = Loc.T(locked ? "ARSENAL_LOCKED" : e.Tag), VerticalAlignment = VerticalAlignment.Center };
        tag.AddThemeFontSizeOverride("font_size", 13);
        tag.AddThemeColorOverride("font_color", new Color(accent.R, accent.G, accent.B, 0.7f));
        header.AddChild(tag);
        vbox.AddChild(header);

        var desc = new Label
        {
            Text         = locked ? Loc.T(LockedDescKey) : Loc.T(e.Description),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        desc.AddThemeFontSizeOverride("font_size", 15);
        desc.AddThemeColorOverride("font_color", locked ? LockGrey : TextColor);
        desc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddChild(desc);

        return panel;
    }

    // ── Bouton stylé (cohérent avec le menu) ──────────────────────────────────
    /// <summary>Bouton « plaque blindée » du §3.2 — cadres et pulsation de focus centralisés
    /// dans <see cref="UiStyle.ApplyButtonFrames(Button, UiStyle.FrameAccent)"/>.</summary>
    private static Button MakeButton(string text)
    {
        var btn = new Button
        {
            Text              = text,
            CustomMinimumSize = new Vector2(280, 52),
        };
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AddThemeColorOverride("font_color", UiPalette.OffWhite);
        UiStyle.ApplyButtonFrames(btn);
        return btn;
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

    /// <summary>Scène de retour. Tous les écrans codex vivent désormais sous le sous-menu Codex, donc
    /// « Retour » y ramène (surchargeable si un écran est atteint depuis ailleurs).</summary>
    protected virtual string BackScenePath => "res://scenes/ui/CodexMenuScreen.tscn";

    private void OnBackPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        _backButton.Disabled = true;
        var tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 1f, 0.35);
        tw.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile(BackScenePath)));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            OnBackPressed();
            return;
        }

        // Défilement clavier/manette : les rangées ne sont pas focalisables (le seul
        // contrôle focalisable est « Retour »), donc on pilote le ScrollContainer à la main.
        if (_scroll == null) return;
        const int step = 64;
        if (@event.IsActionPressed("ui_down", allowEcho: true))
        {
            _scroll.ScrollVertical += step;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_up", allowEcho: true))
        {
            _scroll.ScrollVertical -= step;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_page_down", allowEcho: true))
        {
            _scroll.ScrollVertical += (int)_scroll.Size.Y;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_page_up", allowEcho: true))
        {
            _scroll.ScrollVertical -= (int)_scroll.Size.Y;
            GetViewport().SetInputAsHandled();
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
