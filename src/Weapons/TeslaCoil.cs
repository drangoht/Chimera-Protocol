using Godot;
using System.Collections.Generic;

/// <summary>
/// Bobine Tesla — éclair en chaîne. À chaque tir, frappe l'ennemi le plus proche
/// puis rebondit vers les ennemis voisins (ChainCount sauts, portée ChainRange).
/// VFX : éclairs Line2D dentelés cyan/blanc éblouissants + flash lumineux à chaque nœud.
/// </summary>
public partial class TeslaCoil : WeaponBase
{
    public int   ChainCount { get; set; } = 2;
    public float ChainRange { get; set; } = 160f;

    private PointLight2D? _coilLight;
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        Damage   = 14f;
        Cooldown = 1.20f;

        _lightTex ??= Player.MakeRadialLightTexture(32);
        _coilLight = new PointLight2D
        {
            Color        = new Color(0.4f, 0.85f, 1f),   // cyan électrique
            Energy       = 0.5f,
            Texture      = _lightTex,
            TextureScale = 2.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_coilLight);
        var t = CreateTween().SetLoops();
        t.TweenProperty(_coilLight, "energy", 0.9f, 0.5f).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(_coilLight, "energy", 0.5f, 0.5f).SetEase(Tween.EaseType.InOut);

        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        var first = FindNearest(player.GlobalPosition, null);
        if (first == null) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_overload_pulse");
        ScreenShake.Instance?.Shake(2f, 0.08f);

        var hit  = new HashSet<EnemyBase>();
        Vector2     from    = player.GlobalPosition;
        EnemyBase?  current = first;

        for (int i = 0; i <= ChainCount && current != null; i++)
        {
            hit.Add(current);
            current.TakeDamage(Damage);

            SpawnBolt(from, current.GlobalPosition);
            from = current.GlobalPosition;

            var next = FindNearest(from, hit);
            if (next != null && from.DistanceTo(next.GlobalPosition) <= ChainRange)
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

/// <summary>
/// Éclair dentelé éphémère entre deux points. Line2D blanc-cyan additif + flash
/// lumineux aux extrémités, fade en ~0.16 s.
/// </summary>
public partial class LightningBolt : Node2D
{
    public Vector2 From { get; set; }
    public Vector2 To   { get; set; }

    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        ZIndex = 6;

        var rng    = new RandomNumberGenerator();
        var dir    = To - From;
        float len  = dir.Length();
        var perp   = dir.Normalized().Orthogonal();
        int segs   = Mathf.Clamp((int)(len / 22f) + 2, 3, 14);

        var pts = new Vector2[segs + 1];
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float jitter = (i == 0 || i == segs) ? 0f : rng.RandfRange(-10f, 10f);
            pts[i] = From.Lerp(To, t) + perp * jitter;
        }

        // Halo large (cyan translucide)
        var glow = new Line2D
        {
            Points     = pts,
            Width      = 7f,
            DefaultColor = new Color(0.3f, 0.8f, 1f, 0.35f),
            JointMode  = Line2D.LineJointMode.Round,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
        };
        AddChild(glow);

        // Coeur fin (blanc sur-exposé)
        var core = new Line2D
        {
            Points       = pts,
            Width        = 2.5f,
            DefaultColor = new Color(0.85f, 0.97f, 1f, 1f),
            JointMode    = Line2D.LineJointMode.Round,
        };
        AddChild(core);

        // Flash à l'arrivée
        _lightTex ??= Player.MakeRadialLightTexture(32);
        var flash = new PointLight2D
        {
            Position     = To - GlobalPosition,
            Color        = new Color(0.5f, 0.9f, 1f),
            Energy       = 2.6f,
            Texture      = _lightTex,
            TextureScale = 2.2f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(flash);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(glow,  "modulate:a", 0f, 0.16f);
        tw.TweenProperty(core,  "modulate:a", 0f, 0.16f);
        tw.TweenProperty(flash, "energy",     0f, 0.16f);
        tw.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
