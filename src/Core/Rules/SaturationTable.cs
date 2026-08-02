using System;
using System.Collections.Generic;

/// <summary>
/// <b>Saturation</b> — l'échelle de challenge de fin de partie (logique pure, testable).
///
/// <para><b>Le problème.</b> La défense du joueur croît <b>sans plafond</b> (cartes de surcharge :
/// +45 PV par prise, 270 PV/min mesurés) alors que la menace suit une <b>courbe fixe</b>, densité déjà
/// saturée dès la 8ᵉ minute, et que le plafond de difficulté valait <b>×1,35</b> face à un DPS joueur
/// qui fait ×700 sur une run. Surtout, la menace ne posait qu'<b>une seule question</b> — des
/// statistiques — donc le joueur n'avait qu'une seule réponse, et il gagnait toujours cet échange.
/// Le §31 a réglé l'escalade trois fois (1,5 → 3 → 2,25) sans jamais toucher ce fond.</para>
///
/// <para><b>Le parti pris : un cran = une règle nommée qui retire une certitude.</b> Pas un
/// multiplicateur invisible de plus. Le joueur lit la règle <i>avant</i> de lancer, et peut donc dire
/// pourquoi il est mort et ce qu'il changera. Les crans visent en priorité les axes où le joueur est
/// sans plafond — les <b>soins reçus</b> d'abord, puisque le canal de soin dominant mesuré est le soin
/// ponctuel (86,4 PV/s contre 8,2 de régénération).</para>
///
/// <para><b>Rang 1 = l'ancien « Difficile »</b>, aux mêmes valeurs. Ce n'est pas un hasard exploité
/// après coup : c'est ce qui permet aux records et complétions déjà gagnés de rester valides. « Facile »
/// n'entre pas dans cette échelle — c'est un mode d'<b>assistance</b> (accessibilité), pas une
/// saturation négative.</para>
///
/// <para>Plan : <c>docs/ENDGAME_PLAN.md</c>. Chaque cran doit faire baisser le <b>temps soutenable</b>
/// de plus de 6 % au banc apparié (<c>tools/power_curve_multi.py</c>) — sous ce seuil, un cran coûte un
/// palier au joueur sans rien changer, et ne mérite pas d'exister.</para>
/// </summary>
public static class SaturationTable
{
    /// <summary>Rang maximum livré. Les crans VII-X du plan viendront au lot 4.</summary>
    public const int MaxRank = 6;

    /// <summary>Un cran : son identité de loc et la règle qu'il ajoute.</summary>
    public sealed class Rank
    {
        /// <summary>Rang (1..<see cref="MaxRank"/>).</summary>
        public int Value { get; }
        /// <summary>Clé de traduction du nom (ex. « Hémorragie »).</summary>
        public string NameKey { get; }
        /// <summary>Clé de traduction de la règle, telle qu'affichée avant de lancer la run.</summary>
        public string RuleKey { get; }

        public Rank(int value, string nameKey, string ruleKey)
        {
            Value = value; NameKey = nameKey; RuleKey = ruleKey;
        }
    }

    /// <summary>
    /// Les crans, dans l'ordre. Cumulatifs : la saturation N applique les règles 1…N — on ne panache
    /// pas (ce seraient des mutateurs, explicitement hors périmètre du plan).
    /// </summary>
    public static readonly IReadOnlyList<Rank> Ranks = new[]
    {
        new Rank(1, "SAT_1_NAME", "SAT_1_RULE"),   // Hémorragie  — soins reçus −40 %
        new Rank(2, "SAT_2_NAME", "SAT_2_RULE"),   // Meute       — ennemis plus durs (= ex-Difficile)
        new Rank(3, "SAT_3_NAME", "SAT_3_RULE"),   // Compte à rebours — overtime à la 10ᵉ minute
        new Rank(4, "SAT_4_NAME", "SAT_4_RULE"),   // Sans filet  — le niveau ne soigne plus, filets coupés
        new Rank(5, "SAT_5_NAME", "SAT_5_RULE"),   // Élite ordinaire — élites ×3
        new Rank(6, "SAT_6_NAME", "SAT_6_RULE"),   // Purificateur — les champions frappent en % des PV max
    };

