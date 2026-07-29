using System.Collections.Generic;

/// <summary>
/// Cartes de <b>surcharge</b> — la progression de fin de partie, quand l'arsenal est saturé
/// (logique pure, testable).
///
/// <b>Le trou qu'elles comblent.</b> Mesuré sur la session jouée du 2026-07-29 (Fournaise) : le
/// joueur passe du <b>niveau 124 au niveau 140 en 74 secondes d'overtime</b> pour un gain
/// <b>nul</b> — ses 5 armes sont L20, ses 4 passifs sont saturés, et le §30 a précisément retiré les
/// passifs saturés du pool. <see cref="LevelUpSystem"/> complétait alors les 3 cartes avec
/// <c>XP_BONUS</c> : une carte qui donne de l'XP, laquelle ne sert qu'à gagner des niveaux… qui ne
/// donnent rien. Une boucle fermée sur elle-même.
///
/// Pendant ce temps la menace, elle, ne sature jamais. C'est l'asymétrie qui rendait la fenêtre de
/// 5-10 minutes d'overtime (GDD §9.2) inatteignable quel que soit le réglage des pentes : <b>une
/// menace non bornée exige une défense non bornée</b> (GDD §31.4.3, lu dans les deux sens).
///
/// <b>Le parti pris.</b> Trois cartes seulement, <b>sans plafond de niveau</b>, à effet faible et
/// cumulatif — la réponse classique du genre au même problème. Elles ne sont proposées que lorsque le
/// pool normal ne suffit plus : en run standard, elles n'existent pas. Étant toujours les trois mêmes,
/// le choix ne porte plus sur la découverte mais sur l'arbitrage : encaisser plus fort (PV),
/// tenir dans la durée (régénération), ou raccourcir la menace à la source (dégâts).
///
/// <b>Calibrage.</b> Valeurs de départ, à affiner sur session jouée — la survie n'est pas mesurable
/// en banc (le bot <c>--auto-play</c> ne se déplace pas). Repères de la même mesure : ~13 niveaux
/// gagnés par minute d'overtime, dégâts entrants +0,56/s par seconde. Sur 10 minutes d'overtime le
/// joueur voit donc ~130 cartes, soit ~43 de chaque s'il répartit.
///
/// Si le power-creep du §30 devait réapparaître, <see cref="Damage"/> est le levier à réduire en
/// premier : c'est la seule des trois qui alimente l'offensive.
///
/// Design : <c>docs/GDD.md</c> §33.
/// </summary>
public static class OverloadCards
{
    /// <summary>Type de carte porté par <c>LevelUpCardData.CardType</c> pour toute la famille.</summary>
    public const string CardType = "overload";

    /// <summary>Une carte de surcharge : identité + delta appliqué à chaque prise.</summary>
    public sealed class Card
    {
        /// <summary>Id stable (clés de loc, icône, suivi de niveau).</summary>
        public string Id { get; }
        /// <summary>Clé de traduction du nom.</summary>
        public string NameKey { get; }
        /// <summary>Clé de traduction de la description.</summary>
        public string DescKey { get; }
        /// <summary>Gain apporté par une prise. Toujours le même : la progression est linéaire.</summary>
        public float Delta { get; }

        public Card(string id, string nameKey, string descKey, float delta)
        {
            Id = id; NameKey = nameKey; DescKey = descKey; Delta = delta;
        }
    }

    /// <summary>
    /// +45 PV max par prise, et soigne d'autant (sans quoi la carte ne sauve pas le joueur qui la
    /// prend à 20 % de vie — elle lui donnerait une plus grande barre tout aussi vide).
    /// </summary>
    public static readonly Card Plating =
        new("overload_plating", "CARD_OVERLOAD_PLATING", "CARD_OVERLOAD_PLATING_DESC", 45f);

