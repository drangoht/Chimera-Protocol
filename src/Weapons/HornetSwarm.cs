using Godot;

/// <summary>
/// Nuée de Frelons — évolution de l'Essaim Traqueur + Servo-Moteurs. L'essaim devient une nuée
/// dense : 7 missiles à tête chercheuse par salve, plus rapides et plus mordants, qui saturent
/// l'écran et re-ciblent sans relâche. Réutilise SeekerMissile comme l'Essaim Traqueur.
/// </summary>
public partial class HornetSwarm : WeaponBase
{
    private const int MissileCount = 7;

    public override void _Ready()
    {
        Damage          = 12f;
        Cooldown        = 0.7f;
        ProjectileSpeed = 420f;
        base._Ready();
    }

    protected override void Attack()
    {
        var targets = AcquireNearestEnemies(MissileCount);
        if (targets.Count == 0) return;

        AudioSystem.Instance?.PlaySfx("sfx_weapon_impulse_shoot");

        for (int i = 0; i < MissileCount; i++)
        {
            var target = targets[i % targets.Count];
            var toTarget = (target.GlobalPosition - GlobalPosition).Normalized();
            float fan = (i - (MissileCount - 1) / 2f) * 18f;
            var initialDir = toTarget.Rotated(Mathf.DegToRad(fan));

            var m = new SeekerMissile { Damage = Damage, Speed = ProjectileSpeed, Power = 5 };
            GetTree().Root.AddChild(m);
            m.GlobalPosition = GlobalPosition;
            m.Launch(initialDir, target);
        }
    }
}