    private static int Clamp(int rank) => Math.Clamp(rank, 0, MaxRank);

    // ── Cran II — « Meute » : reprend exactement l'ancien « Difficile » ──────────────────────────
    // Ces trois multiplicateurs ne montent PAS avec les crans suivants, à dessein : le parti pris est
    // qu'un cran ajoute une RÈGLE, pas un facteur. Empiler des statistiques est précisément ce qui
    // avait échoué (le joueur gagne cet échange), et cela rendrait chaque cran illisible.
    //
    // ⚠ Ces statistiques étaient le cran I jusqu'au 2026-07-30. Mesuré seul, il faisait baisser le
    // temps soutenable de 7 % — à peine au-dessus du seuil que la campagne sait détecter — et le
    // testeur n'a « vu aucune différence » en jouant une session entière. Un premier pas invisible fait
    // conclure que tout le système est inopérant, si bien que la porte d'entrée est passée à
    // « Hémorragie », qui touche le canal de soin dominant et se sent immédiatement.
    //
    // ── Relevage du 2026-08-02 (1,30/1,35/1,25 → 1,45/1,80/1,40) ────────────────────────────────
    // Motif : l'échelle entière a été jouée jusqu'au cran VI sur le Sanctuaire (palier de menace 0)
    // et gagnée du premier coup. Les trois multiplicateurs ne montent PAS au même taux, parce qu'ils
    // n'ont pas le même rendement mesuré :
    //
    //   • DÉGÂTS ×1,80 — le seul des trois qui touche la barre de vie, donc le seul qui puisse
    //     produire un frôlement (§34.6). C'est celui qu'on pousse le plus.
    //   • PV ×1,45 — allonge le temps de nettoyage, donc l'exposition. Bridé volontairement : il
    //     traverse ChampionHpMult vers le BOSS (×1,25 ici contre ×1,17 avant), et la règle 3 du
    //     §34.4 interdit d'en faire un mur de patience — il est calibré sur un TTK joué (§20.6).
    //   • SPAWN ×1,40 — le moins relevé, et ce n'est pas de la prudence : le cap simultané de 300
    //     est SATURÉ dès la 8ᵉ minute (§34.1). Au-delà, monter ce facteur ne change rien du tout ;
    //     son effet réel se joue uniquement sur la phase de construction du build.

    /// <summary>Multiplicateur de PV des ennemis basiques.</summary>
    public static float EnemyHpMult(int rank) => Clamp(rank) >= 2 ? 1.45f : 1.00f;

    /// <summary>Multiplicateur de dégâts des ennemis (tous, champions inclus).</summary>
    public static float EnemyDamageMult(int rank) => Clamp(rank) >= 2 ? 1.80f : 1.00f;

    /// <summary>Multiplicateur du volume de spawn.</summary>
    public static float SpawnMult(int rank) => Clamp(rank) >= 2 ? 1.40f : 1.00f;

    /// <summary>
    /// PV des CHAMPIONS (mini-boss et boss de fin) : bonus amorti par
    /// <see cref="LevelThreat.ChampionHpSoftening"/>, pour la même raison qu'au §28 — battre le boss
    /// conditionne la progression, et il est calibré sur un <b>TTK joué</b> (GDD §20.6). Un boss qui
    /// gagne 30 % de PV à chaque cran deviendrait un mur de patience, pas de difficulté.
    /// </summary>
    public static float ChampionHpMult(int rank)
        => 1f + (EnemyHpMult(rank) - 1f) * LevelThreat.ChampionHpSoftening;

