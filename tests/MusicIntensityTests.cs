using Xunit;

/// <summary>
/// Tests de la musique adaptative (src/Core/Rules/MusicIntensity.cs).
/// On vérifie les propriétés qui, si elles cassent, s'entendent en jeu :
/// bornes, monotonie, asymétrie montée/descente, et le fait que le `bed` ne se
/// taise jamais.
/// </summary>
public class MusicIntensityTests
{
    // -----------------------------------------------------------------------
    // Compute
    // -----------------------------------------------------------------------

    [Fact]
    public void Compute_EstNulEnDebutDeRunSansEnnemiEtPleineVie()
        => Assert.Equal(0f, MusicIntensity.Compute(0, 0f, 1f), 4);

    [Fact]
    public void Compute_SatureAUnQuandToutEstAuMaximum()
        => Assert.Equal(1f, MusicIntensity.Compute(
            MusicIntensity.EnemySaturation, MusicIntensity.TimeSaturationSec, 0f), 4);

    [Theory]
    [InlineData(0, 0f, 1f)]
    [InlineData(500, 5000f, 0f)]      // très au-delà des saturations
    [InlineData(-10, -50f, 3f)]       // entrées aberrantes
    public void Compute_ResteBorneEntreZeroEtUn(int enemies, float elapsed, float hp)
    {
        float v = MusicIntensity.Compute(enemies, elapsed, hp);
        Assert.InRange(v, 0f, 1f);
    }

    [Fact]
    public void Compute_CroitAvecLeNombreDEnnemis()
    {
        float few = MusicIntensity.Compute(3, 60f, 1f);
        float many = MusicIntensity.Compute(40, 60f, 1f);
        Assert.True(many > few, $"40 ennemis ({many}) devrait dépasser 3 ({few})");
    }

    [Fact]
    public void Compute_CroitQuandLesPvBaissent()
    {
        float healthy = MusicIntensity.Compute(10, 60f, 1f);
        float hurt = MusicIntensity.Compute(10, 60f, 0.2f);
        Assert.True(hurt > healthy);
    }

    [Fact]
    public void Compute_LesPremiersEnnemisPesentPlusQueLesDerniers()
    {
        // Courbe en racine : l'arrivée d'une poignée d'ennemis doit s'entendre,
        // passer de 40 à 50 ne doit presque rien changer.
        float d1 = MusicIntensity.Compute(10, 0f, 1f) - MusicIntensity.Compute(0, 0f, 1f);
        float d2 = MusicIntensity.Compute(50, 0f, 1f) - MusicIntensity.Compute(40, 0f, 1f);
        Assert.True(d1 > d2 * 2f, $"delta bas={d1}, delta haut={d2}");
    }

    // -----------------------------------------------------------------------
    // Smooth
    // -----------------------------------------------------------------------

    [Fact]
    public void Smooth_MonteTroisFoisPlusViteQuIlNeDescend()
    {
        float up = MusicIntensity.Smooth(0.5f, 1f, 0.1f) - 0.5f;
        float down = 0.5f - MusicIntensity.Smooth(0.5f, 0f, 0.1f);
        Assert.True(up > down * 2.5f, $"montée={up}, descente={down}");
    }

    [Fact]
    public void Smooth_NeDepasseJamaisLaCible()
    {
        Assert.Equal(1f, MusicIntensity.Smooth(0.99f, 1f, 10f), 5);
        Assert.Equal(0f, MusicIntensity.Smooth(0.01f, 0f, 10f), 5);
    }

    [Fact]
    public void Smooth_ConvergeVersLaCible()
    {
        float v = 0f;
        for (int i = 0; i < 200; i++)
            v = MusicIntensity.Smooth(v, 0.8f, 0.05f);
        Assert.Equal(0.8f, v, 3);
    }

    [Fact]
    public void Smooth_NeBougePasSansTempsEcoule()
        => Assert.Equal(0.42f, MusicIntensity.Smooth(0.42f, 1f, 0f), 5);

    // -----------------------------------------------------------------------
    // Courbes de fondu
    // -----------------------------------------------------------------------

    [Fact]
    public void SmoothStep_AtteintLesBornes()
    {
        Assert.Equal(0f, MusicIntensity.SmoothStep(0.1f, 0.2f, 0.4f), 5);
        Assert.Equal(1f, MusicIntensity.SmoothStep(0.9f, 0.2f, 0.4f), 5);
        Assert.Equal(0.5f, MusicIntensity.SmoothStep(0.3f, 0.2f, 0.4f), 5);
    }

    [Fact]
    public void SmoothStep_EstMonotone()
    {
        float prev = -1f;
        for (float x = 0f; x <= 1.001f; x += 0.02f)
        {
            float v = MusicIntensity.SmoothStep(x, 0.2f, 0.8f);
            Assert.True(v >= prev - 1e-6f);
            prev = v;
        }
    }

    // -----------------------------------------------------------------------
    // Choix de la piste
    // -----------------------------------------------------------------------

