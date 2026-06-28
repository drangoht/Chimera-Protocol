using Godot;

/// <summary>
/// Séquence d'introduction narrative jouée au lancement du jeu (scène de boot).
/// Chaque temps montre une IMAGE (sprite du jeu) + une ligne de lore, en fondu enchaîné,
/// puis bascule sur le menu principal. Skippable : toute touche / clic / Échap passe au menu.
/// UI construite entièrement en code (la scène = root Control + script).
/// </summary>
public partial class IntroScreen : Control
{
    // (texte, chemin d'image illustrant le temps)
    private static readonly (string Text, string Img)[] Beats =
    {
        ("Il y a deux siècles, on a relié les réseaux du monde à l'Aether —\nl'énergie magique enfouie dans les profondeurs.",
            "res://assets/sprites/ui/ui_icon_noyau.png"),
        ("La Convergence ne fut ni guerre ni explosion, mais une fusion.\nLes machines cessèrent d'être des outils.",
            "res://assets/sprites/enemies/drone/enemy_drone_idle_01.png"),
        ("De cette corruption naquit la Rouille Vivante.\nElle ne détruit pas : elle intègre. Elle transforme.",
            "res://assets/sprites/enemies/colossus/enemy_colossus_idle_01.png"),
        ("Les Sanctuaires, ruines saturées d'Aether,\ngardent les Noyaux — le dernier espoir des enclaves.",
            "res://assets/sprites/enemies/rusted_core/rusted_core_idle_01.png"),
        ("Quelqu'un doit y descendre.\nCe sera toi.",
            "res://assets/sprites/player/player_idle_01.png"),
    };

    private static readonly Color Cyan = new(0.267f, 1f, 0.933f);
    private static readonly Color Soft = new(0.85f, 0.88f, 0.95f);

    private Control     _beat  = null!;   // conteneur image+texte (fondu commun)
    private TextureRect _image = null!;
    private Label       _line  = null!;
    private ColorRect   _fade  = null!;
    private Tween?      _seq;
    private bool        _leaving = false;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        // Fond sombre
        var bg = new ColorRect { Color = new Color(0.015f, 0.02f, 0.05f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // Conteneur du « temps » courant (image + texte), animé en fondu d'un bloc
        _beat = new Control { Modulate = new Color(1, 1, 1, 0), MouseFilter = MouseFilterEnum.Ignore };
        _beat.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_beat);

        // Image illustrative (pixel art agrandi, centrée en haut)
        _image = new TextureRect
        {
            ExpandMode    = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter   = MouseFilterEnum.Ignore,
        };
        _image.AnchorLeft = 0.34f; _image.AnchorRight = 0.66f;
        _image.AnchorTop  = 0.13f; _image.AnchorBottom = 0.46f;
        _image.OffsetLeft = _image.OffsetRight = _image.OffsetTop = _image.OffsetBottom = 0;
        _beat.AddChild(_image);

        // Ligne de texte centrée (sous l'image)
        _line = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        _line.AnchorLeft = 0.10f; _line.AnchorRight = 0.90f;
        _line.AnchorTop  = 0.52f; _line.AnchorBottom = 0.68f;
        _line.OffsetLeft = _line.OffsetRight = _line.OffsetTop = _line.OffsetBottom = 0;
        _line.AddThemeFontSizeOverride("font_size", 22);
        _line.AddThemeColorOverride("font_color", Soft);
        _line.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _line.AddThemeConstantOverride("outline_size", 5);
        _beat.AddChild(_line);

        // Indice « passer » (hors fondu de beat)
        var hint = new Label
        {
            Text                = "— appuie sur une touche pour passer —",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
            Modulate            = new Color(1, 1, 1, 0.55f),
        };
        hint.SetAnchorsPreset(LayoutPreset.BottomWide);
        hint.AnchorTop = 0.90f; hint.AnchorBottom = 0.97f;
        hint.OffsetLeft = hint.OffsetRight = hint.OffsetTop = hint.OffsetBottom = 0;
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", Cyan);
        AddChild(hint);

        // Overlay de fondu (noir → transparent à l'entrée, → noir à la sortie)
        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);

        AudioSystem.Instance?.PlayMusic("music_menu");

        BuildSequence();
    }

    private void BuildSequence()
    {
        _seq = CreateTween();
        _seq.TweenProperty(_fade, "color:a", 0f, 0.8);   // fondu d'ouverture

        foreach (var (text, img) in Beats)
        {
            string imgPath = img;
            string txt     = text;
            _seq.TweenCallback(Callable.From(() =>
            {
                _line.Text     = txt;
                _image.Texture = GD.Load<Texture2D>(imgPath);
            }));
            _seq.TweenProperty(_beat, "modulate:a", 1f, 0.6);
            _seq.TweenInterval(2.4);
            _seq.TweenProperty(_beat, "modulate:a", 0f, 0.5);
        }

        _seq.TweenCallback(Callable.From(GoToMenu));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        bool trigger = @event is InputEventKey { Pressed: true }
                    || @event is InputEventMouseButton { Pressed: true }
                    || @event is InputEventJoypadButton { Pressed: true };
        if (trigger)
        {
            GetViewport().SetInputAsHandled();
            GoToMenu();
        }
    }

    private void GoToMenu()
    {
        if (_leaving) return;
        _leaving = true;
        _seq?.Kill();

        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 1f, 0.35);
        t.TweenCallback(Callable.From(() =>
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")));
    }
}
