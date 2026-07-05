using Godot;

/// <summary>
/// Tampon de version affiché en permanence en bas à droite de l'écran, au format
/// <c>v&lt;version&gt;-&lt;sha&gt;</c> (cf. <see cref="BuildInfo.Label"/>). Autoload : crée un
/// <see cref="CanvasLayer"/> à haute priorité (au-dessus de tout) avec un label discret,
/// non-interactif, qui persiste d'un écran à l'autre.
/// </summary>
public partial class VersionStamp : Node
{
    private static readonly Color Text = new(0.85f, 0.85f, 0.95f, 0.42f); // blanc cassé translucide

    public override void _Ready()
    {
        var layer = new CanvasLayer { Layer = 128 };
        AddChild(layer);

        var label = new Label
        {
            Text                = BuildInfo.Label,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment   = VerticalAlignment.Bottom,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        // Ancré en bas-droite, décalé vers l'intérieur (marge 8 px).
        label.OffsetLeft   = -220;
        label.OffsetTop    = -24;
        label.OffsetRight  = -8;
        label.OffsetBottom = -6;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Text);
        // Légère ombre pour rester lisible sur fond clair comme sombre.
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        layer.AddChild(label);
    }
}
