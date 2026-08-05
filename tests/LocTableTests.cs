using System.IO;
using Xunit;

/// <summary>
/// Vérifie la table de traduction — <b>sur le vrai <c>ui.csv</c> du jeu</b>, qui contient des
/// virgules dans les phrases et des guillemets d'échappement.
/// </summary>
public class LocTableTests
{
    private static LocTable.Document Real()
        => LocTable.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "localization", "ui.csv")));

    [Fact]
    public void LaTableDuJeuSeCharge()
    {
        var doc = Real();
        Assert.True(doc.Count > 300, $"{doc.Count} libellés — la table du jeu en compte plusieurs centaines");
    }

    [Fact]
    public void LesTroisLanguesSontRendues()
    {
        var doc = Real();

        Assert.Equal("Play",  doc.Get("MENU_PLAY", "en"));
        Assert.Equal("Jouer", doc.Get("MENU_PLAY", "fr"));
        Assert.Equal("Jugar", doc.Get("MENU_PLAY", "es"));
    }

    /// <summary>
    /// Le piège du format : un <c>Split(',')</c> naïf coupe cette ligne au milieu d'une phrase, et le
    /// symptôme est une interface tronquée dans une seule langue — le défaut le plus difficile à voir.
    /// </summary>
    [Fact]
    public void UnePhraseContenantDesVirgulesResteEntiere()
    {
        var doc = Real();
        string rule = doc.Get("SAT_2_RULE", "fr");

        Assert.Contains("+45 % de PV", rule);
        Assert.Contains("vagues plus denses", rule);
    }

    [Fact]
    public void LesCransDeSaturationOntTousLeurNomEtLeurRegle()
    {
        var doc = Real();

        foreach (var rank in SaturationTable.Ranks)
        {
            Assert.True(doc.Has(rank.NameKey), $"{rank.NameKey} absent de ui.csv");
            Assert.True(doc.Has(rank.RuleKey), $"{rank.RuleKey} absent de ui.csv");
            Assert.NotEqual(rank.NameKey, doc.Get(rank.NameKey, "fr"));   // traduit, pas la clé brute
        }
    }

    /// <summary>
    /// Une clé absente rend <b>la clé elle-même</b> : un libellé manquant doit se voir à l'écran et se
    /// chercher, jamais laisser un blanc qu'on prendrait pour un défaut d'affichage.
    /// </summary>
    [Fact]
    public void UneCleAbsenteSeVoit()
    {
        Assert.Equal("CLE_INVENTEE", Real().Get("CLE_INVENTEE", "fr"));
    }

    [Theory]
    [InlineData("a,b,c", 3)]
    [InlineData("a,\"b,c\",d", 3)]
    [InlineData("a,\"il a dit \"\"oui\"\"\",b", 3)]
    public void LeDecoupageRespecteLesGuillemets(string line, int expected)
    {
        Assert.Equal(expected, LocTable.SplitCsvLine(line).Count);
    }

    [Fact]
    public void UnFichierVideNeLevePasDException()
    {
        Assert.Equal(0, LocTable.Parse(null).Count);
        Assert.Equal(0, LocTable.Parse("keys,en,fr,es").Count);   // en-tête seul
    }

    /// <summary>
    /// Le CSV est <b>dupliqué</b> dans <c>StreamingAssets</c> (comme <c>data/*.json</c>) : Unity ne
    /// lit pas la racine du dépôt. Deux copies qui divergent produiraient un jeu traduit
    /// différemment de ce que dit la source — ce test l'interdit.
    /// </summary>
    [Fact]
    public void LaCopieLueParUnityEstIdentiqueALaSource()
    {
        string source = Path.Combine(TestPaths.RepoRoot, "localization", "ui.csv");
        string copy = Path.Combine(TestPaths.RepoRoot, "unity", "Assets", "StreamingAssets",
                                   "localization", "ui.csv");

        Assert.True(File.Exists(copy), "la copie StreamingAssets manque — Unity n'aurait aucune traduction");
        Assert.Equal(File.ReadAllText(source), File.ReadAllText(copy));
    }

    /// <summary>
    /// ⚠ Les « \n » du CSV doivent devenir de vrais sauts de ligne. L'importeur de traductions de
    /// Godot le fait ; le portage lit le CSV brut et ne le faisait pas — les deux caractères
    /// s'affichaient <b>littéralement</b> au milieu des six lignes de la cinématique d'ouverture,
    /// c'est-à-dire du seul texte narratif du jeu.
    /// </summary>
    [Fact]
    public void LesSautsDeLigneEchappesDeviennentDeVraisSautsDeLigne()
    {
        var doc = LocTable.Parse("keys,en,fr,es\nBEAT,\"First line.\\nSecond line.\",fr,es");

        Assert.Equal("First line.\nSecond line.", doc.Get("BEAT", "en"));
        Assert.DoesNotContain("\\n", doc.Get("BEAT", "en"));
    }

    /// <summary>Le texte du jeu lui-même : aucune traduction ne garde une séquence échappée.</summary>
    [Fact]
    public void AucuneTraductionDuJeuNeGardeUnAntislashN()
    {
        var doc = LocTable.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "localization", "ui.csv")));

        foreach (string key in doc.Keys)
            foreach (string language in LocTable.Languages)
                Assert.DoesNotContain("\\n", doc.Get(key, language));
    }
}
