using Godot;

public partial class RustSwarm : EnemyBase
{
    private AnimatedSprite2D? _sprite;

    public override void _Ready()
    {
        MaxHp   = 20f;
        Speed   = 120f;
        Damage  = 5f;
        XpValue = 2;
        base._Ready();

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

    protected override float ContactRadius => 24f;

    protected override void UpdateMovement(Player player, double delta)
    {
        base.UpdateMovement(player, delta);

        // Orientation du sprite selon direction de deplacement
        if (_sprite != null && player != null)
        {
            var dir = player.GlobalPosition - GlobalPosition;
            if (dir.X < -1f)
                _sprite.FlipH = true;
            else if (dir.X > 1f)
                _sprite.FlipH = false;
        }
    }

    protected override void Die()
    {
        if (_isDead) return;
        _isDead = true;

        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled(this);
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
        AudioSystem.Instance?.PlaySfx("sfx_enemy_swarm_die");
    }
}
