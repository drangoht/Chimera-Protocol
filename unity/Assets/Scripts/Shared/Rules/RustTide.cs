using System;

/// <summary>
/// <b>La Marée de Rouille</b> — l'arène se referme en overtime (logique pure, testable).
///
/// <para><b>Le défaut qu'elle corrige.</b> Passé le temps imparti, la menace n'avait plus qu'une
/// seule variable : la valeur d'un coup. Tout le reste était saturé — <see cref="SpawnCurve.MaxAlive"/>
/// atteint dès la 8ᵉ minute, l'intervalle de lot au plancher dès la 11ᵉ, la taille de lot clampée dès
/// la 4ᵉ — si bien que l'accélération <see cref="OvertimeEscalation.DensityAcceleration"/> ne
/// produisait plus <i>rien</i>. Et cette variable unique se heurtait à un plafond structurel : la
/// fenêtre d'invulnérabilité du joueur (0,45 s) borne les dégâts entrants à <b>2,2 coups par
/// seconde</b>, que cinq ennemis le touchent ou trois cents. <b>Ajouter des ennemis n'ajoutait pas de
/// danger.</b> En face, trois croissances joueur linéaires et <i>sans plafond</i> se cumulaient à
/// ~13 niveaux par minute (<see cref="OverloadCards"/>).</para>
///
/// <para><b>Pourquoi rétrécir plutôt que durcir.</b> Trois réglages successifs de la pente d'escalade
/// (1,5 → 3 → 2,25, cf. <see cref="OvertimeEscalation.StatAcceleration"/>) n'ont jamais tenu, et ne
/// pouvaient pas tenir : tant que la fin de la run dépend d'une croissance qui en dépasse une autre,
/// elle dépend d'un réglage de pente, que le prochain build du joueur déplacera. <b>Une fin garantie
/// se construit par une soustraction, pas par une course.</b> L'espace, lui, est fini : il s'épuise
/// quel que soit le build, et il s'épuise à une date connue (<see cref="CloseMinutes"/>).</para>
///
/// <para><b>Trois propriétés qui ne sont pas des détails.</b>
/// <list type="number">
/// <item>La marée <b>n'est pas un mur</b> : le joueur la traverse. Un bord dur transformerait chaque
/// coin en piège mortel dès qu'une poussée l'y colle, et le jeu demande de circuler en permanence
/// entre les masses — c'est déjà pourquoi les obstacles <i>écartent</i> au lieu d'arrêter net
/// (<c>ArenaObstacles.Resolve</c>). Traverser la marée est un choix coûteux, jamais une condamnation
/// immédiate : on peut y couper pour fuir un encerclement.</item>
/// <item>Elle ronge <b>en continu</b>, donc <b>hors i-frames</b>. C'est tout l'intérêt : un débit
/// ignore le plafond des 2,2 coups/s, qui est ce qui rendait la foule inoffensive. Un dégât de
/// contact de plus n'aurait rien changé.</item>
/// <item>Elle se compte en <b>fraction des PV max</b>, pas en points. Un montant absolu serait
/// distancé par <see cref="OverloadCards.Plating"/> en quelques minutes — +45 PV par prise, sans
/// plafond, c'est précisément la course qu'on refuse de courir. Un pourcentage est <b>insensible au
/// build</b> : la marée est un <i>chronomètre</i>, pas un ennemi de plus.</item>
/// </list></para>
///
/// <para><b>Deux phases, et la seconde n'est pas une décoration.</b> Tant qu'il reste du terrain sûr,
/// le danger est <i>géométrique</i> : il ne dépend que de l'enfoncement, et c'est la marée qui avance.
/// Mais un rectangle qui se ferme dégénère en un <b>point</b>, et ce point resterait indéfiniment
/// sûr — la garantie de fin tomberait précisément à l'instant où elle doit se refermer. D'où la
/// <b>submersion</b> (<see cref="FloorFractionPerSecond"/>) : passé <see cref="CloseMinutes"/>, un
/// taux plancher s'applique <i>partout</i>, y compris au centre exact. C'est la seule chose que le
/// temps pilote directement, et elle n'existe que pour fermer ce trou.</para>
///
/// <para>Design : <c>docs/GDD.md</c> §38.</para>
/// </summary>
public static class RustTide
{
    /// <summary>
    /// Minutes d'overtime pendant lesquelles la marée <b>ne bouge pas</b>.
    ///
    /// <para>Sans cette grâce, l'overtime devient punitif à la seconde où il commence et la fenêtre
    /// pendant laquelle le joueur constitue son build de fin de partie disparaît. Elle laisse aussi
    /// le temps de <i>lire</i> l'annonce et de comprendre ce qui va se passer — un joueur qui meurt
    /// sans avoir vu la règle conclut que le jeu est cassé, pas qu'il a mal joué.</para>
    /// </summary>
    public const float GraceMinutes = 1f;

