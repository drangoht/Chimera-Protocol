using System.Linq;
using Xunit;

/// <summary>
/// Tests de la couche de règles pure du système d'Assimilation (GraftTable) — sans dépendance Godot.
/// Le JSON inline reproduit la structure de data/grafts.json (§16 du design chiffré).
/// </summary>
public class GraftTableTests
{
    private const string Json = @"{
      ""slots"": { ""baseCount"": 3, ""maxCount"": 5, ""replacementWhenFull"": true },
      ""gauges"": {
        ""thresholds"": { ""swarm"": 30, ""drone"": 24, ""sentinel"": 14, ""colossus"": 7, ""stalker"": 3 },
        ""aiTypeToGauge"": {
          ""straight_chase"": ""swarm"",
          ""erratic_chase"": ""drone"",
          ""ranged_kiter"": ""sentinel"",
          ""slow_hunter"": ""colossus""
        },
        ""pointsBasicKill"": 1,
        ""eliteKillArchetypePoints"": 2,
        ""eliteKillStalkerPoints"": 1,
        ""miniBossStalkerPoints"": 2,
        ""bossStalkerPoints"": 3,
        ""ownedGraftPausesGauge"": true,
        ""resumePausedGaugeFromSavedValue"": true,
        ""declineThresholdMultiplier"": 1.5
      },
      ""grafts"": [
        { ""id"": ""swarm_symbiote"", ""name"": ""Nuée Symbiotique"", ""gauge"": ""swarm"", ""sourceAiType"": ""straight_chase"", ""rarity"": ""common"",
          ""tint"": [1.3, 0.55, 0.4],
          ""effects"": { ""orbitingAllies"": { ""count"": 3, ""contactDamage"": 5, ""lifestealFraction"": 0.04 } },
          ""statMods"": {} },
        { ""id"": ""erratic_servos"", ""name"": ""Servos Erratiques"", ""gauge"": ""drone"", ""sourceAiType"": ""erratic_chase"", ""rarity"": ""common"",
          ""effects"": { ""dash"": { ""distancePx"": 180, ""cooldownSec"": 3.5, ""cooldownFloorSec"": 1.5 } },
          ""statMods"": {} },
        { ""id"": ""aiming_eye"", ""name"": ""Œil de Visée"", ""gauge"": ""sentinel"", ""sourceAiType"": ""ranged_kiter"", ""rarity"": ""rare"",
          ""effects"": { ""autoTurret"": { ""damage"": 18, ""pierceCount"": 1 } }, ""statMods"": {} },
        { ""id"": ""grafted_carapace"", ""name"": ""Carapace Greffée"", ""gauge"": ""colossus"", ""sourceAiType"": ""slow_hunter"", ""rarity"": ""rare"",
          ""effects"": { ""thorns"": { ""damage"": 18, ""radiusPx"": 40 } },
          ""statMods"": { ""damageReductionAdd"": 0.15, ""maxHpAdd"": 25, ""speedMult"": 0.82 } },
        { ""id"": ""stalker_wave"", ""name"": ""Onde du Rôdeur"", ""gauge"": ""stalker"", ""sourceAiType"": ""champion"", ""rarity"": ""epic"",
          ""effects"": { ""shockwave"": { ""damage"": 60, ""radiusPx"": 160 } }, ""statMods"": {} }
      ]
    }";

    private static GraftTable.GraftConfig Cfg() => GraftTable.Parse(Json);

    // ── Parsing ─────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_LitSlotsEtSeuils()
    {
        var cfg = Cfg();
        Assert.Equal(3, cfg.SlotBaseCount);
        Assert.Equal(5, cfg.SlotMaxCount);
        Assert.Equal(30, cfg.Thresholds["swarm"]);
        Assert.Equal(3, cfg.Thresholds["stalker"]);
        Assert.Equal(5, cfg.Grafts.Count);
    }

    [Fact]
    public void Parse_DeriveLaJaugeChampionDepuisSourceAiType()
        => Assert.Equal("stalker", Cfg().ChampionGaugeKey);

    [Fact]
    public void Parse_LitEffetsEtStatMods()
    {
        var cfg = Cfg();
        var carapace = cfg.GraftById("grafted_carapace")!;
        Assert.Equal(0.15, carapace.Stat("damageReductionAdd"));
        Assert.Equal(25, carapace.Stat("maxHpAdd"));
        Assert.Equal(0.82, carapace.Stat("speedMult"));
        Assert.True(carapace.HasEffect("thorns"));
        Assert.Equal(40, carapace.Effect("thorns", "radiusPx"));

        var swarm = cfg.GraftById("swarm_symbiote")!;
        Assert.Equal(3, swarm.Effect("orbitingAllies", "count"));
        Assert.Equal(0.04, swarm.Effect("orbitingAllies", "lifestealFraction"));
    }

    [Fact]
    public void GraftForGauge_EstBijectif()
    {
        var cfg = Cfg();
        Assert.Equal("swarm_symbiote", cfg.GraftForGauge("swarm")!.Id);
        Assert.Equal("stalker_wave", cfg.GraftForGauge("stalker")!.Id);
        Assert.Null(cfg.GraftForGauge("inconnu"));
    }

    // ── Routage kill → jauge (§12.1) ──────────────────────────────────────────
    [Theory]
    [InlineData("straight_chase", "swarm")]
    [InlineData("erratic_chase", "drone")]
    [InlineData("ranged_kiter", "sentinel")]
    [InlineData("slow_hunter", "colossus")]
    public void RouteKill_BasiqueVaVersSaJauge(string aiType, string gauge)
    {
        var c = GraftTable.RouteKill(Cfg(), aiType, isElite: false, isMiniBoss: false, isBoss: false);
        Assert.Single(c);
        Assert.Equal(gauge, c[0].Gauge);
        Assert.Equal(1, c[0].Points);
    }

    [Fact]
    public void RouteKill_ArchetypeInconnuNeContribuePas()
    {
        var c = GraftTable.RouteKill(Cfg(), "boss_core", false, false, false);
        Assert.Empty(c);
    }

    [Fact]
    public void RouteKill_EliteVaVersArchetypeEtStalker()
    {
        var c = GraftTable.RouteKill(Cfg(), "straight_chase", isElite: true, isMiniBoss: false, isBoss: false);
        Assert.Equal(2, c.Count);
        Assert.Equal(2, c.First(x => x.Gauge == "swarm").Points);
        Assert.Equal(1, c.First(x => x.Gauge == "stalker").Points);
    }

    [Fact]
    public void RouteKill_MiniBossVaUniquementVersStalker()
    {
        var c = GraftTable.RouteKill(Cfg(), "straight_chase", isElite: false, isMiniBoss: true, isBoss: false);
        Assert.Single(c);
        Assert.Equal("stalker", c[0].Gauge);
        Assert.Equal(2, c[0].Points);
    }

    [Fact]
    public void RouteKill_BossVaUniquementVersStalkerAvec3Points()
    {
        var c = GraftTable.RouteKill(Cfg(), "boss_core", isElite: false, isMiniBoss: false, isBoss: true);
        Assert.Single(c);
        Assert.Equal("stalker", c[0].Gauge);
        Assert.Equal(3, c[0].Points);
    }

    [Fact]
    public void RouteKill_BossPrioritaireSurElite()
    {
        // isBoss l'emporte même si isElite est passé à true (cas impossible en jeu, garde-fou de priorité).
        var c = GraftTable.RouteKill(Cfg(), "slow_hunter", isElite: true, isMiniBoss: false, isBoss: true);
        Assert.Single(c);
        Assert.Equal("stalker", c[0].Gauge);
        Assert.Equal(3, c[0].Points);
    }

    // ── Seuils : bonus méta 'graft_metabolism' (§17) ──────────────────────────
    [Theory]
    [InlineData(30, 0.0, 30)]
    [InlineData(30, 0.30, 21)]   // round(30 * 0.70) = 21
    [InlineData(24, 0.30, 17)]   // round(24 * 0.70) = 16.8 -> 17
    [InlineData(14, 0.30, 10)]   // round(14 * 0.70) = 9.8 -> 10
    [InlineData(7, 0.30, 5)]     // round(7 * 0.70) = 4.9 -> 5
    [InlineData(3, 0.30, 2)]     // round(3 * 0.70) = round(2.1) = 2 (formule passée de ceil à round pour
                                 // reproduire les 5 valeurs cibles du design §17 ; ceil aurait laissé stalker à 3)
    public void EffectiveThreshold_AppliqueLeBonusMeta(int baseTh, double bonus, int expected)
        => Assert.Equal(expected, GraftTable.EffectiveThreshold(baseTh, bonus));

    [Fact]
    public void EffectiveThreshold_JamaisSousUn()
        => Assert.Equal(1, GraftTable.EffectiveThreshold(1, 0.9));

    // ── Refus : seuil x1.5 (§12.3) ────────────────────────────────────────────
    [Fact]
    public void DeclinedThreshold_MultipliePar1Point5()
    {
        Assert.Equal(45, GraftTable.DeclinedThreshold(30, 1.5));
        Assert.Equal(5, GraftTable.DeclinedThreshold(3, 1.5));   // ceil(4.5) = 5
    }

    // ── Slots : bonus méta 'graft_slots', plafond 5 ───────────────────────────
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    [InlineData(3, 5)]   // plafonné à maxCount
    public void SlotCount_RespecteLePlafond(int metaBonus, int expected)
        => Assert.Equal(expected, GraftTable.SlotCount(Cfg(), metaBonus));
}
