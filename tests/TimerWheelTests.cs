using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Contrat des minuteries qui remplacent <c>CreateTimer</c> et les nœuds <c>Timer</c> de Godot.
/// Les trois règles de sémantique documentées dans <see cref="TimerWheel"/> sont testées
/// explicitement : ce sont elles qui évitent des bugs très coûteux à diagnostiquer plus tard
/// (boucle infinie, rafale de rattrapage, exécution immédiate déguisée).
/// </summary>
public class TimerWheelTests
{
    [Fact]
    public void NeSeDeclenchePasAvantLEcheance()
    {
        var w = new TimerWheel();
        bool fired = false;
        w.Add(1.0, () => fired = true);

        w.Tick(0.5);
        Assert.False(fired);

        w.Tick(0.4);
        Assert.False(fired);

        w.Tick(0.2);
        Assert.True(fired);
    }

    [Fact]
    public void UneMinuterieSimpleNeSeDeclencheQuUneFois()
    {
        var w = new TimerWheel();
        int count = 0;
        w.Add(0.1, () => count++);

        w.Tick(1.0);
        w.Tick(1.0);
        w.Tick(1.0);

        Assert.Equal(1, count);
        Assert.Equal(0, w.Count);
    }

    [Fact]
    public void UneMinuterieRepetitiveSeRedeclenche()
    {
        var w = new TimerWheel();
        int count = 0;
        w.Add(1.0, () => count++, repeat: true);

        for (int i = 0; i < 5; i++) w.Tick(1.0);

        Assert.Equal(5, count);
        Assert.Equal(1, w.Count);
    }

    /// <summary>Règle 2 : une longue frame ne doit pas produire une salve de rattrapage.</summary>
    [Fact]
    public void UneLongueFrameNeProduitPasDeRafale()
    {
        var w = new TimerWheel();
        int count = 0;
        w.Add(0.1, () => count++, repeat: true);

        w.Tick(5.0);   // 50 intervalles se sont écoulés

        Assert.Equal(1, count);
    }

    /// <summary>Règle 1 : sans elle, ce test bouclerait à l'infini.</summary>
    [Fact]
    public void UneMinuterieAjouteePendantUnTickNeSeDeclenchePasDansLeMemePassage()
    {
        var w = new TimerWheel();
        var order = new List<string>();

        w.Add(0.1, () =>
        {
            order.Add("externe");
            w.Add(0.0, () => order.Add("interne"));
        });

        w.Tick(1.0);
        Assert.Equal(new[] { "externe" }, order);

        w.Tick(1.0);
        Assert.Equal(new[] { "externe", "interne" }, order);
    }

    /// <summary>Règle 3 : un délai nul est une minuterie, pas un appel direct.</summary>
    [Fact]
    public void UnDelaiNulSeDeclencheAuProchainPassage_PasImmediatement()
    {
        var w = new TimerWheel();
        bool fired = false;
        w.Add(0.0, () => fired = true);

        Assert.False(fired);
        w.Tick(0.0);
        Assert.True(fired);
    }

    [Fact]
    public void SeDeclenchentDansLOrdreDeProgrammation()
    {
        var w = new TimerWheel();
        var order = new List<int>();
        for (int i = 0; i < 4; i++) { int n = i; w.Add(0.1, () => order.Add(n)); }

        w.Tick(1.0);

        Assert.Equal(new[] { 0, 1, 2, 3 }, order);
    }

    [Fact]
    public void Cancel_EmpecheLeDeclenchement()
    {
        var w = new TimerWheel();
        bool fired = false;
        int id = w.Add(0.5, () => fired = true);

        Assert.True(w.Cancel(id));
        w.Tick(10.0);

        Assert.False(fired);
        Assert.Equal(0, w.Count);
    }

    [Fact]
    public void Cancel_SurIdentifiantInconnu_RenvoieFaux()
    {
        var w = new TimerWheel();
        Assert.False(w.Cancel(4242));
    }

    [Fact]
    public void UneMinuteriePeutEnAnnulerUneAutrePendantLeTick()
    {
        var w = new TimerWheel();
        bool second = false;
        int idSecond = 0;

        w.Add(0.1, () => w.Cancel(idSecond));
        idSecond = w.Add(0.2, () => second = true);

        w.Tick(1.0);

        Assert.False(second);
    }

    [Fact]
    public void Clear_AnnuleTout()
    {
        var w = new TimerWheel();
        int count = 0;
        w.Add(0.1, () => count++);
        w.Add(0.2, () => count++, repeat: true);

        w.Clear();
        w.Tick(10.0);

        Assert.Equal(0, count);
        Assert.Equal(0, w.Count);
    }

    [Fact]
    public void UneMinuterieRepetitiveExigeUnIntervallePositif()
    {
        var w = new TimerWheel();
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Add(0.0, () => { }, repeat: true));
    }

    [Fact]
    public void Add_RefuseUnRappelNul()
    {
        var w = new TimerWheel();
        Assert.Throws<ArgumentNullException>(() => w.Add(1.0, null!));
    }

    [Fact]
    public void Tick_RefuseUnDeltaNegatif()
    {
        var w = new TimerWheel();
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Tick(-0.5));
    }

    [Fact]
    public void UnTickImbriqueEstIgnore()
    {
        var w = new TimerWheel();
        int nested = -1;
        w.Add(0.1, () => nested = w.Tick(1.0));

        w.Tick(1.0);

        Assert.Equal(0, nested);
    }
}
