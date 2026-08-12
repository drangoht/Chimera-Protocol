using System;
using System.Collections.Generic;

/// <summary>Nature d'une carte proposée à la montée de niveau.</summary>
public enum LevelUpCardKind
{
    /// <summary>Nouvelle arme.</summary>
    NewWeapon,
    /// <summary>Montée d'une arme déjà portée.</summary>
    WeaponUpgrade,
    /// <summary>Passif (nouveau ou monté).</summary>
    Passive,
    /// <summary>Fusion débloquée.</summary>
    Fusion,
    /// <summary>Carte de surcharge — fin de partie, sans plafond.</summary>
    Overload,
}

/// <summary>Une carte proposée.</summary>
public readonly struct LevelUpCard
{
    public readonly LevelUpCardKind Kind;
    public readonly string Id;
    public readonly int NextLevel;

    public LevelUpCard(LevelUpCardKind kind, string id, int nextLevel)
    {
        Kind = kind; Id = id; NextLevel = nextLevel;
    }
}

/// <summary>
/// Construction du choix de cartes à chaque montée de niveau — logique pure et testable (Lot 5).
///
/// <para><b>Pourquoi cette classe est isolée.</b> Elle porte une règle qui a fait l'objet d'un
/// chantier entier du projet : <b>quand le pool est vide, le jeu doit basculer sur les cartes de
/// surcharge</b>. Sans cela, le joueur enchaînait des « niveaux vides » — de l'XP pour gagner des
/// niveaux qui ne donnent rien — pendant que la menace, elle, continuait de croître. Un joueur
/// passait du niveau 124 à 140 en 74 secondes pour un gain <b>nul</b>.</para>
///
/// <para>C'est exactement le genre de règle qu'un portage perd sans bruit : tout continue de
/// fonctionner, le jeu devient simplement injouable en fin de partie.</para>
///
/// <para><b>La bascule était binaire, et c'était le trou.</b> Elle ne se déclenchait que sur un pool
/// <i>totalement</i> vide. Entre les deux — une seule passive non saturée, deux armes encore
/// montables — le joueur recevait une main de <b>une ou deux cartes</b> en fin de run : pas un choix,
/// un ultimatum, sur l'écran dont tout l'intérêt est d'en offrir un. Signalé en jouant le
/// 2026-08-12 (« vers la fin de run le menu affiche de temps en temps 1 ou 2 items »). Le pool ne
/// s'épuise pas d'un coup, il s'assèche ; la réponse doit donc s'appliquer par degrés, carte par
/// carte manquante.</para>
/// </summary>
public static class LevelUpPool
{
    /// <summary>Nombre de cartes proposées à chaque montée.</summary>
    public const int CardsPerLevel = 3;

