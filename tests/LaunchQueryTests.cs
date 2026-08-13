using Xunit;

/// <summary>
/// Traduction de la chaîne de requête d'une URL en arguments de ligne de commande.
///
/// <para>C'est ce qui rend les drapeaux de mise au point utilisables dans un navigateur, où il n'y a
/// pas de ligne de commande : <c>?biome=neon&amp;invuln</c> doit produire exactement ce que
/// <c>--biome=neon --invuln</c> produit sur Windows. Une divergence ici ne casserait rien
/// visiblement — elle rendrait simplement un drapeau inopérant sur une seule plateforme, ce qui est
/// le mode d'échec favori de ce projet.</para>
/// </summary>
public class LaunchQueryTests
{
    private const string Program = "Chimera Protocol";

    [Fact]
    public void SansRequete_RendSeulementLeNomDuProgramme()
    {
        // La ligne de commande rend toujours argv[0]. Un code qui saute le premier élément — il en
        // existe — se comporterait autrement sur le web si on ne l'imitait pas.
        Assert.Equal(new[] { Program }, LaunchQuery.ToArgs("https://exemple.org/jeu/index.html", Program));
    }

    [Fact]
    public void UrlNulle_NeLevePas()
    {
        Assert.Equal(new[] { Program }, LaunchQuery.ToArgs(null, Program));
    }

    [Fact]
    public void PaireCleValeur_DevientUnArgumentPrefixe()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?biome=neon", Program);

        Assert.Equal(new[] { Program, "--biome=neon" }, args);
    }

    [Fact]
    public void CleSeule_DevientUnDrapeau()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?invuln", Program);

        Assert.Equal(new[] { Program, "--invuln" }, args);
    }

    [Fact]
    public void PlusieursParametres_SontTousRendus()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?biome=neon&seed=42&invuln", Program);

        Assert.Equal(new[] { Program, "--biome=neon", "--seed=42", "--invuln" }, args);
    }

    /// <summary>
    /// Le fragment n'appartient pas à la requête.
    /// </summary>
    /// <remarks>
    /// Sans cette coupe, <c>?lang=en#accueil</c> donnerait la langue « en#accueil » — une valeur qui
    /// ne correspond à rien, donc un repli silencieux sur la langue par défaut. C'est exactement la
    /// forme de défaut qui a fait sortir un menu français d'une capture lancée en anglais.
    /// </remarks>
    [Fact]
    public void Fragment_EstIgnore()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?lang=en#accueil", Program);

        Assert.Equal(new[] { Program, "--lang=en" }, args);
    }

    [Fact]
    public void ValeurEncodee_EstDecodee()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?screenshots=mes%20captures", Program);

        Assert.Equal(new[] { Program, "--screenshots=mes captures" }, args);
    }

    [Fact]
    public void ParametreVide_EstIgnore()
    {
        // « ?&&biome=neon » vient d'une URL construite à la main ou tronquée : les séparateurs vides
        // ne doivent pas produire des arguments « -- » que les appelants prendraient pour des clés.
        var args = LaunchQuery.ToArgs("https://exemple.org/?&&biome=neon&", Program);

        Assert.Equal(new[] { Program, "--biome=neon" }, args);
    }

    [Fact]
    public void CleVide_EstIgnoree()
    {
        var args = LaunchQuery.ToArgs("https://exemple.org/?=orpheline&biome=givre", Program);

        Assert.Equal(new[] { Program, "--biome=givre" }, args);
    }

    [Fact]
    public void ValeurVide_RendUnArgumentAValeurVide()
    {
        // `--biome=` est ce que rendrait la ligne de commande : c'est à l'appelant de juger qu'un
        // biome vide est inconnu, et RunConfig le journalise déjà. On ne décide pas à sa place ici.
        var args = LaunchQuery.ToArgs("https://exemple.org/?biome=", Program);

        Assert.Equal(new[] { Program, "--biome=" }, args);
    }

    [Fact]
    public void RequeteVide_NeProduitAucunArgument()
    {
        Assert.Equal(new[] { Program }, LaunchQuery.ToArgs("https://exemple.org/?", Program));
    }
}
