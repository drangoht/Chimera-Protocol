using Godot;

/// <summary>
/// Pickup "Noyau d'Aether" — ramassage MANUEL par contact (rayon 20 px, pas d'aspiration).
/// Contribue à la monnaie meta via RunStatsTracker.
/// Placeholder visuel : Polygon2D violet #AA44FF.
/// Collision Layer/Mask définis dans AetherCore.tscn (même schéma que XpOrb).
/// PointLight2D violet pulsant (0.8 → 1.4 → 0.8, 1 s boucle).
/// </summary>
public partial class AetherCore : Area2D
{
    private static Texture2D? _coreLightTex;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AddCoreLight();
    }

    private void AddCoreLight()
    {
        _coreLightTex ??= Player.MakeRadialLightTexture(64);
        var light = new PointLight2D
        {
            Name         = "CoreLight",
            Color        = new Color(0.667f, 0.267f, 1f, 1f), // #AA44FF
            Energy       = 0.8f,
            Texture      = _coreLightTex,
            TextureScale = 2.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        // Pulsation energy 0.8 → 1.4 → 0.8 en 1 s, boucle infinie
        var tween = CreateTween().SetLoops();
        tween.TweenProperty(light, "energy", 1.4f, 0.5f)
             .SetEase(Tween.EaseType.InOut)
             .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(light, "energy", 0.8f, 0.5f)
             .SetEase(Tween.EaseType.InOut)
             .SetTrans(Tween.TransitionType.Sine);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player) return;

        // SFX collecte Noyau d'Aether — son plus marque que les orbes XP (evenement notable)
        AudioSystem.Instance?.PlaySfx("sfx_core_collect");

        RunStatsTracker.Instance?.RegisterCoreCollected();
        QueueFree();
    }
}
