using Xunit;

/// <summary>
/// Tests des incarnations du boss de fin (src/Core/Rules/BossIncarnations.cs).
/// L'enjeu : la table est la source de vérité de ce que le joueur affronte à la fin de chaque
/// niveau. Un biome oublié ou une signature dupliquée passerait la compilation et se traduirait
/// en jeu par « le même boss qu'avant », c'est-à-dire le bug que §29 corrige.
/// </summary>
public class BossIncarnationsTests
{
    [Fact]
    public void ChaqueBiomeJouableAUneIncarnation()
    {
        foreach (string biome in LevelThreat.Order)
        {
            var inc = BossIncarnations.For(biome);
            Assert.Equal(biome, inc.BiomeId);
        }
    }

    [Fact]
    public void LaTableSuitLOrdreDeDeblocageDesNiveaux()
    {
        Assert.Equal(LevelThreat.Order.Length, BossIncarnations.All.Length);
        for (int i = 0; i < LevelThreat.Order.Length; i++)
            Assert.Equal(LevelThreat.Order[i], BossIncarnations.All[i].BiomeId);
    }

    [Fact]
    public void LesSignaturesSontToutesDistinctes()
    {
        var vues = new System.Collections.Generic.HashSet<BossSignature>();
        foreach (var inc in BossIncarnations.All)
            Assert.True(vues.Add(inc.Signature), $"signature dupliquée : {inc.Signature}");
    }

    [Fact]
    public void LesIdentifiantsEtClesDeLocSontUniquesEtNonVides()
    {
        var ids  = new System.Collections.Generic.HashSet<string>();
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (var inc in BossIncarnations.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(inc.Id));
            Assert.False(string.IsNullOrWhiteSpace(inc.NameKey));
            Assert.True(ids.Add(inc.Id));
            Assert.True(keys.Add(inc.NameKey));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("biome_inexistant")]
    public void UnBiomeInconnuRetombeSurLaSouche(string? biome)
        => Assert.Equal(BossIncarnations.Root.Id, BossIncarnations.For(biome).Id);

    [Fact]
    public void LaSoucheEstCelleDuPremierNiveau()
    {
        Assert.Equal(LevelThreat.Order[0], BossIncarnations.Root.BiomeId);
        Assert.Equal(BossSignature.DirectedFan, BossIncarnations.Root.Signature);
        Assert.Equal("", BossIncarnations.Root.FramesPath);   // sprite d'origine, pas de variante
    }

    [Fact]
    public void LaSoucheNeTeintePasSonSprite()
    {
        var root = BossIncarnations.Root;
        Assert.Equal(1f, root.TintR, 3);
        Assert.Equal(1f, root.TintG, 3);
        Assert.Equal(1f, root.TintB, 3);
    }

    [Fact]
    public void LesPeriodesDeSignatureSontExploitablesEnJeu()
    {
        // Trop court = illisible dans le chaos ; trop long = signature invisible sur ~30 s de TTK.
        foreach (var inc in BossIncarnations.All)
            Assert.InRange(inc.BaseIntervalSec, 3f, 10f);
    }

    [Fact]
    public void EnPhaseTroisChaqueSignatureSeDeclencheAuMoinsDeuxFoisSurUnTtkTypique()
    {
        // ~10 s passées en phase III sur un TTK de référence de ~30 s (GDD §20.2 / §29.4).
        foreach (var inc in BossIncarnations.All)
        {
            float periode = BossPhases.SignatureInterval(BossPhases.Count - 1, inc.BaseIntervalSec);
            Assert.True(periode <= 5f, $"{inc.Id} : signature trop rare en phase III ({periode:0.0} s)");
        }
    }

    [Fact]
    public void ById_TrouveChaqueIncarnationEtRetombeSurLaSouche()
    {
        foreach (var inc in BossIncarnations.All)
            Assert.Equal(inc.BiomeId, BossIncarnations.ById(inc.Id).BiomeId);
        Assert.Equal(BossIncarnations.Root.Id, BossIncarnations.ById("inconnu").Id);
    }

    [Fact]
    public void LesVariantesDeclarentUnJeuDeSpritesDedie()
    {
        foreach (var inc in BossIncarnations.All)
        {
            if (inc.Id == BossIncarnations.Root.Id) continue;
            Assert.StartsWith("res://assets/sprites/enemies/rusted_core/", inc.FramesPath);
            Assert.EndsWith("_frames.tres", inc.FramesPath);
        }
    }
}
