using Xunit;

/// <summary>
/// Tests unitaires de la couche de règles pure (src/Core/Rules) — sans dépendance Godot.
/// </summary>
public class XpCurveTests
{
    [Theory]
    [InlineData(1, 5)]     // L1 = 5 XP
    [InlineData(10, 95)]   // linéaire +10
    [InlineData(19, 185)]
    [InlineData(20, 390)]  // mur de mi-run
    [InlineData(21, 208)]  // phase 2
    [InlineData(30, 325)]
    public void Threshold_SuitLaCourbeAttendue(int level, int expected)
        => Assert.Equal(expected, XpCurve.Threshold(level));

    [Fact]
    public void Threshold_EstStrictementCroissanteSaufLeMur()
    {
        // Croissance générale (le mur L20 fait un saut, puis ça repart plus bas mais croît).
        for (int l = 1; l < 19; l++)
            Assert.True(XpCurve.Threshold(l + 1) > XpCurve.Threshold(l));
        for (int l = 21; l < 40; l++)
            Assert.True(XpCurve.Threshold(l + 1) > XpCurve.Threshold(l));
    }
}

public class EchoFormulaTests
{
    // floor(t/20) + floor(k/10) + cores*5 + base
    [Theory]
    [InlineData(30, 0, 0, 11)]     // run_30s_0kills_0cores : 1 + 0 + 0 + 10
    [InlineData(180, 120, 4, 51)]  // 9 + 12 + 20 + 10
    [InlineData(300, 250, 8, 90)]  // 15 + 25 + 40 + 10
    [InlineData(900, 600, 25, 240)]// 45 + 60 + 125 + 10
    public void Calculate_RespecteLaCalibration(int t, int k, int c, int expected)
        => Assert.Equal(expected, EchoFormula.Calculate(t, k, c, 20, 10, 5, 10));

    [Fact]
    public void Calculate_DiviseurNulNeFaitPasCrasher()
        => Assert.Equal(10, EchoFormula.Calculate(0, 0, 0, 0, 0, 5, 10));
}

public class EnemyScalingTests
{
    [Fact]
    public void Scaled_BossNormalA13Min()
    {
        // 12000 * (1 + 13*0.05) * 1.0 = 19800
        Assert.Equal(19800f, EnemyScaling.Scaled(12000f, 13f, 0.05f, 1.0f), 1);
    }

    [Fact]
    public void Scaled_AppliqueLaDifficulte()
    {
        // Difficile (×1.3) sur le boss : 19800 * 1.3 = 25740
        Assert.Equal(25740f, EnemyScaling.Scaled(12000f, 13f, 0.05f, 1.3f), 1);
    }

    [Fact]
    public void Scaled_T0EgaleLaBaseFoisDifficulte()
        => Assert.Equal(80f, EnemyScaling.Scaled(100f, 0f, 0.12f, 0.8f), 3);
}

public class DifficultyTuningTests
{
    [Theory]
    [InlineData(0, 0.60f, 0.80f, 0.70f)] // Facile
    [InlineData(1, 1.00f, 1.00f, 1.00f)] // Normal
    [InlineData(2, 1.35f, 1.30f, 1.25f)] // Difficile
    public void Multiplicateurs_ParDifficulte(int diff, float dmg, float hp, float spawn)
    {
        Assert.Equal(dmg,   DifficultyTuning.EnemyDamage(diff), 3);
        Assert.Equal(hp,    DifficultyTuning.EnemyHp(diff), 3);
        Assert.Equal(spawn, DifficultyTuning.Spawn(diff), 3);
    }
}

public class RarityWeightsTests
{
    [Theory]
    [InlineData("common", 60f)]
    [InlineData("rare", 30f)]
    [InlineData("epic", 10f)]
    [InlineData("inconnu", 60f)] // fallback commun
    public void Weight_ParRarete(string rarity, float expected)
        => Assert.Equal(expected, RarityWeights.Weight(rarity));
}
