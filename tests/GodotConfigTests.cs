using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Vérifie la lecture du <c>settings.cfg</c> de Godot — <b>sur une sauvegarde réelle de joueur</b>,
/// figée dans <c>tests/fixtures/</c>.
///
/// <para>C'est le seul endroit du portage dont l'échec est <b>irréversible pour le joueur</b> : une
/// clé mal lue, et ce sont ses crans de saturation, ses complétions, ses records et son arsenal
/// découvert qui disparaissent (§9.3, risque R5). Un échantillon inventé ne prouverait rien —
/// il faut le fichier tel que le jeu l'a écrit après 141 runs.</para>
/// </summary>
public class GodotConfigTests
{
    private static GodotConfig.Document Real()
        => GodotConfig.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "tests", "fixtures", "legacy_settings.cfg")));

    [Fact]
    public void LesSectionsDuFichierReelSontLues()
    {
        var doc = Real();

        foreach (string section in new[]
                 { "audio", "display", "gameplay", "progress", "highscores", "discovered" })
            Assert.Contains(section, doc.Sections);
    }

    [Fact]
    public void LesTypesSimplesSontRendusDansLeurType()
    {
        var doc = Real();

        Assert.Equal("fr", doc.GetString("display", "language"));   // guillemets retirés
        Assert.Equal(2, doc.GetInt("display", "mode"));
        Assert.True(doc.GetBool("display", "vsync"));
        Assert.False(doc.GetBool("display", "show_fps"));
        Assert.Equal(0.8f, doc.GetFloat("audio", "music"), 3);
    }

    /// <summary>
    /// La table <c>biome:cran</c> de la 1.25.0 : c'est elle qui porte la progression de difficulté
    /// choisie par le joueur, niveau par niveau.
    /// </summary>
    [Fact]
    public void LaTableDeSaturationParBiomeEstReconstruite()
    {
        var doc = Real();
        var table = GodotConfig.ParsePairTable(doc.GetStringArray("gameplay", "saturation_by_level"));

        Assert.Equal(4, table.Count);
        Assert.Equal(1, table["sanctuaire"]);
        Assert.Equal(1, table["neon"]);

        // Le cran le plus haut battu : la preuve que le joueur a fini le cran VI du Sanctuaire.
        var beaten = GodotConfig.ParsePairTable(doc.GetStringArray("gameplay", "saturation_beaten_by_level"));
        Assert.Equal(6, beaten["sanctuaire"]);

        Assert.Equal(2, doc.GetInt("gameplay", "save_version"));
    }

    [Fact]
    public void LesRecordsEtCompletionsSurvivent()
    {
        var doc = Real();

        Assert.Equal(2157, doc.GetInt("highscores", "fournaise"));
        Assert.Equal(1176, doc.GetInt("highscores", "sanctuaire"));

        // Les clés d'une section ouverte se listent : le jeu ne connaît pas d'avance tous les biomes.
        Assert.Equal(5, doc.Keys("highscores").Count);

        var completions = GodotConfig.ParsePairTable(doc.GetStringArray("progress", "completions"));
        Assert.Equal(5, completions.Count);
    }

    [Fact]
    public void LArsenalDecouvertSurvit()
    {
        var doc = Real();

        var weapons = doc.GetStringArray("discovered", "weapons");
        Assert.Equal(20, weapons.Count);
        Assert.Contains("frost_veil", weapons);

        Assert.Equal(8, doc.GetStringArray("discovered", "grafts").Count);
    }

    /// <summary>
    /// Une donnée absente ou abîmée doit rendre la valeur par défaut, <b>jamais</b> faire échouer la
    /// lecture entière : perdre toute la sauvegarde parce qu'une ligne a changé serait précisément le
    /// défaut qu'on cherche à éviter.
    /// </summary>
    [Fact]
    public void UneDonneeAbimeeNeFaitPasPerdreLeReste()
    {
        var doc = GodotConfig.Parse("[a]\nbon=1\ncasse=\n=orphelin\nligne sans egal\n[b]\nautre=\"x\"");

        Assert.Equal(1, doc.GetInt("a", "bon"));
        Assert.Equal(42, doc.GetInt("a", "casse", 42));
        Assert.Equal(42, doc.GetInt("a", "inexistant", 42));
        Assert.Equal("x", doc.GetString("b", "autre"));
    }

    [Fact]
    public void UnFichierVideOuAbsentNeLevePasDException()
    {
        Assert.Empty(GodotConfig.Parse(null).Sections);
        Assert.Empty(GodotConfig.Parse("").Sections);
        Assert.Empty(GodotConfig.Parse("   \n\n  ").Sections);
    }

    [Theory]
    [InlineData("sanctuaire:6", "sanctuaire", 6)]
    [InlineData("neon:0", "neon", 0)]
    public void UnePaireSeDecompose(string entry, string expectedKey, int expectedValue)
    {
        Assert.True(GodotConfig.TrySplitPair(entry, out string key, out int value));
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("sanctuaire")]
    [InlineData("sanctuaire:")]
    [InlineData(":6")]
    [InlineData("sanctuaire:pas_un_nombre")]
    public void UnePaireMalFormeeEstIgnoree(string entry)
    {
        Assert.False(GodotConfig.TrySplitPair(entry, out _, out _));
    }

    /// <summary>
    /// La sauvegarde JSON, elle, se relit telle quelle : c'est <b>le même DTO</b> compilé par les deux
    /// moteurs. Ce test le prouve sur la vraie sauvegarde — 60 946 Échos, 14 améliorations achetées.
    /// </summary>
    [Fact]
    public void LaSauvegardeReelleSeRelitSansPerte()
    {
        string json = File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "tests", "fixtures", "legacy_save.json"));

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };

        var data = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json, options);

        Assert.NotNull(data);
        Assert.Equal(60946, data!.Meta.CurrentEchoes);
        Assert.Equal(74696, data.Meta.TotalEchoesEarned);
        Assert.Equal(14, data.Meta.Upgrades.Count);
        Assert.Equal(5, data.Meta.Upgrades["damage_boost"]);
        Assert.True(data.Meta.UnlockedChallenges.Count > 0);
        Assert.True(data.Meta.LifetimeRuns > 0, "le compteur de runs cumulées doit survivre");
    }
}
