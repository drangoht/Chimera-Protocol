using Godot;

/// <summary>
/// Lance Vectorielle — arme DIRIGÉE : au lieu de viser l'ennemi le plus proche, elle tire un
/// projectile perforant dans la DIRECTION DE VISÉE du joueur (sa dernière direction de déplacement,
/// cf. <see cref="Player.AimDirection"/>). Récompense le skill de placement/alignement là où le
/// reste de l'arsenal auto-vise. Perforant dès le niveau 1 ; les hauts niveaux ajoutent des
/// projectiles en éventail serré. Tire même sans cible (balaie).
/// </summary>
public partial class VectorLance : WeaponBase
{
    [Export] public PackedScene BulletScene { get; set; } = null!;

    public int   ProjectileCount { get; set; } = 1;
    public bool  IsPiercing      { get; set; } = true;
    /// <summary>Amplitude totale de l'éventail (deg) quand ProjectileCount > 1. 0 = tir unique droit.</summary>
    public float SpreadDegrees   { get; set; } = 0f;

    public override void _Ready()
    {
        Damage          = 16f;
        Cooldown        = 0.75f;
        ProjectileSpeed = 520f;
        ProjectileCount = 1;
        IsPiercing      = true;
        SpreadDegrees   = 0f;
        BulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
        base._Ready();
    }

    protected override void Attack()
    {
        var player = GameManager.Instance?.PlayerInstance;
        if (player == null) return;

        Vector2 aim = player.AimDirection;
        if (aim == Vector2.Zero) aim = Vector2.Down;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");
        int power = InventorySystem.Instance?.GetWeaponLevel("vector_lance") ?? 1;

        int count = Mathf.Max(1, ProjectileCount);
        for (int i = 0; i < count; i++)
        {
            // Éventail centré sur la direction de visée (offset nul si un seul projectile).
            float offset = count > 1
                ? -SpreadDegrees * 0.5f + SpreadDegrees * i / (count - 1)
                : 0f;
            var dir = aim.Rotated(Mathf.DegToRad(offset));

            var bullet = BulletScene.Instantiate<Bullet>();
            GetTree().Root.AddChild(bullet);
            bullet.GlobalPosition = GlobalPosition;
            bullet.Direction      = dir;
            bullet.Speed          = ProjectileSpeed;
            bullet.Damage         = Damage;
            bullet.IsPiercing     = IsPiercing;
            bullet.Power          = power;

            SpawnMuzzleFlash(GlobalPosition, dir);
        }
    }

    private void SpawnMuzzleFlash(Vector2 pos, Vector2 dir)
    {
        var flash = new MuzzleFlash { Rotation = dir.Angle() };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, flash);
        flash.SetDeferred("global_position", pos);
    }
}
