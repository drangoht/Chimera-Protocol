using Xunit;

/// <summary>
/// Tests de la mesure de pression ressentie (src/Core/Rules/PressureMeter.cs).
///
/// Ce qui est vérifié ici est ce qui rendrait l'instrument inutilisable pour l'usage auquel il est
/// destiné — arbitrer un cran de saturation. En premier lieu qu'il voie ce que le temps soutenable ne
/// voit pas (un pic sans perte nette), et qu'il ne dépende pas de son propre réglage : un frôlement
/// doit compter pour un, quelle que soit la fréquence des frames ou la découpe des fenêtres.
/// Cf. docs/GDD.md §34.5, docs/TEST_REPORT.md (2026-08-01).
/// </summary>
public class PressureMeterTests
{
    private const float Frame = 1f / 60f;

    /// <summary>Maintient un ratio pendant une durée donnée.</summary>
    private static void Hold(PressureMeter m, float ratio, float seconds)
    {
        for (float t = 0f; t < seconds; t += Frame) m.Observe(ratio * 1000f, 1000f, Frame);
    }

    // ─── Ce que la métrique doit voir ────────────────────────────────────────

    [Fact]
    public void Un_Pic_Sans_Perte_Nette_Est_Compte()
    {
        // LE cas qui justifie la classe : le joueur plonge à 10 % puis remonte à plein. Débits
        // moyennés identiques à ceux d'une run tranquille, temps soutenable inchangé — et pourtant
        // c'est exactement ce qu'un joueur appelle « c'était chaud ».
        var m = new PressureMeter();
        Hold(m, 1.00f, 5f);
        Hold(m, 0.10f, 1f);
        Hold(m, 1.00f, 5f);

        Assert.Equal(1, m.CloseCalls);
        Assert.Equal(0.10f, m.LowestRatio, 2);
    }

    [Fact]
    public void Une_Run_Sans_Danger_Ne_Compte_Rien()
    {
        // L'état mesuré au cran 0 : le joueur passe l'overtime au-dessus de 90 % de ses PV max.
        var m = new PressureMeter();
        Hold(m, 0.95f, 60f);

        Assert.Equal(0, m.CloseCalls);
        Assert.Equal(0f, m.DangerSeconds, 3);
        Assert.Equal(0f, m.DangerFraction, 3);
    }

    [Fact]
    public void Deux_Episodes_Separes_Comptent_Deux()
    {
        var m = new PressureMeter();
        Hold(m, 1.00f, 2f);
        Hold(m, 0.20f, 1f);
        Hold(m, 0.90f, 2f);   // remonté au-dessus de SafeRatio → le compteur est réarmé
        Hold(m, 0.15f, 1f);

        Assert.Equal(2, m.CloseCalls);
    }

    // ─── Ce qui rendrait la mesure fausse ────────────────────────────────────

    [Fact]
    public void Une_Oscillation_Autour_Du_Seuil_Ne_Compte_Quun_Episode()
    {
        // Sans hystérésis, ce scénario compterait un frôlement par oscillation et la métrique
        // mesurerait l'agitation de la barre, pas le danger. 0,35 est au-dessus du seuil de danger
        // mais SOUS le seuil de sortie : l'épisode n'est pas clos.
        var m = new PressureMeter();
        for (int i = 0; i < 20; i++)
        {
            Hold(m, 0.25f, 0.2f);
            Hold(m, 0.35f, 0.2f);
        }

        Assert.Equal(1, m.CloseCalls);
    }

    [Fact]
    public void Le_Compte_Ne_Depend_Pas_De_La_Frequence_De_Frame()
    {
        // Un frôlement est un épisode, pas un échantillon : doubler le pas de temps ne doit rien
        // changer au compte — sinon --timescale rendrait deux campagnes incomparables.
        var rapide = new PressureMeter();
        var lent   = new PressureMeter();
        for (float t = 0f; t < 3f; t += 1f / 120f) rapide.Observe(200f, 1000f, 1f / 120f);
        for (float t = 0f; t < 3f; t += 1f / 20f)  lent.Observe(200f, 1000f, 1f / 20f);

        Assert.Equal(rapide.CloseCalls, lent.CloseCalls);
        Assert.Equal(rapide.DangerSeconds, lent.DangerSeconds, 1);
    }

    [Fact]
    public void Un_Creux_A_Cheval_Sur_Deux_Fenetres_Ne_Compte_Quune_Fois()
    {
        // ResetWindow conserve l'état d'hystérésis. Le remettre à zéro ferait dépendre le total du
        // nombre de fenêtres, c'est-à-dire du réglage de l'instrument et non du jeu mesuré.
        var m = new PressureMeter();
        Hold(m, 0.20f, 1f);
        int avant = m.CloseCalls;
        m.ResetWindow();
        Hold(m, 0.20f, 1f);   // toujours le MÊME creux, jamais remonté

        Assert.Equal(1, avant);
        Assert.Equal(0, m.CloseCalls);
    }

    [Fact]
    public void Une_Nouvelle_Fenetre_Nherite_Pas_Du_Creux_De_La_Precedente()
    {
        var m = new PressureMeter();
        Hold(m, 0.10f, 1f);
        Hold(m, 1.00f, 1f);
        m.ResetWindow();
        Hold(m, 0.80f, 1f);

        Assert.Equal(0.80f, m.LowestRatio, 2);
    }

    [Fact]
    public void Sans_Joueur_Aucun_Echantillon()
    {
        // Entre deux scènes, MaxHp vaut 0 : des PV nuls ne sont pas un frôlement, et un
        // faux frôlement par changement d'écran polluerait toutes les campagnes.
        var m = new PressureMeter();
        for (int i = 0; i < 100; i++) m.Observe(0f, 0f, Frame);

        Assert.Equal(0, m.CloseCalls);
        Assert.Equal(1f, m.LowestRatio, 3);
    }

    [Fact]
    public void La_Mort_Est_Un_Frolement()
    {
        var m = new PressureMeter();
        Hold(m, 1.00f, 2f);
        m.Observe(0f, 1000f, Frame);

        Assert.Equal(1, m.CloseCalls);
        Assert.Equal(0f, m.LowestRatio, 3);
    }

    [Fact]
    public void La_Part_De_Danger_Est_Rapportee_A_La_Fenetre()
    {
        var m = new PressureMeter();
        Hold(m, 1.00f, 6f);
        Hold(m, 0.20f, 2f);

        Assert.Equal(0.25f, m.DangerFraction, 2);
    }

    [Fact]
    public void Reset_Complet_Reduit_Tout_Y_Compris_Lhysteresis()
    {
        var m = new PressureMeter();
        Hold(m, 0.20f, 1f);
        m.Reset();
        Hold(m, 0.20f, 1f);   // nouvelle run : le même creux redevient un frôlement

        Assert.Equal(1, m.CloseCalls);
    }
}
