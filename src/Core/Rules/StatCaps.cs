using System;

/// <summary>
/// Plafonds de stats du joueur et formules d'application (logique pure, testable).
/// Source unique des hardcaps (PlayerStats y réfère). Sans dépendance Godot.
/// </summary>
public static class StatCaps
{
    /// <summary>Réduction des dégâts reçus maximale (reinforced_plating + Blindage).</summary>
    public const float MaxDamageReduction = 0.40f;
    /// <summary>Cooldown effectif plancher (après toutes les réductions).</summary>
    public const float MinCooldown = 0.15f;
    /// <summary>Vitesse de déplacement maximale (servo_motors).</summary>
    public const float MaxSpeed = 380f;

    /// <summary>Cooldown effectif après réduction, jamais sous MinCooldown.</summary>
    public static float EffectiveCooldown(float baseCooldown, float cooldownReduction)
        => Math.Max(MinCooldown, baseCooldown * (1f - cooldownReduction));

    /// <summary>Plafonne la réduction de dégâts à MaxDamageReduction.</summary>
    public static float CapDamageReduction(float damageReduction) => Math.Min(damageReduction, MaxDamageReduction);

    /// <summary>Plafonne la vitesse à MaxSpeed.</summary>
    public static float CapSpeed(float speed) => Math.Min(speed, MaxSpeed);

    /// <summary>Plafonne la réduction de cooldown cumulée à 100%.</summary>
    public static float CapCooldownReduction(float cooldownReduction) => Math.Min(1f, cooldownReduction);
}
