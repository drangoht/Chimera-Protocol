using System;

/// <summary>
/// <b>Réserve de régénération</b> — le surplus de régénération qui serait perdu à PV pleins est mis de
/// côté et absorbe le prochain coup (logique pure, testable).
///
/// <para><b>Le problème mesuré.</b> Campagne du 2026-07-30 (4 runs appariées, overtime, Fournaise) :
/// la régénération tourne à <b>19,2 PV/s nominaux pour 8,2 réellement rendus</b> — <b>58 % du débit
/// est jeté</b>, parce que le porteur passe <b>100 % de l'overtime au-dessus de 90 % de ses PV max</b>
/// et meurt d'un <b>pic</b> qui traverse, pas d'une usure. Un flux continu de PV n'a donc presque
/// aucune fenêtre pour agir. C'est la cause réelle du « choix mort » constaté en jeu (1 prise
/// d'Auto-réparation contre 44 de Blindage, GDD §33.5) : le problème n'était pas la valeur de la
/// carte, mais le fait que la majorité de cette valeur n'arrivait jamais au joueur.</para>
///
/// <para><b>Le parti pris.</b> Ne rien ajouter — ne plus rien perdre. À PV pleins, le tick de
/// régénération alimente une réserve ; quand un coup passe, la réserve l'absorbe avant les PV. La
/// carte garde son identité (un débit passif, aucune touche à presser) mais devient un <b>tampon
/// anti-pic</b>, c'est-à-dire une réponse au mode de mort réellement observé.</para>
///
/// <para><b>Pourquoi le plafond dépend du DÉBIT et pas seulement des PV max.</b> Un plafond fixé à une
/// fraction des PV max seuls serait atteint tôt ou tard par n'importe quel débit : une seule prise
/// finirait par offrir le même tampon que quarante, et la carte perdrait toute progression — le défaut
/// que les cartes de surcharge existent pour éviter (GDD §33). Le plafond vaut donc
/// <see cref="ReserveSeconds"/> secondes de régénération accumulée, borné par
/// <see cref="MaxFractionOfMaxHp"/> des PV max pour que le tampon ne dépasse jamais une part
/// raisonnable de la barre de vie.</para>
///
/// <para>Design : <c>docs/GDD.md</c> §33.6. Mesures : <c>docs/TEST_REPORT.md</c> (2026-07-30).</para>
/// </summary>
public static class RegenReserve
{
    /// <summary>
    /// Secondes de régénération que la réserve peut accumuler. Règle le <b>temps d'avance</b> qu'un
    /// joueur peut se constituer en restant intact, donc la taille du pic qu'il encaisse gratuitement.
    /// </summary>
    public const float ReserveSeconds = 20f;

    /// <summary>
    /// Plafond absolu, en fraction des PV max. Garde-fou : sans lui, un débit très élevé en fin de run
    /// (~40 prises) offrirait un second réservoir de vie comparable à la barre elle-même.
    /// </summary>
    public const float MaxFractionOfMaxHp = 0.25f;

    /// <summary>Capacité de la réserve pour un débit et des PV max donnés (0 si aucun débit).</summary>
    public static float Capacity(float regenPerSecond, float maxHp)
    {
        if (regenPerSecond <= 0f || maxHp <= 0f) return 0f;
        return Math.Min(regenPerSecond * ReserveSeconds, maxHp * MaxFractionOfMaxHp);
    }

    /// <summary>
    /// Répartit un tick de régénération : d'abord les PV manquants, <b>puis</b> la réserve. L'ordre
    /// compte — soigner d'abord reste toujours préférable à stocker, un PV rendu maintenant vaut mieux
    /// qu'un PV promis.
    /// </summary>
    /// <returns>
    /// <c>healed</c> : PV effectivement rendus au joueur ; <c>stored</c> : PV versés à la réserve ;
    /// <c>reserve</c> : nouvel état de la réserve. La somme <c>healed + stored</c> peut être
    /// inférieure au tick si la réserve est pleine — ce résidu est le seul gaspillage qui subsiste.
    /// </returns>
    public static (float Healed, float Stored, float Reserve) ApplyRegen(
        float currentHp, float maxHp, float reserve, float regenPerSecond, float delta)
    {
        float tick = regenPerSecond * delta;
        if (tick <= 0f || currentHp <= 0f) return (0f, 0f, reserve);

        float healed = Math.Min(tick, Math.Max(0f, maxHp - currentHp));
        float left = tick - healed;

        float capacity = Capacity(regenPerSecond, maxHp);
        // La réserve peut dépasser sa capacité si le débit vient de baisser (fin d'un buff, PV max
        // réduits) : on ne la tronque pas ici, on cesse simplement de la remplir.
        float stored = Math.Min(left, Math.Max(0f, capacity - reserve));

        return (healed, stored, reserve + stored);
    }

    /// <summary>
    /// Fait absorber un coup par la réserve. Appelée <b>après</b> les réductions de dégâts et les
    /// fenêtres d'invulnérabilité : la réserve est le dernier rempart avant les PV, jamais un
    /// substitut aux i-frames.
    /// </summary>
    /// <returns>
    /// <c>remaining</c> : dégâts qui atteignent les PV ; <c>absorbed</c> : dégâts encaissés par la
    /// réserve (à compter comme régénération enfin rendue dans la télémétrie) ; <c>reserve</c> :
    /// nouvel état.
    /// </returns>
    public static (float Remaining, float Absorbed, float Reserve) Absorb(float damage, float reserve)
    {
        if (damage <= 0f || reserve <= 0f) return (Math.Max(0f, damage), 0f, Math.Max(0f, reserve));
        float absorbed = Math.Min(damage, reserve);
        return (damage - absorbed, absorbed, reserve - absorbed);
    }
}
