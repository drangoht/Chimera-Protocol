using Godot;

/// <summary>
/// Bannière transitoire plein écran (CanvasLayer auto-libéré) : apparaît, se maintient, puis
/// s'efface. Utilisée pour annoncer « OVERTIME » et « NIVEAU TERMINÉ ». Auto-contenue.
/// </summary>
public partial class Banner : CanvasLayer
{
    private string _text  = "";
    private Color  _color = Colors.White;

    /// <summary>Affiche une bannière (créée et ajoutée à la racine).</summary>
    public static void Show(SceneTree tree, string text, Color color)
    {
        if (tree == null) return;
        var b = new Banner { _text = text, _color = color };
        tree.Root.AddChild(b);
    }

    public override void _Ready()
    {
        Layer = 85;

        var label = new Label
        {
            Text                = _text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Modulate            = new Color(1, 1, 1, 0),
        };
        label.SetAnchorsPreset(Control.LayoutPreset.Center);
        label.AnchorLeft = 0f; label.AnchorRight = 1f; label.AnchorTop = 0.30f; label.AnchorBottom = 0.42f;
        label.OffsetLeft = label.OffsetRight = label.OffsetTop = label.OffsetBottom = 0;
        label.AddThemeFontSizeOverride("font_size", 44);
        label.AddThemeColorOverride("font_color", _color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", 8);
        AddChild(label);

        var tw = CreateTween();
        tw.TweenProperty(label, "modulate:a", 1f, 0.3);
        tw.TweenInterval(1.4);
        tw.TweenProperty(label, "modulate:a", 0f, 0.6);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
