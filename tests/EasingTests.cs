using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

/// <summary>
/// Vérifie que les courbes d'interpolation du port Unity reproduisent celles de Godot 4.7.
///
/// <para>Comme pour <see cref="Pcg32Tests"/>, la référence est un <b>relevé du moteur</b>
/// (<c>tools/unity/dump_godot_easing.gd</c>, Godot 4.7-stable, 2026-08-03) et non une
/// documentation. Le tableau brut est conservé tel quel ci-dessous : il se régénère d'une commande,
/// et le lire en diff dit immédiatement ce qui a bougé.</para>
///
/// <para>Enjeu : <c>Tween</c> compte <b>502 sites d'appel</b> dans le projet. Une courbe fausse ne
/// casse rien de fonctionnel — elle fait dériver toute l'animation, d'un écart qui ne se voit qu'en
/// mouvement.</para>
/// </summary>
public class EasingTests
{
    private static readonly double[] Samples = { 0.0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875, 1.0 };

    /// <summary>
    /// Sortie brute de <c>dump_godot_easing.gd</c>. Format : <c>TRANS EASE v1 … v9</c>.
    /// </summary>
    private const string GodotReference = @"
LINEAR IN 0.000000000 0.125000000 0.250000000 0.375000000 0.500000000 0.625000000 0.750000000 0.875000000 1.000000000
LINEAR OUT 0.000000000 0.125000000 0.250000000 0.375000000 0.500000000 0.625000000 0.750000000 0.875000000 1.000000000
LINEAR IN_OUT 0.000000000 0.125000000 0.250000000 0.375000000 0.500000000 0.625000000 0.750000000 0.875000000 1.000000000
LINEAR OUT_IN 0.000000000 0.125000000 0.250000000 0.375000000 0.500000000 0.625000000 0.750000000 0.875000000 1.000000000
SINE IN 0.000000000 0.019214720 0.076120466 0.168530390 0.292893231 0.444429755 0.617316544 0.804909706 1.000000000
SINE OUT 0.000000000 0.195090324 0.382683426 0.555570245 0.707106769 0.831469595 0.923879504 0.980785251 1.000000000
SINE IN_OUT 0.000000000 0.038060233 0.146446615 0.308658272 0.500000000 0.691341698 0.853553414 0.961939752 1.000000000
SINE OUT_IN 0.000000000 0.191341713 0.353553385 0.461939752 0.500000000 0.538060248 0.646446586 0.808658302 1.000000000
QUINT IN 0.000000000 0.000030518 0.000976563 0.007415771 0.031250000 0.095367432 0.237304688 0.512908936 1.000000000
QUINT OUT 0.000000000 0.487091064 0.762695313 0.904632568 0.968750000 0.992584229 0.999023438 0.999969482 1.000000000
QUINT IN_OUT 0.000000000 0.000488281 0.015625000 0.118652344 0.500000000 0.881347656 0.984375000 0.999511719 1.000000000
QUINT OUT_IN 0.000000000 0.381347656 0.484375000 0.499511719 0.500000000 0.500488281 0.515625000 0.618652344 1.000000000
QUART IN 0.000000000 0.000244141 0.003906250 0.019775391 0.062500000 0.152587891 0.316406250 0.586181641 1.000000000
QUART OUT 0.000000000 0.413818359 0.683593750 0.847412109 0.937500000 0.980224609 0.996093750 0.999755859 1.000000000
QUART IN_OUT 0.000000000 0.001953125 0.031250000 0.158203125 0.500000000 0.841796875 0.968750000 0.998046875 1.000000000
QUART OUT_IN 0.000000000 0.341796875 0.468750000 0.498046875 0.500000000 0.501953125 0.531250000 0.658203125 1.000000000
QUAD IN 0.000000000 0.015625000 0.062500000 0.140625000 0.250000000 0.390625000 0.562500000 0.765625000 1.000000000
QUAD OUT 0.000000000 0.234375000 0.437500000 0.609375000 0.750000000 0.859375000 0.937500000 0.984375000 1.000000000
QUAD IN_OUT 0.000000000 0.031250000 0.125000000 0.281250000 0.500000000 0.718750000 0.875000000 0.968750000 1.000000000
QUAD OUT_IN 0.000000000 0.218750000 0.375000000 0.468750000 0.500000000 0.531250000 0.625000000 0.781250000 1.000000000
EXPO IN 0.000000000 0.001322670 0.004524271 0.012139007 0.030250000 0.073325440 0.175776690 0.419448227 0.999000013
EXPO OUT 0.000000000 0.580131352 0.824046493 0.926600218 0.969718754 0.987847865 0.995470226 0.998674989 1.000000000
EXPO IN_OUT 0.000000000 0.002262136 0.015125000 0.087888345 0.500249982 0.912067473 0.984867215 0.997736454 1.000000000
EXPO OUT_IN 0.000000000 0.412023246 0.484859377 0.497735113 0.500000000 0.502262115 0.515124977 0.587888300 0.999499977
ELASTIC IN 0.000000000 0.002011490 -0.005524272 0.011378719 -0.015625020 0.000000046 0.088388264 -0.364118814 1.000000000
ELASTIC OUT 0.000000000 1.364118814 0.911611676 1.000000000 1.015625000 0.988621294 1.005524278 0.997988522 1.000000000
ELASTIC IN_OUT 0.000000000 -0.001381069 0.011969446 -0.083057880 0.500000000 1.083057880 0.988030553 1.001381040 1.000000000
ELASTIC OUT_IN 0.000000000 0.455805838 0.507812500 0.502762139 0.500000000 0.497237861 0.492187500 0.544194162 1.000000000
CUBIC IN 0.000000000 0.001953125 0.015625000 0.052734375 0.125000000 0.244140625 0.421875000 0.669921875 1.000000000
CUBIC OUT 0.000000000 0.330078125 0.578125000 0.755859375 0.875000000 0.947265625 0.984375000 0.998046875 1.000000000
CUBIC IN_OUT 0.000000000 0.007812500 0.062500000 0.210937500 0.500000000 0.789062500 0.937500000 0.992187500 1.000000000
CUBIC OUT_IN 0.000000000 0.289062500 0.437500000 0.492187500 0.500000000 0.507812500 0.562500000 0.710937500 1.000000000
CIRC IN 0.000000000 0.007843256 0.031754136 0.072975218 0.133974612 0.219375253 0.338562191 0.515877068 1.000000000
CIRC OUT 0.000000000 0.484122932 0.661437809 0.780624747 0.866025388 0.927024782 0.968245864 0.992156744 1.000000000
CIRC IN_OUT 0.000000000 0.015877068 0.066987306 0.169281095 0.500000000 0.830718875 0.933012724 0.984122932 1.000000000
CIRC OUT_IN 0.000000000 0.330718905 0.433012694 0.484122932 0.500000000 0.515877068 0.566987276 0.669281125 1.000000000
BOUNCE IN 0.000000000 0.038085938 0.027343750 0.202148438 0.234375000 0.030273378 0.527343750 0.881835938 1.000000000
BOUNCE OUT 0.000000000 0.118164063 0.472656250 0.969726622 0.765625000 0.797851563 0.972656250 0.961914063 1.000000000
BOUNCE IN_OUT 0.000000000 0.013671875 0.117187500 0.263671875 0.500000000 0.736328125 0.882812500 0.986328125 1.000000000
BOUNCE OUT_IN 0.000000000 0.236328125 0.382812500 0.486328125 0.500000000 0.513671875 0.617187500 0.763671875 1.000000000
BACK IN 0.000000000 -0.021310665 -0.064136565 -0.096818559 -0.087697506 -0.005114265 0.182590306 0.507075369 1.000000000
BACK OUT 0.000000000 0.492924631 0.817409694 1.005114317 1.087697506 1.096818566 1.064136505 1.021310687 1.000000000
BACK IN_OUT 0.000000000 -0.053005688 -0.099681839 0.028482914 0.500000000 0.971517086 1.099681854 1.053005695 1.000000000
BACK OUT_IN 0.000000000 0.408704847 0.543848753 0.532068253 0.500000000 0.467931718 0.456151247 0.591295123 1.000000000
SPRING IN 0.000000000 0.004469454 0.013654888 -0.073801041 -0.051015735 0.106634736 0.336663306 0.620930195 1.000000000
SPRING OUT 0.000000000 0.379069775 0.663336694 0.893365264 1.051015735 1.073801041 0.986345112 0.995530546 1.000000000
SPRING IN_OUT 0.000000000 0.006827444 -0.025507867 0.168331653 0.500000000 0.831668377 1.025507927 0.993172526 1.000000000
SPRING OUT_IN 0.000000000 0.331668347 0.525507867 0.493172556 0.500000000 0.506827474 0.474492133 0.668331623 1.000000000
";

