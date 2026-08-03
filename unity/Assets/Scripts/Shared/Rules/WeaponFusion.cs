using System;

/// <summary>
/// Règles des fusions d'armes — logique pure, extraite pour être <b>testable</b>
/// (docs/UNITY_MIGRATION_PLAN.md, Lot 3).
///
/// <para><b>Pourquoi cette classe existe.</b> La version 1.21.0 du jeu a corrigé un déséquilibre
/// majeur : les 9 fusions <b>divisaient le DPS de fin de run par 3 à 6</b> (mesuré : 103 DPS tout
/// fusionné contre 410 avec une arme montée conservée, à niveau de joueur égal). La carte présentée
/// comme « l'évolution ultime » était en réalité le pire choix du jeu.</para>
///
/// <para>La cause tenait en deux points, tous deux reproduits ici :</para>
/// <list type="number">
///   <item>la fusion <b>repartait au niveau 1</b>, effaçant tous les niveaux investis — et la perte
///         était <b>définitive</b>, puisque l'arme de base disparaît du pool de cartes et que la
///         fusion n'y entrait pas ;</item>
///   <item>ses dégâts n'étaient <b>jamais multipliés</b> : elle gardait sa valeur de fiche à vie
///         quand l'arme remplacée montait, elle, avec les niveaux et les passifs.</item>
/// </list>
///
/// <para>Sous Godot, ces règles vivaient à l'intérieur d'un <c>Node</c>, donc hors de portée des
/// tests. Les extraire est le seul moyen de <b>verrouiller</b> la régression : le portage est
/// exactement le moment où un tel bug se réintroduit sans que personne ne le voie.</para>
/// </summary>
public static class WeaponFusion
{
    /// <summary>
    /// Niveau au-delà duquel les dégâts d'une fusion sont extrapolés. Une fusion n'a qu'<b>un seul</b>
    /// palier de statistiques — sa mécanique (rafale perforante, aura continue, essaim orbital) ne
    /// se décrit pas dans le tableau de niveaux du JSON, donc sa classe pose ses valeurs elle-même.
    /// </summary>
    public const int DefinedMax = 1;

    /// <summary>
    /// Niveau hérité par une fusion de l'arme qu'elle remplace.
    /// </summary>
    /// <param name="replacedWeaponLevel">
    /// Niveau atteint par l'arme remplacée. Zéro ou négatif signifie « inconnue » — on retombe
    /// alors sur 1 plutôt que sur 0, car une arme de niveau 0 n'existe pas.
    /// </param>
    /// <remarks>
    /// ⚠ <b>Ne jamais remplacer par une constante.</b> C'est précisément le retour à 1 qui
    /// effaçait les niveaux investis en 1.21.0.
    /// </remarks>
    public static int InheritedLevel(int replacedWeaponLevel)
        => Math.Max(1, replacedWeaponLevel);

    /// <summary>
    /// Dégâts effectifs d'une fusion : sa valeur de fiche, portée au niveau hérité puis multipliée
    /// par le bonus de dégâts du joueur.
    /// </summary>
    /// <param name="baseDamage">Valeur de fiche posée par la classe de la fusion.</param>
    /// <param name="level">Niveau hérité (voir <see cref="InheritedLevel"/>).</param>
    /// <param name="damageMultiplier">Multiplicateur global du joueur (Noyau Thermique, Hub…).</param>
    /// <remarks>
    /// Le niveau 1 correspond aux valeurs d'origine ; au-delà, la même progression que pour une arme
    /// dépassant ses niveaux définis (<see cref="WeaponLeveling"/>), soit +10 % par niveau.
    /// <para>⚠ Toujours repartir de la valeur de <b>fiche</b>, jamais de la valeur courante : le
    /// recalcul est rejoué à chaque achat de passif, et cumulerait sinon les multiplicateurs.</para>
    /// </remarks>
    public static float EffectiveDamage(float baseDamage, int level, float damageMultiplier)
        => WeaponLeveling.ExtrapolatedDamage(baseDamage, level, DefinedMax) * damageMultiplier;

    /// <summary>
    /// Une fusion est-elle débloquée ? Elle exige l'arme source à un niveau minimum <b>et</b> le
    /// passif associé.
    /// </summary>
    public static bool CanFuse(int weaponLevel, int requiredWeaponLevel, bool hasRequiredPassive)
        => hasRequiredPassive && weaponLevel >= requiredWeaponLevel;
}
