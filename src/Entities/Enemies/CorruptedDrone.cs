using Godot;

/// <summary>
/// Drone Corrompu — IA erratic_chase.
/// Se dirige vers le joueur mais dévie aléatoirement de ±45° toutes les 0.4–0.8 s.
/// Très rapide (220 px/s), fragile (15 HP).
/// </summary>
public partial class CorruptedDrone : EnemyBase
{
    // Paramètres IA (valeurs depuis data/enemies.json)
    private readonly float _dirChangeMin = 0.4f;
    private readonly float _dirChangeMax = 0.8f;
    private readonly float _deviationDeg = 45f;

    private float _dirTimer = 0f;
    private Vector2 _currentDir = Vector2.Zero;

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D? _sprite;

    public override void _Ready()
    {
        MaxHp   = 15f;
        Speed   = 220f;
        Damage  = 8f;
        XpValue = 3;
        base._Ready();
        _dirTimer = _rng.RandfRange(_dirChangeMin, _dirChangeMax);

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("move");
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite?.Animation == "death")
            QueueFree();
    }

    protected override void UpdateMovement(Player player, double delta)
    {
        _dirTimer -= (float)delta;

        if (_dirTimer <= 0f || _currentDir == Vector2.Zero)
        {
            // Recalcule : direction vers joueur + déviation aléatoire
            var toPlayer = (player.GlobalPosition - GlobalPosition).Normalized();
            float angleRad = Mathf.DegToRad(_rng.RandfRange(-_deviationDeg, _deviationDeg));
            _currentDir = toPlayer.Rotated(angleRad);
            _dirTimer = _rng.RandfRange(_dirChangeMin, _dirChangeMax);

            // Lors d'une déviation importante, repasser en idle pour un frame (effet erratique)
            if (Mathf.Abs(angleRad) > Mathf.DegToRad(30f))
                _sprite?.Play("idle");
            else
                _sprite?.Play("move");
        }

        Velocity = _currentDir * Speed;
        MoveAndSlide();

        // Orientation du sprite selon direction de déplacement
        if (_sprite != null)
        {
            if (_currentDir.X < -0.1f)
                _sprite.FlipH = true;
            else if (_currentDir.X > 0.1f)
                _sprite.FlipH = false;
        }
    }

    protected override float ContactRadius => 20f;

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        SpawnXpOrb();
        SpawnDeathBurst();

        if (_sprite != null)
            _sprite.Play("death");  // QueueFree déclenché par OnAnimationFinished
        else
            QueueFree();
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_drone_die");
    }
}
