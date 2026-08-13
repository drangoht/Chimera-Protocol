using Xunit;

/// <summary>
/// Statistiques de cadence.
///
/// <para>Ce qui est vérifié ici n'est pas l'arithmétique — c'est le <b>choix de mesure</b> : une
/// moyenne seule masque exactement ce qu'on cherche. Le projet a déjà payé cette erreur sur la
/// pression subie, où la moyenne des dégâts ne voyait aucun des pics qui tuaient le joueur.</para>
/// </summary>
public class FrameStatsTests
{
    [Fact]
    public void SansImage_NeDiviseParZero()
    {
        var s = new FrameStats();

        Assert.Equal(0.0, s.AverageFps);
        Assert.Equal(0.0, s.ShareBelowThreshold);
        Assert.Equal(0, s.Frames);
    }

    [Fact]
    public void SoixanteImagesDUneSeconde_DonnentSoixante()
    {
        var s = new FrameStats();
        for (int i = 0; i < 60; i++) s.Add(1.0 / 60.0);

        Assert.Equal(60.0, s.AverageFps, 1);
        Assert.Equal(60, s.Frames);
    }

    /// <summary>
    /// Le cas qui justifie l'existence de cette classe.
    /// </summary>
    /// <remarks>
    /// Cinquante-neuf images à 120 et une seule à 200 ms : la moyenne reste flatteuse, et pourtant le
    /// joueur a vu un à-coup d'un cinquième de seconde. Si seule la moyenne était rapportée, le
    /// relevé dirait que tout va bien.
    /// </remarks>
    [Fact]
    public void UneImageLente_NeDisparaitPasDansLaMoyenne()
    {
        var s = new FrameStats();
        for (int i = 0; i < 59; i++) s.Add(1.0 / 120.0);
        s.Add(0.200);

        Assert.True(s.AverageFps > 55.0, $"moyenne flatteuse attendue, obtenue {s.AverageFps:F1}");
        Assert.Equal(200.0, s.WorstFrameMs, 0);
        Assert.Equal(1, s.FramesBelowThreshold);
    }

    [Fact]
    public void PartSousLeSeuil_CompteLesImagesTropLongues()
    {
        var s = new FrameStats(threshold: 30.0);

        // 1/30 = 33,3 ms. À 25 ms on est au-dessus du seuil, à 50 ms en dessous.
        for (int i = 0; i < 3; i++) s.Add(0.025);
        for (int i = 0; i < 1; i++) s.Add(0.050);

        Assert.Equal(1, s.FramesBelowThreshold);
        Assert.Equal(0.25, s.ShareBelowThreshold, 3);
    }

    /// <summary>
    /// Une durée nulle ou négative ne vient pas du rendu.
    /// </summary>
    /// <remarks>
    /// Elle vient d'une pause, d'un changement de scène, ou de la toute première image. La compter
    /// tirerait la cadence vers le haut — c'est-à-dire dans le sens rassurant, celui qui ne fait pas
    /// chercher plus loin.
    /// </remarks>
    [Fact]
    public void DureeNulleOuNegative_EstIgnoree()
    {
        var s = new FrameStats();
        s.Add(1.0 / 60.0);
        s.Add(0.0);
        s.Add(-0.5);

        Assert.Equal(1, s.Frames);
        Assert.Equal(60.0, s.AverageFps, 1);
    }

    [Fact]
    public void Reset_NeTraineePasLaFenetrePrecedente()
    {
        var s = new FrameStats();
        s.Add(0.5);
        s.Reset();
        s.Add(1.0 / 60.0);

        Assert.Equal(1, s.Frames);
        Assert.Equal(0, s.FramesBelowThreshold);
        Assert.Equal(1000.0 / 60.0, s.WorstFrameMs, 1);
    }

    [Fact]
    public void Format_PorteLesTroisChiffres()
    {
        var s = new FrameStats();
        for (int i = 0; i < 10; i++) s.Add(1.0 / 60.0);

        string line = s.Format();

        Assert.Contains("moy=", line);
        Assert.Contains("pire=", line);
        Assert.Contains("sous30=", line);
    }
}
