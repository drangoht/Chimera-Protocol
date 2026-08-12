using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Verrouille la règle du <b>pool épuisé → cartes de surcharge</b>, qui a fait l'objet d'un
/// chantier entier du projet.
///
/// <para>Sans elle, le joueur enchaîne des « niveaux vides » : de l'XP pour gagner des niveaux qui
/// ne donnent rien, pendant que la menace continue de croître. Le relevé d'origine est éloquent —
/// niveau 124 à 140 en <b>74 secondes</b> pour un gain <b>nul</b>.</para>
///
/// <para>C'est exactement le type de règle qu'un portage perd sans bruit : tout continue de
/// fonctionner, le jeu devient simplement injouable en fin de partie.</para>
/// </summary>
public class LevelUpPoolTests
{
    private static readonly string[] Weapons  = { "w1", "w2", "w3" };
    private static readonly string[] Passives = { "p1", "p2" };

    private const int WeaponMax = 5;
    private const int PassiveMax = 3;
    private const int Slots = 6;

    /// <summary>Tirage déterministe : toujours le premier candidat, pour des tests reproductibles.</summary>
    private static int First(IReadOnlyList<float> _) => 0;

    /// <summary>Rareté par défaut : ces tests portent sur la COMPOSITION du pool, pas sur les poids.</summary>
    private static string Common(LevelUpCard _) => "common";

    private static List<LevelUpCard> Build(
        Dictionary<string, int>? weapons = null,
        Dictionary<string, int>? passives = null,
        IReadOnlyList<string>? fusions = null,
        int slots = Slots)
        => LevelUpPool.Build(
            weapons ?? new Dictionary<string, int>(), Weapons, WeaponMax,
            passives ?? new Dictionary<string, int>(), Passives, PassiveMax,
            slots, fusions ?? System.Array.Empty<string>(), Common, First);

    // ─── Le verrou ────────────────────────────────────────────────────────────