    // ── Cran I — « Hémorragie » ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur des soins <b>reçus</b> (orbes, lifesteal, carte Blindage qui soigne de son gain).
    /// Vise le canal de soin <b>dominant</b> : 86,4 PV/s mesurés en overtime, contre 8,2 pour la
    /// régénération. Un cran qui l'ignorerait n'agirait que sur 10 % de la défense réelle du joueur.
    ///
    /// <para><b>C'est le cran I depuis le 2026-07-30</b>, et c'est un choix de lisibilité autant que
    /// d'équilibrage : la porte d'entrée de l'échelle doit se <i>sentir</i>. Les statistiques (« Meute »,
    /// désormais cran II) occupaient cette place et le testeur n'a « vu aucune différence » sur une
    /// session entière — le banc lui donne raison, leur effet sur le temps soutenable (−7 %) frôlait le
    /// seuil que la campagne sait détecter. Un premier pas invisible fait conclure que tout le système
    /// est inopérant.</para>
    ///
    /// <para><b>0,60 → 0,35 le 2026-08-02.</b> Ce n'est pas un ajustement de dosage mais la
    /// conséquence directe du §34.4 ter : au cran 0 le joueur reçoit <b>293,6 PV/s et n'en retient que
    /// 58,8</b> — il en jette 80 %. À 0,60 l'offre tombe à ~176 PV/s, soit <i>encore trois fois</i> ce
    /// qu'il sait absorber : la coupe disparaissait entièrement dans le gaspillage, et c'est
    /// précisément ce que la mesure a montré (l'offre baisse de 46 %, le joueur en retient
    /// <i>davantage</i>). À 0,35 elle vaut ~103 PV/s pour 58,8 retenus — la marge de gaspillage passe
    /// de ×5 à ×1,75, et c'est le premier réglage où la première goutte peut réellement manquer.
    /// Descendre plus bas (0,25 ≈ ×1,25 de marge) ferait basculer la porte d'entrée de l'échelle en
    /// mur, ce que la règle 1 du §34.4 interdit.</para>
    ///
    /// <para><b>Ce cran porte DEUX leviers depuis le 2026-08-02</b>, comme « Sans filet » en porte
    /// plusieurs pour une seule règle : la réduction ci-dessus, et la <b>suppression du soin de passage
    /// de niveau</b> (<see cref="LevelUpHealsEnabled"/>, venu du cran IV). Les deux attaquent le même
    /// canal, et le second y pesait à lui seul plus que tout le reste réuni.</para>
    /// </summary>
    public static float HealingMult(int rank) => Clamp(rank) >= 1 ? 0.35f : 1.00f;

    // ── Cran III — « Compte à rebours » ─────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur de la durée de run avant overtime. Exprimé en facteur et non en minutes fixes :
    /// la durée de référence vit dans <c>data/meta_upgrades.json</c> (780 s aujourd'hui) et l'upgrade
    /// méta <c>overtime_stabilizer</c> la module déjà — une valeur en dur ici les contredirait.
    /// 0,62 × 13 min ≈ <b>8 minutes</b> (0,77 ≈ 10 min jusqu'au 2026-08-02).
    ///
    /// <para>Ce cran attaque le <b>temps de construction du build</b>, pas la puissance : entrer en
    /// overtime trois minutes plus tôt, c'est y entrer avec un arsenal non saturé. Le relevé du
    /// 2026-07-29 a montré que l'état d'entrée en overtime explique un facteur <b>2,4</b> sur la survie
    /// — c'est donc l'un des leviers les plus puissants du lot, et le moins coûteux en code.</para>
    ///
    /// <para><b>0,77 → 0,62 le 2026-08-02</b>, soit 10 → 8 minutes. C'est le cran relevé le plus
    /// franchement, et pour la raison inverse de celle du cran II : il ne repose sur <b>aucun
    /// multiplicateur</b>, donc il échappe au rendement décroissant qui rend les statistiques
    /// invisibles. Retirer deux minutes de construction retire des niveaux d'arme entiers, pas un
    /// pourcentage. ⚠ C'est aussi celui qui rapproche le plus le boss d'un mur : il arrive à la fin du
    /// compte à rebours, donc trois minutes plus tôt qu'en run normale, face à un arsenal d'autant
    /// moins construit. Le cran III n'étant atteignable qu'après avoir battu I et II, le coût est
    /// assumé — mais c'est ici qu'il faut regarder si la victoire devient inaccessible.</para>
    /// </summary>
    public static float RunDurationMult(int rank) => Clamp(rank) >= 3 ? 0.62f : 1.00f;

