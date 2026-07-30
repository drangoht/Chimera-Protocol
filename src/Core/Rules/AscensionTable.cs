using System;
using System.Collections.Generic;

/// <summary>
/// <b>Ascension</b> — l'échelle de challenge de fin de partie (logique pure, testable).
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
/// ascension négative.</para>
///
/// <para>Plan : <c>docs/ENDGAME_PLAN.md</c>. Chaque cran doit faire baisser le <b>temps soutenable</b>
/// de plus de 6 % au banc apparié (<c>tools/power_curve_multi.py</c>) — sous ce seuil, un cran coûte un
/// palier au joueur sans rien changer, et ne mérite pas d'exister.</para>
/// </summary>
public static class AscensionTable
{
    /// <summary>Rang maximum livré. Les crans VI-X du plan viendront aux lots 2 et 4.</summary>
    public const int MaxRank = 5;

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
    /// Les crans, dans l'ordre. Cumulatifs : l'ascension N applique les règles 1…N — on ne panache
    /// pas (ce seraient des mutateurs, explicitement hors périmètre du plan).
    /// </summary>
    public static readonly IReadOnlyList<Rank> Ranks = new[]
    {
        new Rank(1, "ASC_1_NAME", "ASC_1_RULE"),   // Meute       — ennemis plus durs (= ex-Difficile)
        new Rank(2, "ASC_2_NAME", "ASC_2_RULE"),   // Hémorragie  — soins reçus −40 %
        new Rank(3, "ASC_3_NAME", "ASC_3_RULE"),   // Compte à rebours — overtime à la 10ᵉ minute
        new Rank(4, "ASC_4_NAME", "ASC_4_RULE"),   // Sans filet  — plus de Noyau de Secours ni de Plaque
        new Rank(5, "ASC_5_NAME", "ASC_5_RULE"),   // Élite ordinaire — élites ×3
    };

    private static int Clamp(int rank) => Math.Clamp(rank, 0, MaxRank);

    // ── Cran I — « Meute » : reprend exactement l'ancien « Difficile » ───────────────────────────
    // Ces trois multiplicateurs ne montent PAS avec les crans suivants, à dessein : le parti pris est
    // qu'un cran ajoute une RÈGLE, pas un facteur. Empiler des statistiques est précisément ce qui
    // avait échoué (le joueur gagne cet échange), et cela rendrait chaque cran illisible.

    /// <summary>Multiplicateur de PV des ennemis basiques.</summary>
    public static float EnemyHpMult(int rank) => Clamp(rank) >= 1 ? 1.30f : 1.00f;

    /// <summary>Multiplicateur de dégâts des ennemis (tous, champions inclus).</summary>
    public static float EnemyDamageMult(int rank) => Clamp(rank) >= 1 ? 1.35f : 1.00f;

    /// <summary>Multiplicateur du volume de spawn.</summary>
    public static float SpawnMult(int rank) => Clamp(rank) >= 1 ? 1.25f : 1.00f;

    /// <summary>
    /// PV des CHAMPIONS (mini-boss et boss de fin) : bonus amorti par
    /// <see cref="LevelThreat.ChampionHpSoftening"/>, pour la même raison qu'au §28 — battre le boss
    /// conditionne la progression, et il est calibré sur un <b>TTK joué</b> (GDD §20.6). Un boss qui
    /// gagne 30 % de PV à chaque cran deviendrait un mur de patience, pas de difficulté.
    /// </summary>
    public static float ChampionHpMult(int rank)
        => 1f + (EnemyHpMult(rank) - 1f) * LevelThreat.ChampionHpSoftening;

    // ── Cran II — « Hémorragie » ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur des soins <b>reçus</b> (orbes, lifesteal, carte Blindage qui soigne de son gain).
    /// Vise le canal de soin <b>dominant</b> : 86,4 PV/s mesurés en overtime, contre 8,2 pour la
    /// régénération. Un cran qui l'ignorerait n'agirait que sur 10 % de la défense réelle du joueur.
    /// </summary>
    public static float HealingMult(int rank) => Clamp(rank) >= 2 ? 0.60f : 1.00f;

    // ── Cran III — « Compte à rebours » ─────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur de la durée de run avant overtime. Exprimé en facteur et non en minutes fixes :
    /// la durée de référence vit dans <c>data/meta_upgrades.json</c> (780 s aujourd'hui) et l'upgrade
    /// méta <c>overtime_stabilizer</c> la module déjà — une valeur en dur ici les contredirait.
    /// 0,77 × 13 min ≈ <b>10 minutes</b>.
    ///
    /// <para>Ce cran attaque le <b>temps de construction du build</b>, pas la puissance : entrer en
    /// overtime trois minutes plus tôt, c'est y entrer avec un arsenal non saturé. Le relevé du
    /// 2026-07-29 a montré que l'état d'entrée en overtime explique un facteur <b>2,4</b> sur la survie
    /// — c'est donc l'un des leviers les plus puissants du lot, et le moins coûteux en code.</para>
    /// </summary>
    public static float RunDurationMult(int rank) => Clamp(rank) >= 3 ? 0.77f : 1.00f;

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
    /// valide le cadre d'ascension lui-même.</para>
    /// </summary>
    public static bool SafetyNetsEnabled(int rank) => Clamp(rank) < 4;