    public static TheoryData<string, string, double[]> Reference
    {
        get
        {
            var data = new TheoryData<string, string, double[]>();
            foreach (string raw in GodotReference.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;

                string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var values = new double[p.Length - 2];
                for (int i = 0; i < values.Length; i++)
                    values[i] = double.Parse(p[i + 2], CultureInfo.InvariantCulture);

                data.Add(p[0], p[1], values);
            }
            return data;
        }
    }

    /// <summary>
    /// Tolérance : Godot calcule ses courbes en <c>real_t</c> (simple précision) et le relevé est
    /// imprimé à 9 décimales, alors que le port calcule en <c>double</c>. 1e-6 est donc la
    /// précision réellement disponible — assez serré pour rejeter toute formule fausse, et non une
    /// marge de confort : une erreur de formule se compte en centièmes, pas en millionièmes.
    /// </summary>
    private const double Tolerance = 1e-6;

    [Theory]
    [MemberData(nameof(Reference))]
    public void Evaluate_ReproduitLesCourbesDeGodot(string trans, string ease, double[] expected)
    {
        var t = ParseTrans(trans);
        var e = ParseEase(ease);

        for (int i = 0; i < Samples.Length; i++)
        {
            double actual = Easing.Evaluate(t, e, Samples[i]);
            Assert.True(Math.Abs(expected[i] - actual) < Tolerance,
                $"{trans}/{ease} à t={Samples[i]} : attendu {expected[i]:F9}, obtenu {actual:F9} " +
                $"(écart {Math.Abs(expected[i] - actual):E3})");
        }
    }

