using Godot;

/// <summary>
/// Pickup « Aimant » — drop aléatoire rare d'un ennemi. Au contact du joueur, attire
/// vers lui TOUTES les orbes d'XP présentes dans l'arène (aspiration globale type
/// Vampire Survivors). Comme l'HpOrb, il n'est pas magnétisé : le joueur doit marcher dessus.
/// </summary>
public partial class MagnetPickup : Area2D
{
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        AddOrbLight();
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player) return;

        AudioSystem.Instance?.PlaySfx("sfx_core_collect");

        // Force l'attraction de toutes les orbes d'XP actuellement sur le terrain.
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupXpOrbs))
            if (node is XpOrb orb)
                orb.ForceMagnet = true;

        QueueFree();
    }

    private void AddOrbLight()
    {
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Color        = new Color(0.267f, 0.667f, 1f, 1f), // cyan magnétique
            Energy       = 0.6f,
            Texture      = _lightTex,
            TextureScale = 2.0f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        var tween = CreateTween().SetLoops();
        tween.TweenProperty(light, "energy", 1.3f, 0.4f).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(light, "energy", 0.6f, 0.4f).SetEase(Tween.EaseType.InOut);
    }
}
