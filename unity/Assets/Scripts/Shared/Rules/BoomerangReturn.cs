using System;

/// <summary>
/// Retour d'un projectile boomerang vers son lanceur — logique pure, testable.
///
/// <para><b>Le défaut que cette règle existe pour rendre impossible.</b> Exactement celui de
/// <see cref="PickupMagnet"/>, un an plus tard et sur une autre entité : la vitesse du glaive était
/// une constante (420 px/s) posée dans <c>GlaiveProjectile</c>, et la vitesse maximale du joueur une
/// autre constante (<see cref="StatCaps.MaxSpeed"/> = 380 px/s) posée dans un autre fichier. À plein
/// régime, la lame ne gagnait plus que <b>40 px/s</b> sur sa cible : elle mettait alors <b>six
/// secondes</b> à revenir là où elle en met une à vitesse de base, et l'arme paraissait simplement
/// cesser de tirer — sa recharge attend le retour.</para>
///
/// <para><b>Le principe retenu</b>, le même que pour les orbes : ce qui se règle n'est pas la vitesse
/// du projectile, c'est la vitesse à laquelle il <i>gagne du terrain</i>. Elle ne dépend donc plus de
/// ce que le joueur a acheté — rappeler sa lame se sent pareil à 200 px/s et à 380.</para>
///
/// <para>⚠ La distinction avec <see cref="PickupMagnet"/> n'est pas cosmétique : une orbe rampe
/// jusqu'à son porteur, un boomerang <b>revient</b>. Le retour est la moitié utile de l'arme (elle
/// touche deux fois) et il conditionne la cadence, donc il est délibérément plus rapide que l'aller —
/// c'est ce qui fait lire « rappelée » plutôt que « ramenée ».</para>
/// </summary>
public static class BoomerangReturn
{
    /// <summary>
    /// Vitesse à laquelle le projectile <b>gagne du terrain</b> sur son lanceur, en pixels par
    /// seconde — la seule valeur de ressenti de toute la règle.
    /// </summary>
    /// <remarks>
    /// À 300, une lame lâchée à 240 px (la portée de base du Glaive) rejoint son porteur en
    /// <b>0,74 s à vitesse de base et 0,80 s au plafond</b> — un écart de 8 %, là où l'ancien
    /// comportement allait de 1,1 s à <b>6,0 s</b>, soit un facteur cinq et demi.
    /// </remarks>
    public const float ClosingSpeed = 300f;

    /// <summary>
    /// Le retour est plus rapide que l'aller, dans ce rapport.
    /// </summary>
    /// <remarks>
    /// Sans lui, un joueur <b>immobile</b> — qui ne fuit rien — verrait sa lame revenir exactement à
    /// la vitesse à laquelle elle est partie, et le trajet retour durerait aussi longtemps que
    /// l'aller. C'est ce temps mort, pas le seul cas du joueur rapide, qui faisait dire « le
    /// boomerang est trop lent à revenir ».
    /// </remarks>
    public const float ReturnBoost = 1.25f;

    /// <summary>
    /// Vitesse de retour face à un lanceur qui se déplace à <paramref name="carrierSpeed"/>.
    /// <b>Toujours strictement supérieure</b> à celle du lanceur : c'est l'invariant de cette règle.
    /// </summary>
    /// <param name="outboundSpeed">Vitesse du projectile à l'aller, en pixels par seconde.</param>
    /// <param name="carrierSpeed">Vitesse courante du lanceur, en pixels par seconde.</param>
    public static float SpeedAgainst(float outboundSpeed, float carrierSpeed)
    {
        float chased = Math.Max(0f, carrierSpeed);
        return Math.Max(Math.Max(0f, outboundSpeed) * ReturnBoost, chased + ClosingSpeed);
    }

    /// <summary>
    /// Secondes qu'il faut au projectile pour franchir <paramref name="distance"/> face à un lanceur
    /// qui fuit à <paramref name="carrierSpeed"/> — la durée pendant laquelle l'arme ne tire pas.
    /// </summary>
    public static float ReturnSeconds(float distance, float outboundSpeed, float carrierSpeed)
    {
        float closing = SpeedAgainst(outboundSpeed, carrierSpeed) - Math.Max(0f, carrierSpeed);
        return closing <= 0f ? float.PositiveInfinity : Math.Max(0f, distance) / closing;
    }
}
