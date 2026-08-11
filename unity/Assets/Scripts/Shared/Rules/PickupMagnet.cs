using System;

/// <summary>
/// Aimantation des ramassages — l'orbe d'XP qui rejoint son porteur (logique pure, testable).
///
/// <para><b>Le défaut que cette règle existe pour rendre impossible.</b> La vitesse d'attraction
/// était une constante (300 px/s) posée dans <c>XpOrb</c>, et la vitesse maximale du joueur une autre
/// constante (<see cref="StatCaps.MaxSpeed"/> = 380 px/s) posée dans un autre fichier. Deux nombres
/// qui ne se connaissaient pas, et dont l'un dépassait l'autre : <b>un joueur rapide ne pouvait plus
/// être rattrapé par ses propres orbes</b>. Ils le suivaient en file indienne au ras du sol jusqu'à
/// sortir du rayon, puis s'arrêtaient — une traînée de récompenses jamais versées, que rien ne
/// signalait puisque l'XP perdue ne se compte pas.</para>
///
/// <para>Même sous le plafond, l'écart était mince : à la vitesse de base (200 px/s), l'orbe ne
/// gagnait que <b>100 px/s</b> sur sa cible et mettait près d'une seconde à parcourir le rayon
/// d'aimantation. D'où la queue d'orbes derrière un joueur qui traverse une zone qu'il vient de
/// nettoyer.</para>
///
/// <para><b>Le principe retenu</b> : ce qui se règle n'est pas la vitesse de l'orbe, c'est la vitesse
/// à laquelle il <i>gagne du terrain</i>. Elle ne dépend donc plus de ce que le joueur a acheté —
/// aspirer ses orbes se sent pareil à 200 px/s et à 380.</para>
/// </summary>
public static class PickupMagnet
{
    /// <summary>Distance à laquelle l'orbe se met à suivre son porteur, en pixels.</summary>
    public const float Radius = 100f;

    /// <summary>
    /// Vitesse à laquelle l'orbe <b>gagne du terrain</b> sur son porteur, en pixels par seconde —
    /// la seule valeur de ressenti de toute la règle.
    /// </summary>
    /// <remarks>
    /// À 220, un orbe posé au bord du rayon rejoint un joueur lancé en <b>0,45 s</b>. L'ancien
    /// comportement en demandait <b>0,84</b>, et c'est ce délai que l'on voyait sous forme de traînée.
    /// La descendre revient à rallonger la queue ; la monter beaucoup ferait disparaître le geste
    /// d'aller chercher sa récompense, qui est le seul intérêt de l'orbe (cf. <c>XpOrb</c>).
    /// </remarks>
    public const float ClosingSpeed = 220f;

    /// <summary>Plancher de vitesse, pour un porteur à l'arrêt : l'orbe ne rampe pas.</summary>
    public const float MinSpeed = 300f;

    /// <summary>Multiplicateur appliqué quand un aimant ramassé attire toute l'arène.</summary>
    public const float ForcedBoost = 2.5f;

    /// <summary>
    /// Distance de ramassage effectif, en pixels.
    /// </summary>
    /// <remarks>
    /// 20 px, soit le <b>contact réel</b> des deux corps du moteur d'origine (buste du joueur
    /// 20 × 26, orbe de rayon 8) — c'est la même valeur que <c>AetherCore.BaseRadius</c>. Le portage
    /// avait retenu 16 : l'orbe devait presque atteindre le centre du sprite pour être pris, ce qui
    /// ajoutait un dernier temps mort là où le joueur voit déjà l'orbe sur lui.
    /// </remarks>
    public const float PickupRadius = 20f;

    /// <summary>
    /// Vitesse d'attraction face à un porteur qui se déplace à <paramref name="carrierSpeed"/>.
    /// <b>Toujours strictement supérieure</b> à celle du porteur : c'est l'invariant de cette règle.
    /// </summary>
    public static float SpeedAgainst(float carrierSpeed, bool forced = false)
    {
        float chased = Math.Max(0f, carrierSpeed);
        float speed = Math.Max(MinSpeed, chased + ClosingSpeed);

        return forced ? speed * ForcedBoost : speed;
    }

    /// <summary>
    /// Secondes qu'il faut à un orbe pour franchir <paramref name="distance"/> face à un porteur qui
    /// fuit à <paramref name="carrierSpeed"/> — la durée pendant laquelle il traîne derrière lui.
    /// </summary>
    public static float ChaseSeconds(float distance, float carrierSpeed, bool forced = false)
    {
        float closing = SpeedAgainst(carrierSpeed, forced) - Math.Max(0f, carrierSpeed);
        float span = Math.Max(0f, distance - PickupRadius);

        return closing <= 0f ? float.PositiveInfinity : span / closing;
    }
}
