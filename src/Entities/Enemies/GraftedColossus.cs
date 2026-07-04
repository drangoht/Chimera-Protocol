using Godot;

/// <summary>
/// Colosse Greffé — IA slow_hunter.
/// Avance en ligne droite. Frappe toutes les 1.5 s à portée de 36 px pour 20 dégâts.
/// Drop un Noyau d'Aether à la mort (100% chance).
/// L'animation death (10 frames) se joue en entier avant le QueueFree.
/// La frame 7 (index 6) est le flash de libération du Noyau — spawn différé en fin d'animation.
/// </summary>
public partial class GraftedColossus : EnemyBase
{
    private const float MeleeCooldown = 1.5f;
    private float _meleeTimer = 0f;

    private static PackedScene? _aetherCoreScene;
    private static PackedScene? _deathBurstScene;
    private static PackedScene? _shockwaveScene;
    private AnimatedSprite2D? _sprite;

    // Capturés avant base.Die() / QueueFree pour le spawn différé de l'AetherCore
    private Node? _cachedParent;
    private Vector2 _cachedDeathPos;

    public override void _Ready()
    {
        MaxHp   = 200f;
        Speed   = 55f;
        Damage  = 20f;
        XpValue = 25;
        base._Ready();
        _aetherCoreScene ??= GD.Load<PackedScene>("res://scenes/entities/AetherCore.tscn");

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.AnimationFinished += OnAnimationFinished;
            _sprite.Play("move");
        }
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;

        if (_sprite.Animation == "attack")
        {
            // Retour au déplacement après la frappe
            _sprite.Play("move");
        }
        else if (_sprite.Animation == "death")
        {
            // Death burst — reproduit EnemyBase.SpawnDeathBurst() inline (base.Die() non appelable)
            _deathBurstScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_enemy_death_burst.tscn");
            if (_deathBurstScene != null)
            {
                var burst = _deathBurstScene.Instantiate<Node2D>();
                GetTree().Root.CallDeferred(Node.MethodName.AddChild, burst);
                burst.SetDeferred("global_position", _cachedDeathPos);
            }

            // Screen shake + shockwave ring (pas de hitstop : le ralenti à la mort
            // d'un mob récurrent nuit au flow de jeu)
            ScreenShake.Instance?.Shake(12f, 0.35f);
            SpawnShockwaveRing();

            // Animation terminée : spawn de l'Aether Core puis destruction du nœud
            SpawnAetherCore(_cachedParent, _cachedDeathPos);
            QueueFree();
        }
    }

    protected override void UpdateMovement(Player player, double delta)
    {
        // Ligne droite inexorable
        var direction = (player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = direction * Speed;
        MoveAndSlide();

        // Orientation du sprite selon direction de déplacement
        if (_sprite != null)
        {
            if (direction.X < -0.1f)
                _sprite.FlipH = true;
            else if (direction.X > 0.1f)
                _sprite.FlipH = false;
        }
    }

    protected override void HandleContactDamage(Player player, double delta)
    {
        if (GlobalPosition.DistanceTo(player.GlobalPosition) < ContactRadius)
        {
            _meleeTimer -= (float)delta;
            if (_meleeTimer <= 0f)
            {
                var stats = player.Stats;
                float reduced = Damage * (1f - stats.DamageReduction);
                player.TakeDamage(reduced);
                ApplyLifesteal(reduced);   // affixe Vampirique (élite)
                _meleeTimer = MeleeCooldown;

                // Lancer l'animation attack à chaque frappe
                _sprite?.Play("attack");
            }
        }
    }

    protected override float ContactRadius => 36f;

    protected override void Die()
    {
        if (_isDead) return;

        // Capture parent et position AVANT tout QueueFree
        _cachedParent   = GetParent();
        _cachedDeathPos = GlobalPosition;

        // Signaux et XP spawning immédiats (depuis EnemyBase, sans QueueFree)
        _isDead = true;
        EmitSignal(SignalName.Died, XpValue);
        GameManager.Instance?.NotifyEnemyKilled();
        PlayDeathSfx();
        TriggerEliteExplosion();   // affixe Explosif (élite) — Die() surchargé n'appelle pas base.Die()

        // Spawn XP orb via EnemyBase.SpawnXpOrb (gère le tier et l'appel différé)
        SpawnXpOrb();

        // Lancer l'animation death — OnAnimationFinished() spawne l'AetherCore et appelle QueueFree
        if (_sprite != null)
        {
            _sprite.Play("death");
            // Ne pas appeler base.Die() : on gère QueueFree manuellement en fin d'animation
        }
        else
        {
            // Pas de sprite : spawn immédiat et destruction
            SpawnAetherCore(_cachedParent, _cachedDeathPos);
            QueueFree();
        }
    }

    private void SpawnShockwaveRing()
    {
        _shockwaveScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_shockwave_ring.tscn");
        if (_shockwaveScene == null) return;

        var ring = _shockwaveScene.Instantiate<Node2D>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, ring);
        ring.SetDeferred("global_position", _cachedDeathPos);
    }

    private void SpawnAetherCore(Node? parent, Vector2 spawnPos)
    {
        if (_aetherCoreScene == null || parent == null) return;

        var core = _aetherCoreScene.Instantiate<AetherCore>();

        // Différé : même contrainte que SpawnXpOrb dans EnemyBase
        parent.CallDeferred(Node.MethodName.AddChild, core);
        core.SetDeferred("global_position", spawnPos);
    }

    protected override void PlayDeathSfx()
    {
        AudioSystem.Instance?.PlaySfx("sfx_enemy_colossus_die");
    }
}
