using Godot;

/// <summary>
/// Colonne Solaire — évolution du Jet de Pyre + Noyau Thermique. Le jet dirigé devient une
/// éruption solaire : à chaque pulse, tout ennemi dans un large rayon subit des dégâts directs
/// ET une brûlure massive (DoT), et une couronne de flammes jaillit dans toutes les directions.
/// Réutilise PyreFlame (VFX de cône) réparti en couronne, et ApplyBurn comme le Jet de Pyre.
/// </summary>
public partial class SolarColumn : WeaponBase
{
    private const float RadiusPx     = 155f;
    private const float BurnDps      = 18f;
    private const float BurnDuration = 3.0f;
    private const int   FlameArms    = 6;      // cônes de flammes en couronne (VFX)
    private const float ConeAngle    = 70f;

    private PointLight2D? _sunLight;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage   = 10f;
        Cooldown = 0.7f;

        // Aura volontairement modérée : additive + glob + biomes clairs (Fournaise) saturent
        // vite et masquent le joueur au pic. On garde l'ambiance solaire sans blob (cf. BUG-701).
        _lightTex ??= Player.MakeRadialLightTexture(64);
        _sunLight = new PointLight2D
        {
            Color        = new Color(1f, 0.6f, 0.2f),   // orange solaire
            Energy       = 0.35f,
            Texture      = _lightTex,
            TextureScale = 3.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_sunLight);
        var t = CreateTween().SetLoops();
        t.TweenProperty(_sunLight, "energy", 0.6f,  0.35f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(_sunLight, "energy", 0.35f, 0.35f).SetEase(Tween.EaseType.InOut);

        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_plasma_swing");
        ScreenShake.Instance?.Shake(3f, 0.1f);

        // Éruption radiale : dégâts + brûlure à tout ennemi dans le rayon.
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            if (e.GlobalPosition.DistanceTo(GlobalPosition) > RadiusPx + 16f) continue;
            e.TakeDamage(Damage);
            e.ApplyBurn(BurnDps, BurnDuration);
        }

        // Couronne de flammes (VFX) : un cône PyreFlame dans chaque direction.
        for (int i = 0; i < FlameArms; i++)
        {
            float ang = i * Mathf.Tau / FlameArms;
            var flame = new PyreFlame { ConeAngle = ConeAngle, Range = RadiusPx, Rotation = ang };
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, flame);
            flame.SetDeferred("global_position", GlobalPosition);
        }
    }
}