    [Fact]
    public void PoolEpuise_BasculeSurLesCartesDeSurcharge()
    {
        // Tout au maximum : plus rien d'ordinaire à proposer.
        var weapons  = Weapons.ToDictionary(w => w, _ => WeaponMax);
        var passives = Passives.ToDictionary(p => p, _ => PassiveMax);

        var cards = Build(weapons, passives);

        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.Equal(LevelUpCardKind.Overload, c.Kind));
    }

    [Fact]
    public void PoolEpuise_NeRenvoieJamaisUnChoixVide()
    {
        var weapons  = Weapons.ToDictionary(w => w, _ => WeaponMax);
        var passives = Passives.ToDictionary(p => p, _ => PassiveMax);

        // Un choix vide, c'est un niveau vide : le bug que ce chantier a corrigé.
        Assert.NotEmpty(Build(weapons, passives));
    }

    [Fact]
    public void IsExhausted_DetecteLEpuisement()
    {
        var full = Weapons.ToDictionary(w => w, _ => WeaponMax);
        var fullP = Passives.ToDictionary(p => p, _ => PassiveMax);

        Assert.True(LevelUpPool.IsExhausted(full, Weapons, WeaponMax, fullP, Passives, PassiveMax, Slots));
        Assert.False(LevelUpPool.IsExhausted(
            new Dictionary<string, int>(), Weapons, WeaponMax,
            new Dictionary<string, int>(), Passives, PassiveMax, Slots));
    }

    [Fact]
    public void UnePassiveNonMaxeeSuffitAGarderLePoolOuvert()
    {
        var weapons = Weapons.ToDictionary(w => w, _ => WeaponMax);
        var passives = new Dictionary<string, int> { ["p1"] = PassiveMax, ["p2"] = PassiveMax - 1 };

        Assert.False(LevelUpPool.IsExhausted(weapons, Weapons, WeaponMax, passives, Passives, PassiveMax, Slots));

        // Le pool n'est pas épuisé : la carte ordinaire qui reste est bel et bien proposée. Les deux
        // places vides à côté d'elle sont comblées par la surcharge — c'est le complément, pas la
        // bascule (qui, elle, remplacerait TOUT le choix).
        var cards = Build(weapons, passives);
        Assert.Contains(cards, c => c.Kind == LevelUpCardKind.Passive && c.Id == "p2");
    }

    // ─── Composition du choix ─────────────────────────────────────────────────

    [Fact]
    public void ProposeTroisCartes()
    {
        Assert.Equal(LevelUpPool.CardsPerLevel, Build().Count);
    }

    [Fact]
    public void NeProposeJamaisDeuxFoisLaMemeCarte()
    {
        var cards = Build();
        Assert.Equal(cards.Count, cards.Select(c => c.Kind + ":" + c.Id).Distinct().Count());
    }

    [Fact]
    public void UneArmeAuMaximumSortDuPool()
    {
        var weapons = new Dictionary<string, int> { ["w1"] = WeaponMax };
        Assert.DoesNotContain(Build(weapons), c => c.Id == "w1");
    }

    /// <summary>
    /// Une carte inapplicable est un choix mort, indiscernable d'un bug pour le joueur.
    /// </summary>
    [Fact]
    public void ArsenalPlein_NeProposePlusDeNouvelleArme()
    {
        var weapons = new Dictionary<string, int> { ["w1"] = 1, ["w2"] = 1 };
        var cards = Build(weapons, slots: 2);

        Assert.DoesNotContain(cards, c => c.Kind == LevelUpCardKind.NewWeapon);
    }

    [Fact]
    public void UneMonteeDArmePorteLeNiveauSuivant()
    {
        var weapons = new Dictionary<string, int> { ["w1"] = 2 };
        var card = Build(weapons).First(c => c.Id == "w1");

        Assert.Equal(LevelUpCardKind.WeaponUpgrade, card.Kind);
        Assert.Equal(3, card.NextLevel);
    }

    /// <summary>
    /// Une fusion est un choix rare : la manquer serait frustrant, elle passe donc devant.
    /// </summary>
    [Fact]
    public void UneFusionDebloqueeEstProposeeEnPriorite()
    {
        var cards = Build(fusions: new[] { "fusion_x" });
        Assert.Contains(cards, c => c.Kind == LevelUpCardKind.Fusion && c.Id == "fusion_x");
    }

    /// <summary>
    /// Le défaut signalé en jouant le 2026-08-12 : « vers la fin de run le menu de level up affiche
    /// de temps en temps 1 ou 2 items seulement ».
    ///
    /// <para>Le pool ne s'épuise pas d'un coup, il s'assèche — et la bascule sur la surcharge ne
    /// répondait qu'au cas <b>totalement</b> vide. Entre les deux, l'écran dont tout l'intérêt est
    /// d'offrir un choix en proposait un seul.</para>
    /// </summary>
    [Fact]
    public void UnPoolPlusPetitQueTroisEstCompleteParLaSurcharge()
    {
        // Un seul candidat ordinaire : trois armes au max, une passive au max, l'arsenal plein.
        var weapons = new Dictionary<string, int> { ["w1"] = WeaponMax, ["w2"] = WeaponMax, ["w3"] = WeaponMax };
        var passives = new Dictionary<string, int> { ["p1"] = PassiveMax, ["p2"] = PassiveMax - 1 };

        var cards = Build(weapons, passives, slots: 3);

        Assert.Equal(LevelUpPool.CardsPerLevel, cards.Count);
        Assert.Contains(cards, c => c.Id == "p2");
        Assert.Equal(2, cards.Count(c => c.Kind == LevelUpCardKind.Overload));
    }

    /// <summary>
    /// Le complément ne remplace jamais le contenu : la carte ordinaire reste en tête, la surcharge
    /// ne fait que boucher les trous. Sans cela, la surcharge mangerait une arme encore montable.
    /// </summary>
    [Fact]
    public void LeComplementNeMangeJamaisUneCarteOrdinaire()
    {
        var weapons = new Dictionary<string, int> { ["w1"] = WeaponMax, ["w2"] = WeaponMax, ["w3"] = 1 };
        var passives = Passives.ToDictionary(p => p, _ => PassiveMax);

        var cards = Build(weapons, passives, slots: 3);

        Assert.Equal(LevelUpPool.CardsPerLevel, cards.Count);
        Assert.Equal(LevelUpCardKind.WeaponUpgrade, cards[0].Kind);
        Assert.Equal("w3", cards[0].Id);
    }

    /// <summary>Une main est toujours pleine — c'est l'invariant que le défaut du 2026-08-12 violait.</summary>
    [Fact]
    public void UneMainEstToujoursPleine()
    {
        foreach (int weaponLevel in new[] { 0, 1, WeaponMax })
        foreach (int passiveLevel in new[] { 0, 1, PassiveMax })
        foreach (int slots in new[] { 1, 3, Slots })
        {
            var weapons = Weapons.ToDictionary(w => w, _ => weaponLevel);
            var passives = Passives.ToDictionary(p => p, _ => passiveLevel);

            if (weaponLevel == 0) weapons.Clear();   // aucune arme portée

            Assert.Equal(LevelUpPool.CardsPerLevel, Build(weapons, passives, slots: slots).Count);
        }
    }

    [Fact]
    public void LesTroisCartesDeSurchargeSontDistinctes()
    {
        var cards = LevelUpPool.BuildOverload();
        Assert.Equal(3, cards.Count);
        Assert.Equal(3, cards.Select(c => c.Id).Distinct().Count());
    }
}
