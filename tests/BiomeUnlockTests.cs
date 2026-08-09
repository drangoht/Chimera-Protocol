using System.Collections.Generic;
using Xunit;

/// <summary>
/// Vérifie le déblocage progressif des niveaux — la seule porte du jeu, et donc la seule chose qui
/// donne une raison de <b>gagner</b> plutôt que de survivre.
/// </summary>
public class BiomeUnlockTests
{
    private static Dictionary<string, int> None() => new();

    [Fact]
    public void LePremierNiveauEstToujoursOuvert()
    {
        Assert.True(BiomeUnlock.IsUnlocked(LevelThreat.Order[0], None()));
        Assert.Null(BiomeUnlock.BlockedBy(LevelThreat.Order[0], None()));
    }

    [Fact]
    public void UnNiveauResteFermeTantQueLePrecedentNEstPasTermine()
    {
        var completions = None();

        Assert.False(BiomeUnlock.IsUnlocked(LevelThreat.Order[1], completions));
        Assert.Equal(LevelThreat.Order[0], BiomeUnlock.BlockedBy(LevelThreat.Order[1], completions));

        completions[LevelThreat.Order[0]] = 1;
        Assert.True(BiomeUnlock.IsUnlocked(LevelThreat.Order[1], completions));

        // …mais pas celui d'après : on ouvre une porte à la fois.
        Assert.False(BiomeUnlock.IsUnlocked(LevelThreat.Order[2], completions));
    }

    /// <summary>
    /// La progression acquise ne se reprend jamais : une complétion vaut, quel que soit le cran
    /// auquel elle a été obtenue. Sans quoi monter en difficulté <b>refermerait</b> les niveaux
    /// suivants.
    /// </summary>
    [Fact]
    public void UneCompletionOuvreQuelQueSoitLeCran()
    {
        var completions = new Dictionary<string, int> { [LevelThreat.Order[0]] = 1 };
        Assert.True(BiomeUnlock.IsUnlocked(LevelThreat.Order[1], completions));
    }

    /// <summary>
    /// <b>Le cran I se mérite</b> (décision de l'auteur, 2026-08-09).
    ///
    /// <para>Tant que le Noyau n'est pas tombé une fois sur ce biome, seul le cran 0 s'ouvre.
    /// Auparavant « rien battu » et « cran 0 battu » valaient tous deux zéro, si bien qu'une remise à
    /// zéro complète laissait l'échelle avec un barreau d'avance — le premier cran était offert au
    /// lieu d'être gagné.</para>
    /// </summary>
    [Fact]
    public void LeCranUnNeSOuvreQuApresUneVictoire()
    {
        var beaten = new Dictionary<string, int>();

        Assert.Equal(0, BiomeUnlock.MaxSelectableRank("sanctuaire", beaten));
        Assert.True(SaturationTable.CanSelect(0, SaturationTable.NoneBeaten));
        Assert.False(SaturationTable.CanSelect(1, SaturationTable.NoneBeaten));
    }

    [Fact]
    public void OnNeMonteQueDUnCranALaFois()
    {
        // Une victoire au cran 0 ouvre le I, et rien de plus : c'est le +1 qui rend l'échelle
        // gravissable — sans un cran ouvert au-dessus de ce qui est prouvé, on n'en gagnerait jamais
        // un de plus.
        var beaten = new Dictionary<string, int> { ["sanctuaire"] = 0 };
        Assert.Equal(1, BiomeUnlock.MaxSelectableRank("sanctuaire", beaten));

        beaten["sanctuaire"] = 3;
        Assert.Equal(4, BiomeUnlock.MaxSelectableRank("sanctuaire", beaten));
    }

    [Fact]
    public void LEchelleNeDepasseJamaisSonDernierCran()
    {
        var beaten = new Dictionary<string, int> { ["sanctuaire"] = SaturationTable.MaxRank };
        Assert.Equal(SaturationTable.MaxRank, BiomeUnlock.MaxSelectableRank("sanctuaire", beaten));
    }

    /// <summary>
    /// Le cran se règle <b>par niveau</b> depuis la 1.25.0 : avoir gravi l'échelle sur un biome
    /// n'ouvre rien sur un autre.
    /// </summary>
    [Fact]
    public void LesCransNeSeTransferentPasDUnBiomeALAutre()
    {
        var beaten = new Dictionary<string, int> { ["sanctuaire"] = 5 };

        Assert.Equal(6, BiomeUnlock.MaxSelectableRank("sanctuaire", beaten));

        // Sur un biome jamais terminé, l'échelle repart de zéro — et se regagne à partir de sa
        // première victoire, exactement comme sur le premier.
        Assert.Equal(0, BiomeUnlock.MaxSelectableRank("neon", beaten));
    }
}