    /// <summary>
    /// Minute d'overtime à laquelle il ne reste <b>aucun terrain sûr</b>.
    ///
    /// <para>C'est la garantie de fin : passé ce temps, la zone sûre est nulle et la submersion
    /// s'applique partout, à un taux qu'aucune régénération ne suit
    /// (<see cref="MaxFractionPerSecond"/> vide une barre pleine en quatre secondes). Aucun build ne
    /// peut la repousser, puisque le taux se compte en fraction des PV max.</para>
    ///
    /// <para>Réglée sur la fenêtre visée par le design — 5 à 10 minutes d'overtime (GDD §9.2) : la
    /// plupart des runs finissent avant, écrasées par une foule qui n'a plus où se disperser. La
    /// fermeture totale est le <b>plafond</b> de la distribution, pas sa moyenne.</para>
    /// </summary>
    public const float CloseMinutes = 11f;

    /// <summary>
    /// Durée, après <see cref="CloseMinutes"/>, sur laquelle la submersion monte de rien au taux
    /// maximal. Courte à dessein : une fois l'arène fermée, il n'y a plus de décision à prendre, et
    /// étirer l'agonie n'ajoute pas de jeu.
    /// </summary>
    public const float SubmersionRampMinutes = 0.5f;

    /// <summary>
    /// Fraction des PV max perdue par seconde au <b>bord</b> de la marée (profondeur nulle).
    ///
    /// <para>Volontairement soutenable : 2 %/s permet d'y entrer quelques secondes pour contourner un
    /// encerclement. Une bordure qui tue au contact ne serait pas une zone à traverser, mais le mur
    /// que le point 1 de l'en-tête écarte.</para>
    /// </summary>
    public const float EdgeFractionPerSecond = 0.02f;

    /// <summary>Profondeur (px) sur laquelle les dégâts gagnent <see cref="DepthFractionPerSecond"/>.</summary>
    public const float DepthScalePx = 200f;

    /// <summary>Fraction des PV max ajoutée par seconde et par <see cref="DepthScalePx"/> d'enfoncement.</summary>
    public const float DepthFractionPerSecond = 0.06f;

    /// <summary>
    /// Plafond du taux de rongement (fraction des PV max par seconde).
    ///
    /// <para>À 25 %/s, quatre secondes suffisent à vider une barre pleine — la fin est garantie sans
    /// qu'un nombre parte à l'infini quand la profondeur atteint la demi-diagonale de l'arène.</para>
    /// </summary>
    public const float MaxFractionPerSecond = 0.25f;