    // ── Cran IV — « Sans filet » ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Les consommables méta de survie — <c>extra_life</c> (Noyau de Secours) et
    /// <c>damage_absorb</c> (Plaque Adaptative) — sont-ils actifs ?
    ///
    /// <para>Ce cran vise directement le <b>power-creep de la méta-progression</b> : ces deux filets
    /// s'achètent une fois et profitent à toutes les runs suivantes, si bien qu'une partie ne commence
    /// jamais vraiment à zéro. Les retirer ne change aucune statistique — il rend simplement la
    /// première erreur définitive.</para>
    ///
    /// <para><b>Note d'ordonnancement</b> : ce cran était le X du plan et occupe la place du IV
    /// (« une phase de boss en plus »), déplacé au lot 2. Motif : la 4ᵉ phase demande de paramétrer
    /// <c>BossPhases</c> (<c>Count</c> est une constante, les tables sont fixes) et touche le HUD, la
    /// télémétrie, une douzaine d'appels dans <c>RustedCore</c> et des tests aux seuils codés en dur —
    /// donc une règle publiée à re-tester sur cinq incarnations. Cela n'a pas sa place dans le lot qui
    /// valide le cadre de saturation lui-même.</para>
    /// </summary>
    public static bool SafetyNetsEnabled(int rank) => Clamp(rank) < 4;

    // ⚠ Le soin de passage de niveau A APPARTENU À CE CRAN du 2026-07-30 au 2026-08-02, et c'est lui
    // qui le rendait UNIVERSEL — les trois filets restants s'achètent. Il est parti au cran I (voir
    // plus bas) parce qu'un rattrapage de ~158 % des PV max par minute rendait tout durcissement de la
    // faune sans effet en dessous du cran IV. Conséquence à connaître : ce cran ne retire plus rien à
    // un joueur qui n'a rien acheté au Hub — le défaut exact que la règle 2 du §34.4 interdit. Si un
    // jour le cas se présente, c'est ici qu'il faudra rendre au cran un levier universel.

    // ── Le soin de passage de niveau appartient au cran I ────────────────────────────────────────

    /// <summary>
    /// Le passage de niveau soigne-t-il encore (25 % des PV max) ? Non <b>dès le cran I</b>.
    ///
    /// <para><b>Déplacé du cran IV au cran I le 2026-08-02, sur une run jouée.</b> Le testeur au rang 1 :
    /// « il a fallu que je reste immobile en overtime pour vraiment mourir ». Le calcul explique
    /// pourquoi, et il ne laisse aucune place au doute : en overtime les niveaux tombent à
    /// <b>~18 par minute</b> (mesuré), chacun rendant 25 % des PV max — soit, <i>même après</i>
    /// Hémorragie (×0,35), de l'ordre de <b>158 % des PV max rendus chaque minute</b>, gratuitement et
    /// sans décision du joueur. Aucun réglage de la faune ne peut lutter contre un rattrapage de cette
    /// taille ; le durcir revenait à remplir un seau percé.</para>
    ///
    /// <para><b>Pourquoi le cran I et pas un cran intermédiaire.</b> Ce n'est pas une règle de plus mais
    /// <b>le même canal</b> qu'Hémorragie : l'un réduit les soins reçus, l'autre supprime la source de
    /// soin la plus grosse du jeu. Les séparer donnait deux crans qui disent la même chose à des
    /// hauteurs différentes, alors que le §34.4 ter a montré que couper le canal des soins <i>à moitié</i>
    /// ne retire rien tant que le gaspillage absorbe la coupe. Réunis, ils forment une règle unique et
    /// lisible : <i>« on ne se soigne presque plus, et monter de niveau ne répare rien »</i>.</para>
    ///
    /// <para>⚠ <b>Ce que ce déplacement coûte, et il faut le savoir.</b> Le cran IV ne conserve que des
    /// filets <b>achetés</b> (Noyau de Secours, Plaque Adaptative, Stabilisateur de Surcharge), ce qui
    /// contredit la règle 2 du §34.4 : « un cran ne repose jamais sur un levier optionnel ». C'était
    /// précisément le soin de niveau qui rendait ce cran universel. Régression assumée — on ne peut pas
    /// avoir le rattrapage aux deux endroits, et un joueur qui atteint le cran IV a forcément assez joué
    /// pour posséder ces trois achats (la sauvegarde de référence les a tous à leur niveau maximum).
    /// <b>À surveiller</b> : si un jour un joueur signale que le cran IV ne lui retire rien, c'est ici
    /// qu'il faudra lui rendre un levier universel.</para>
    /// </summary>
    public static bool LevelUpHealsEnabled(int rank) => Clamp(rank) < 1;