    /// <summary>
    /// Construit le choix. Renvoie <b>toujours</b> <see cref="CardsPerLevel"/> cartes : ce que le pool
    /// ordinaire ne fournit plus est complété par les <b>cartes de surcharge</b>.
    /// </summary>
    /// <param name="ownedWeapons">Armes portées et leur niveau.</param>
    /// <param name="allWeaponIds">Toutes les armes existantes.</param>
    /// <param name="weaponMaxLevel">Niveau maximum d'une arme.</param>
    /// <param name="ownedPassives">Passifs portés et leur niveau.</param>
    /// <param name="allPassiveIds">Tous les passifs existants.</param>
    /// <param name="passiveMaxLevel">Niveau maximum d'un passif.</param>
    /// <param name="weaponSlots">Nombre maximal d'armes portées.</param>
    /// <param name="availableFusions">Fusions actuellement déblocables.</param>
    /// <param name="rarityOf">
    /// Rareté d'une carte (« common », « rare », « epic »), qui décide de son <b>poids de tirage</b>
    /// via <see cref="RarityWeights"/>.
    /// </param>
    /// <param name="pick">
    /// Tirage sans remise : reçoit les <b>poids</b> des candidats restants et rend l'index choisi.
    /// Injecté pour rester déterministe.
    /// </param>
    /// <remarks>
    /// ⚠ Le tirage était <b>uniforme</b> jusqu'au 2026-08-09 : la rareté s'affichait sur la carte —
    /// cadre coloré, étiquette « Épique » — et ne pesait sur rien. `RarityWeights` était portée,
    /// testée, et lue par personne. Un joueur voyait donc des cartes annoncées rares tomber aussi
    /// souvent que les autres, ce qui vide l'étiquette de son sens sans jamais ressembler à un bug.
    /// </remarks>
    public static List<LevelUpCard> Build(
        IReadOnlyDictionary<string, int> ownedWeapons,
        IReadOnlyList<string> allWeaponIds,
        int weaponMaxLevel,
        IReadOnlyDictionary<string, int> ownedPassives,
        IReadOnlyList<string> allPassiveIds,
        int passiveMaxLevel,
        int weaponSlots,
        IReadOnlyList<string> availableFusions,
        Func<LevelUpCard, string> rarityOf,
        Func<IReadOnlyList<float>, int> pick)
    {
        var candidates = new List<LevelUpCard>();

        // Une fusion débloquée passe devant : c'est un choix rare, et le manquer serait frustrant.
        foreach (string fusionId in availableFusions)
            candidates.Add(new LevelUpCard(LevelUpCardKind.Fusion, fusionId, 1));

        foreach (string id in allWeaponIds)
        {
            int level = ownedWeapons.TryGetValue(id, out int l) ? l : 0;

            if (level == 0)
            {
                // Pas de nouvelle arme si l'arsenal est plein : proposer une carte inapplicable
                // serait un choix mort, indiscernable d'un bug.
                if (ownedWeapons.Count < weaponSlots)
                    candidates.Add(new LevelUpCard(LevelUpCardKind.NewWeapon, id, 1));
            }
            else if (level < weaponMaxLevel)
            {
                candidates.Add(new LevelUpCard(LevelUpCardKind.WeaponUpgrade, id, level + 1));
            }
            // Une arme au maximum sort du pool — c'est ce qui finit par le vider.
        }

        foreach (string id in allPassiveIds)
        {
            int level = ownedPassives.TryGetValue(id, out int l) ? l : 0;
            if (level < passiveMaxLevel)
                candidates.Add(new LevelUpCard(LevelUpCardKind.Passive, id, level + 1));
        }

        // ⚠ LE point du chantier « cartes de surcharge » : pool vide → progression de fin de partie
        // sans plafond, au lieu de niveaux vides.
        if (candidates.Count == 0) return BuildOverload();

        var chosen = new List<LevelUpCard>();
        var remaining = new List<LevelUpCard>(candidates);

        int count = Math.Min(CardsPerLevel, remaining.Count);
        var weights = new List<float>(remaining.Count);

        for (int i = 0; i < count; i++)
        {
            // Recalculés à chaque tour : le tirage est SANS REMISE, donc les poids restants changent.
            weights.Clear();
            foreach (var card in remaining) weights.Add(RarityWeights.Weight(rarityOf(card)));

            int index = Math.Clamp(pick(weights), 0, remaining.Count - 1);
            chosen.Add(remaining[index]);
            remaining.RemoveAt(index);
        }

        TopUpWithOverload(chosen);
        return chosen;
    }

    /// <summary>
    /// Complète une main incomplète avec des cartes de surcharge, dans leur ordre d'affichage.
    /// </summary>
    /// <remarks>
    /// <para>C'est le <b>même</b> filet que la bascule du pool vide, appliqué par degrés : la
    /// surcharge est précisément la progression sans plafond, donc la seule chose qui reste à offrir
    /// quand le contenu fini se raréfie. Une main de deux cartes n'était pas « un pool plus petit »,
    /// c'était l'écran de choix qui cessait d'en être un.</para>
    ///
    /// <para>⚠ <b>Ajoutées à la fin, jamais tirées au sort parmi les autres.</b> Les faire concourir
    /// au tirage pondéré les ferait apparaître à la place d'une arme encore montable — la surcharge
    /// mangerait le contenu au lieu de boucher le trou qu'il laisse.</para>
    /// </remarks>
    private static void TopUpWithOverload(List<LevelUpCard> chosen)
    {
        if (chosen.Count >= CardsPerLevel) return;

        foreach (var card in OverloadCards.All)
        {
            if (chosen.Count >= CardsPerLevel) return;
            chosen.Add(new LevelUpCard(LevelUpCardKind.Overload, card.Id, 1));
        }
    }

    /// <summary>Les trois cartes de surcharge, proposées quand plus rien d'autre n'est disponible.</summary>
    public static List<LevelUpCard> BuildOverload()
    {
        var cards = new List<LevelUpCard>();
        foreach (var card in OverloadCards.All)
            cards.Add(new LevelUpCard(LevelUpCardKind.Overload, card.Id, 1));
        return cards;
    }

    /// <summary>
    /// Le pool ordinaire est-il épuisé ? Utile au HUD et aux bancs pour repérer l'entrée en
    /// progression de fin de partie.
    /// </summary>
    public static bool IsExhausted(
        IReadOnlyDictionary<string, int> ownedWeapons, IReadOnlyList<string> allWeaponIds,
        int weaponMaxLevel,
        IReadOnlyDictionary<string, int> ownedPassives, IReadOnlyList<string> allPassiveIds,
        int passiveMaxLevel, int weaponSlots)
    {
        foreach (string id in allWeaponIds)
        {
            int level = ownedWeapons.TryGetValue(id, out int l) ? l : 0;
            if (level == 0 && ownedWeapons.Count < weaponSlots) return false;
            if (level > 0 && level < weaponMaxLevel) return false;
        }

        foreach (string id in allPassiveIds)
        {
            int level = ownedPassives.TryGetValue(id, out int l) ? l : 0;
            if (level < passiveMaxLevel) return false;
        }

        return true;
    }
}
