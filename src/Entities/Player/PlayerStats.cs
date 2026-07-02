using Godot;

[GlobalClass]
public partial class PlayerStats : Resource
{
    [Export] public float MaxHp { get; set; } = 100f;
    [Export] public float CurrentHp { get; set; } = 100f;
    [Export] public float Speed { get; set; } = 200f;
    [Export] public float Damage { get; set; } = 10f;

    // --- Passifs ---
    /// <summary>Multiplicateur de dégâts global (thermal_core). Base 1.0.</summary>
    [Export] public float DamageMultiplier { get; set; } = 1.0f;
    /// <summary>Réduction des dégâts reçus (reinforced_plating). Hardcap 0.40.</summary>
    [Export] public float DamageReduction { get; set; } = 0.0f;
    /// <summary>Réduction de cooldown cumulée (capacitor). Hardcap : cooldown_final >= 0.15 s.</summary>
    [Export] public float CooldownReduction { get; set; } = 0.0f;
    /// <summary>Régénération HP/s continue (upgrade meta hp_regen). Défaut 0, max 1.2 HP/s (3 niveaux).</summary>
    [Export] public float HpRegenPerSecond { get; set; } = 0.0f;

    // Vitesse de base sauvegardée pour les recalculs servo_motors
    [Export] public float BaseSpeed { get; set; } = 200f;

    // Source unique des plafonds : StatCaps (logique pure testable). Réexposés ici pour
    // les appelants existants (PlayerStats.MaxDamageReduction, etc.).
    public const float MaxDamageReduction = StatCaps.MaxDamageReduction;
    public const float MinCooldown        = StatCaps.MinCooldown;
    public const float MaxSpeed           = StatCaps.MaxSpeed;
}
