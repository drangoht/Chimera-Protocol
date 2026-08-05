using Xunit;

/// <summary>
/// Renouveler / Passer : les deux améliorations du Hub qui n'existaient pas dans le portage Unity.
/// Achetées, elles ne faisaient rien — le pire mode de défaillance d'une monnaie de méta-progression,
/// puisque rien ne le signale.
/// </summary>
public class LevelUpChargesTests
{
    [Fact]
    public void Sans_achat_aucune_charge_et_aucun_bouton()
    {
        var charges = new LevelUpCharges(rerollLevel: 0, skipLevel: 0);

        Assert.False(charges.RerollUnlocked);
        Assert.False(charges.SkipUnlocked);
        Assert.False(charges.TryReroll());
        Assert.False(charges.TrySkip());
    }

    [Fact]
    public void Le_niveau_d_amelioration_donne_le_nombre_de_charges()
    {
        var charges = new LevelUpCharges(rerollLevel: 3, skipLevel: 2);

        Assert.Equal(3, charges.RerollsLeft);
        Assert.Equal(2, charges.SkipsLeft);
    }

    [Fact]
    public void Une_charge_depensee_ne_revient_pas()
    {
        var charges = new LevelUpCharges(rerollLevel: 2, skipLevel: 1);

        Assert.True(charges.TryReroll());
        Assert.Equal(1, charges.RerollsLeft);

        Assert.True(charges.TryReroll());
        Assert.Equal(0, charges.RerollsLeft);

        Assert.False(charges.TryReroll());
        Assert.Equal(0, charges.RerollsLeft);
    }

    [Fact]
    public void Les_deux_compteurs_sont_independants()
    {
        var charges = new LevelUpCharges(rerollLevel: 1, skipLevel: 1);

        Assert.True(charges.TryReroll());
        Assert.Equal(1, charges.SkipsLeft);
        Assert.True(charges.TrySkip());
    }

    /// <summary>
    /// Le bouton reste visible une fois la dernière charge dépensée — il devient grisé. Le faire
    /// disparaître donnerait à croire que l'achat du Hub a été perdu.
    /// </summary>
    [Fact]
    public void Le_deblocage_survit_a_l_epuisement_des_charges()
    {
        var charges = new LevelUpCharges(rerollLevel: 1, skipLevel: 1);

        charges.TryReroll();
        charges.TrySkip();

        Assert.True(charges.RerollUnlocked);
        Assert.True(charges.SkipUnlocked);
        Assert.Equal(0, charges.RerollsLeft);
    }

    [Fact]
    public void Un_niveau_negatif_ne_donne_pas_de_charge_negative()
    {
        var charges = new LevelUpCharges(rerollLevel: -4, skipLevel: -1);

        Assert.Equal(0, charges.RerollsLeft);
        Assert.Equal(0, charges.SkipsLeft);
    }
}