    [Fact]
    public void RunCalmeResteSurLeCouplet()
    {
        Assert.Equal(MusicLayer.Calm, MusicIntensity.Select(MusicLayer.Calm, 0f, false));
        Assert.Equal(MusicLayer.Calm, MusicIntensity.Select(MusicLayer.Calm, 0.3f, false));
    }

    [Fact]
    public void PressionMontante_BasculeSurLeRefrain()
        => Assert.Equal(MusicLayer.Combat,
                        MusicIntensity.Select(MusicLayer.Calm, MusicIntensity.CombatEnter, false));

    [Fact]
    public void Hysteresis_UneIntensiteIntermediaireNeRebasculePas()
    {
        // Entre les deux seuils, chaque piste garde la main : c'est ce qui évite
        // l'aller-retour permanent quand l'intensité oscille autour d'un seuil.
        float milieu = (MusicIntensity.CombatEnter + MusicIntensity.CombatExit) / 2f;

        Assert.Equal(MusicLayer.Calm, MusicIntensity.Select(MusicLayer.Calm, milieu, false));
        Assert.Equal(MusicLayer.Combat, MusicIntensity.Select(MusicLayer.Combat, milieu, false));
    }

    [Fact]
    public void SeuilDeSortieEstBienSousLeSeuilDEntree()
        => Assert.True(MusicIntensity.CombatExit < MusicIntensity.CombatEnter);

    [Fact]
    public void RetourAuCalme_SeulementSousLeSeuilBas()
    {
        Assert.Equal(MusicLayer.Calm,
                     MusicIntensity.Select(MusicLayer.Combat, MusicIntensity.CombatExit, false));
        Assert.Equal(MusicLayer.Combat,
                     MusicIntensity.Select(MusicLayer.Combat, MusicIntensity.CombatExit + 0.01f, false));
    }

    [Fact]
    public void BossPrendLaMainQuelleQueSoitLIntensite()
    {
        Assert.Equal(MusicLayer.Boss, MusicIntensity.Select(MusicLayer.Calm, 0f, true));
        Assert.Equal(MusicLayer.Boss, MusicIntensity.Select(MusicLayer.Combat, 1f, true));
    }

    [Fact]
    public void SortieDeBoss_RepasseParLeRefrainSiLaPressionResteHaute()
    {
        // Un boss meurt rarement dans le calme : retomber directement sur le
        // couplet alors que l'écran est encore plein serait un contresens.
        Assert.Equal(MusicLayer.Combat, MusicIntensity.Select(MusicLayer.Boss, 0.9f, false));
        Assert.Equal(MusicLayer.Calm, MusicIntensity.Select(MusicLayer.Boss, 0.1f, false));
    }

    // -----------------------------------------------------------------------
    // Fondu croisé
    // -----------------------------------------------------------------------

    [Fact]
    public void Approach_AtteintLaCibleEnUneDureeDeFondu()
    {
        float w = 0f;
        for (int i = 0; i < 30; i++)  // 30 × 0,1 s = 3 s
            w = MusicIntensity.Approach(w, 1f, 0.1f, MusicIntensity.CrossfadeSec);

        Assert.Equal(1f, w, 3);
    }

    [Fact]
    public void Approach_NeDepassePasLaCible()
    {
        Assert.Equal(1f, MusicIntensity.Approach(0.99f, 1f, 10f, MusicIntensity.CrossfadeSec), 5);
        Assert.Equal(0f, MusicIntensity.Approach(0.01f, 0f, 10f, MusicIntensity.CrossfadeSec), 5);
    }

    [Fact]
    public void PisteAuPremierPlan_EstAPleinNiveau()
        => Assert.Equal(0f, MusicIntensity.WeightToDb(1f), 5);

    [Fact]
    public void PisteRetiree_EstMuette()
        => Assert.Equal(MusicIntensity.Silence, MusicIntensity.WeightToDb(0f));

    [Fact]
    public void FonduCroise_ConserveLaPuissanceTotale()
    {
        // Deux morceaux générés séparément sont décorrélés : leurs PUISSANCES
        // s'additionnent. Si la somme ne reste pas constante, on entend un trou
        // (ou une bosse) de volume au milieu de chaque bascule.
        for (float w = 0f; w <= 1.001f; w += 0.05f)
        {
            float a = ToAmplitude(MusicIntensity.WeightToDb(w));
            float b = ToAmplitude(MusicIntensity.WeightToDb(1f - w));

            Assert.Equal(1f, a * a + b * b, 3);
        }
    }

    [Fact]
    public void FonduCroise_ProgresseSansSaut()
    {
        float prev = 0f;
        for (float w = 0f; w <= 1.001f; w += 0.01f)
        {
            float amp = ToAmplitude(MusicIntensity.WeightToDb(w));
            Assert.True(amp >= prev - 1e-5f, $"le gain redescend à w={w}");
            Assert.True(amp - prev < 0.12f, $"saut d'amplitude de {amp - prev} à w={w}");
            prev = amp;
        }
    }

    private static float ToAmplitude(float db) =>
        db <= MusicIntensity.Silence ? 0f : (float)System.Math.Pow(10.0, db / 20.0);
}
