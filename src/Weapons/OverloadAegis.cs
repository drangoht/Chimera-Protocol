using Godot;

/// <summary>
/// Égide de Surcharge — fusion du Champ de Surcharge + Plaque Renforcée.
/// Réutilise <see cref="OverloadField"/> (pulse de zone + knockback) mais en version
/// forteresse : rayon massif, knockback brutal, cadence rapide, et chaque pulse
/// régénère une fraction des PV max — un build défensif qui se soigne en repoussant.
/// </summary>
public partial class OverloadAegis : OverloadField
{
    /// <summary>Fraction des PV max rendue à chaque pulse.</summary>
    private const float HealPerPulse = 0.02f;

    public override void _Ready()
    {
        base._Ready();          // construit la lumière + l'indicateur de rayon
        Radius    = 200f;       // _Process resynchronise l'indicateur visuel
        Knockback = 80f;
        Damage    = 18f;
        Cooldown  = 1.0f;       // pulse deux fois plus souvent que la base
    }

    protected override void Attack()
    {
        base.Attack();          // dégâts + knockback + flash + SFX
        // Sustain défensif : chaque onde régénère un peu de PV.
        GameManager.Instance.PlayerInstance?.Heal(HealPerPulse);
    }
}
