using Xunit;

/// <summary>
/// <b>Verrou de non-régression du déséquilibre corrigé en 1.21.0</b> : les 9 fusions divisaient le
/// DPS de fin de run par 3 à 6, parce qu'elles repartaient au niveau 1 et que leurs dégâts
/// n'étaient jamais multipliés.
///
/// <para>Le plan de migration en fait un <b>critère de sortie explicite</b> du Lot 3 : « les fusions
/// héritent bien du niveau — le bug de la 1.21.0 ne doit pas réapparaître ». Un portage est
/// exactement le moment où ce genre de bug se réintroduit sans que personne ne le voie, parce que
/// la valeur fautive (1) est aussi une valeur parfaitement plausible.</para>
/// </summary>
public class WeaponFusionTests
{
    // ─── Le cœur du verrou ────────────────────────────────────────────────────

    /// <summary>
    /// Le bug historique, énoncé tel quel : une arme montée au niveau 12 qui fusionne ne doit
    /// <b>pas</b> retomber à 1.
    /// </summary>
    [Theory]
    [InlineData(5, 5)]
    [InlineData(12, 12)]
    [InlineData(20, 20)]
    [InlineData(37, 37)]
    public void UneFusionHeriteDuNiveauDeLArmeQuElleRemplace(int weaponLevel, int expected)
    {
        Assert.Equal(expected, WeaponFusion.InheritedLevel(weaponLevel));
    }

    [Fact]
    public void UneFusionNeRepartJamaisAuNiveauUnQuandLArmeEtaitMontee()
    {
        // Formulation directe du bug : si ceci vaut 1, la 1.21.0 est réintroduite.
        Assert.NotEqual(1, WeaponFusion.InheritedLevel(12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void UnNiveauInconnuRetombeSurUnEtNonSurZero(int unknown)
    {
        // Une arme de niveau 0 n'existe pas : le repli doit être 1, sinon la fusion serait inerte.
        Assert.Equal(1, WeaponFusion.InheritedLevel(unknown));
    }

    /// <summary>
    /// Second volet du bug : les dégâts d'une fusion doivent suivre le niveau <b>et</b> le
    /// multiplicateur du joueur. Une fusion figée à sa valeur de fiche est précisément ce qui
    /// divisait le DPS.
    /// </summary>
    [Fact]
    public void LesDegatsDUneFusionSuiventLeNiveau()
    {
        float atLevel1  = WeaponFusion.EffectiveDamage(22f, 1, 1f);
        float atLevel12 = WeaponFusion.EffectiveDamage(22f, 12, 1f);

        Assert.Equal(22f, atLevel1, 4);
        Assert.True(atLevel12 > atLevel1 * 2f,
            $"une fusion niveau 12 doit largement dépasser son niveau 1 (obtenu {atLevel12})");
    }

    [Fact]
    public void LesDegatsDUneFusionSuiventLeMultiplicateurDuJoueur()
    {
        float plain   = WeaponFusion.EffectiveDamage(22f, 5, 1f);
        float boosted = WeaponFusion.EffectiveDamage(22f, 5, 2f);

        Assert.Equal(plain * 2f, boosted, 4);
    }

    /// <summary>
    /// Reproduction chiffrée du scénario de 1.21.0 : à niveau égal, une fusion ne doit plus valoir
    /// une fraction de l'arme qu'elle remplace. On vérifie l'ordre de grandeur, pas une valeur
    /// exacte — c'est le <i>rapport</i> qui était cassé.
    /// </summary>
    [Fact]
    public void UneFusionNEstPlusTroisAOuSixFoisPlusFaibleQueLArmeRemplacee()
    {
        const float fusionSheetDamage = 22f;
        const int   level = 12;
        const float damageMultiplier = 1.6f;   // Noyau Thermique + Hub

        float buggy    = fusionSheetDamage;                                             // avant 1.21.0
        float corrected = WeaponFusion.EffectiveDamage(fusionSheetDamage, level, damageMultiplier);

        Assert.True(corrected > buggy * 3f,
            $"le correctif doit valoir plus du triple de la valeur figée (obtenu {corrected} contre {buggy})");
    }

    [Fact]
    public void AuNiveauUnLaFusionVautExactementSaFiche()
    {
        Assert.Equal(40f, WeaponFusion.EffectiveDamage(40f, 1, 1f), 4);
    }

    // ─── Déblocage ────────────────────────────────────────────────────────────

    [Fact]
    public void UneFusionExigeLeNiveauDArmeEtLePassif()
    {
        Assert.True(WeaponFusion.CanFuse(weaponLevel: 5, requiredWeaponLevel: 5, hasRequiredPassive: true));
        Assert.True(WeaponFusion.CanFuse(9, 5, true));
    }

    [Theory]
    [InlineData(4, 5, true)]    // niveau insuffisant
    [InlineData(5, 5, false)]   // passif manquant
    [InlineData(1, 5, false)]   // ni l'un ni l'autre
    public void UneFusionResteVerrouilleeSiUneConditionManque(
        int weaponLevel, int required, bool hasPassive)
    {
        Assert.False(WeaponFusion.CanFuse(weaponLevel, required, hasPassive));
    }

    /// <summary>
    /// Le palier de statistiques d'une fusion vaut 1 : sa mécanique propre n'est pas descriptible
    /// dans le tableau de niveaux du JSON. Si cette constante changeait, toute la progression des
    /// fusions se décalerait silencieusement.
    /// </summary>
    [Fact]
    public void UneFusionNAQuUnSeulPalierDefini()
    {
        Assert.Equal(1, WeaponFusion.DefinedMax);
    }
}
