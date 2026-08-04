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
    private static int First(int _) => 0;

    private static List<LevelUpCard> Build(
        Dictionary<string, int>? weapons = null,
        Dictionary<string, int>? passives = null,
        IReadOnlyList<string>? fusions = null,
        int slots = Slots)
        => LevelUpPool.Build(
            weapons ?? new Dictionary<string, int>(), Weapons, WeaponMax,
            passives ?? new Dictionary<string, int>(), Passives, PassiveMax,
            slots, fusions ?? System.Array.Empty<string>(), First);

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
        Assert.DoesNotContain(Build(weapons, passives), c => c.Kind == LevelUpCardKind.Overload);
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

    [Fact]
    public void UnPoolPlusPetitQueTroisNeLevePasDErreur()
    {
        // Un seul candidat possible : deux armes au max, une passive au max, un slot occupé.
        var weapons = new Dictionary<string, int> { ["w1"] = WeaponMax, ["w2"] = WeaponMax, ["w3"] = WeaponMax };
        var passives = new Dictionary<string, int> { ["p1"] = PassiveMax, ["p2"] = PassiveMax - 1 };

        var cards = Build(weapons, passives, slots: 3);

        Assert.Single(cards);
        Assert.Equal("p2", cards[0].Id);
    }

    [Fact]
    public void LesTroisCartesDeSurchargeSontDistinctes()
    {
        var cards = LevelUpPool.BuildOverload();
        Assert.Equal(3, cards.Count);
        Assert.Equal(3, cards.Select(c => c.Id).Distinct().Count());
    }
}
