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

    [Fact]
    public void OnNeMonteQueDUnCranALaFois()
    {
        var beaten = new Dictionary<string, int>();

        // Convention de SaturationTable : 0 = « aucun cran battu ». Un joueur neuf peut donc déjà
        // choisir le cran I — c'est la porte d'entrée de l'échelle, et elle doit se sentir.
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
        Assert.Equal(1, BiomeUnlock.MaxSelectableRank("neon", beaten));   // remis à la porte d'entrée
    }
}