    /// <summary>
    /// +0,6 PV/s de régénération par prise. C'est le levier qui transforme l'attrition en quelque
    /// chose de soutenable : en overtime le joueur ne meurt pas d'un coup encaissé, il meurt de ne
    /// jamais reprendre ce qu'il a perdu.
    /// </summary>
    public static readonly Card Regen =
        new("overload_regen", "CARD_OVERLOAD_REGEN", "CARD_OVERLOAD_REGEN_DESC", 0.6f);

    /// <summary>
    /// +5 % de dégâts par prise (additif sur le multiplicateur global, comme <c>thermal_core</c>).
    /// Levier de survie <i>indirect</i> : ce qui meurt vite ne frappe pas. Volontairement le plus
    /// faible des trois en effet ressenti — cf. la note sur le power-creep en tête de classe.
    /// </summary>
    public static readonly Card Damage =
        new("overload_damage", "CARD_OVERLOAD_DAMAGE", "CARD_OVERLOAD_DAMAGE_DESC", 0.05f);

    /// <summary>Les trois cartes, dans l'ordre d'affichage.</summary>
    public static readonly IReadOnlyList<Card> All = new[] { Plating, Regen, Damage };

    /// <summary>
    /// Repère de calibrage, <b>mesuré</b> sur la session jouée du 2026-07-29 (Fournaise, palier 3) :
    /// PV max du joueur de 1060 à l'entrée en overtime à 2680 après 5:18, soit ~306 PV gagnés par
    /// minute d'overtime. C'est la vitesse à laquelle la défense du joueur croît une fois l'arsenal
    /// saturé — la grandeur que l'escalade de la menace doit dépasser pour que l'overtime tue
    /// (cf. <see cref="OvertimeEscalation.StatAcceleration"/> et GDD §31.4.3).
    ///
    /// Vaut pour un joueur qui privilégie <see cref="Plating"/> : sur cette session, 44 prises de
    /// Blindage et ~35 de Surtension pour 80 niveaux — <see cref="Regen"/> n'a été choisie qu'une
    /// fois, cf. GDD §33.5.
    ///
    /// <b>⚠ Ce repère décrit un profil de jeu, pas une constante du système.</b> La session du même
    /// jour à <c>StatAcceleration = 2,25</c> (8:36 d'overtime, 81 niveaux) a produit l'arbitrage
    /// <b>inverse</b> — ~46 prises de Surtension pour 25 de Blindage — et la défense n'y a crû que de
    /// <b>131 PV/min</b> en moyenne : 270 PV/min sur les trois premières minutes, puis 56, avec un
    /// plateau strictement plat de près de deux minutes. Même joueur, deux runs, un facteur 2,3 sur
    /// la pente de la défense. Tout réglage qui suppose 306 PV/min soutenus se trompe : c'est un
    /// <i>maximum observé sur un profil défensif</i>, pas une valeur à laquelle opposer la menace.
    /// </summary>
    public const float MeasuredHpGainPerOvertimeMinute = 306f;

    /// <summary>Carte d'id <paramref name="id"/>, ou <c>null</c> si l'id n'est pas une surcharge.</summary>
    public static Card? ById(string id)
    {
        foreach (var c in All)
            if (c.Id == id) return c;
        return null;
    }

    /// <summary>true si <paramref name="id"/> désigne une carte de surcharge.</summary>
    public static bool IsOverload(string id) => ById(id) != null;

    /// <summary>
    /// Bonus cumulé après <paramref name="takes"/> prises de la même carte. Strictement linéaire et
    /// <b>sans plafond</b> : c'est tout l'objet de ces cartes. Contrairement aux passifs, aucun
    /// amortissement (<see cref="PassiveScaling"/>) ne s'applique — amortir une progression censée
    /// répondre à une menace non bornée la ramènerait à un plafond, soit exactement le défaut
    /// qu'elles corrigent.
    /// </summary>
    public static float CumulativeBonus(Card card, int takes)
        => takes <= 0 ? 0f : card.Delta * takes;
}
