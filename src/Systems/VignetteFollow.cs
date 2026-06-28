using Godot;

/// <summary>
/// Met à jour chaque frame le uniform "center" du shader screen_vignette
/// avec la position normalisée du joueur à l'écran.
/// Corrige le décalage visuel quand la caméra est bloquée en bord de carte.
/// </summary>
public partial class VignetteFollow : Node
{
    private ShaderMaterial? _mat;

    public override void _Ready()
    {
        var vignette = GetNodeOrNull<ColorRect>("/root/Game/PostFX/Vignette");
        if (vignette?.Material is ShaderMaterial mat)
            _mat = mat;
    }

    public override void _Process(double delta)
    {
        if (_mat == null) return;
        var player = GameManager.Instance?.PlayerInstance;
        if (player == null) return;

        var camera = player.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera == null) return;

        var viewportSize  = GetViewport().GetVisibleRect().Size;
        // GetScreenCenterPosition() donne le centre de l'ecran en coords monde,
        // en tenant compte des limites de camera (bords de carte).
        var screenCenter  = camera.GetScreenCenterPosition();
        var offsetPx      = player.GlobalPosition - screenCenter;
        var screenPosPx   = viewportSize / 2f + offsetPx;
        var normalized    = (screenPosPx / viewportSize).Clamp(Vector2.Zero, Vector2.One);
        _mat.SetShaderParameter("center", normalized);
    }
}
