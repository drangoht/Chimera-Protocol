using Godot;

/// <summary>
/// Surimpression permanente en bas à droite de l'écran : tampon de version au format
/// <c>v&lt;version&gt;-&lt;sha&gt;</c> (cf. <see cref="BuildInfo.Label"/>) et, optionnellement,
/// compteur d'images/s. Autoload : crée un <see cref="CanvasLayer"/> à haute priorité
/// (au-dessus de tout) avec des labels discrets, non-interactifs, qui persistent d'un écran
/// à l'autre. Les deux affichages se règlent depuis l'écran Options (<see cref="GameSettings"/>).
/// </summary>
public partial class VersionStamp : Node
{
    public static VersionStamp? Instance { get; private set; }

    private static readonly Color Text = new(0.85f, 0.85f, 0.95f, 0.42f); // blanc cassé translucide

    private Label? _stamp;
    private Label? _fps;

    public override void _Ready()
    {
        Instance = this;
        // Le compteur doit continuer de tourner quand l'arbre est en pause (menu pause).
        ProcessMode = ProcessModeEnum.Always;

        var layer = new CanvasLayer { Layer = 128 };
        AddChild(layer);

        _stamp = MakeLabel(BuildInfo.Label, bottomOffset: 0);
        layer.AddChild(_stamp);

        // Compteur d'IPS posé juste au-dessus du tampon (même gouttière bas-droite).
        _fps = MakeLabel("", bottomOffset: -18);
        layer.AddChild(_fps);

        var s = GameSettings.Instance;
        // Capture vidéo promotionnelle : ni tampon de build, ni compteur en surimpression.
        SetStampVisible(!DebugHooks.TrailerMode && (s?.ShowVersionStamp ?? true));
        SetFpsVisible(!DebugHooks.TrailerMode && (s?.ShowFps ?? false));
    }

    /// <summary>Affiche ou masque le tampon de version (réglage Options).</summary>
    public void SetStampVisible(bool visible)
    {
        if (_stamp != null) _stamp.Visible = visible && !DebugHooks.TrailerMode;
    }

    /// <summary>Affiche ou masque le compteur d'images/s (réglage Options).</summary>
    public void SetFpsVisible(bool visible)
    {
        bool on = visible && !DebugHooks.TrailerMode;
        if (_fps != null) _fps.Visible = on;
        // Le rafraîchissement ne tourne que si le compteur est affiché.
        SetProcess(on);
    }

    public override void _Process(double delta)
    {
        if (_fps == null) return;
        _fps.Text = $"{Engine.GetFramesPerSecond():0} FPS";
    }

    /// <summary>Label discret ancré en bas à droite, décalé verticalement de
    /// <paramref name="bottomOffset"/> px (0 = ligne du bas).</summary>
    private static Label MakeLabel(string text, int bottomOffset)
    {
        var label = new Label
        {
            Text                = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Bottom,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        // Ancré en bas-droite, décalé vers l'intérieur (marge 8 px).
        label.OffsetLeft   = -220;
        label.OffsetTop    = -24 + bottomOffset;
        label.OffsetRight  = -8;
        label.OffsetBottom = -6  + bottomOffset;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Text);
        // Légère ombre pour rester lisible sur fond clair comme sombre.
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