    /// <summary>
    /// L'upgrade méta <c>overtime_stabilizer</c> (Stabilisateur de Surcharge, −5 %/niveau jusqu'à
    /// −15 % sur la pente d'escalade d'overtime) est-il actif ? Non à partir du cran IV.
    ///
    /// <para><b>Troisième filet coupé par ce cran, ajouté le 2026-08-02.</b> Les deux premiers
    /// (<see cref="SafetyNetsEnabled"/>) sauvent d'une mort ; celui-ci empêche la mort d'arriver, en
    /// aplatissant la seule courbe du jeu qui monte sans fin. C'est donc le plus fort des trois sur
    /// une run longue — exactement le régime où le joueur s'ennuie — et il était intact à tous les
    /// crans.</para>
    ///
    /// <para>Il illustre aussi le motif du chantier : c'est un objet <b>acheté au Hub</b> qui rend le
    /// jeu plus facile pour toutes les runs suivantes. « Sans filet » est le cran dont le nom promet
    /// justement d'annuler ces acquis-là, et le voir survivre à l'échelle entière était une
    /// incohérence. ⚠ La règle 2 du §34.4 (« jamais un levier optionnel ») reste satisfaite : ce cran
    /// coupe aussi le filet <b>universel</b> qu'est le soin de passage de niveau, donc il retire
    /// quelque chose même à qui n'a rien acheté.</para>
    /// </summary>
    public static bool MetaOvertimeDampeningEnabled(int rank) => Clamp(rank) < 4;

    // ── Cran V — « Élite ordinaire » ────────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur de la fréquence d'apparition des élites (affixes du §« Affixes d'élite »).
    /// L'élite cesse d'être un événement : les affixes (Blindé, Explosif, Vampirique…) deviennent la
    /// texture normale de la nuée, ce qui demande de lire la foule au lieu de la traverser.
    ///
    /// <para><b>×3 → ×4 le 2026-08-02.</b> Ce facteur n'agit qu'en amont d'un plafond
    /// (<see cref="EliteChanceCap"/>) : le relever seul ne fait que faire <i>atteindre le plafond plus
    /// tôt</i> dans la run. C'est pour cela qu'il monte peu et que le plafond, lui, monte franchement
    /// — c'est ce dernier qui décide de la texture de la nuée en fin de partie.</para>
    /// </summary>
    public static float EliteFrequencyMult(int rank) => Clamp(rank) >= 5 ? 4.00f : 1.00f;

    /// <summary>
    /// Plafond de la probabilité d'élite à ce rang. Relevé <b>explicitement</b> au cran V au lieu de
    /// laisser le facteur ×3 traverser le plafond d'origine : 3 × 0,28 vaudrait 84 % d'élites, soit la
    /// « horde » que <see cref="EliteAffixTable.MaxChance"/> interdit par commentaire, avec le coût des
    /// affixes sur 200-300 entités. À 0,70 la grande majorité de la nuée peut être élite, jamais la
    /// totalité — il reste toujours des ennemis ordinaires pour lire la foule, et la lecture de la
    /// foule est précisément ce que ce cran demande.
    ///
    /// <para><b>0,55 → 0,70 le 2026-08-02</b> — c'est ici que se joue réellement le durcissement du
    /// cran V, pas dans <see cref="EliteFrequencyMult"/>. ⚠ <b>Point de vigilance performance</b> :
    /// chaque élite porte un affixe et un <c>EliteAura</c>, sur une cible de 200-300 entités
    /// simultanées. +27 % d'élites est le seul relevage de ce chantier dont le coût n'est pas
    /// gratuit — à surveiller en IPS sur une nuée d'overtime, pas seulement en équilibrage.</para>
    /// </summary>
    public static float EliteChanceCap(int rank)
        => Clamp(rank) >= 5 ? 0.70f : EliteAffixTable.MaxChance;

