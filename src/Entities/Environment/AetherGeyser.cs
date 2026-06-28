using Godot;

/// <summary>
/// Zone de danger environnementale du Sanctuaire en Ruines.
/// Cycle : actif 2 s (frames 2-3, degats 5 HP/s) / inactif 3 s (frame 1, pas de degats).
/// Rayon de danger : 24 px.
/// Phase 4 : PointLight2D "Light" animé — energy 0.4 (inactif) ↔ 1.8 (actif) via Tween.
/// </summary>
public partial class AetherGeyser : Node2D
{
    private const float DamagePersecond  = 5f;
    private const float ActiveDuration   = 2f;
    private const float InactiveDuration = 3f;
    private const float EnergyInactive   = 0.4f;
    private const float EnergyActive     = 1.8f;

    private AnimatedSprite2D? _sprite;
    private Area2D?           _area;
    private PointLight2D?     _light;
    private Player?           _playerInZone;

    private float _cycleTimer = 0f;
    private bool  _isActive   = false;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _area   = GetNodeOrNull<Area2D>("Area2D");
        _light  = GetNodeOrNull<PointLight2D>("Light");

        if (_area != null)
        {
            _area.BodyEntered += OnBodyEntered;
            _area.BodyExited  += OnBodyExited;
        }

        // Configure la texture de lumière en code (GradientTexture2D radiale blanc→transparent)
        if (_light != null)
        {
            var gradient = new Gradient();
            gradient.SetColor(0, Colors.White);
            gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
            var lightTex = new GradientTexture2D
            {
                Gradient   = gradient,
                Width      = 64,
                Height     = 64,
                Fill       = GradientTexture2D.FillEnum.Radial,
                FillFrom   = new Vector2(0.5f, 0.5f),
                FillTo     = new Vector2(1.0f, 0.5f),
            };
            _light.Texture = lightTex;
            _light.Energy  = EnergyInactive;
        }

        // Commence en phase inactive
        _isActive   = false;
        _cycleTimer = InactiveDuration;
        _sprite?.Play("idle");
    }

    public override void _Process(double delta)
    {
        _cycleTimer -= (float)delta;

        if (_cycleTimer <= 0f)
        {
            _isActive   = !_isActive;
            _cycleTimer = _isActive ? ActiveDuration : InactiveDuration;

            _sprite?.Play(_isActive ? "active" : "idle");
            SetActive(_isActive);
        }

        if (_isActive && _playerInZone != null && IsInstanceValid(_playerInZone))
        {
            var stats = _playerInZone.Stats;
            float dmg = DamagePersecond * (float)delta * (1f - stats.DamageReduction);
            _playerInZone.TakeDamage(dmg);
        }
    }

    /// <summary>
    /// Anime la lueur du geyser via Tween.
    /// Actif  : energy 0.4 → 1.8 en 0.3 s.
    /// Inactif : energy 1.8 → 0.4 en 0.5 s.
    /// </summary>
    private void SetActive(bool active)
    {
        if (_light == null) return;

        var tween = CreateTween();
        if (active)
        {
            tween.TweenProperty(_light, "energy", EnergyActive, 0.3);
            ScreenShake.Instance?.Shake(2f, 0.12f);
        }
        else
            tween.TweenProperty(_light, "energy", EnergyInactive, 0.5);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
            _playerInZone = player;
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
            _playerInZone = null;
    }
}
