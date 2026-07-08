using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Tests de la couche de règles pure du système de Défis (ChallengeTable) — sans dépendance Godot.
/// Le JSON inline reproduit la structure de data/challenges.json.
/// </summary>
public class ChallengeTableTests
{
    private const string Json = @"{
      ""challenges"": [
        { ""id"": ""kill100"", ""nameKey"": ""N1"", ""descKey"": ""D1"", ""category"": ""combat"",
          ""condition"": { ""type"": ""kills_in_run"", ""value"": 100 },
          ""reward"": { ""type"": ""echoes"", ""value"": 50 } },
        { ""id"": ""survive5"", ""nameKey"": ""N2"", ""descKey"": ""D2"", ""category"": ""survival"",
          ""condition"": { ""type"": ""survive_seconds"", ""value"": 300 },
          ""reward"": { ""type"": ""echoes"", ""value"": 60 } },
        { ""id"": ""fullChimera"", ""nameKey"": ""N3"", ""descKey"": ""D3"", ""category"": ""assimilation"",
          ""condition"": { ""type"": ""grafts_in_run"", ""value"": 3 },
          ""reward"": { ""type"": ""perk"", ""id"": ""start_graft_swarm"" } },
        { ""id"": ""fuse"", ""nameKey"": ""N4"", ""descKey"": ""D4"", ""category"": ""assimilation"",
          ""condition"": { ""type"": ""fusion_in_run"" },
          ""reward"": { ""type"": ""cosmetic"", ""id"": ""title_chimera"" } },
        { ""id"": ""neon"", ""nameKey"": ""N5"", ""descKey"": ""D5"", ""category"": ""mastery"",
          ""condition"": { ""type"": ""complete_biome"", ""param"": ""neon"" },
          ""reward"": { ""type"": ""perk"", ""id"": ""start_weapon_glaive"" } },
        { ""id"": ""hard"", ""nameKey"": ""N6"", ""descKey"": ""D6"", ""category"": ""mastery"",
          ""condition"": { ""type"": ""complete_difficulty"", ""value"": 2 },
          ""reward"": { ""type"": ""cosmetic"", ""id"": ""title_apex"" } },
        { ""id"": ""allBiomes"", ""nameKey"": ""N7"", ""descKey"": ""D7"", ""category"": ""mastery"",
          ""condition"": { ""type"": ""biomes_completed"", ""value"": 5 },
          ""reward"": { ""type"": ""perk"", ""id"": ""start_extra_slot"" } },
        { ""id"": ""veteran"", ""nameKey"": ""N8"", ""descKey"": ""D8"", ""category"": ""survival"",
          ""condition"": { ""type"": ""lifetime_runs"", ""value"": 25 },
          ""reward"": { ""type"": ""echoes"", ""value"": 150 } }
      ]
    }";

    private static List<ChallengeTable.ChallengeDef> Defs() => ChallengeTable.Parse(Json);

    private static ChallengeTable.ChallengeContext Ctx(
        int time = 0, int kills = 0, int cores = 0, bool completed = false, string biome = "",
        int diff = 1, int grafts = 0, bool fusion = false, long lifeKills = 0, int lifeRuns = 0, int biomesDone = 0)
        => new(time, kills, cores, completed, biome, diff, grafts, fusion, lifeKills, lifeRuns, biomesDone);

    // ── Parsing ────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ReadsAllChallenges()
    {
        var defs = Defs();
        Assert.Equal(8, defs.Count);
        var kill = defs.First(d => d.Id == "kill100");
        Assert.Equal("kills_in_run", kill.CondType);
        Assert.Equal(100, kill.CondValue);
        Assert.Equal(ChallengeTable.RewardKind.Echoes, kill.RewardType);
        Assert.Equal(50, kill.RewardEchoes);
    }

    [Fact]
    public void Parse_ReadsRewardVariants()
    {
        var defs = Defs();
        Assert.Equal(ChallengeTable.RewardKind.Perk,     defs.First(d => d.Id == "fullChimera").RewardType);
        Assert.Equal("start_graft_swarm",                defs.First(d => d.Id == "fullChimera").RewardId);
        Assert.Equal(ChallengeTable.RewardKind.Cosmetic, defs.First(d => d.Id == "fuse").RewardType);
        Assert.Equal("title_chimera",                    defs.First(d => d.Id == "fuse").RewardId);
    }

    [Fact]
    public void Parse_ReadsConditionParam()
        => Assert.Equal("neon", Defs().First(d => d.Id == "neon").CondParam);

    // ── IsMet : conditions numériques ────────────────────────────────────────────

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(500, true)]
    public void IsMet_KillsInRun_UsesThreshold(int kills, bool expected)
    {
        var def = Defs().First(d => d.Id == "kill100");
        Assert.Equal(expected, ChallengeTable.IsMet(def, Ctx(kills: kills)));
    }

    [Fact]
    public void IsMet_SurviveSeconds_UsesThreshold()
    {
        var def = Defs().First(d => d.Id == "survive5");
        Assert.False(ChallengeTable.IsMet(def, Ctx(time: 299)));
        Assert.True(ChallengeTable.IsMet(def, Ctx(time: 300)));
    }

    [Fact]
    public void IsMet_FusionInRun_IgnoresValue()
    {
        var def = Defs().First(d => d.Id == "fuse");
        Assert.False(ChallengeTable.IsMet(def, Ctx(fusion: false)));
        Assert.True(ChallengeTable.IsMet(def, Ctx(fusion: true)));
    }

    // ── IsMet : complétion (biome / difficulté) ──────────────────────────────────

    [Fact]
    public void IsMet_CompleteBiome_RequiresCompletionAndMatchingBiome()
    {
        var def = Defs().First(d => d.Id == "neon");
        Assert.False(ChallengeTable.IsMet(def, Ctx(completed: false, biome: "neon")));   // pas complété
        Assert.False(ChallengeTable.IsMet(def, Ctx(completed: true,  biome: "givre")));  // mauvais biome
        Assert.True (ChallengeTable.IsMet(def, Ctx(completed: true,  biome: "neon")));
    }

    [Theory]
    [InlineData(1, false)]   // Normal < Difficile
    [InlineData(2, true)]    // Difficile
    public void IsMet_CompleteDifficulty_NeedsRankAndCompletion(int rank, bool expected)
    {
        var def = Defs().First(d => d.Id == "hard");
        Assert.Equal(expected, ChallengeTable.IsMet(def, Ctx(completed: true, diff: rank)));
        Assert.False(ChallengeTable.IsMet(def, Ctx(completed: false, diff: 2)));  // jamais si pas complété
    }

    // ── IsMet : cumulés ──────────────────────────────────────────────────────────

    [Fact]
    public void IsMet_LifetimeRuns_UsesCumulativeCounter()
    {
        var def = Defs().First(d => d.Id == "veteran");
        Assert.False(ChallengeTable.IsMet(def, Ctx(lifeRuns: 24)));
        Assert.True(ChallengeTable.IsMet(def, Ctx(lifeRuns: 25)));
    }

    [Fact]
    public void IsMet_UnknownType_NeverMet()
    {
        var def = new ChallengeTable.ChallengeDef { Id = "x", CondType = "does_not_exist", CondValue = 0 };
        Assert.False(ChallengeTable.IsMet(def, Ctx(kills: 999999, completed: true)));
    }

    // ── NewlyCompleted ───────────────────────────────────────────────────────────

    [Fact]
    public void NewlyCompleted_ReturnsMetButNotAlreadyUnlocked()
    {
        var defs = Defs();
        var ctx = Ctx(kills: 150, time: 400);   // satisfait kill100 ET survive5
        var already = new HashSet<string> { "kill100" };

        var newly = ChallengeTable.NewlyCompleted(defs, in ctx, already);

        Assert.Contains("survive5", newly);
        Assert.DoesNotContain("kill100", newly);   // déjà débloqué
    }

    [Fact]
    public void NewlyCompleted_EmptyWhenNothingMet()
    {
        var newly = ChallengeTable.NewlyCompleted(Defs(), Ctx(), new HashSet<string>());
        Assert.Empty(newly);
    }

    [Fact]
    public void NewlyCompleted_PreservesDefinitionOrder()
    {
        var ctx = Ctx(kills: 200, time: 400);   // kill100 (index 0) avant survive5 (index 1)
        var newly = ChallengeTable.NewlyCompleted(Defs(), in ctx, new HashSet<string>());
        Assert.Equal(new[] { "kill100", "survive5" }, newly.ToArray());
    }
}
