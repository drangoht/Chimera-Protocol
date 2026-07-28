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
    // standard = floor(min(t,capTimeSecs)/20) + floor(min(k,capKills)/10) + min(c,capCores)*5 + base
    // overtime = min( floor(max(0,t-capTimeSecs)/20*0.15) + floor(max(0,k-capKills)/10*0.15)
    //               + floor(max(0,c-capCores)*5*0.15), overtimeBonusCap )
    [Theory]
    [InlineData(30, 0, 0, 11)]        // run_30s_0kills_0cores : 1 + 0 + 0 + 10 (sous les caps)
    [InlineData(180, 120, 4, 51)]     // run_3min_120kills_4cores : 9 + 12 + 20 + 10 (sous les caps)
    [InlineData(300, 250, 8, 90)]     // run_5min_250kills_8cores : 15 + 25 + 40 + 10 (sous les caps)
    [InlineData(780, 520, 22, 211)]   // run_complete_780s_520kills_22cores : pile aux caps, overtime=0
    [InlineData(1080, 920, 29, 224)]  // run_overtime_modeste_18min : standard 211 + overtime 13
    [InlineData(2400, 3000, 60, 288)] // run_overtime_excellente_40min : standard 211 + overtime 77
    [InlineData(3600, 8000, 100, 311)]// run_overtime_extreme_60min : standard 211 + overtime plafonné à 100
    public void Calculate_RespecteLaCalibration(int t, int k, int c, int expected)
        => Assert.Equal(expected, EchoFormula.Calculate(t, k, c, 20, 10, 5, 10, 780, 520, 22, 0.15, 100));

    [Fact]
    public void Calculate_DiviseurNulNeFaitPasCrasher()
        => Assert.Equal(10, EchoFormula.Calculate(0, 0, 0, 0, 0, 5, 10, 780, 520, 22, 0.15, 100));

    // ── Palier de menace (multiplicateur de récompense du niveau) ─────────────

    [Fact]
    public void TierMult_ParDefautNeChangeRien()
        => Assert.Equal(211, EchoFormula.Calculate(780, 520, 22, 20, 10, 5, 10, 780, 520, 22, 0.15, 100, 1.0));

    [Fact]
    public void TierMult_MajoreChaqueComposante()
    {
        // Run pile aux caps au dernier palier (×1.45) : composantes 39/52/110/10 (total 211 au
        // palier 0) → 56 + 75 + 159 + 14 = 304.
        Assert.Equal(304, EchoFormula.Calculate(780, 520, 22, 20, 10, 5, 10, 780, 520, 22, 0.15, 100, 1.45));
    }

    [Fact]
    public void TierMult_SommeDesComposantesEgaleLeTotal()
    {
        // Contrat avec RunEndScreen : il anime les composantes une par une via ApplyTier et leur
        // somme DOIT tomber sur le total crédité (sinon l'écran de fin ment au joueur).
        const double mult = 1.32;
        var (total, overtime) = EchoFormula.CalculateDetailed(1080, 920, 29, 20, 10, 5, 10, 780, 520, 22, 0.15, 100, mult);
        int somme = EchoFormula.ApplyTier(780 / 20, mult)
                  + EchoFormula.ApplyTier(520 / 10, mult)
                  + EchoFormula.ApplyTier(22 * 5,   mult)
                  + EchoFormula.ApplyTier(10,       mult)
                  + overtime;
        Assert.Equal(total, somme);
    }

    [Fact]
    public void TierMult_NeContournePasLePlafondDeSurcharge()
    {
        // Le plafond overtime (100) s'applique avant le palier : 100 × 1.45 = 145, pas davantage
        // quelle que soit la durée de survie.
        var (_, overtime) = EchoFormula.CalculateDetailed(36000, 80000, 1000, 20, 10, 5, 10, 780, 520, 22, 0.15, 100, 1.45);
        Assert.Equal(145, overtime);
    }
}

public class LevelThreatTests
{
    [Theory]
    [InlineData("sanctuaire", 0)]
    [InlineData("aether", 1)]
    [InlineData("givre", 2)]
    [InlineData("fournaise", 3)]
    [InlineData("neon", 4)]
    [InlineData("biome_inconnu", 0)]   // id inconnu → palier neutre
    [InlineData("", 0)]
    [InlineData(null, 0)]              // hors run / biome pas encore posé
    public void TierOf_SuitLOrdreDeDeblocage(string? biomeId, int expected)
        => Assert.Equal(expected, LevelThreat.TierOf(biomeId));

