using System.IO;
using Xunit;

/// <summary>
/// Vérifie la migration <b>sur la sauvegarde réelle du testeur</b> (141 runs, 60 946 Échos en
/// banque) : c'est le seul point du portage dont l'échec est irréversible pour un joueur.
/// </summary>
public class SaveMigrationTests
{
    private static string Fixture(string name)
        => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "tests", "fixtures", name));

    private static SettingsData RealSettings() => SaveMigration.FromLegacySettings(Fixture("legacy_settings.cfg"));
    private static SaveData     RealSave()     => SaveMigration.ReadSave(Fixture("legacy_save.json"));

    [Fact]
    public void LaProgressionMetaSurvitIntegralement()
    {
        var save = RealSave();

        Assert.Equal(60946, save.Meta.CurrentEchoes);
        Assert.Equal(74696, save.Meta.TotalEchoesEarned);
        Assert.Equal(13750, save.Meta.TotalEchoesSpent);
        Assert.Equal(14, save.Meta.Upgrades.Count);
        Assert.Equal(3, save.Meta.Upgrades["overtime_stabilizer"]);
    }

    [Fact]
    public void LesPreferencesEtLaProgressionDuFichierCfgSurvivent()
    {
        var s = RealSettings();

        Assert.Equal("fr", s.Language);
        Assert.Equal(2, s.SaveVersion);
        Assert.Equal(0.8f, s.MusicVolume, 3);
        Assert.Equal(0.95f, s.ShakeIntensity, 3);

        // La preuve de ce qui serait perdu sans migration : difficulté par biome, records, complétions.
        Assert.Equal(6, s.SaturationBeatenByLevel["sanctuaire"]);
        Assert.Equal(4, s.SaturationByLevel.Count);
        Assert.Equal(5, s.Completions.Count);
        Assert.Equal(2157, s.HighScores["fournaise"]);
        Assert.Equal(20, s.DiscoveredWeapons.Count);
        Assert.Equal(8, s.DiscoveredGrafts.Count);
    }

    /// <summary>
    /// Aller-retour complet : ce qui est migré doit se relire à l'identique au format Unity, sinon la
    /// perte n'arrive pas à la migration mais au <b>second lancement</b> — bien plus difficile à
    /// diagnostiquer.
    /// </summary>
    [Fact]
    public void LAllerRetourAuFormatUnityNePerdRien()
    {
        var settings = SaveMigration.ReadSettings(SaveMigration.WriteSettings(RealSettings()));
        var save     = SaveMigration.ReadSave(SaveMigration.WriteSave(RealSave()));

        Assert.Equal(6, settings.SaturationBeatenByLevel["sanctuaire"]);
        Assert.Equal(2157, settings.HighScores["fournaise"]);
        Assert.Equal(20, settings.DiscoveredWeapons.Count);
        Assert.Equal("fr", settings.Language);

        Assert.Equal(60946, save.Meta.CurrentEchoes);
        Assert.Equal(14, save.Meta.Upgrades.Count);
    }

    [Fact]
    public void UneSauvegardeAbimeeNeFaitPasEchouerLeDemarrage()
    {
        Assert.Equal(0, SaveMigration.ReadSave("{ ceci n'est pas du json").Meta.CurrentEchoes);
        Assert.Equal(0, SaveMigration.ReadSave(null).Meta.CurrentEchoes);
        Assert.Equal("fr", SaveMigration.ReadSettings("<xml/>").Language);
    }

    /// <summary>
    /// Un joueur sans historique ne doit pas voir de message de reprise : « rien à migrer » et
    /// « migration ratée » se ressemblent trop pour être confondus dans un journal.
    /// </summary>
    [Fact]
    public void UneInstallationNeuveNAnnoncePasDeReprise()
    {
        Assert.False(SaveMigration.CarriesProgress(new SaveData(), new SettingsData()));
        Assert.True(SaveMigration.CarriesProgress(RealSave(), RealSettings()));
    }
}
