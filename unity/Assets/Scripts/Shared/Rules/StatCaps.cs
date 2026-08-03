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
    /// <summary>
    /// Réduction de recharge cumulée maximale (capacitor + Hub). Plafonnait à 1,00, c'est-à-dire à
    /// rien : le Capaciteur franchissait 100 % dès son niveau 8 et <b>toutes</b> les armes tombaient
    /// alors au plancher <see cref="MinCooldown"/>, quelle que soit leur cadence de fiche — une arme
    /// lourde à 1,2 s tirait exactement aussi vite qu'un canon léger à 0,4 s. Le plafond à 0,75
    /// laisse une cadence très forte (×4) tout en préservant l'écart entre armes lentes et rapides,
    /// qui est une dimension de design à part entière. Cf. <c>docs/GDD.md</c> §30.
    /// </summary>
    public const float MaxCooldownReduction = 0.75f;
    /// <summary>Vitesse de déplacement maximale (servo_motors).</summary>
    public const float MaxSpeed = 380f;

    /// <summary>Cooldown effectif après réduction, jamais sous MinCooldown.</summary>
    public static float EffectiveCooldown(float baseCooldown, float cooldownReduction)
        => Math.Max(MinCooldown, baseCooldown * (1f - cooldownReduction));

    /// <summary>Plafonne la réduction de dégâts à MaxDamageReduction.</summary>
    public static float CapDamageReduction(float damageReduction) => Math.Min(damageReduction, MaxDamageReduction);

    /// <summary>Plafonne la vitesse à MaxSpeed.</summary>
    public static float CapSpeed(float speed) => Math.Min(speed, MaxSpeed);

    /// <summary>Plafonne la réduction de cooldown cumulée à <see cref="MaxCooldownReduction"/>.</summary>
    public static float CapCooldownReduction(float cooldownReduction)
        => Math.Min(MaxCooldownReduction, cooldownReduction);
}