    [Fact]
    public void Palier0_EstNeutre()
    {
        Assert.Equal(1f, LevelThreat.EnemyHpMult(0), 3);
        Assert.Equal(1f, LevelThreat.ChampionHpMult(0), 3);
        Assert.Equal(1f, LevelThreat.EnemyDamageMult(0), 3);
        Assert.Equal(1f, LevelThreat.SpawnMult(0), 3);
        Assert.Equal(0f, LevelThreat.TimeOffsetMinutes(0), 3);
        Assert.Equal(1.0, LevelThreat.EchoMult(0), 3);
    }

    [Fact]
    public void Multiplicateurs_CroissentAvecLePalier()
    {
        for (int t = 1; t <= LevelThreat.MaxTier; t++)
        {
            Assert.True(LevelThreat.EnemyHpMult(t)     > LevelThreat.EnemyHpMult(t - 1));
            Assert.True(LevelThreat.EnemyDamageMult(t) > LevelThreat.EnemyDamageMult(t - 1));
            Assert.True(LevelThreat.SpawnMult(t)       > LevelThreat.SpawnMult(t - 1));
            Assert.True(LevelThreat.TimeOffsetMinutes(t) > LevelThreat.TimeOffsetMinutes(t - 1));
            Assert.True(LevelThreat.EchoMult(t)        > LevelThreat.EchoMult(t - 1));
        }
    }

    [Fact]
    public void ChampionsEncaissentMoinsQueLesBasiques()
    {
        // Le boss est la condition de déblocage du niveau suivant : son bonus de PV est amorti,
        // mais reste supérieur à 1 (il monte quand même avec le palier).
        for (int t = 1; t <= LevelThreat.MaxTier; t++)
        {
            Assert.True(LevelThreat.ChampionHpMult(t) < LevelThreat.EnemyHpMult(t));
            Assert.True(LevelThreat.ChampionHpMult(t) > 1f);
        }
    }

    [Fact]
    public void PalierHorsBornes_EstClampe()
    {
        Assert.Equal(LevelThreat.EnemyHpMult(0), LevelThreat.EnemyHpMult(-3), 3);
        Assert.Equal(LevelThreat.EnemyHpMult(LevelThreat.MaxTier), LevelThreat.EnemyHpMult(99), 3);
        Assert.Equal(LevelThreat.EchoMult(LevelThreat.MaxTier), LevelThreat.EchoMult(42), 3);
    }

    [Fact]
    public void ProgressionRestePlafonnee()
    {
        // Garde-fou de design : un palier ne doit jamais doubler les stats (le joueur gagne en
        // puissance via le Hub, il ne doit pas repartir de zéro à chaque niveau).
        Assert.True(LevelThreat.EnemyHpMult(LevelThreat.MaxTier)     <= 1.6f);
        Assert.True(LevelThreat.EnemyDamageMult(LevelThreat.MaxTier) <= 1.6f);
        Assert.True(LevelThreat.SpawnMult(LevelThreat.MaxTier)       <= 1.25f);
    }
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

    // ── Courbe non-linéaire (early grace + late accel) ────────────────────────

    [Fact]
    public void CurvedFactor_DebutPlusDouxQueLineaire()
    {
        // À t=0, le facteur est sous 1.0 (ennemis affaiblis) alors que le linéaire vaut 1.0.
        Assert.Equal(0.85f, EnemyScaling.CurvedFactor(0f, 0.14f), 3);   // 1 - 0.15
        Assert.True(EnemyScaling.CurvedFactor(0.5f, 0.14f) < 1f + 0.5f * 0.14f);
    }

    [Fact]
    public void CurvedFactor_RejointLeLineaireEntreEarlyEtLate()
    {
        // Entre la fin de la grâce (1.5) et le début de l'accélération (4), la courbe == linéaire.
        Assert.Equal(1f + 2f * 0.14f, EnemyScaling.CurvedFactor(2f, 0.14f), 3);
        Assert.Equal(1f + 4f * 0.14f, EnemyScaling.CurvedFactor(4f, 0.14f), 3);
    }

