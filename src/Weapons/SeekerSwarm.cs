using Godot;

/// <summary>
/// Essaim Traqueur — lance des missiles à tête chercheuse vers les ennemis proches (ils s'incurvent
/// et re-ciblent si une cible meurt). 2 missiles au niveau 1, +1 par niveau (jusqu'à 5). Chaque
/// missile part avec un léger éventail puis pourchasse sa cible. Si l'arène est vide, ne tire pas.
/// </summary>
public partial class SeekerSwarm : WeaponBase
{
    public int MissileCount { get; set; } = 2;

    public override void _Ready()
    {
        Damage          = 7f;
        Cooldown        = 1.1f;
        ProjectileSpeed = 300f;
        MissileCount    = 2;
        base._Ready();
    }

    protected override void Attack()
    {
        var targets = AcquireNearestEnemies(MissileCount);
        if (targets.Count == 0) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");
        int power = InventorySystem.Instance?.GetWeaponLevel("seeker_swarm") ?? 1;

        for (int i = 0; i < MissileCount; i++)
        {
            var target = targets[i % targets.Count];
            // Éventail initial autour de la cible : les missiles s'écartent puis incurvent dessus.
            var toTarget = (target.GlobalPosition - GlobalPosition).Normalized();
            float fan = (i - (MissileCount - 1) / 2f) * 22f;
            var initialDir = toTarget.Rotated(Mathf.DegToRad(fan));

            var m = new SeekerMissile { Damage = Damage, Speed = ProjectileSpeed, Power = power };
            GetTree().Root.AddChild(m);
            m.GlobalPosition = GlobalPosition;
            m.Launch(initialDir, target);
        }
    }
}