    /// <summary>
    /// Part des demi-dimensions de l'arène encore sûre après <paramref name="overtimeMinutes"/>
    /// d'overtime : 1 avant que la marée ne parte, 0 à <see cref="CloseMinutes"/>.
    ///
    /// <para>La fraction décroît linéairement, donc l'<b>aire</b> sûre décroît en carré : l'espace
    /// s'effondre de plus en plus vite sans qu'aucune courbe ne soit écrite. La foule se concentre
    /// d'elle-même, puisque les ennemis naissent toujours en bordure d'arène et convergent.</para>
    /// </summary>
    public static float SafeFraction(float overtimeMinutes)
    {
        float t = Math.Max(0f, overtimeMinutes) - GraceMinutes;
        if (t <= 0f) return 1f;

        float span = CloseMinutes - GraceMinutes;
        if (span <= 0f) return 0f;

        return Math.Clamp(1f - t / span, 0f, 1f);
    }

    /// <summary>
    /// Taux plancher appliqué <b>partout</b> une fois l'arène fermée — la submersion. Nul tant qu'il
    /// reste du terrain sûr : avant <see cref="CloseMinutes"/>, se tenir au centre ne coûte rien, et
    /// c'est ce qui fait de la zone sûre une récompense de placement.
    /// </summary>
    public static float FloorFractionPerSecond(float overtimeMinutes)
    {
        float over = Math.Max(0f, overtimeMinutes) - CloseMinutes;
        if (over <= 0f) return 0f;

        // Pas de garde-fou sur une rampe nulle : SubmersionRampMinutes est une constante non nulle,
        // et le compilateur le sait — la branche était signalée inatteignable (CS0162). Du code qui
        // ne décide de rien.
        return Math.Min(MaxFractionPerSecond,
                        MaxFractionPerSecond * (over / SubmersionRampMinutes));
    }

    /// <summary>
    /// Enfoncement (px) d'un point dans la marée : 0 en terrain sûr, croissant vers le bord de
    /// l'arène.
    ///
    /// <para>C'est la <b>distance euclidienne au rectangle sûr</b>, et non l'écart sur le seul axe le
    /// plus engagé : un coin enfonce sur deux axes à la fois et doit donc ronger plus fort qu'un
    /// milieu de bord. Sans quoi les quatre coins seraient les meilleurs abris de la fin de partie —
    /// exactement les endroits où l'on ne veut pas que le joueur s'installe.</para>
    /// </summary>
    public static float Depth(float x, float y, float safeHalfWidth, float safeHalfHeight)
    {
        float dx = Math.Max(0f, Math.Abs(x) - Math.Max(0f, safeHalfWidth));
        float dy = Math.Max(0f, Math.Abs(y) - Math.Max(0f, safeHalfHeight));
        if (dx <= 0f && dy <= 0f) return 0f;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Fraction des PV max rongée par seconde à <paramref name="depthPx"/> d'enfoncement, submersion
    /// comprise. Nulle en terrain sûr tant que l'arène n'est pas fermée.
    /// </summary>
    public static float FractionPerSecond(float depthPx, float overtimeMinutes)
    {
        float geometric = depthPx > 0f
            ? EdgeFractionPerSecond + DepthFractionPerSecond * (depthPx / DepthScalePx)
            : 0f;

        float rate = Math.Max(geometric, FloorFractionPerSecond(overtimeMinutes));
        return Math.Min(MaxFractionPerSecond, rate);
    }

    /// <summary>
    /// PV rongés pendant <paramref name="deltaSeconds"/> à la position donnée. Point d'entrée unique
    /// du moteur : il n'a ni à composer les fonctions ci-dessus, ni à connaître leur ordre.
    /// </summary>
    public static float DamageOverTime(float x, float y, float maxHp,
                                       float overtimeMinutes, float deltaSeconds,
                                       float arenaHalfWidth, float arenaHalfHeight)
    {
        if (maxHp <= 0f || deltaSeconds <= 0f) return 0f;

        float fraction = SafeFraction(overtimeMinutes);
        float depth = Depth(x, y, arenaHalfWidth * fraction, arenaHalfHeight * fraction);
        return maxHp * FractionPerSecond(depth, overtimeMinutes) * deltaSeconds;
    }
}