    // ── Cran V — « Élite ordinaire » ────────────────────────────────────────────────────────────

    /// <summary>
    /// Multiplicateur de la fréquence d'apparition des élites (affixes du §« Affixes d'élite »).
    /// L'élite cesse d'être un événement : les affixes (Blindé, Explosif, Vampirique…) deviennent la
    /// texture normale de la nuée, ce qui demande de lire la foule au lieu de la traverser.
    /// </summary>
    public static float EliteFrequencyMult(int rank) => Clamp(rank) >= 5 ? 3.00f : 1.00f;

    /// <summary>
    /// Plafond de la probabilité d'élite à ce rang. Relevé <b>explicitement</b> au cran V au lieu de
    /// laisser le facteur ×3 traverser le plafond d'origine : 3 × 0,28 vaudrait 84 % d'élites, soit la
    /// « horde » que <see cref="EliteAffixTable.MaxChance"/> interdit par commentaire, avec le coût des
    /// affixes sur 200-300 entités. À 0,55 la majorité de la nuée peut être élite, jamais la totalité —
    /// il reste toujours des ennemis ordinaires pour lire la foule.
    /// </summary>
    public static float EliteChanceCap(int rank)
        => Clamp(rank) >= 5 ? 0.55f : EliteAffixTable.MaxChance;

    // ── Économie ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gain d'Échos : <b>+20 % par cran, cumulatif</b> (rang 5 ≈ ×2,49). Pente plus forte que celle
    /// des paliers de niveau (<see cref="LevelThreat.EchoMult"/>, +10 %/palier) parce que le coût en
    /// compétence l'est aussi.
    ///
    /// <para><b>Garde-fou anti-farm</b> : le gain <i>horaire</i> d'une ascension haute doit rester
    /// supérieur à celui d'une ascension basse rejouée vite. Si une run d'ascension 5 dure deux fois
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
    /// (rang le plus élevé où il a battu un boss de fin, tous biomes confondus ; 0 = aucun).
    ///
    /// <para>Le déblocage est <b>global</b> et non par biome : cinq niveaux × dix crans se
    /// transformeraient en corvée. Les <i>records</i>, eux, restent indexés par biome et par ascension —
    /// la grille existe pour qui veut la remplir, sans être un péage.</para>
    /// </summary>
    public static int MaxSelectable(int highestBeaten) => Math.Min(Clamp(highestBeaten) + 1, MaxRank);

    /// <summary>true si le joueur peut lancer une run à ce rang.</summary>
    public static bool CanSelect(int rank, int highestBeaten)
        => rank >= 0 && rank <= MaxSelectable(highestBeaten);

    // ── Migration des sauvegardes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit une ancienne valeur de <c>GameDifficulty</c> (0 = Facile, 1 = Normal, 2 = Difficile)
    /// vers le nouveau couple (difficulté d'assistance, ascension).
    ///
    /// <para>L'ascension <b>absorbe</b> l'ancien axe de difficulté : sans cela, quatre axes
    /// multiplicatifs se cumuleraient en silence (réglage joueur × palier de niveau × overtime ×
    /// ascension) et aucun diagnostic ne serait plus possible — le §31 a mis trois sessions jouées à
    /// isoler une cause pour cette raison précise.</para>
    ///
    /// <para>« Difficile » devient donc <i>Normal + ascension 1</i>, aux valeurs identiques : les
    /// complétions et records déjà gagnés à cette difficulté restent <b>exacts</b>, ils ne sont pas
    /// réinterprétés à la hausse ni effacés.</para>
    /// </summary>
    /// <returns>
    /// <c>Difficulty</c> : valeur à conserver pour l'assistance (0 = Facile, 1 = Normal) ;
    /// <c>Ascension</c> : rang correspondant.
    /// </returns>
    public static (int Difficulty, int Ascension) MigrateLegacyDifficulty(int legacy) => legacy switch
    {
        0 => (0, 0),   // Facile : assistance, hors échelle d'ascension
        2 => (1, 1),   // Difficile : Normal + ascension 1 (mêmes multiplicateurs)
        _ => (1, 0),   // Normal
    };
}
