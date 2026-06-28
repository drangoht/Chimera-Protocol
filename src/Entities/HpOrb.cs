using Godot;

/// <summary>
/// Orbe de soin droppé aléatoirement par les ennemis.
/// Restaure HealPercent % des HP max au contact du joueur.
/// Drop chance : 8% ennemis normaux, 25% mini-boss.
/// </summary>
public partial class HpOrb : Area2D
{
    public float HealPercent { get; set; } = 0.15f;

    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AddOrbLight();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        AudioSystem.Instance?.PlaySfx("sfx_core_collect");
        player.Heal(HealPercent);
        QueueFree();
    }

    private void AddOrbLight()
    {
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Color        = new Color(1f, 0.25f, 0.25f, 1f),
            Energy       = 0.5f,
            Texture      = _lightTex,
            TextureScale = 1.8f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        var tween = CreateTween().SetLoops();
        tween.TweenProperty(light, "energy", 1.1f, 0.45f).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(light, "energy", 0.5f, 0.45f).SetEase(Tween.EaseType.InOut);
    }
}