    // ⚠ Il a existé ici, le 2026-08-01, un `ElitesKeepHealthDrops` qui retirait aux élites leur orbe de
    // PV privilégié au cran V. Motif : le cran 5 semblait rendre +41 % de soins qu'au cran 0 (4/4, net)
    // et « annuler Hémorragie ». RETIRÉ le jour même, le diagnostic étant faux — deux fois :
    //
    //   ① supprimer la source soupçonnée (hpDropChance 0,27 → 0,08 sur 55 % de la nuée) n'a pas bougé
    //     la mesure d'un point (85,3 → 85,0 PV/s) ;
    //   ② la colonne lue comptait le soin RETENU, borné par les PV manquants. Instrumenté à part
    //     (`soins_bruts_ps`), le soin OFFERT au cran 5 vaut 157,3 PV/s contre 293,6 au cran 0 :
    //     **−46,4 %, 0/4** — Hémorragie fait exactement son travail, et un peu plus que promis.
    //
    // Le « surplus de soin » n'était que la contrepartie de +55 % de dégâts subis : un joueur plus
    // souvent blessé convertit des soins qu'il gaspillait. Cf. PowerTelemetry.NotifyHealed.

    // ── Cran VI — « Purificateur » ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Part des PV max du joueur qu'un coup de <b>champion</b> inflige au minimum. Le premier cran du
    /// lot 2, et le premier qui vise la <b>capacité d'absorption</b> plutôt qu'un flux.
    ///
    /// <para><b>Pourquoi ce levier, et pas un de plus sur les soins.</b> Le canal des soins est saturé
    /// de gaspillage : au cran 0 le joueur reçoit <b>293,6 PV/s et n'en retient que 58,8</b> — il en jette
    /// 80 %. Un cran qui coupe l'offre tape donc dans le vide (Hémorragie la divise par deux et le joueur
    /// en retient <i>davantage</i>, cf. GDD §34.4 ter). Ce qui <b>crée</b> le surplus, en revanche, n'est
    /// borné par rien : le joueur gagne <b>277 PV max par minute</b> d'overtime via la carte Blindage
    /// (+45/prise, sans plafond), soit +2770 PV sur dix minutes — pendant que les dégâts ennemis restent
    /// des <b>valeurs absolues</b> issues d'une courbe fixe. Un dégât exprimé en fraction des PV max est
    /// la seule forme qui ne se laisse pas distancer par cette pente.</para>
    ///
    /// <para><b>Pourquoi les champions, et eux seuls.</b> Deux raisons qui se rejoignent. La lisibilité
    /// d'abord : un champion est gros, unique à l'écran et télégraphié, donc le joueur voit <i>ce qui</i>
    /// le frappe — le même effet porté par la faune de base produirait de la mort inexpliquée en nuée,
    /// ce que le plan interdit. Et le point aveugle ensuite : le boss de fin est tué <b>13 fois par run</b>
    /// et son TTK est <b>insensible aux crans</b> (7,9-35,8 s au cran 5 sur le biome le plus facile contre
    /// 9,8-37,4 s au cran 0 sur le plus dur) ; aucun cran ne le touchait. Un projectile de boss coûte
    /// aujourd'hui <b>~1,3 %</b> de la barre d'un joueur d'overtime : voilà pourquoi la condition de
    /// victoire du jeu est devenue un distributeur de récompenses.</para>
    ///
    /// <para><b>Il ne gagne aucun PV</b>, à dessein : le boss ne devient pas plus <i>long</i>, il devient
    /// plus <i>dangereux</i>. C'est le seul geste qui respecte la règle 3 du §34.4 — jamais un mur de
    /// patience sur le boss, il est calibré sur un TTK joué (§20.6).</para>
    /// </summary>
    public const float PurgeFraction = 0.12f;

