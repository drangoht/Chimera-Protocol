using Xunit;

/// <summary>
/// Vérifie que le générateur du port Unity reproduit la RNG de Godot 4.7 <b>bit pour bit</b>.
///
/// <para>Les valeurs de référence ne sont pas recopiées d'une documentation : elles ont été
/// <b>extraites du moteur lui-même</b> par <c>tools/unity/dump_godot_rng.gd</c>
/// (Godot 4.7-stable, 2026-08-03). C'est ce qui fait la différence entre « on utilise PCG32 » et
/// « on utilise LE PCG32 de Godot ».</para>
///
/// <para>Enjeu : ce test est la condition d'existence de la comparaison inter-moteurs sur graine
/// appariée (docs/UNITY_MIGRATION_PLAN.md §4.3 et §8.2).</para>
/// </summary>
public class Pcg32Tests
{
    // ─── Valeurs de référence extraites de Godot 4.7-stable ───────────────────

    public static TheoryData<ulong, uint[]> RandiReference => new()
    {
        { 1UL,          new uint[] { 1811587497, 683407368, 2033395789, 2375931748, 2873319489, 2189615729, 3391941925, 1039475129 } },
        { 42UL,         new uint[] { 492690617, 1919685028, 3561993920, 683038915, 1183706632, 413921556, 222559498, 436142503 } },
        { 12345UL,      new uint[] { 1321476956, 17539747, 3348728241, 2863338820, 85463406, 1024873269, 4179236141, 1040420088 } },
        { 2026UL,       new uint[] { 4259094339, 3407118278, 3458149024, 798131971, 3103822214, 2931364964, 1677351924, 2166360689 } },
        { 4294967295UL, new uint[] { 1866142959, 445709547, 1895696473, 2770767185, 1830928941, 3388100838, 1814272995, 3563466527 } },
    };

    public static TheoryData<ulong, double[]> RandfReference => new()
    {
        { 1UL,     new[] { 0.421793073, 0.159118176, 0.473436862, 0.553189695, 0.668996811, 0.509809613, 0.789748013, 0.242021665 } },
        { 42UL,    new[] { 0.114713475, 0.446961492, 0.829341352, 0.159032390, 0.275603175, 0.096373625, 0.051818673, 0.101547338 } },
        { 12345UL, new[] { 0.307680339, 0.004083791, 0.779686570, 0.666673005, 0.019898500, 0.238621905, 0.973054230, 0.242241681 } },
    };

    public static TheoryData<ulong, int[]> RandiRangeReference => new()
    {
        { 1UL,     new[] { 97, 68, 89, 48, 89, 29, 25, 29 } },
        { 42UL,    new[] { 17, 28, 20, 15, 32, 56, 98, 3 } },
        { 12345UL, new[] { 56, 47, 41, 20, 6, 69, 41, 88 } },
        { 2026UL,  new[] { 39, 78, 24, 71, 14, 64, 24, 89 } },
    };

    // ─── Fidélité au moteur ───────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(RandiReference))]
    public void NextUInt_ReproduitExactementGodot(ulong seed, uint[] expected)
    {
        var rng = new Pcg32(seed);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], rng.NextUInt());
    }

    [Theory]
    [MemberData(nameof(RandfReference))]
    public void NextFloat_ReproduitExactementGodot(ulong seed, double[] expected)
    {
        var rng = new Pcg32(seed);
        // Tolérance = précision d'impression du dump (9 décimales), pas une marge de confort :
        // le moteur a été relevé en %.9f, on ne peut pas exiger mieux que ce qu'il a imprimé.
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], rng.NextFloat(), 9);
    }

    [Theory]
    [MemberData(nameof(RandiRangeReference))]
    public void RangeInt_ReproduitExactementGodot(ulong seed, int[] expected)
    {
        var rng = new Pcg32(seed);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], rng.RangeInt(0, 99));
    }

    /// <summary>
    /// Godot calcule <c>randf()</c> en <b>simple précision</b>. Mener le même calcul en
    /// <c>double</c> — ce qui paraît strictement meilleur — fait diverger le port d'environ 1e-8
    /// dès le premier tirage. Ce test verrouille le piège, qui serait autrement « corrigé » un jour
    /// par quelqu'un cherchant à gagner en précision.
    /// </summary>
    [Fact]
    public void NextFloat_EstEnSimplePrecision_PasEnDouble()
    {
        var rng = new Pcg32(1UL);
        float v = rng.NextFloat();

        Assert.Equal(0.421793073, v, 9);                       // valeur du moteur
        Assert.NotEqual(1811587497 / 4294967295.0, v, 9);      // le même calcul en double diverge
    }

    // ─── Contrat propre au générateur ─────────────────────────────────────────

    [Fact]
    public void Seed_EstReproductible()
    {
        var a = new Pcg32(2026UL);
        var b = new Pcg32(2026UL);
        for (int i = 0; i < 64; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void Seed_ReamorceUnGenerateurDejaUtilise()
    {
        var rng = new Pcg32(7UL);
        uint first = rng.NextUInt();
        for (int i = 0; i < 10; i++) rng.NextUInt();

        rng.Seed(7UL);
        Assert.Equal(first, rng.NextUInt());
    }

    [Fact]
    public void GrainesDifferentes_DonnentDesSuitesDifferentes()
    {
        var a = new Pcg32(1UL);
        var b = new Pcg32(2UL);
        Assert.NotEqual(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void RangeInt_ResteDansLesBornesIncluses()
    {
        var rng = new Pcg32(99UL);
        for (int i = 0; i < 5000; i++)
        {
            int v = rng.RangeInt(-3, 5);
            Assert.InRange(v, -3, 5);
        }
    }

    [Fact]
    public void RangeInt_AccepteDesBornesInversees()
    {
        var rng = new Pcg32(5UL);
        for (int i = 0; i < 200; i++) Assert.InRange(rng.RangeInt(10, 2), 2, 10);
    }

    [Fact]
    public void RangeInt_BorneUnique_RenvoieToujoursLaMemeValeur()
    {
        var rng = new Pcg32(3UL);
        for (int i = 0; i < 50; i++) Assert.Equal(4, rng.RangeInt(4, 4));
    }

    [Fact]
    public void RangeDouble_ResteDansLesBornes()
    {
        var rng = new Pcg32(11UL);
        for (int i = 0; i < 5000; i++) Assert.InRange(rng.RangeDouble(-5.0, 12.5), -5.0, 12.5);
    }

    /// <summary>
    /// Verrouille une <b>divergence connue et assumée</b> plutôt que de la laisser se découvrir en
    /// production : <c>RangeDouble</c> ne reproduit PAS <c>randf_range</c> de Godot (formulation non
    /// identifiée, cf. la remarque de la méthode). Ce test échouera le jour où quelqu'un la rendra
    /// exacte — et ce sera une bonne nouvelle à constater, pas une régression.
    /// </summary>
    [Fact]
    public void RangeDouble_NeReproduitPasGodot_LimiteConnue()
    {
        var rng = new Pcg32(1UL);
        double godotFirstValue = 0.767284053;   // relevé sur le moteur
        Assert.NotEqual(godotFirstValue, rng.RangeDouble(-5.0, 12.5), 6);
    }
}