    [Fact]
    public void CurvedFactor_LateDepasseLeLineaire()
    {
        // À t=12, la composante quadratique rend les ennemis nettement plus coriaces qu'en linéaire.
        float linear = 1f + 12f * 0.14f;
        Assert.True(EnemyScaling.CurvedFactor(12f, 0.14f) > linear * 1.2f);
    }

    [Fact]
    public void ScaledCurved_AppliqueLaDifficulte()
    {
        // Le multiplicateur de difficulté reste un facteur externe pur.
        float f = EnemyScaling.CurvedFactor(8f, 0.14f);
        Assert.Equal(100f * f * 1.3f, EnemyScaling.ScaledCurved(100f, 8f, 0.14f, 1.3f), 3);
    }
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
    [InlineData(0.75f, 0.75f)]
    [InlineData(1.5f, 0.75f)] // plafonné : 100% écraserait toutes les armes sur le même plancher
    public void CapCooldownReduction(float input, float expected)
        => Assert.Equal(expected, StatCaps.CapCooldownReduction(input), 3);

    [Fact]
    public void CapCooldownReduction_LaisseLesArmesDifferenciees()
    {
        // Le défaut corrigé : à 100 % de réduction, une arme lourde tirait exactement aussi vite
        // qu'un canon léger (toutes deux au plancher). Sous le plafond, l'écart de cadence de
        // fiche doit subsister.
        float cr     = StatCaps.MaxCooldownReduction;
        float lourde = StatCaps.EffectiveCooldown(1.20f, cr);
        float legere = StatCaps.EffectiveCooldown(0.40f, cr);
        Assert.True(lourde > legere, "l'arme lourde doit rester plus lente que l'arme légère");
    }
}

public class PassiveScalingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExtrapolatedDelta_NeTouchePasAuxNiveauxDefinis(int level)
        => Assert.Equal(0.15f, PassiveScaling.ExtrapolatedDelta(0.15f, level, 3), 4);

    [Fact]
    public void ExtrapolatedDelta_DecroitAuDelaDesNiveauxDefinis()
    {
        float precedent = PassiveScaling.ExtrapolatedDelta(0.15f, 3, 3);
        for (int lvl = 4; lvl <= 20; lvl++)
        {
            float d = PassiveScaling.ExtrapolatedDelta(0.15f, lvl, 3);
            Assert.True(d < precedent, $"le niveau {lvl} doit rapporter moins que le précédent");
            Assert.True(d > 0f, "un niveau supplémentaire rapporte toujours quelque chose");
            precedent = d;
        }
    }

    [Fact]
    public void CumulativeBonus_ResteBienEnDecaDeLAdditifPur()
    {
        // thermal_core : 0,15/niveau, 3 niveaux définis, plafond L20.
        // Additif pur (comportement corrigé) : 0,15 × 20 = 3,00 → multiplicateur ×4,00.
        float cumul = PassiveScaling.CumulativeBonus(0.15f, 20, 3);
        Assert.True(cumul < 1.75f, $"cumul trop élevé : {cumul}");
        Assert.True(cumul > 1.20f, $"cumul trop faible, la progression n'est plus lisible : {cumul}");
    }

    [Fact]
    public void CumulativeBonus_CapaciteurResteUtileSurSesPremiersNiveaux()
    {
        // capacitor : 0,14/niveau, 3 niveaux définis. Chaque niveau doit encore rapporter quelque
        // chose au moins jusqu'à L6 — sans quoi la carte serait morte presque tout de suite. Le
        // plafond (0,75) est atteint peu après : LevelUpSystem cesse alors de proposer la carte.
        Assert.True(PassiveScaling.CumulativeBonus(0.14f, 6, 3) < StatCaps.MaxCooldownReduction);
        // Et il finit bien par le franchir : c'est le plafond qui borne, pas l'amortissement.
        Assert.True(PassiveScaling.CumulativeBonus(0.14f, 20, 3) > StatCaps.MaxCooldownReduction);
    }

    [Fact]
    public void CumulativeBonus_EstStrictementCroissant()
    {
        for (int lvl = 1; lvl < 30; lvl++)
            Assert.True(PassiveScaling.CumulativeBonus(0.15f, lvl + 1, 3)
                      > PassiveScaling.CumulativeBonus(0.15f, lvl, 3));
    }
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