    /// <summary>Fraction des PV max plancher pour un coup de champion (0 sous le cran VI).</summary>
    public static float ChampionMinDamageFraction(int rank)
        => Clamp(rank) >= 6 ? PurgeFraction : 0f;

    /// <summary>
    /// Dégât <b>brut</b> d'un coup de champion, avant réduction de dégâts : le nominal, ou le plancher en
    /// fraction des PV max si celui-ci est plus élevé.
    ///
    /// <para><b>Le plancher s'applique AVANT la réduction de dégâts</b>, volontairement : la DR, les
    /// i-frames et la réserve de régénération (§33.6) continuent de fonctionner normalement. Un cran
    /// retire <b>une</b> certitude — celle de pouvoir encaisser un champion en s'appuyant sur une barre
    /// de vie sans plafond — et pas trois. La réserve anti-pic est même la réponse que le joueur a le
    /// droit de construire contre ce cran (effet de système recherché, §34.5).</para>
    ///
    /// <para>⚠ <b>À n'appliquer qu'aux coups DISCRETS</b> — contact à intervalle, projectile, attaque
    /// télégraphiée. Jamais aux dégâts continus exprimés en PV/seconde (faisceau du boss, flaques de
    /// magma, geysers) : un plancher appliqué à chaque tick d'un DPS × delta tuerait le joueur en
    /// quelques frames, et transformerait une zone au sol en mort instantanée sans télégraphe utile.</para>
    /// </summary>
    public static float ChampionDamage(float nominalDamage, float playerMaxHp, int rank)
    {
        if (playerMaxHp <= 0f) return nominalDamage;
        return Math.Max(nominalDamage, ChampionMinDamageFraction(rank) * playerMaxHp);
    }

    // ── Économie ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gain d'Échos : <b>+20 % par cran, cumulatif</b> (rang 5 ≈ ×2,49). Pente plus forte que celle
    /// des paliers de niveau (<see cref="LevelThreat.EchoMult"/>, +10 %/palier) parce que le coût en
    /// compétence l'est aussi.
    ///
    /// <para><b>Garde-fou anti-farm</b> : le gain <i>horaire</i> d'une saturation haute doit rester
    /// supérieur à celui d'une saturation basse rejouée vite. Si une run de saturation 5 dure deux fois
    /// plus longtemps, ×2,49 la garde rentable ; à ×1,5 le joueur optimal redescendrait — exactement le
    /// travers que <c>LevelThreat.EchoMult</c> corrige déjà entre biomes.</para>
    /// </summary>
    public static double EchoMult(int rank) => Math.Pow(1.20, Clamp(rank));

    /// <summary>Les règles actives à ce rang (vide au rang 0), pour l'affichage avant la run.</summary>
    public static IReadOnlyList<Rank> ActiveRanks(int rank)
    {
        int r = Clamp(rank);
        var list = new List<Rank>(r);
        for (int i = 0; i < r; i++) list.Add(Ranks[i]);
        return list;
    }

    // ── Déblocage ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rang maximum <b>sélectionnable</b> quand le joueur a déjà validé <paramref name="highestBeaten"/>
    /// (rang le plus élevé où il a battu le boss de fin <b>de ce niveau</b> ; 0 = aucun).
    ///
    /// <para>Le déblocage est <b>par niveau</b> (décision de l'auteur, 2026-07-30, qui renverse le §7.3
    /// du plan). Conséquence assumée : l'échelle se regagne sur chacun des cinq biomes. En contrepartie,
    /// un biome tardif — déjà plus dur via <see cref="LevelThreat"/> — ne se retrouve pas ouvert au
    /// cran 5 parce que le joueur l'a gagné sur le Sanctuaire.</para>
    /// </summary>
    public static int MaxSelectable(int highestBeaten) => Math.Min(Clamp(highestBeaten) + 1, MaxRank);

