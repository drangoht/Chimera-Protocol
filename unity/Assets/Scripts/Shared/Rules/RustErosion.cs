using System;

/// <summary>
/// <b>Le front rongé de la Marée de Rouille</b> — la limite du terrain sûr n'est pas une arête
/// droite (logique pure, testable).
///
/// <para><b>Le défaut qu'elle corrige.</b> Signalé en jouant le 2026-08-22 : « la marée est un peu
/// trop carrée, dans la vraie vie la rouille n'est pas nette comme ça ». Le premier rendu posait
/// quatre rectangles pleins autour du rectangle sûr — géométriquement exact, et ne ressemblant à
/// rien de ce que le mot « rouille » promet. La corrosion progresse par <b>morsures</b> : elle
/// mange plus vite là où le métal est déjà entamé, et le front qu'elle laisse est dentelé.</para>
///
/// <para><b>Pourquoi la morsure appartient à la RÈGLE et pas seulement au rendu.</b> Il aurait été
/// bien plus simple de dessiner un bord irrégulier par-dessus une géométrie restée rectangulaire.
/// C'est exactement la classe de défaut que ce projet documente sans relâche : deux systèmes qui
/// disent deux choses, dont le plus visible est le faux. Un joueur qui voit une échancrure mordre
/// vers lui doit prendre des dégâts <i>à l'endroit qu'il voit</i>, sinon la seule information que la
/// marée donne — « à partir d'ici ça fait mal » — devient un mensonge de 70 pixels. Le contour
/// dessiné et le contour qui ronge sont donc <b>la même fonction</b>, évaluée des deux côtés.</para>
///
/// <para><b>La morsure ne va QUE vers l'intérieur.</b> <see cref="Offset"/> est toujours négatif ou
/// nul : le bord érodé est un sous-ensemble du rectangle nominal, jamais un débord. C'est ce qui
/// préserve la garantie de fin de partie sans avoir à la re-démontrer — à
/// <see cref="RustTide.CloseMinutes"/> le rectangle nominal est nul, donc le rectangle érodé aussi,
/// et aucune bosse de bruit ne peut fabriquer la poche sûre que le §38 existe pour interdire.
/// L'amplitude est en outre bornée par une <i>part</i> du demi-axe (<see cref="MaxShare"/>) : quand
/// l'arène se referme, la dentelure se referme avec elle au lieu de traverser de part en part une
/// zone sûre devenue plus petite qu'elle.</para>
///
/// <para><b>Pourquoi trois sinus et pas un bruit.</b> Cette fonction est évaluée sur le processeur
/// (les dégâts) <b>et</b> sur la carte graphique (le rendu), et les deux doivent tomber sur le même
/// contour. Un bruit à base de <c>frac(sin(dot(…)))</c> — l'idiome habituel des shaders — n'a aucune
/// garantie de reproductibilité entre un <c>float</c> HLSL et un <c>float</c> C#, et l'écart
/// arriverait précisément là où il se voit : sur le liseré. Une somme de trois sinusoïdes de
/// longueurs d'onde étrangères entre elles se recale au millième près des deux côtés, ne se répète
/// pas à l'œil, et coûte trois <c>sin</c>.</para>
///
/// <para>Rendu : <c>Resources/Shaders/RustTide.shader</c> (mêmes constantes, même formule).
/// Design : <c>docs/GDD.md</c> §38.</para>
/// </summary>
public static class RustErosion
{
    /// <summary>Côté droit de l'arène (x positif).</summary>
    public const int Right = 0;

    /// <summary>Côté gauche (x négatif).</summary>
    public const int Left = 1;

    /// <summary>Côté haut (y positif).</summary>
    public const int Top = 2;

    /// <summary>Côté bas (y négatif).</summary>
    public const int Bottom = 3;

    /// <summary>
    /// Profondeur maximale d'une morsure, en pixels.
    ///
    /// <para>72 px sur un demi-axe de 608 à 960 : assez pour que la dentelure se lise d'un coup d'œil
    /// à l'échelle où la caméra montre la marée, trop peu pour déplacer l'équilibrage — la fraction
    /// d'aire sûre perdue en moyenne est de l'ordre de 8 %, et la <i>date</i> de fermeture, qui est
    /// la seule chose dont dépend la garantie de fin, ne bouge pas d'une seconde.</para>
    /// </summary>
    public const float AmplitudePx = 72f;

    /// <summary>
    /// Part du demi-axe sûr qu'une morsure ne peut pas dépasser.
    ///
    /// <para>Sans ce plafond, la dernière minute donnerait une zone sûre de 40 px traversée de part
    /// en part par une dentelure de 72 : elle clignoterait entre « il reste un abri » et « il n'en
    /// reste pas », ce qui est le pire message possible à l'instant où le joueur cherche où se
    /// mettre. Avec lui, la dentelure se referme proportionnellement et la lecture reste la
    /// même du début à la fin.</para>
    /// </summary>
    public const float MaxShare = 0.5f;

    private const float Tau = 6.28318530718f;