public class VersionCompareTests
{
    [Theory]
    [InlineData("1.2.1", "1.2.0")]   // correctif
    [InlineData("1.3.0", "1.2.9")]   // mineur
    [InlineData("2.0.0", "1.9.9")]   // majeur
    [InlineData("1.2.1", "1.2")]     // correctif vs composant manquant cote local
    [InlineData("v1.2.1", "1.2.0")]  // prefixe v tolere
    public void IsNewer_DetecteUneVersionPlusRecente(string remote, string local)
        => Assert.True(VersionCompare.IsNewer(remote, local));

    [Theory]
    [InlineData("1.2.0", "1.2.0")]   // identique
    [InlineData("1.2.0", "1.2.1")]   // plus ancienne
    [InlineData("1.2", "1.2.0")]     // equivalent (composant absent = 0)
    [InlineData("", "1.2.0")]        // distant vide
    public void IsNewer_FauxQuandPasPlusRecente(string remote, string local)
        => Assert.False(VersionCompare.IsNewer(remote, local));
}

public class EliteAffixTableTests
{
    [Fact]
    public void EliteChance_MonteAvecLeTempsEtEstPlafonnee()
    {
        Assert.Equal(EliteAffixTable.BaseChance, EliteAffixTable.EliteChance(0f), 4); // ~3% au début
        Assert.True(EliteAffixTable.EliteChance(5f) > EliteAffixTable.EliteChance(0f)); // croît
        Assert.Equal(EliteAffixTable.MaxChance, EliteAffixTable.EliteChance(100f), 4);  // plafonné
    }

    [Theory]
    [InlineData(0f, 0.0f, true)]   // 0 < 3% → élite
    [InlineData(0f, 0.5f, false)]  // 0.5 >= 3% → normal
    [InlineData(100f, 0.27f, true)]// sous le plafond 28%
    [InlineData(100f, 0.5f, false)]// au-dessus du plafond
    public void ShouldBeElite_ComparingAuTirage(float tMin, float roll, bool expected)
        => Assert.Equal(expected, EliteAffixTable.ShouldBeElite(tMin, roll));

    [Theory]
    [InlineData(0.0f, EliteAffix.Armored)]      // 1er segment
    [InlineData(0.99f, EliteAffix.Vampiric)]    // dernier segment
    [InlineData(1.5f, EliteAffix.Vampiric)]     // au-delà de 1 → clampé sur le dernier
    [InlineData(-1f, EliteAffix.Armored)]       // sous 0 → clampé sur le premier
    public void Pick_TombeDansLeBonAffixe(float roll, EliteAffix expected)
        => Assert.Equal(expected, EliteAffixTable.Pick(roll));

    [Fact]
    public void Pick_CouvreTousLesAffixesSurLaPlage()
    {
        var seen = new System.Collections.Generic.HashSet<EliteAffix>();
        for (int i = 0; i < EliteAffixTable.All.Length; i++)
            seen.Add(EliteAffixTable.Pick((i + 0.5f) / EliteAffixTable.All.Length));
        Assert.Equal(EliteAffixTable.All.Length, seen.Count); // chaque affixe atteignable
    }

    [Fact]
    public void Modifiers_None_EstNeutre()
    {
        var m = EliteAffixTable.Modifiers(EliteAffix.None);
        Assert.Equal(1f, m.HpMult, 3);
        Assert.Equal(1f, m.DamageTakenMult, 3);
        Assert.Equal(0f, m.ExplodeDamageMult, 3);
    }

    [Fact]
    public void Modifiers_ChaqueAffixe_RecompenseEnXpEtAUneSignature()
    {
        foreach (var a in EliteAffixTable.All)
        {
            var m = EliteAffixTable.Modifiers(a);
            Assert.True(m.XpMult > 1f, $"{a} doit récompenser davantage en XP");
            Assert.True(m.HpDropChance > 0f, $"{a} doit pouvoir dropper un orbe de PV");
        }
        // Signatures distinctives attendues
        Assert.True(EliteAffixTable.Modifiers(EliteAffix.Armored).DamageTakenMult < 1f);
        Assert.True(EliteAffixTable.Modifiers(EliteAffix.Regenerating).RegenFractionPerSecond > 0f);
        Assert.True(EliteAffixTable.Modifiers(EliteAffix.Explosive).ExplodeDamageMult > 0f);
        Assert.True(EliteAffixTable.Modifiers(EliteAffix.Frenzied).SpeedMult > 1f);
        Assert.True(EliteAffixTable.Modifiers(EliteAffix.Vampiric).LifestealFraction > 0f);
    }
}