    /// <summary>true si le joueur peut lancer une run à ce rang.</summary>
    public static bool CanSelect(int rank, int highestBeaten)
        => rank >= 0 && rank <= MaxSelectable(highestBeaten);

    // ── Migration des sauvegardes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit une ancienne valeur de <c>GameDifficulty</c> (0 = Facile, 1 = Normal, 2 = Difficile)
    /// vers le nouveau couple (difficulté d'assistance, saturation).
    ///
    /// <para>La saturation <b>absorbe</b> l'ancien axe de difficulté : sans cela, quatre axes
    /// multiplicatifs se cumuleraient en silence (réglage joueur × palier de niveau × overtime ×
    /// saturation) et aucun diagnostic ne serait plus possible — le §31 a mis trois sessions jouées à
    /// isoler une cause pour cette raison précise.</para>
    ///
    /// <para>« Difficile » devient donc <i>Normal + saturation 1</i>, aux valeurs identiques : les
    /// complétions et records déjà gagnés à cette difficulté restent <b>exacts</b>, ils ne sont pas
    /// réinterprétés à la hausse ni effacés.</para>
    /// </summary>
    /// <returns>
    /// <c>Difficulty</c> : valeur à conserver pour l'assistance (0 = Facile, 1 = Normal) ;
    /// <c>Saturation</c> : cran <b>sélectionné</b> après migration — toujours 0, voir ci-dessous ;
    /// <c>Beaten</c> : cran à <b>créditer</b> au déblocage si le joueur a déjà terminé un niveau.
    /// </returns>
    public static (int Difficulty, int Saturation, int Beaten) MigrateLegacyDifficulty(int legacy)
        => legacy switch
        {
            0 => (0, 0, 0),   // Facile : assistance, hors échelle de saturation
            // Difficile : le joueur a démontré qu'il jouait au-dessus de Normal → on lui OUVRE le
            // cran 1, mais on ne l'ACTIVE pas. Depuis l'échange du 2026-07-30, le cran 1 est
            // « Hémorragie » (soins −40 %) et non plus l'équivalent de « Difficile » : l'activer
            // d'office imposerait à un joueur existant une règle de gameplay qu'il n'a pas choisie,
            // au premier lancement après mise à jour. Le déblocage est une porte, pas un certificat.
            2 => (1, 0, 1),
            _ => (1, 0, 0),   // Normal
        };

    /// <summary>Version courante du schéma de saturation dans <c>settings.cfg</c>.</summary>
    /// <remarks>1 = un cran global · 2 = un cran par niveau.</remarks>
    public const int SchemaVersion = 2;

    /// <summary>
    /// Convertit un état de saturation <b>global</b> (schéma 1) en état <b>par niveau</b> (schéma 2) :
    /// le cran choisi et le cran débloqué sont <b>diffusés à tous les biomes</b>.
    ///
    /// <para>C'est la seule conversion fidèle possible. Sous le schéma 1 le déblocage était global :
    /// le joueur avait effectivement accès à ce cran sur <i>tous</i> les niveaux, et le lui retirer
    /// serait une régression. Quant à savoir <i>sur quel</i> biome il l'a gagné — l'information n'a
    /// jamais été écrite.</para>
    ///
    /// <para>Le choix est borné par ce qui est débloqué : un fichier incohérent (cran 4 choisi, rien
    /// de battu) ne doit pas ouvrir l'échelle par la porte de derrière.</para>
    /// </summary>
    public static (Dictionary<string, int> Choice, Dictionary<string, int> Beaten) DiffuseGlobalRanks(
        IReadOnlyList<string> biomes, int globalChoice, int globalBeaten)
    {
        int beaten = Clamp(globalBeaten);
        int choice = Math.Clamp(Clamp(globalChoice), 0, MaxSelectable(beaten));

        var choiceMap = new Dictionary<string, int>();
        var beatenMap = new Dictionary<string, int>();
        foreach (var biome in biomes)
        {
            choiceMap[biome] = choice;
            beatenMap[biome] = beaten;
        }
        return (choiceMap, beatenMap);
    }
}