    // Trois longueurs d'onde étrangères entre elles : le motif ne se referme pas sur lui-même à
    // l'échelle d'un côté d'arène, donc l'œil n'y trouve pas de période. La plus grande donne les
    // grandes échancrures, la plus courte le grain qui les rend « rongées » plutôt qu'ondulées.
    private const float Lambda1 = 337f;
    private const float Lambda2 = 139f;
    private const float Lambda3 = 53f;

    // Vitesses en cycles par seconde. Volontairement minuscules, et de signes opposés : ce qui doit
    // se voir n'est pas une ondulation qui défile mais une échancrure qui se creuse ici pendant
    // qu'une autre se comble là — le grignotement. Deux ondes qui vont en sens contraire donnent
    // cette interférence sur place ; toutes dans le même sens, on verrait la dentelure GLISSER le
    // long du bord, ce qu'aucune corrosion ne fait.
    private const float Speed1 =  0.035f;
    private const float Speed2 = -0.061f;
    private const float Speed3 =  0.092f;

    // Poids : la somme vaut exactement 1, donc le résultat couvre [-1, 1] sans renormalisation.
    private const float Weight1 = 0.50f;
    private const float Weight2 = 0.32f;
    private const float Weight3 = 0.18f;

    /// <summary>
    /// Décalage de phase propre à chaque côté. Sans lui, gauche et droite recevraient la même
    /// dentelure au même endroit et l'arène se lirait comme un test de Rorschach — une symétrie
    /// parfaite est la seule chose qu'un motif naturel ne fait jamais.
    /// </summary>
    private static float SidePhase(int side) => side * 0.377f;

    /// <summary>
    /// Profondeur relative de la morsure au point <paramref name="u"/> du côté
    /// <paramref name="side"/>, dans [0, 1] : 0 = bord intact, 1 = morsure maximale.
    ///
    /// <para><paramref name="u"/> se compte le long du bord, en pixels du monde (l'ordonnée pour les
    /// côtés verticaux, l'abscisse pour les horizontaux). C'est ce qui donne à la dentelure une
    /// taille constante à l'écran quelle que soit la longueur du côté restant.</para>
    /// </summary>
    public static float Bite01(int side, float u, float timeSeconds)
    {
        float phase = SidePhase(side);

        float n = Weight1 * (float)Math.Sin(Tau * (u / Lambda1 + timeSeconds * Speed1 + phase))
                + Weight2 * (float)Math.Sin(Tau * (u / Lambda2 + timeSeconds * Speed2 + phase * 2f))
                + Weight3 * (float)Math.Sin(Tau * (u / Lambda3 + timeSeconds * Speed3 + phase * 3f));

        return Math.Clamp(0.5f + 0.5f * n, 0f, 1f);
    }

    /// <summary>
    /// Amplitude effective de la morsure, en pixels. Trois bornes, et chacune répond à un cas qui
    /// s'est présenté :
    /// <list type="number">
    /// <item><see cref="AmplitudePx"/> — la profondeur voulue, en régime établi.</item>
    /// <item><c>safeHalf × <see cref="MaxShare"/></c> — la dentelure se referme avec l'arène, au lieu
    /// de traverser de part en part une zone sûre devenue plus petite qu'elle.</item>
    /// <item><b><c>arenaHalf − safeHalf</c> — la corrosion ne peut pas être plus profonde que ce
    /// qu'elle a déjà mangé.</b> Sans cette borne, la dentelure existe <i>avant</i> que la marée ne
    /// parte : le bord de l'arène serait mordu de 72 px dès la première seconde d'overtime, alors que
    /// <see cref="RustTide.GraceMinutes"/> existe précisément pour laisser une minute pendant
    /// laquelle rien ne bouge et où le joueur peut lire l'annonce. Pire, la règle pure rendrait des
    /// dégâts non nuls pour une run <i>hors overtime</i>, où la marée n'existe pas du tout. Cette
    /// borne-là n'est pas un garde-fou : c'est le moment où la dentelure naît, et elle grandit
    /// ensuite d'elle-même au rythme de l'avancée.</item>
    /// </list>
    /// </summary>
    public static float Amplitude(float safeHalf, float arenaHalf)
    {
        float safe = Math.Max(0f, safeHalf);
        float eaten = Math.Max(0f, arenaHalf - safe);
        return Math.Min(AmplitudePx, Math.Min(safe * MaxShare, eaten));
    }

    /// <summary>
    /// Déplacement du bord au point <paramref name="u"/>, en pixels. <b>Toujours ≤ 0</b> : la rouille
    /// mange du terrain, elle n'en rend jamais.
    /// </summary>
    public static float Offset(int side, float u, float safeHalf, float arenaHalf, float timeSeconds)
        => -Amplitude(safeHalf, arenaHalf) * Bite01(side, u, timeSeconds);

    /// <summary>
    /// Demi-axe sûr <i>effectif</i> au point <paramref name="u"/> : le contour que le joueur voit et
    /// celui à partir duquel il prend des dégâts, qui sont le même.
    /// </summary>
    public static float EdgeAt(int side, float u, float safeHalf, float arenaHalf, float timeSeconds)
        => Math.Max(0f, Math.Max(0f, safeHalf) + Offset(side, u, safeHalf, arenaHalf, timeSeconds));
}
