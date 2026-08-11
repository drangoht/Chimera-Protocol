using Xunit;

/// <summary>
/// L'aimantation des orbes d'XP.
///
/// <para>Ces tests existent pour un défaut précis et déjà arrivé : la vitesse d'attraction était une
/// constante (300 px/s) qui ignorait le plafond de vitesse du joueur (380 px/s), posé dans un autre
/// fichier. <b>Un joueur rapide n'était plus rattrapable par ses propres orbes</b> — ils le suivaient
/// en file au ras du sol jusqu'à sortir du rayon. C'est le joueur qui l'a vu ; rien ne pouvait le
/// signaler, l'XP jamais ramassée ne se compte nulle part.</para>
/// </summary>
public class PickupMagnetTests
{
    /// <summary>
    /// L'invariant central, vérifié sur toute la plage de vitesses atteignables — de l'arrêt au
    /// plafond, en passant par la Célérité qui pousse la statistique au-delà avant plafonnement.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(200f)]                        // vitesse de base
    [InlineData(StatCaps.MaxSpeed - 1f)]
    [InlineData(StatCaps.MaxSpeed)]           // le plafond : c'est là que ça cassait
    [InlineData(StatCaps.MaxSpeed * 2f)]      // au-delà de tout ce que le jeu permet
    public void UnOrbeGagneToujoursDuTerrainSurSonPorteur(float carrierSpeed)
    {
        float speed = PickupMagnet.SpeedAgainst(carrierSpeed);

        Assert.True(speed > carrierSpeed,
            $"a {carrierSpeed} px/s, l'orbe avance a {speed} px/s : il ne rattrape jamais");
    }

    [Fact]
    public void LaVitesseDeRapprochementNeDependPasDeCeQueLeJoueurAAchete()
    {
        // C'est tout l'objet de la règle : aspirer ses orbes se sent pareil à 200 px/s et à 380.
        float lent = PickupMagnet.SpeedAgainst(200f) - 200f;
        float rapide = PickupMagnet.SpeedAgainst(StatCaps.MaxSpeed) - StatCaps.MaxSpeed;

        Assert.Equal(lent, rapide, 3);
        Assert.Equal(PickupMagnet.ClosingSpeed, rapide, 3);
    }

    [Fact]
    public void UnPorteurImmobileNeVoitPasSesOrbesRamper()
    {
        Assert.Equal(PickupMagnet.MinSpeed, PickupMagnet.SpeedAgainst(0f), 3);
    }

    [Fact]
    public void LAimantRamasseAccelereEncore()
    {
        float normal = PickupMagnet.SpeedAgainst(200f);
        float force = PickupMagnet.SpeedAgainst(200f, forced: true);

        Assert.Equal(normal * PickupMagnet.ForcedBoost, force, 3);
    }

    /// <summary>
    /// La traînée, mesurée : le temps qu'un orbe passe derrière un joueur lancé. C'est LA grandeur
    /// que le joueur voit — une file d'orbes au sol est exactement ce délai, multiplié par le nombre
    /// d'ennemis tombés dans la seconde.
    /// </summary>
    [Fact]
    public void UnOrbePrisAuBordDuRayonRejointLeJoueurEnMoinsDUneDemiSeconde()
    {
        float seconds = PickupMagnet.ChaseSeconds(PickupMagnet.Radius, 200f);

        Assert.True(seconds < 0.5f, $"{seconds:0.00} s de trainee derriere le joueur");

        // Et jamais infini, quelle que soit la vitesse : c'est le défaut d'origine, reformulé.
        Assert.True(float.IsFinite(PickupMagnet.ChaseSeconds(PickupMagnet.Radius, StatCaps.MaxSpeed)));
    }

    [Fact]
    public void LeRamassageSeFaitAuContactDesCorpsDuJeuDOrigine()
    {
        // Buste du joueur 20 × 26 et orbe de rayon 8 sous Godot : le contact tombait entre 18 et
        // 21 px centre à centre. 16 px — la valeur du portage — exigeait un recouvrement presque
        // total des deux sprites.
        Assert.InRange(PickupMagnet.PickupRadius, 18f, 21f);
    }

    [Fact]
    public void LeRayonDAimantationLaisseLeJoueurAllerChercherSonXp()
    {
        // L'orbe n'est pas un ramassage automatique : il oblige à entrer dans la zone nettoyée.
        // Un rayon proche de la moitié d'un écran supprimerait ce geste.
        Assert.InRange(PickupMagnet.Radius, 60f, 160f);
    }
}
