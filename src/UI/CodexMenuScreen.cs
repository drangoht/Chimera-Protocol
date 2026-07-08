using Godot;
using System.Collections.Generic;

/// <summary>
/// Sous-menu « Codex » — regroupe les écrans d'information (Bestiaire, Arsenal, Chimère, Défis, Perks)
/// pour désencombrer le menu principal. Construit ses boutons en code (style cohérent avec le menu),
/// avec fondu, navigation clavier/manette et retour au menu principal. Les écrans qu'il ouvre reviennent
/// ici (cf. CodexScreenBase.BackScenePath).
/// </summary>
public partial class CodexMenuScreen : Control
{
    private static readonly Color Accent = new(0.267f, 1f, 0.933f);   // cyan
    private static readonly Color BgColor = new(0.06f, 0.06f, 0.11f, 1f);

    private ColorRect _fade = null!;
    private readonly List<Button> _buttons = new();

    // Libellé (clé loc + repli FR) → scène cible. Retour géré à part.
    private static readonly (string Key, string Fallback, string Scene)[] Entries =
    {
        ("MENU_BESTIARY",   "Bestiaire", "res://scenes/ui/BestiaryScreen.tscn"),
        ("MENU_ARSENAL",    "Arsenal",   "res://scenes/ui/ArsenalScreen.tscn"),
        ("MENU_CHIMERA",    "Chimère",   "res://scenes/ui/ChimeraCodexScreen.tscn"),
        ("MENU_CHALLENGES", "Défis",     "res://scenes/ui/ChallengesScreen.tscn"),
        ("MENU_PERKS",      "Perks",     "res://scenes/ui/PerksScreen.tscn"),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildUI();
        AudioSystem.Instance?.PlayMusic("music_menu");

        _fade.Color = new Color(0, 0, 0, 1);
        CreateTween().TweenProperty(_fade, "color:a", 0f, 0.4);

        if (_buttons.Count > 0) _buttons[0].GrabFocus();
    }

    private void BuildUI()
    {
        var bg = new ColorRect { Color = BgColor };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var vbox = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -140f, OffsetRight = 140f, OffsetTop = -220f, OffsetBottom = 220f,
            GrowHorizontal = GrowDirection.Both, GrowVertical = GrowDirection.Both,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        var title = new Label { Text = TextOr("MENU_CODEX", "Codex"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", Accent);
        vbox.AddChild(title);

        var sep = new Control { CustomMinimumSize = new Vector2(0, 8) };
        vbox.AddChild(sep);

        foreach (var (key, fallback, scene) in Entries)
        {
            string target = scene;
            var btn = MakeButton(TextOr(key, fallback));
            btn.Pressed += () => Go(target);
            vbox.AddChild(btn);
            _buttons.Add(btn);
        }

        var back = MakeButton("◄  " + Loc.T("COMMON_BACK"));
        back.Pressed += () => Go("res://scenes/MainMenu.tscn");
        vbox.AddChild(back);
        _buttons.Add(back);

        SetupFocusChain();

        _fade = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 100 };
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_fade);
    }

    private Button MakeButton(string text)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(280, 52) };
        btn.AddThemeFontSizeOverride("font_size", 20);
        btn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
        btn.AddThemeStyleboxOverride("normal",  BtnStyle(2, new Color(0.267f, 1f, 0.933f, 0.8f), 0.85f));
        btn.AddThemeStyleboxOverride("hover",   BtnStyle(3, new Color(0.667f, 0.267f, 1f), 0.95f));
        btn.AddThemeStyleboxOverride("pressed", BtnStyle(3, new Color(0.667f, 0.267f, 1f), 1f));
        btn.AddThemeStyleboxOverride("focus",   BtnStyle(3, new Color(0.667f, 0.267f, 1f), 0.95f));
        btn.MouseEntered += () => { AudioSystem.Instance?.PlaySfx("sfx_ui_button"); Hover(btn, true); };
        btn.FocusEntered += () => Hover(btn, true);
        btn.MouseExited  += () => Hover(btn, false);
        btn.FocusExited  += () => Hover(btn, false);
        return btn;
    }

    private static StyleBoxFlat BtnStyle(int border, Color borderCol, float bgA)
    {
        var s = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.16f, bgA) };
        s.SetBorderWidthAll(border); s.BorderColor = borderCol; s.SetCornerRadiusAll(4);
        return s;
    }

    private void Hover(Button btn, bool on)
    {
        btn.PivotOffset = btn.Size / 2f;
        CreateTween().TweenProperty(btn, "scale", on ? new Vector2(1.04f, 1.04f) : Vector2.One, 0.1);
    }

    private void SetupFocusChain()
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            var b = _buttons[i];
            b.FocusNeighborTop    = b.GetPathTo(_buttons[(i - 1 + _buttons.Count) % _buttons.Count]);
            b.FocusNeighborBottom = b.GetPathTo(_buttons[(i + 1) % _buttons.Count]);
        }
    }

    private bool _leaving;
    private void Go(string scene)
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        var tw = CreateTween();
        tw.TweenProperty(_fade, "color:a", 1f, 0.35);
        tw.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(scene)));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Go("res://scenes/MainMenu.tscn");
        }
    }

    private static string TextOr(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
    }
}
