using Godot;
using System.Collections.Generic;

/// <summary>
/// Projectile de la Lame Boomerang : part en ligne droite en décélérant jusqu'à une portée max,
/// puis revient vers le joueur. Touche chaque ennemi une fois à l'aller ET une fois au retour
/// (le set de touchés est vidé au demi-tour). Visuel : glaive losange cyan en rotation + halo.
/// Mirroir collision du Bullet (Area2D par défaut, BodyEntered sur les ennemis).
/// </summary>
public partial class GlaiveProjectile : Area2D
{
    public float   Damage    { get; set; } = 10f;
    public Vector2 Direction { get; set; } = Vector2.Right;
    public float   Speed     { get; set; } = 540f;
    public float   Range     { get; set; } = 240f;
    public int     Power     { get; set; } = 1;

    private bool  _returning;
    private float _traveled;
    private readonly HashSet<EnemyBase> _hitThisPhase = new();
    private static Texture2D? _lightTex;

    public override void _Ready()
    {
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 14f } });

        // Lame losange + contour clair
        AddChild(new Polygon2D
        {
            Polygon = new Vector2[] { new(0, -13), new(7, 0), new(0, 13), new(-7, 0) },
            Color   = new Color(0.45f, 1f, 0.92f),
        });
        AddChild(new Line2D
        {
            Points       = new[] { new Vector2(0, -13), new Vector2(7, 0), new Vector2(0, 13), new Vector2(-7, 0), new Vector2(0, -13) },
            Width        = 2f,
            DefaultColor = new Color(0.85f, 1f, 1f),
            JointMode    = Line2D.LineJointMode.Round,
        });

        _lightTex ??= Player.MakeRadialLightTexture(32);
        AddChild(new PointLight2D
        {
            Color        = new Color(0.4f, 1f, 0.92f),
            Energy       = 1.1f + Power * 0.2f,
            Texture      = _lightTex,
            TextureScale = 2.4f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        });

        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Rotation += 16f * dt;   // rotation rapide (spin de boomerang)

        if (!_returning)
        {
            float frac = Mathf.Clamp(_traveled / Range, 0f, 1f);
            float v    = Speed * (1f - 0.85f * frac);   // décélère vers l'apex
            Position  += Direction * v * dt;
            _traveled += v * dt;
            if (frac >= 1f) { _returning = true; _hitThisPhase.Clear(); }
        }
        else
        {
            var player = GameManager.Instance?.PlayerInstance;
            if (player == null) { QueueFree(); return; }
            var toPlayer = player.GlobalPosition - GlobalPosition;
            if (toPlayer.Length() < 26f) { QueueFree(); return; }
            Position += toPlayer.Normalized() * Speed * 1.15f * dt;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not EnemyBase enemy || _hitThisPhase.Contains(enemy)) return;
        _hitThisPhase.Add(enemy);
        enemy.TakeDamage(Damage);
    }
}
