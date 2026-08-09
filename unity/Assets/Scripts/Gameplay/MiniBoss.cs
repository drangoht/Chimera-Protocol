using UnityEngine;

/// <summary>
/// Champion de mi-run — socle des six mini-boss (Lot 4).
///
/// <para>Un champion n'est pas « un ennemi avec plus de PV » : il pose une <b>question de placement</b>
/// que la faune ne pose pas, et à laquelle le joueur doit répondre par un réflexe précis. Le projet
/// s'impose une règle : chaque champion de biome demande le réflexe <b>inverse</b> de l'incarnation
/// finale de son biome, pour que le boss ne soit pas un simple champion en plus gros.</para>
///
/// <para><b>Hiérarchie de taille</b>, alignée sur le rôle et non sur l'ordre d'ajout : faune 32 px ·
/// mini-boss globaux 64 · <b>champions de biome 72</b> · boss 154. Les champions ont un jour été les
/// plus petits de tous par un raisonnement inversé — leur taille avait été bornée pour « ne pas
/// égaler le boss », lequel est en réalité rendu bien plus grand.</para>
/// </summary>
public class MiniBoss : EnemyBase
{
    [Header("Champion")]
    [Tooltip("Rayon de contact — calé sur la silhouette, pas sur celle de la faune.")]
    public float ChampionContactRadius = 36f;

    [Tooltip("Probabilité de laisser un orbe de PV à la mort.")]
    public float HpDropChance = 0.25f;

    protected override float ContactRadius => ChampionContactRadius;

    /// <summary>
    /// Un champion compte comme tel pour la musique adaptative, le HUD — et pour le <b>plancher de
    /// dégâts du cran VI</b> (<see cref="EnemyBase.DealDiscreteDamage"/>).
    /// </summary>
    public override bool IsChampion => true;
}
