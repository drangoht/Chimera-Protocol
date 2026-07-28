using Xunit;

/// <summary>
/// Tests des phases de boss (src/Core/Rules/BossPhases.cs).
/// On vérifie ce qui se voit en jeu si ça casse : le bon seuil, l'irréversibilité de la
/// progression, et le fait que chaque phase soit strictement plus pressante que la précédente
/// (une table mal ordonnée rendrait la phase III plus molle que la phase I sans erreur de compil).
/// Cf. docs/GDD.md §29.
/// </summary>
public class BossPhasesTests
{
    // -----------------------------------------------------------------------
    // PhaseAt — seuils
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1.00f, 0)]
    [InlineData(0.67f, 0)]
    [InlineData(0.66f, 0)]   // seuil non franchi : on entre en phase II STRICTEMENT sous 0,66
    [InlineData(0.659f, 1)]
    [InlineData(0.34f, 1)]
    [InlineData(0.33f, 1)]
    [InlineData(0.329f, 2)]
    [InlineData(0.00f, 2)]
    public void PhaseAt_RespecteLesSeuils(float ratio, int attendu)
        => Assert.Equal(attendu, BossPhases.PhaseAt(ratio));

    [Theory]
    [InlineData(5f)]
    [InlineData(-3f)]
    [InlineData(float.NaN)]
    public void PhaseAt_ResteDansLesBornesSurEntreeAberrante(float ratio)
    {
        int p = BossPhases.PhaseAt(ratio);
        Assert.InRange(p, 0, BossPhases.Count - 1);
    }

    [Fact]
    public void PhaseAt_EstMonotoneQuandLesPvBaissent()
    {
        int precedent = 0;
        for (int i = 100; i >= 0; i--)
        {
            int p = BossPhases.PhaseAt(i / 100f);
            Assert.True(p >= precedent, $"la phase a reculé à {i} % de PV");
            precedent = p;
        }
    }

    // -----------------------------------------------------------------------
    // Advance — irréversibilité
    // -----------------------------------------------------------------------

    [Fact]
    public void Advance_NeRecuelJamaisSiLeBossRegagneDesPv()
    {
        int phase = BossPhases.Advance(0, 0.20f);      // chute directe en phase III
        Assert.Equal(2, phase);
        Assert.Equal(2, BossPhases.Advance(phase, 0.90f));  // soigné à fond : reste en III
    }

    [Fact]
    public void Advance_PeutSauterUnePhaseSurGrosBurst()
        => Assert.Equal(2, BossPhases.Advance(0, 0.05f));

    [Theory]
    [InlineData(-4)]
    [InlineData(99)]
    public void Advance_BorneLaPhaseCourante(int phaseCourante)
        => Assert.InRange(BossPhases.Advance(phaseCourante, 1f), 0, BossPhases.Count - 1);

    // -----------------------------------------------------------------------
    // Tables — l'intensité monte, jamais l'inverse
    // -----------------------------------------------------------------------

    [Fact]
    public void LesIntervallesSeResserrentAChaquePhase()
    {
        for (int p = 1; p < BossPhases.Count; p++)
        {
            Assert.True(BossPhases.BurstInterval(p) < BossPhases.BurstInterval(p - 1));
            Assert.True(BossPhases.ShockInterval(p) < BossPhases.ShockInterval(p - 1));
            Assert.True(BossPhases.SignatureRate(p) > BossPhases.SignatureRate(p - 1));
            Assert.True(BossPhases.SpeedMult(p) >= BossPhases.SpeedMult(p - 1));
        }
    }

    [Fact]
    public void PhaseUnNeModifiePasLesValeursDeBase()
    {
        Assert.Equal(1f, BossPhases.SignatureRate(0), 4);
        Assert.Equal(1f, BossPhases.SpeedMult(0), 4);
        Assert.Equal(7f, BossPhases.SignatureInterval(0, 7f), 4);
    }

    [Fact]
    public void SignatureInterval_RaccourcitLaPeriodeDeBase()
    {
        float p1 = BossPhases.SignatureInterval(0, 8f);
        float p3 = BossPhases.SignatureInterval(2, 8f);
        Assert.True(p3 < p1);
        Assert.Equal(8f / BossPhases.SignatureRate(2), p3, 4);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(7)]
    public void LesTablesAcceptentUnePhaseHorsBornes(int phase)
    {
        Assert.True(BossPhases.BurstInterval(phase) > 0f);
        Assert.True(BossPhases.ShockInterval(phase) > 0f);
        Assert.True(BossPhases.SpeedMult(phase) > 0f);
    }

    // -----------------------------------------------------------------------
    // Adds & affichage
    // -----------------------------------------------------------------------

    [Fact]
    public void SeuleLaDernierePhaseInvoqueDesAdds()
    {
        Assert.False(BossPhases.SummonsAdds(0));
        Assert.False(BossPhases.SummonsAdds(1));
        Assert.True(BossPhases.SummonsAdds(2));
    }

    [Fact]
    public void RomanNumeral_CouvreLesTroisPhases()
    {
        Assert.Equal("I", BossPhases.RomanNumeral(0));
        Assert.Equal("II", BossPhases.RomanNumeral(1));
        Assert.Equal("III", BossPhases.RomanNumeral(2));
    }

    // -----------------------------------------------------------------------
    // Cohérence de la table de seuils
    // -----------------------------------------------------------------------

    [Fact]
    public void LesSeuilsSontDecroissantsEtCouvrentToutesLesPhases()
    {
        Assert.Equal(BossPhases.Count - 1, BossPhases.Thresholds.Length);
        for (int i = 1; i < BossPhases.Thresholds.Length; i++)
            Assert.True(BossPhases.Thresholds[i] < BossPhases.Thresholds[i - 1]);
    }

    [Fact]
    public void LaBasculeCouteMoinsDeDixPourCentDuTtkDeReference()
    {
        // TTK de référence ~28 s (GDD §20.2) ; 2 bascules ne doivent pas dénaturer le combat.
        float coutTotal = BossPhases.TransitionSeconds * (BossPhases.Count - 1);
        Assert.True(coutTotal / 28f < 0.10f);
    }
}
