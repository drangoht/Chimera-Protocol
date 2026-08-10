using System.IO;
using Xunit;

/// <summary>
/// Vérifie la lecture des passifs — <b>sur le vrai <c>weapons.json</c> du jeu</b>, et surtout la
/// règle qui a coûté le plus cher au projet : l'extrapolation au-delà des trois niveaux définis,
/// amortie partout <b>sauf sur les PV max</b>.
/// </summary>
public class PassiveTableTests
{
    private static string RealJson()
        => File.ReadAllText(Path.Combine(TestPaths.Data, "weapons.json"));

    [Fact]
    public void LesQuatrePassifsDuJeuSAnalysent()
    {
        var passives = PassiveTable.Parse(RealJson());

        Assert.Equal(4, passives.Count);
        foreach (string id in PassiveTable.AllPassiveIds)
            Assert.True(passives.ContainsKey(id), $"passif '{id}' absent des données");
    }

    [Fact]
    public void ChaquePassifADesPaliersEtUnPlafond()
    {
        var passives = PassiveTable.Parse(RealJson());

        foreach (var (id, def) in passives)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Name), $"{id} sans nom");
            Assert.True(def.Levels.Count > 0, $"{id} sans palier");
            Assert.True(def.MaxLevel > def.DefinedMax,
                $"{id} : le plafond ({def.MaxLevel}) doit dépasser les niveaux décrits ({def.DefinedMax}) — " +
                "c'est précisément la zone d'extrapolation");
        }
    }

    /// <summary>
    /// Le défaut de la 1.22.0, verrouillé : le Capaciteur franchissait 100 % de réduction de recharge
    /// dès son niveau 8, ce qui mettait <b>toutes</b> les armes au plancher de cadence.
    /// </summary>
    [Fact]
    public void LaReductionDeRechargeSAmortitAuDelaDesNiveauxDecrits()
    {
        var passives = PassiveTable.Parse(RealJson());
        var capacitor = passives["capacitor"];

        float defined = PassiveTable.DeltaFor(capacitor, PassiveStat.CooldownReduction, capacitor.DefinedMax);
        float beyond  = PassiveTable.DeltaFor(capacitor, PassiveStat.CooldownReduction, capacitor.DefinedMax + 5);

        Assert.True(beyond < defined, $"delta amorti attendu, obtenu {beyond} contre {defined}");
        Assert.True(beyond > 0f, "l'amortissement ne doit jamais annuler le gain : la carte resterait un choix mort");
    }

    /// <summary>
    /// L'exception assumée : amortir les PV max plafonnait la défense à 451 dès la 11ᵉ minute et
    /// ramenait la survie en overtime à ~1 min (GDD §31.6).
    /// </summary>
    [Fact]
    public void LesPvMaxNeSAmortissentJamais()
    {
        var passives = PassiveTable.Parse(RealJson());
        var plating = passives["reinforced_plating"];

        float defined = PassiveTable.DeltaFor(plating, PassiveStat.MaxHp, plating.DefinedMax);
        float beyond  = PassiveTable.DeltaFor(plating, PassiveStat.MaxHp, plating.DefinedMax + 12);

        Assert.Equal(defined, beyond, 4);
        Assert.True(defined > 0f, "la plaque renforcée doit bien donner des PV");
        Assert.False(PassiveTable.IsDamped(PassiveStat.MaxHp));
    }

    [Fact]
    public void LaReductionDeDegatsDuMemePassifSAmortitElle()
    {
        var passives = PassiveTable.Parse(RealJson());
        var plating = passives["reinforced_plating"];

        float defined = PassiveTable.DeltaFor(plating, PassiveStat.DamageReduction, plating.DefinedMax);
        float beyond  = PassiveTable.DeltaFor(plating, PassiveStat.DamageReduction, plating.DefinedMax + 5);

        Assert.True(beyond < defined,
            "un même passif porte deux stats de nature différente : seules les PV max échappent à l'amortissement");
    }

    [Fact]
    public void UnChampAbsentNeDonneAucunDelta()
    {
        var passives = PassiveTable.Parse(RealJson());

        // Les Servo-Moteurs ne touchent que la vitesse : demander leurs dégâts doit rendre zéro,
        // et non une valeur par défaut appliquée en silence.
        Assert.Equal(0f, PassiveTable.DeltaFor(passives["servo_motors"], PassiveStat.DamageMultiplier, 1), 4);
        Assert.True(PassiveTable.DeltaFor(passives["servo_motors"], PassiveStat.Speed, 1) > 0f);
    }

    [Fact]
    public void UnJsonVideNeLeveAucuneException()
    {
        Assert.Empty(PassiveTable.Parse(""));
        Assert.Empty(PassiveTable.Parse("{}"));
    }
}
