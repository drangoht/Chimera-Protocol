using Godot;

/// <summary>
/// Orbe d'XP lâchée à la mort d'un ennemi.
/// - Si le joueur est à moins de 80 px, l'orbe se déplace vers lui à 300 px/s.
/// - Au contact (BodyEntered), ajoute la valeur à XpSystem et se détruit.
/// - Trail GPUParticles2D actif pendant l'aspiration magnétique.
/// - AnimationPlayer "pulse" modulate.a 0.7→1.0 en boucle hors aspiration.
/// </summary>
public partial class XpOrb : Area2D
{
    public int Value   { get; set; } = 2;
    public int OrbTier { get; set; } = 0; // 0=T1 vert, 1=T2 cyan, 2=T3 violet, 3=T4 or

    private const float MagnetRadius = 80f;
    private const float MagnetSpeed  = 300f;

    /// <summary>Forcé par le pickup Aimant : l'orbe est attirée vers le joueur à toute distance.</summary>
    public bool ForceMagnet { get; set; } = false;

    private bool _isMagneted = false;

    private GpuParticles2D? _trail;
    private AnimationPlayer? _anim;

    private static Texture2D? _orbLightTex;

    public override void _Ready()
    {
        AddToGroup(Constants.GroupXpOrbs);
        BodyEntered += OnBodyEntered;

        _trail = GetNodeOrNull<GpuParticles2D>("Trail");
        _anim  = GetNodeOrNull<AnimationPlayer>("Anim");

        // Démarre le pulse d'inactivité
        _anim?.Play("pulse");

        // Lumière XP et visuels selon le tier (ApplyTierVisuals en premier pour avoir la couleur)
        ApplyTierVisuals();
        AddXpLight();
    }

    private static readonly Color[] TierLightColors =
    {
        new Color(0.267f, 1f,     0.4f),   // T1 #44FF66 vert
        new Color(0.267f, 0.667f, 1f),     // T2 #44AAFF cyan
        new Color(0.667f, 0.267f, 1f),     // T3 #AA44FF violet
        new Color(1f,     0.843f, 0f),     // T4 #FFD700 or
    };
    private static readonly float[] TierScales = { 1.0f, 1.25f, 1.5f, 1.75f };

    private void ApplyTierVisuals()
    {
        int t = Mathf.Clamp(OrbTier, 0, 3);

        var visual = GetNodeOrNull<Polygon2D>("Visual");
        if (visual != null)
        {
            visual.Scale = Vector2.One * TierScales[t];
            visual.Color = TierLightColors[t];
        }
    }

    private void AddXpLight()
    {
        _orbLightTex ??= Player.MakeRadialLightTexture(32);
        int t = Mathf.Clamp(OrbTier, 0, 3);
        var light = new PointLight2D
        {
            Color        = TierLightColors[t],
            Energy       = 0.3f,
            Texture      = _orbLightTex,
            TextureScale = 1.2f * TierScales[t],
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(light);
    }

    public override void _PhysicsProcess(double delta)
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
        bool wasMagneted = _isMagneted;
        _isMagneted = ForceMagnet || dist < MagnetRadius;

        if (_isMagneted && dist > 1f)
        {
            // Aspiration globale (Aimant) plus rapide pour traverser l'arène façon « vacuum »
            float speed = ForceMagnet ? MagnetSpeed * 2.5f : MagnetSpeed;
            var dir = (player.GlobalPosition - GlobalPosition).Normalized();
            GlobalPosition += dir * speed * (float)delta;
        }

        // Transition entre pulse et trail
        if (_isMagneted != wasMagneted)
        {
            if (_isMagneted)
            {
                _anim?.Stop();
                if (_trail != null) _trail.Emitting = true;
            }
            else
            {
                if (_trail != null) _trail.Emitting = false;
                _anim?.Play("pulse");
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player) return;

        // SFX collecte XP — son tres discret (joue tres frequemment)
        AudioSystem.Instance?.PlaySfx("sfx_xp_collect");

        XpSystem.Instance.AddXp(Value);
        QueueFree();
    }
}
