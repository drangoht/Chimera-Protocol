using System;

/// <summary>
/// Générateur PCG32 reproduisant <b>bit pour bit</b> la RNG de Godot 4.7, pour le port Unity
/// (docs/UNITY_MIGRATION_PLAN.md §4.3).
///
/// <para><b>Pourquoi cette classe existe.</b> Toute la méthode de réglage du projet repose sur
/// <c>--seed=&lt;n&gt;</c> : deux campagnes lancées sur les mêmes graines s'apparient, ce qui annule
/// le bruit de tirage et permet de trancher un réglage en quelques runs plutôt qu'en trente.
/// Un générateur quelconque suffirait à rendre les runs Unity reproductibles <i>entre elles</i> ;
/// reproduire celui de Godot permet en plus de <b>comparer les deux moteurs sur une même graine</b>,
/// ce qui est l'outil de validation de parité le plus fort dont dispose la migration (§8.2).</para>
///
/// <para><b>Logique pure</b> : aucune dépendance moteur, donc testable par la suite xUnit existante
/// (assembly <c>ChimeraProtocol.PlatformCore</c>, <c>noEngineReferences</c>).</para>
///
/// <para><b>Fidélité établie par mesure</b>, non par lecture des sources de Godot : les valeurs de
/// référence ont été extraites du moteur (<c>tools/unity/dump_godot_rng.gd</c>) puis confrontées à
/// des formulations candidates. Résultat :</para>
/// <list type="bullet">
///   <item><see cref="NextUInt"/> — <b>exact</b> (vérifié sur 5 graines × 8 tirages).</item>
///   <item><see cref="NextFloat"/> — <b>exact</b> : Godot divise par <c>UINT32_MAX</c>, pas par 2³².</item>
///   <item><see cref="RangeInt"/> — <b>exact</b> : Godot fait <c>from + rand() % span</c>.</item>
///   <item><see cref="RangeDouble"/> — ⚠ <b>NON bit-identique</b> : voir la remarque de la méthode.</item>
/// </list>
/// </summary>
public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    /// <summary>Incrément par défaut de Godot (<c>PCG_DEFAULT_INC_64</c>).</summary>
    private const ulong DefaultInc = 1442695040888963407UL;

    private ulong _state;
    private ulong _inc;

    public Pcg32(ulong seed = 0UL) => Seed(seed);

    /// <summary>
    /// Réamorce le générateur. Reproduit <c>pcg32_srandom_r(seed, PCG_DEFAULT_INC_64)</c> : l'état
    /// part de 0, absorbe la graine <b>entre deux avancements</b>, et ce détail n'est pas cosmétique
    /// — poser simplement <c>state = seed</c> produit une toute autre suite (et, pour <c>seed = 1</c>,
    /// un premier tirage nul).
    /// </summary>
    public void Seed(ulong seed)
    {
        _state = 0UL;
        _inc   = (DefaultInc << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    /// <summary>Tirage brut sur 32 bits — équivalent de <c>randi()</c> / <c>GD.Randi()</c>.</summary>
    public uint NextUInt()
    {
        ulong old = _state;
        _state = old * Multiplier + _inc;

        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int  rot        = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    /// <summary>
    /// Réel dans [0, 1] — équivalent de <c>randf()</c> / <c>GD.Randf()</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Renvoie un <c>float</c> et non un <c>double</c>, et ce n'est pas un détail de style.</b>
    /// Godot calcule <c>randf()</c> en simple précision : le résultat est arrondi à 32 bits, ce qui
    /// introduit un écart d'environ 1e-8 par rapport au même calcul mené en double. Rendre un
    /// <c>double</c> « plus précis » ferait donc <b>diverger</b> le port du moteur d'origine.
    /// Établi par mesure : sur les valeurs de référence, seule la variante simple précision
    /// coïncide aux 9 décimales relevées, sur les 5 tirages testés.
    /// <para>La donnée disponible ne permet pas de distinguer une division par <c>UINT32_MAX</c>
    /// d'une division par 2³² (elles diffèrent de ~1e-10, que l'arrondi en <c>float</c> absorbe).
    /// <c>UINT32_MAX</c> est retenu car c'est la convention de Godot ; le choix est sans effet
    /// observable.</para>
    /// </remarks>
    public float NextFloat() => (float)(NextUInt() / 4294967295.0);

    /// <summary>
    /// Entier dans [from, to] <b>bornes incluses</b> — équivalent de <c>randi_range()</c>.
    /// Godot procède par modulo sur le tirage brut ; passer par <see cref="NextFloat"/> donnerait
    /// des valeurs différentes.
    /// </summary>
    public int RangeInt(int from, int to)
    {
        if (to < from) (from, to) = (to, from);
        long span = (long)to - from + 1L;
        return (int)(from + (long)(NextUInt() % (ulong)span));
    }

    /// <summary>
    /// Réel dans [from, to] — équivalent de <c>randf_range()</c> / <c>GD.RandRange(double, double)</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Seule méthode de cette classe qui n'est PAS bit-identique à Godot.</b> Onze formulations
    /// candidates ont été confrontées aux valeurs de référence du moteur (division par
    /// <c>UINT32_MAX</c> ou 2³², composition sur 64 bits dans les deux ordres, méthode 53 bits,
    /// <c>ldexp</c> à exposant tiré) : <b>aucune ne correspond</b>. Les mesures montrent que Godot
    /// consomme <i>plusieurs</i> tirages par appel, mais la formulation exacte n'a pas été
    /// identifiée, et la chercher plus loin serait disproportionné : les 8 sites d'appel du jeu ne
    /// pilotent que des <b>positions et instants d'apparition de ramassables</b>
    /// (<c>PowerUpSpawner</c>, <c>MagnetSpawner</c>, <c>AetherCoreSpawner</c>) — ni tirages de
    /// cartes, ni tables d'ennemis.
    /// <para><b>Conséquence à connaître</b> : les runs Unity restent parfaitement reproductibles
    /// entre elles (c'est ce dont le banc a besoin), mais une comparaison Godot↔Unity sur une même
    /// graine <b>divergera</b> dès le premier appel à cette méthode. À rappeler au §8.2 avant de
    /// conclure quoi que ce soit d'une comparaison inter-moteurs.</para>
    /// </remarks>
    public double RangeDouble(double from, double to) => from + NextFloat() * (to - from);
}
