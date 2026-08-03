/// <summary>
/// Statistiques du joueur — port de <c>PlayerStats</c> (docs/UNITY_MIGRATION_PLAN.md, Lot 2).
///
/// <para><b>Classe C# ordinaire, et non un <c>ScriptableObject</c>.</b> Sous Godot c'était une
/// <c>Resource</c>, mais uniquement pour être éditable dans l'inspecteur : ces valeurs sont un
/// <b>état de run</b>, remis à zéro à chaque partie. En faire un asset Unity exposerait au bug
/// classique du <c>ScriptableObject</c> — l'état survit à l'arrêt du jeu dans l'éditeur, et une
/// partie commence avec les PV de la précédente.</para>
///
/// <para>Les plafonds viennent de <see cref="StatCaps"/> (logique pure, partagée avec Godot) : il
/// n'y a pas deux sources de vérité pour un plafond.</para>
/// </summary>
public sealed class PlayerStats
{
    public float MaxHp     { get; set; } = 100f;
    public float CurrentHp { get; set; } = 100f;
    public float Speed     { get; set; } = 200f;
    public float Damage    { get; set; } = 10f;

    /// <summary>Multiplicateur de dégâts global (passif <c>thermal_core</c>).</summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>Réduction des dégâts subis (<c>reinforced_plating</c>), bornée par <see cref="StatCaps"/>.</summary>
    public float DamageReduction { get; set; }

    /// <summary>Réduction de recharge cumulée (<c>capacitor</c>), bornée par <see cref="StatCaps"/>.</summary>
    public float CooldownReduction { get; set; }

    /// <summary>Régénération continue, en PV par seconde.</summary>
    public float HpRegenPerSecond { get; set; }

    /// <summary>
    /// Surplus de régénération mis en réserve (<see cref="RegenReserve"/>) : absorbé avant les PV
    /// au prochain coup. État de run.
    /// </summary>
    public float RegenReserveCharge { get; set; }

    /// <summary>Secondes restantes de suspension de la régénération après un coup encaissé.</summary>
    public float RegenSuppressLeft { get; set; }

    /// <summary>Vitesse de base, conservée pour recalculer les modificateurs de vitesse.</summary>
    public float BaseSpeed { get; set; } = 200f;

    /// <summary>Remet les statistiques à leur état de début de run.</summary>
    public void ResetForRun()
    {
        MaxHp = 100f;
        CurrentHp = MaxHp;
        Speed = BaseSpeed = 200f;
        Damage = 10f;
        DamageMultiplier = 1f;
        DamageReduction = 0f;
        CooldownReduction = 0f;
        HpRegenPerSecond = 0f;
        RegenReserveCharge = 0f;
        RegenSuppressLeft = 0f;
    }
}
