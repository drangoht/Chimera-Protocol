using Xunit;

/// <summary>
/// Tests des vagues de la Marée de Rouille (unity/Assets/Scripts/Shared/Rules/TideWaves.cs).
///
/// ⚠ <b>Ce fichier a perdu la moitié de sa couverture le 2026-08-22, et il faut le savoir.</b> Le
/// placement, l'opacité et le sens de déplacement des vagues se calculaient ici, en C# ; ils se
/// calculent désormais par pixel dans <c>Resources/Shaders/RustTide.shader</c>, pour que les vagues
/// épousent le front rongé au lieu d'être des bandes rectangulaires. Aucun test unitaire ne peut
/// suivre du HLSL. Les garanties qui étaient vérifiées ici — une vague ne franchit pas le liseré,
/// elle naît et meurt transparente, elle va vers l'intérieur — <b>tiennent maintenant à la relecture
/// du shader et à l'œil</b>. Le sens de déplacement, en particulier, est <i>invisible à la capture
/// d'écran</i> : deux images espacées de quelques secondes sont compatibles avec les deux sens.
/// C'est le prix payé pour un bord qui ne soit pas droit ; il est noté dans
/// <c>docs/PITFALLS_UNITY.md</c> §Fin de partie.
///
/// Ce qui reste ici est ce qui ne pouvait <b>pas</b> descendre dans le shader : la phase, qui doit
/// être accumulée d'une image à l'autre, là où un shader n'a pas d'état. Cf. docs/GDD.md §38.
/// </summary>
public class TideWavesTests
{
    // Valeurs réelles du rendu (RustTideZone) : les tests doivent porter sur ce que le jeu dessine.
    private const float Speed = 110f;
    private const float Spacing = 210f;

    // ─── La phase : accumulée, donc continue ─────────────────────────────────

    [Fact]
    public void LaPhaseAccumuleeResteContinueQuandLaNappeSEpaissit()
    {
        // Dix minutes d'avancée, image par image, pendant que la nappe passe de 400 à 1360 unités :
        // c'est exactement la situation où une phase recalculée depuis l'horloge saute. Le rendu au
        // shader espace désormais les vagues d'une distance constante, mais la forme accumulée doit
        // rester robuste à un dénominateur qui bouge — c'est elle, et non l'espacement fixe, qui est
        // la garantie.
        float phase = 0f;
        const float dt = 1f / 60f;
        float precedente = 0f;

        for (int frame = 0; frame < 60 * 600; frame++)
        {
            float depth = 400f + 960f * (frame / (60f * 600f));
            phase = TideWaves.AdvancePhase(phase, dt, Speed, depth);

            // Un pas d'image ne peut franchir qu'une fraction minuscule du cycle — sauf au
            // rebouclage, qui est le seul saut légitime.
            float delta = phase - precedente;
            if (delta < 0f) delta += 1f;
            Assert.True(delta < 0.02f, $"saut de phase de {delta:0.###} a l'image {frame}");

            precedente = phase;
        }
    }

    [Fact]
    public void UnePhaseRecalculeeDepuisLHorlogeSauterait()
    {
        // Le défaut que la forme accumulée évite — vérifié plutôt que décrit, pour que personne ne
        // « simplifie » AdvancePhase en une fonction de l'horloge un jour de refactoring.
        const float t = 600f;
        float avant  = TideWaves.Frac(t * Speed / 1000f);
        float apres  = TideWaves.Frac(t * Speed / 1000.01f);

        float delta = System.Math.Abs(avant - apres);
        Assert.True(delta > 0.05f,
                    "un centieme d'unite de profondeur doit suffire a faire sauter la forme naive : "
                    + "c'est la raison d'etre de la phase accumulee");
    }

    [Fact]
    public void LaPhaseResteToujoursDansLIntervalleUnitaire()
    {
        float phase = 0f;
        for (int i = 0; i < 10_000; i++)
        {
            phase = TideWaves.AdvancePhase(phase, 1f / 60f, Speed, Spacing);
            Assert.InRange(phase, 0f, 0.9999999f);
        }
    }

    [Fact]
    public void UnEspacementNulNeFaitPasDiverguerLaPhase()
    {
        // Le shader reçoit la phase telle quelle : une division par zéro y produirait un NaN, donc
        // une nappe entièrement transparente — la marée deviendrait invisible sans qu'aucune erreur
        // ne soit levée.
        float phase = TideWaves.AdvancePhase(0.4f, 1f / 60f, Speed, 0f);
        Assert.InRange(phase, 0f, 0.9999999f);
    }

    [Fact]
    public void FracRameneUneEntreeNegativeDansLIntervalleUnitaire()
    {
        Assert.InRange(TideWaves.Frac(-0.25f), 0f, 1f);
        Assert.Equal(0.75f, TideWaves.Frac(-0.25f), 4);
    }

    // ─── Le lien avec la vitesse réelle du bord ──────────────────────────────

    [Fact]
    public void LesVaguesVontBeaucoupPlusViteQueLeBordNeRecule()
    {
        // La raison d'être de tout le fichier. Si ce rapport tombait à quelques unités, on
        // retrouverait un mouvement sous-perceptible — et l'à-coup avec lui.
        const float reculDuBordParSeconde = 960f / ((RustTide.CloseMinutes - RustTide.GraceMinutes) * 60f);

        Assert.True(Speed / reculDuBordParSeconde > 50f,
                    $"les vagues ne vont que {Speed / reculDuBordParSeconde:0} fois plus vite que le bord");
    }
}
