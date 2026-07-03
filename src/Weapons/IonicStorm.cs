using Godot;
using System.Collections.Generic;

/// <summary>
/// Tempête Ionique — évolution de la Bobine Tesla + Capaciteur. L'arc n'a plus de temps mort :
/// il crépite en continu, frappe la cible la plus proche puis rebondit sur un long chapelet
/// d'ennemis (chaînes nombreuses, portée doublée). Dégâts lourds, cadence quasi permanente.
/// Réutilise LightningBolt (VFX d'éclair) comme la Bobine Tesla.
/// </summary>
public partial class IonicStorm : WeaponBase
{
    private const int   Chains       = 5;
    private const float ChainRangePx = 220f;

    private PointLight2D? _stormLight;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage   = 20f;
        Cooldown = 0.4f;

        _lightTex ??= Player.MakeRadialLightTexture(32);
        _stormLight = new PointLight2D
        {
            Color        = new Color(0.45f, 0.9f, 1f),   // cyan électrique intense
            Energy       = 0.7f,
            Texture      = _lightTex,
            TextureScale = 3.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_stormLight);
        var t = CreateTween().SetLoops();
        t.TweenProperty(_stormLight, "energy", 1.15f, 0.35f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(_stormLight, "energy", 0.7f,  0.35f).SetEase(Tween.EaseType.InOut);

        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        var first = FindNearest(player.GlobalPosition, null);
        if (first == null) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_overload_pulse");
        ScreenShake.Instance?.Shake(2.5f, 0.06f);

        var hit  = new HashSet<EnemyBase>();
        Vector2     from    = player.GlobalPosition;
        EnemyBase?  current = first;

        for (int i = 0; i <= Chains && current != null; i++)
        {
            hit.Add(current);
            current.TakeDamage(Damage);

            SpawnBolt(from, current.GlobalPosition);
            from = current.GlobalPosition;

            var next = FindNearest(from, hit);
            if (next != null && from.DistanceTo(next.GlobalPosition) <= ChainRangePx)
                current = next;
            else
                current = null;
        }
    }

    private EnemyBase? FindNearest(Vector2 origin, HashSet<EnemyBase>? exclude)
    {
        EnemyBase? best = null;
        float bestDist = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e) continue;
            if (exclude != null && exclude.Contains(e)) continue;
            float d = origin.DistanceSquaredTo(e.GlobalPosition);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    private void SpawnBolt(Vector2 from, Vector2 to)
    {
        var bolt = new LightningBolt { From = from, To = to };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, bolt);
    }
}