    /// <summary>
    /// Godot ne ramène PAS Expo à ses bornes : le terme correctif −0,001 laisse <c>Expo/In</c> à
    /// 0,999 en fin de course. « Corriger » cela pour faire propre romprait la parité — d'où ce
    /// test dédié, qui documente l'anomalie autant qu'il la verrouille.
    /// </summary>
    [Fact]
    public void Expo_NAtteintPasSaBorneHaute_CommeDansGodot()
    {
        double v = Easing.Evaluate(TransType.Expo, EaseType.In, 1.0);
        Assert.True(Math.Abs(0.999 - v) < 1e-6, $"attendu ~0,999, obtenu {v:F9}");
        Assert.NotEqual(1.0, v, 6);
    }

    /// <summary>
    /// <c>Expo/InOut</c> n'est pas la composition générique de In et Out : il vaut 0,50025 à
    /// mi-course. C'est la seule transition, avec Elastic et Back, à disposer d'une formule dédiée.
    /// </summary>
    [Fact]
    public void ExpoInOut_NEstPasLaCompositionGenerique()
    {
        double v = Easing.Evaluate(TransType.Expo, EaseType.InOut, 0.5);
        Assert.True(Math.Abs(0.500249982 - v) < 1e-6, $"attendu ~0,50025, obtenu {v:F9}");
    }

    /// <summary>
    /// Elastic et Back sortent volontairement de [0, 1] (dépassement, rebond). Un shim qui
    /// « sécuriserait » la sortie par un clamp supprimerait l'effet visuel recherché.
    /// </summary>
    [Theory]
    [InlineData(TransType.Elastic, EaseType.Out, 0.125)]
    [InlineData(TransType.Back,    EaseType.Out, 0.5)]
    public void ElasticEtBack_DepassentLIntervalleUnite(TransType trans, EaseType ease, double t)
    {
        Assert.True(Easing.Evaluate(trans, ease, t) > 1.0);
    }

    [Fact]
    public void Back_PasseSousZeroEnEntree()
    {
        Assert.True(Easing.Evaluate(TransType.Back, EaseType.In, 0.25) < 0.0);
    }

    [Theory]
    [InlineData(TransType.Linear)]
    [InlineData(TransType.Quad)]
    [InlineData(TransType.Bounce)]
    public void Evaluate_BorneLesEntreesHorsIntervalle(TransType trans)
    {
        Assert.Equal(0.0, Easing.Evaluate(trans, EaseType.In, -5.0));
        Assert.Equal(Easing.Evaluate(trans, EaseType.In, 1.0), Easing.Evaluate(trans, EaseType.In, 42.0));
    }

    [Fact]
    public void Interpolate_ProduitLesBornesDemandees()
    {
        Assert.Equal(10.0, Easing.Interpolate(10.0, 30.0, 0.0, TransType.Quad, EaseType.In), 9);
        Assert.Equal(30.0, Easing.Interpolate(10.0, 30.0, 1.0, TransType.Quad, EaseType.In), 9);
        Assert.Equal(20.0, Easing.Interpolate(10.0, 30.0, 0.5, TransType.Linear, EaseType.In), 9);
    }

    [Fact]
    public void Interpolate_FonctionneAussiADecroissance()
    {
        Assert.Equal(30.0, Easing.Interpolate(30.0, 10.0, 0.0, TransType.Cubic, EaseType.Out), 9);
        Assert.Equal(10.0, Easing.Interpolate(30.0, 10.0, 1.0, TransType.Cubic, EaseType.Out), 9);
    }

    private static TransType ParseTrans(string s) => s switch
    {
        "LINEAR"  => TransType.Linear,
        "SINE"    => TransType.Sine,
        "QUINT"   => TransType.Quint,
        "QUART"   => TransType.Quart,
        "QUAD"    => TransType.Quad,
        "EXPO"    => TransType.Expo,
        "ELASTIC" => TransType.Elastic,
        "CUBIC"   => TransType.Cubic,
        "CIRC"    => TransType.Circ,
        "BOUNCE"  => TransType.Bounce,
        "BACK"    => TransType.Back,
        "SPRING"  => TransType.Spring,
        _         => throw new ArgumentException($"transition inconnue : {s}"),
    };

    private static EaseType ParseEase(string s) => s switch
    {
        "IN"     => EaseType.In,
        "OUT"    => EaseType.Out,
        "IN_OUT" => EaseType.InOut,
        "OUT_IN" => EaseType.OutIn,
        _        => throw new ArgumentException($"atténuation inconnue : {s}"),
    };
}
