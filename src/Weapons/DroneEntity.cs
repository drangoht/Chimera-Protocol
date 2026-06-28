using Godot;
using System.Collections.Generic;

/// <summary>
/// Entité drone individuelle utilisée par DroneSwarm.
/// Area2D en orbite ; inflige des dégâts aux ennemis dans sa zone.
/// VFX : PointLight2D jaune-orange pulsant + petite traînée de particules.
/// </summary>
public partial class DroneEntity : Area2D
{
    public float Damage        { get; set; } = 12f;
    public float DamageInterval { get; set; } = 0.5f;

    private readonly Dictionary<EnemyBase, float> _damageCooldowns = new();
    private Tween? _lightTween;

    private static Texture2D? _droneLightTex;

    public override void _Ready()
    {
        // ── VFX : PointLight2D jaune-orange ───────────────────────────────
        _droneLightTex ??= Player.MakeRadialLightTexture(32);
        var light = new PointLight2D
        {
            Name         = "DroneLight",
            Color        = new Color(1f, 0.667f, 0.133f, 1f),   // jaune-orange #FFAA22
            Energy       = 0.5f,
            Texture      = _droneLightTex,
            TextureScale = 2.5f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);

        // Pulsation rapide 0.5 → 1.0 → 0.5, boucle 0.4 s (donne un effet propulsion)
        _lightTween = CreateTween().SetLoops();
        _lightTween.TweenProperty(light, "energy", 1.0f, 0.2);
        _lightTween.TweenProperty(light, "energy", 0.5f, 0.2);

        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        var toRemove = new List<EnemyBase>();
        foreach (var (enemy, _) in _damageCooldowns)
            if (!IsInstanceValid(enemy)) toRemove.Add(enemy);
        foreach (var e in toRemove) _damageCooldowns.Remove(e);

        var keys = new List<EnemyBase>(_damageCooldowns.Keys);
        foreach (var enemy in keys)
        {
            _damageCooldowns[enemy] -= (float)delta;
            if (_damageCooldowns[enemy] <= 0f)
            {
                if (IsInstanceValid(enemy))
                    enemy.TakeDamage(Damage);
                _damageCooldowns[enemy] = DamageInterval;
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is EnemyBase enemy && !_damageCooldowns.ContainsKey(enemy))
            _damageCooldowns[enemy] = 0f; // premier tick immédiat
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is EnemyBase enemy)
            _damageCooldowns.Remove(enemy);
    }
}
