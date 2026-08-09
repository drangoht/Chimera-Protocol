using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Remise à zéro totale demandée par le joueur (options).
///
/// <para>Ce qui se joue ici est une <b>frontière</b> : ce qui se gagne disparaît, ce qui se règle
/// survit. Un joueur qui repart de zéro ne demande pas à retrouver un jeu dans une langue qu'il ne
/// lit pas, avec ses touches redevenues ZQSD et son écran remis en fenêtré.</para>
/// </summary>
public class SettingsResetTests
{
    private static SettingsData Populated()
    {
        var s = new SettingsData
        {
            // Préférences : doivent SURVIVRE.
            Language = "es",
            MasterVolume = 0.42f,
            DisplayMode = 0,
            ShakeIntensity = 0f,
            Difficulty = 2,
        };

        // Progression : doit disparaître.
        s.Completions["sanctuaire"] = 3;
        s.HighScores["sanctuaire"] = 812;
        s.DiscoveredWeapons.Add("tesla_coil");
        s.DiscoveredGrafts.Add("neural_link");
        s.SaturationByLevel["sanctuaire"] = 4;
        s.SaturationBeatenByLevel["sanctuaire"] = 3;

        return s;
    }

    [Fact]
    public void ResetProgress_ClearsEverythingThatWasEarned()
    {
        var s = Populated();
        s.ResetProgress();

        Assert.Empty(s.Completions);
        Assert.Empty(s.HighScores);
        Assert.Empty(s.DiscoveredWeapons);
        Assert.Empty(s.DiscoveredGrafts);
    }

    /// <summary>
    /// Les crans de saturation tombent avec le reste — le <b>choisi</b> comme le <b>battu</b>. Un
    /// cran se débloque en battant le précédent : en laisser un survivre offrirait une échelle
    /// gravie à un joueur dont le compteur de victoires vient d'être remis à zéro.
    /// </summary>
    [Fact]
    public void ResetProgress_ClearsSaturationLadder()
    {
        var s = Populated();
        s.ResetProgress();

        Assert.Empty(s.SaturationByLevel);
        Assert.Empty(s.SaturationBeatenByLevel);
    }

    [Fact]
    public void ResetProgress_KeepsPreferences()
    {
        var s = Populated();
        s.ResetProgress();

        Assert.Equal("es", s.Language);
        Assert.Equal(0.42f, s.MasterVolume, 3);
        Assert.Equal(0, s.DisplayMode);
        Assert.Equal(0f, s.ShakeIntensity);
        Assert.Equal(2, s.Difficulty);
    }

    /// <summary>Deux appels de suite ne lèvent rien : le bouton est cliquable deux fois.</summary>
    [Fact]
    public void ResetProgress_IsIdempotent()
    {
        var s = Populated();
        s.ResetProgress();
        s.ResetProgress();

        Assert.Empty(s.Completions);
    }
}
