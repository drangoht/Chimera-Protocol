using Xunit;

/// <summary>
/// Le retour du Glaive.
///
/// <para>Ces tests existent pour un défaut précis, signalé en jouant le 2026-08-12 : « les boomerangs
/// sont trop lents à revenir vers le joueur, surtout quand celui-ci a augmenté sa vitesse de
/// déplacement ». C'est mot pour mot le défaut des orbes d'XP (<see cref="PickupMagnetTests"/>), sur
/// une autre entité — une constante de projectile (420 px/s) qui ignorait le plafond de vitesse du
/// joueur (380 px/s), posé dans un autre fichier.</para>
///
/// <para>Et il se punissait lui-même : la recharge de l'arme attend le retour de la lame, donc plus
/// le joueur achetait de vitesse, moins son Glaive tirait.</para>
/// </summary>
public class BoomerangReturnTests
{
    /// <summary>Vitesse à l'aller du Glaive — <c>GlaiveProjectile.Speed</c>.</summary>
    private const float GlaiveSpeed = 420f;

    /// <summary>Portée de base du Glaive — <c>weapons.json</c>, palier 1.</summary>
    private const float GlaiveRange = 240f;

    /// <summary>
    /// L'invariant central, vérifié sur toute la plage de vitesses atteignables — de l'arrêt au
    /// plafond, et au-delà de ce que le jeu permet.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(200f)]                        // vitesse de base
    [InlineData(StatCaps.MaxSpeed - 1f)]
    [InlineData(StatCaps.MaxSpeed)]           // le plafond : c'est là que ça cassait
    [InlineData(StatCaps.MaxSpeed * 2f)]
    public void UneLameGagneToujoursDuTerrainSurSonPorteur(float carrierSpeed)
    {
        float speed = BoomerangReturn.SpeedAgainst(GlaiveSpeed, carrierSpeed);

        Assert.True(speed > carrierSpeed,
            $"a {carrierSpeed} px/s, la lame revient a {speed} px/s : elle ne rentre jamais");
    }

    [Fact]
    public void LaVitesseDeRapprochementNeDependPasDeCeQueLeJoueurAAchete()
    {
        float lent = BoomerangReturn.SpeedAgainst(GlaiveSpeed, 200f) - 200f;
        float rapide = BoomerangReturn.SpeedAgainst(GlaiveSpeed, StatCaps.MaxSpeed) - StatCaps.MaxSpeed;

        // Le retour reste au moins aussi franc au plafond qu'à la vitesse de base : c'est tout
        // l'objet de la règle. (À basse vitesse, `ReturnBoost` le rend même un peu plus vif.)
        Assert.True(rapide >= BoomerangReturn.ClosingSpeed);
        Assert.True(lent >= BoomerangReturn.ClosingSpeed);
    }

    /// <summary>
    /// Le temps de retour, mesuré. C'est LA grandeur que le joueur voit — et qu'il subit deux fois,
    /// puisque la cadence de l'arme l'attend.
    /// </summary>
    [Fact]
    public void UnRetourNeDureJamaisPlusDUneSecondeQuelleQueSoitLaVitesse()
    {
        for (float carrier = 0f; carrier <= StatCaps.MaxSpeed; carrier += 20f)
        {
            float seconds = BoomerangReturn.ReturnSeconds(GlaiveRange, GlaiveSpeed, carrier);

            Assert.True(seconds < 1f,
                $"a {carrier} px/s, la lame met {seconds:0.00} s a rentrer — l'arme ne tire pas pendant ce temps");
        }
    }

    /// <summary>
    /// L'écart entre le meilleur et le pire cas, qui est la mesure exacte du défaut : il valait un
    /// facteur 5,5 (1,1 s à l'arrêt, 6,0 s au plafond).
    /// </summary>
    [Fact]
    public void LEcartEntreUnJoueurLentEtUnJoueurRapideResteMarginal()
    {
        float lent = BoomerangReturn.ReturnSeconds(GlaiveRange, GlaiveSpeed, 200f);
        float rapide = BoomerangReturn.ReturnSeconds(GlaiveRange, GlaiveSpeed, StatCaps.MaxSpeed);

        Assert.True(rapide / lent < 1.2f, $"{lent:0.00} s contre {rapide:0.00} s");
    }

    /// <summary>
    /// Un joueur immobile ne fuit rien : sans le rappel, sa lame reviendrait exactement à la vitesse
    /// à laquelle elle est partie, et le retour durerait aussi longtemps que l'aller.
    /// </summary>
    [Fact]
    public void LeRetourEstPlusVifQueLAller()
    {
        Assert.True(BoomerangReturn.SpeedAgainst(GlaiveSpeed, 0f) > GlaiveSpeed);
    }
}
