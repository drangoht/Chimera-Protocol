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

    /// <summary>
    /// <b>Suspension sous le feu</b> : secondes pendant lesquelles un coup encaissé coupe entièrement
    /// la régénération — PV rendus <i>et</i> remplissage de la réserve.
    ///
    /// <para><b>Le problème mesuré</b> (session jouée du 2026-08-02, cran VI, fin de partie) : « la
    /// régénération est vraiment trop forte, je suis resté immobile un bon moment sans mourir ». Ce
    /// n'était pas un problème de dosage mais d'espèce. Les PV max sont un <b>stock</b> — ça se vide —
    /// alors que la régénération est un <b>débit</b>, et un débit opposé à un débit produit un seuil
    /// binaire : dès que la régénération dépasse les dégâts nets reçus par seconde, le joueur devient
    /// invulnérable <i>pour toujours</i>, sans bouger. La régénération était par ailleurs la seule stat
    /// défensive sans borne (<see cref="StatCaps"/> plafonne la réduction de dégâts, la recharge et la
    /// vitesse ; <see cref="OverloadCards.Regen"/> est linéaire et exemptée de
    /// <see cref="PassiveScaling"/>), donc ce seuil finissait par être franchi dans toute run assez
    /// longue — à ~13 niveaux par minute d'overtime, mécaniquement.</para>
    ///
    /// <para><b>Pourquoi suspendre plutôt que plafonner.</b> Un plafond recréerait la défense bornée que
    /// les cartes de surcharge existent pour supprimer (GDD §33, §31.4.3 : une menace non bornée exige
    /// une défense non bornée) et ne ferait que <i>repousser</i> le seuil — au-dessus du DPS entrant,
    /// l'immobilité redeviendrait gratuite. La suspension laisse le débit strictement illimité : elle ne
    /// retire pas de la puissance, elle retire une <b>certitude</b>, celle de pouvoir encaisser sans
    /// jamais se désengager. La réserve garde alors exactement le sens qu'elle avait : ce que le joueur
    /// a capitalisé <i>en restant intact</i>.</para>
    ///
    /// <para><b>Calibrage.</b> 4 s pour ~9 fenêtres d'invulnérabilité (<c>Player.InvulnWindow</c> = 0,45 s) :
    /// au contact d'une nuée le joueur est touché en continu, donc la régénération reste coupée tant
    /// qu'il ne décroche pas, et décrocher doit être un acte franc, pas un pas de côté.</para>
    ///
    /// <para>⚠ Effet secondaire voulu sur le cran VI : le plancher des champions vaut 12 % des PV max
    /// (<c>SaturationTable</c>) quand la réserve monte à <see cref="MaxFractionOfMaxHp"/> = 25 % — une
    /// réserve pleine absorbait donc <b>deux coups planchers entiers</b>. Sous le feu, elle ne se
    /// recharge plus, et le cran retrouve la morsure pour laquelle il a été écrit.</para>
    /// </summary>
    public const float SuppressionSeconds = 4f;

    /// <summary>Durée de suspension à réarmer quand un coup atteint réellement le joueur.</summary>
    public static float Suppress() => SuppressionSeconds;

    /// <summary>
    /// Décompte la suspension. Séparé de <see cref="ApplyRegen"/> pour continuer de tourner quand le
    /// joueur n'a aucune régénération — sans quoi une carte prise juste après un coup repartirait avec
    /// un compteur figé.
    /// </summary>
    public static float TickSuppression(float suppressLeft, float delta)
        => Math.Max(0f, suppressLeft - Math.Max(0f, delta));

    /// <summary>
    /// true tant que la régénération est coupée par un coup récent.
    ///
    /// <para>⚠ Le seuil n'est pas <c>&gt; 0</c> : le compteur est décrémenté frame par frame, et 240
    /// soustractions de <c>1/60f</c> à partir de 4 s laissent un résidu de l'ordre de 1e-7 — assez pour
    /// qu'une comparaison stricte à zéro garde la régénération éteinte indéfiniment. Un epsilon d'une
    /// fraction de milliseconde est sans effet perceptible et supprime la classe entière de bugs.</para>
    /// </summary>
    public static bool IsSuppressed(float suppressLeft) => suppressLeft > 1e-4f;

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
    /// <param name="suppressLeft">
    /// Secondes de <see cref="SuppressionSeconds">suspension</see> restantes. Tant qu'elles sont
    /// positives, le tick est entièrement perdu : ni PV rendus, ni mise en réserve. La réserve déjà
    /// constituée continue en revanche d'absorber (<see cref="Absorb"/>) — on coupe la source, on ne
    /// confisque pas ce qui a été gagné avant le coup.
    /// </param>
    /// <returns>
    /// <c>healed</c> : PV effectivement rendus au joueur ; <c>stored</c> : PV versés à la réserve ;
    /// <c>reserve</c> : nouvel état de la réserve. La somme <c>healed + stored</c> peut être
    /// inférieure au tick si la réserve est pleine — ce résidu est le seul gaspillage qui subsiste.
    /// </returns>
    public static (float Healed, float Stored, float Reserve) ApplyRegen(
        float currentHp, float maxHp, float reserve, float regenPerSecond, float delta,
        float suppressLeft = 0f)
    {
        if (IsSuppressed(suppressLeft)) return (0f, 0f, reserve);

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
