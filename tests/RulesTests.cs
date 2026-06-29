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

public class SpawnCurveTests
{
    [Fact]
    public void SpawnInterval_DecroitPuisPlafonneA03()
    {
        Assert.Equal(1.0f, SpawnCurve.SpawnInterval(0f), 3);       // début
        Assert.True(SpawnCurve.SpawnInterval(5f) < 1.0f);          // décroît
        Assert.Equal(0.3f, SpawnCurve.SpawnInterval(20f), 3);      // plancher
    }

    [Fact]
    public void BatchCount_EstBorneEntre1Et10()
    {
        Assert.Equal(2, SpawnCurve.BatchCount(0f));
        Assert.Equal(10, SpawnCurve.BatchCount(60f));   // clamp haut
        Assert.True(SpawnCurve.BatchCount(3f) is >= 1 and <= 10);
    }

    [Fact]
    public void MaxEnemies_CroitPuisPlafonneAuCap()
    {
        Assert.Equal(12, SpawnCurve.MaxEnemies(0f, 1f));
        Assert.Equal(SpawnCurve.MaxAlive, SpawnCurve.MaxEnemies(60f, 1f)); // plafonné à 300
    }

    [Fact]
    public void MaxEnemies_AppliqueLeMultiplicateurDeSpawn()
        => Assert.Equal(6, SpawnCurve.MaxEnemies(0f, 0.5f)); // (12)*0.5 = 6

    [Fact]
    public void WaveSize_CroitAvecLeTempsEtLeMult()
    {
        Assert.Equal(12, SpawnCurve.WaveSize(0f, 1f));
        Assert.Equal(8,  SpawnCurve.WaveSize(0f, 0.7f)); // (12)*0.7 = 8.4 -> 8
    }
}

public class WeaponLevelingTests
{
    [Fact]
    public void ExtrapolatedDamage_SousLeNiveauDefini_RendLaBase()
        => Assert.Equal(50f, WeaponLeveling.ExtrapolatedDamage(50f, 4, 5), 3);

    [Fact]
    public void ExtrapolatedDamage_AuNiveauDefini_RendLaBase()
        => Assert.Equal(50f, WeaponLeveling.ExtrapolatedDamage(50f, 5, 5), 3);

    [Theory]
    [InlineData(6, 55f)]   // +10% au-delà du niveau 5
    [InlineData(10, 75f)]  // +50%
    [InlineData(20, 125f)] // +150%
    public void ExtrapolatedDamage_AuDela_Applique10PctParNiveau(int level, float expected)
        => Assert.Equal(expected, WeaponLeveling.ExtrapolatedDamage(50f, level, 5), 3);
}

public class StatCapsTests
{
    [Theory]
    [InlineData(1.0f, 0.0f, 1.0f)]   // sans réduction
    [InlineData(1.0f, 0.5f, 0.5f)]   // -50%
    [InlineData(1.0f, 0.9f, 0.15f)]  // plancher MinCooldown
    [InlineData(0.4f, 0.6f, 0.16f)]
    public void EffectiveCooldown_PlafonneAuPlancher(float baseCd, float cr, float expected)
        => Assert.Equal(expected, StatCaps.EffectiveCooldown(baseCd, cr), 3);

    [Theory]
    [InlineData(0.30f, 0.30f)]
    [InlineData(0.50f, 0.40f)] // plafonné
    public void CapDamageReduction(float input, float expected)
        => Assert.Equal(expected, StatCaps.CapDamageReduction(input), 3);

    [Theory]
    [InlineData(300f, 300f)]
    [InlineData(400f, 380f)] // plafonné
    public void CapSpeed(float input, float expected)
        => Assert.Equal(expected, StatCaps.CapSpeed(input), 3);

    [Theory]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1.5f, 1.0f)] // plafonné à 100%
    public void CapCooldownReduction(float input, float expected)
        => Assert.Equal(expected, StatCaps.CapCooldownReduction(input), 3);
}

public class CrowdControlCapsTests
{
    [Theory]
    [InlineData(0.80f, 0.80f)]   // slow modéré inchangé
    [InlineData(0.60f, 0.60f)]   // pile au plancher
    [InlineData(0.30f, 0.60f)]   // plafonné à -40 %
    [InlineData(1.50f, 1.00f)]   // borné à 1 (pas d'accélération)
    public void CapSlowMult_BorneDansLaPlage(float input, float expected)
        => Assert.Equal(expected, CrowdControlCaps.CapSlowMult(input), 3);

    [Theory]
    [InlineData(20f, 20f)]
    [InlineData(60f, 60f)]
    [InlineData(90f, 60f)]   // plafonné
    [InlineData(-5f, 0f)]    // jamais négatif
    public void CapBurnDps_BorneDansLaPlage(float input, float expected)
        => Assert.Equal(expected, CrowdControlCaps.CapBurnDps(input), 3);
}

public class WeightedPickerTests
{
    private static readonly float[] Weights = { 60f, 30f, 10f }; // commun / rare / épique

    [Theory]
    [InlineData(0f, 0)]      // début du segment commun
    [InlineData(60f, 0)]     // borne haute commun (inclusive)
    [InlineData(60.5f, 1)]   // segment rare
    [InlineData(90f, 1)]     // borne haute rare
    [InlineData(90.5f, 2)]   // segment épique
    [InlineData(100f, 2)]    // borne haute totale
    public void PickIndex_TombeDansLeBonSegment(float roll, int expected)
        => Assert.Equal(expected, WeightedPicker.PickIndex(Weights, roll));

    [Fact]
    public void PickIndex_AuDelaDuTotal_ReplieSurLeDernier()
        => Assert.Equal(2, WeightedPicker.PickIndex(Weights, 999f));

    [Fact]
    public void PickIndex_PoidsUnique_RendZero()
        => Assert.Equal(0, WeightedPicker.PickIndex(new[] { 42f }, 10f));

    [Fact]
    public void PickIndex_RepartitionConformeAuxPoids()
    {
        // Balayage déterministe de [0,100) : ~60% commun, ~30% rare, ~10% épique.
        int[] counts = new int[3];
        for (int r = 0; r < 100; r++) counts[WeightedPicker.PickIndex(Weights, r + 0.5f)]++;
        Assert.Equal(60, counts[0]);
        Assert.Equal(30, counts[1]);
        Assert.Equal(10, counts[2]);
    }
}
