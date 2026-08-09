using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Comparaison de versions — la règle qui décide si le bandeau de mise à jour s'affiche.
///
/// <para>Elle était portée, testée par personne, et appelée par personne : le contrôle de version
/// n'existait pas côté Unity. Ces tests verrouillent au moins la règle, pour que le bandeau rétabli
/// ne se déclenche ni à tort ni jamais.</para>
/// </summary>
public class VersionCompareTests
{
    [Theory]
    [InlineData("1.27.0", "1.26.0")]   // correctif publié
    [InlineData("1.26.1", "1.26.0")]
    [InlineData("2.0.0", "1.99.99")]
    [InlineData("1.26.0", "1.9.0")]    // ⚠ comparaison NUMÉRIQUE : « 26 » passe après « 9 »
    public void UneVersionPlusRecente_DeclencheLeBandeau(string remote, string local)
        => Assert.True(VersionCompare.IsNewer(remote, local));

    [Theory]
    [InlineData("1.26.0", "1.26.0")]   // à jour
    [InlineData("1.25.1", "1.26.0")]   // le joueur est en avance (build local)
    [InlineData("1.9.0", "1.26.0")]
    public void UneVersionEgaleOuAncienne_NeDeclenchePas(string remote, string local)
        => Assert.False(VersionCompare.IsNewer(remote, local));

    /// <summary>
    /// Un manifeste illisible ne doit jamais déclencher quoi que ce soit : ce fichier vit hors du
    /// binaire et peut être réécrit à tout moment.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas du json")]
    public void UnManifesteIllisible_NeDeclenchePas(string remote)
        => Assert.False(VersionCompare.IsNewer(remote, "1.26.0"));
}
