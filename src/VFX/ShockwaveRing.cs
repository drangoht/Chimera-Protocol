using Godot;

/// <summary>
/// Anneau cyan lumineux qui se dilate depuis la position de mort du Colosse Greffé.
/// Radius 0 → 128 px en 0.4 s, puis auto-détruit.
/// Instancié via CallDeferred depuis GraftedColossus.OnAnimationFinished().
/// </summary>
public partial class ShockwaveRing : Node2D
{
    [Export] public float Duration = 0.4f;

    private ShaderMaterial? _mat;
    private static Texture2D? _ringTex;

    public override void _Ready()
    {
        // Sprite2D avec texture blanche 4×4 et shader shockwave_ring
        _ringTex ??= MakeWhiteTexture(256);

        var shader = GD.Load<Shader>("res://assets/shaders/shockwave_ring.gdshader");
        if (shader == null)
        {
            GD.PrintErr("[ShockwaveRing] Shader introuvable : res://assets/shaders/shockwave_ring.gdshader");
            QueueFree();
            return;
        }

        _mat = new ShaderMaterial { Shader = shader };

        var sprite = new Sprite2D
        {
            Texture   = _ringTex,
            Material  = _mat,
            ZIndex    = 5,
        };
        AddChild(sprite);

        // Animation du paramètre progress 0 → 1 sur Duration
        var tween = CreateTween();
        tween.TweenMethod(
            Callable.From<float>(p => _mat.SetShaderParameter("progress", p)),
            0.0f, 1.0f, Duration);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>Crée une texture blanche unie de taille <paramref name="size"/>×<paramref name="size"/>.</summary>
    private static Texture2D MakeWhiteTexture(int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        return ImageTexture.CreateFromImage(img);
    }
}
