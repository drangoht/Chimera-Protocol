using System;

/// <summary>Les 12 courbes d'interpolation de Godot (<c>Tween.TransitionType</c>).</summary>
public enum TransType { Linear, Sine, Quint, Quart, Quad, Expo, Elastic, Cubic, Circ, Bounce, Back, Spring }

/// <summary>Les 4 modes d'atténuation de Godot (<c>Tween.EaseType</c>).</summary>
public enum EaseType { In, Out, InOut, OutIn }

/// <summary>
/// Reproduit les courbes d'interpolation de Godot 4.7 pour le shim <c>GTween</c> du port Unity
/// (docs/UNITY_MIGRATION_PLAN.md §4.1).
///
/// <para><b>Pourquoi la fidélité compte ici.</b> <c>Tween</c> est l'idiome Godot le plus utilisé du
/// projet — <b>502 sites d'appel</b>, dont 280 dans l'UI. Une courbe « à peu près » ne casse rien de
/// fonctionnel : elle fait dériver <i>toute</i> l'animation du jeu, d'un écart qui ne se voit qu'en
/// mouvement et qu'aucune capture avant/après ne rattrape.</para>
///
/// <para><b>Établi par mesure</b> (<c>tools/unity/dump_godot_easing.gd</c>), comme pour
/// <see cref="Pcg32"/> : 12 transitions × 4 modes × 9 échantillons relevés sur le moteur, puis
/// vérifiés par tests. Les singularités relevées sont conservées <b>volontairement</b> — ce sont
/// précisément elles qu'une réimplémentation « propre » manquerait :</para>
/// <list type="bullet">
///   <item><b>Expo n'atteint pas ses bornes</b> : <c>Expo/In</c> vaut <c>0,999</c> à t=1 (et non 1),
///         par le terme correctif <c>−0,001</c> de Godot ;</item>
///   <item><b>Expo/InOut n'est pas la composition générique</b> de In et Out : il vaut
///         <c>0,50025</c> à mi-course, pas 0,5 ;</item>
///   <item><b>Elastic et Back sortent de [0,1]</b> (dépassement et rebond) — c'est voulu.</item>
/// </list>
///
/// <para>Logique pure, sans dépendance moteur : couverte par la suite xUnit existante.</para>
/// </summary>
public static class Easing
{
    /// <summary>Évalue la courbe pour une progression <paramref name="t"/> dans [0, 1].</summary>
    public static double Evaluate(TransType trans, EaseType ease, double t)
    {
        if (t <= 0.0) return 0.0;
        if (t >= 1.0) return ease switch
        {
            // Godot évalue la borne par la formule et non par un cas particulier : Expo y rend
            // 0,999 et non 1. Court-circuiter à 1 « pour faire propre » romprait la parité.
            EaseType.In    => In(trans, 1.0),
            EaseType.Out   => Out(trans, 1.0),
            EaseType.InOut => InOut(trans, 1.0),
            _              => 0.5 + 0.5 * In(trans, 1.0),
        };

        return ease switch
        {
            EaseType.In    => In(trans, t),
            EaseType.Out   => Out(trans, t),
            EaseType.InOut => InOut(trans, t),
            EaseType.OutIn => OutIn(trans, t),
            _              => t,
        };
    }

    /// <summary>Interpole entre deux valeurs — équivalent de <c>Tween.interpolate_value</c>.</summary>
    public static double Interpolate(double from, double to, double t, TransType trans, EaseType ease)
        => from + (to - from) * Evaluate(trans, ease, t);

    // ─── In ───────────────────────────────────────────────────────────────────

    private static double In(TransType tr, double t) => tr switch
    {
        TransType.Linear  => t,
        TransType.Sine    => 1.0 - Math.Cos(t * Math.PI / 2.0),
        TransType.Quint   => t * t * t * t * t,
        TransType.Quart   => t * t * t * t,
        TransType.Quad    => t * t,
        TransType.Expo    => t == 0.0 ? 0.0 : Math.Pow(2.0, 10.0 * (t - 1.0)) - 0.001,
        TransType.Elastic => ElasticIn(t),
        TransType.Cubic   => t * t * t,
        TransType.Circ    => -(Math.Sqrt(1.0 - t * t) - 1.0),
        TransType.Bounce  => 1.0 - BounceOut(1.0 - t),
        TransType.Back    => t * t * ((BackS + 1.0) * t - BackS),
        TransType.Spring  => 1.0 - SpringOut(1.0 - t),
        _                 => t,
    };

    // ─── Out ──────────────────────────────────────────────────────────────────

    private static double Out(TransType tr, double t) => tr switch
    {
        TransType.Linear  => t,
        TransType.Sine    => Math.Sin(t * Math.PI / 2.0),
        TransType.Quint   => Pow5(t - 1.0) + 1.0,
        TransType.Quart   => -(Pow4(t - 1.0) - 1.0),
        TransType.Quad    => -t * (t - 2.0),
        TransType.Expo    => t == 1.0 ? 1.0 : 1.001 * (1.0 - Math.Pow(2.0, -10.0 * t)),
        TransType.Elastic => ElasticOut(t),
        TransType.Cubic   => Pow3(t - 1.0) + 1.0,
        TransType.Circ    => Math.Sqrt(1.0 - (t - 1.0) * (t - 1.0)),
        TransType.Bounce  => BounceOut(t),
        TransType.Back    => BackOut(t),
        TransType.Spring  => SpringOut(t),
        _                 => t,
    };

    // ─── InOut (dédié par transition — la composition générique ne suffit pas) ─

    private static double InOut(TransType tr, double t)
    {
        switch (tr)
        {
            case TransType.Linear: return t;
            case TransType.Sine:   return -(Math.Cos(Math.PI * t) - 1.0) / 2.0;

            case TransType.Expo:
            {
                // Godot ne compose PAS In et Out ici : le facteur correctif diffère (0,0005 /
                // 1,0005), d'où la valeur 0,50025 à mi-course au lieu de 0,5.
                if (t == 0.0) return 0.0;
                double u = t * 2.0;
                if (u == 2.0) return 1.0;
                if (u < 1.0) return 0.5 * Math.Pow(2.0, 10.0 * (u - 1.0)) - 0.0005;
                return 0.5 * 1.0005 * (-Math.Pow(2.0, -10.0 * (u - 1.0)) + 2.0);
            }

            case TransType.Elastic: return ElasticInOut(t);

            case TransType.Back:
            {
                double s = BackS * 1.525;
                double u = t * 2.0;
                if (u < 1.0) return 0.5 * (u * u * ((s + 1.0) * u - s));
                u -= 2.0;
                return 0.5 * (u * u * ((s + 1.0) * u + s) + 2.0);
            }

            default:
            {
                // Toutes les autres transitions se composent bien : moitié In, moitié Out.
                double u = t * 2.0;
                return u < 1.0 ? 0.5 * In(tr, u) : 0.5 + 0.5 * Out(tr, u - 1.0);
            }
        }
    }

    // ─── OutIn (générique chez Godot, contrairement à InOut) ──────────────────

    private static double OutIn(TransType tr, double t)
    {
        double u = t * 2.0;
        return u < 1.0 ? 0.5 * Out(tr, u) : 0.5 + 0.5 * In(tr, u - 1.0);
    }

    // ─── Noyaux ───────────────────────────────────────────────────────────────

    private const double BackS = 1.70158;
    private const double ElasticP = 0.3;

    private static double Pow3(double x) => x * x * x;
    private static double Pow4(double x) => x * x * x * x;
    private static double Pow5(double x) => x * x * x * x * x;

    private static double BackOut(double t)
    {
        t -= 1.0;
        return t * t * ((BackS + 1.0) * t + BackS) + 1.0;
    }

    private static double ElasticIn(double t)
    {
        if (t == 0.0) return 0.0;
        if (t == 1.0) return 1.0;
        double u = t - 1.0;
        double s = ElasticP / 4.0;
        return -(Math.Pow(2.0, 10.0 * u) * Math.Sin((u - s) * (2.0 * Math.PI) / ElasticP));
    }

    private static double ElasticOut(double t)
    {
        if (t == 0.0) return 0.0;
        if (t == 1.0) return 1.0;
        double s = ElasticP / 4.0;
        return Math.Pow(2.0, -10.0 * t) * Math.Sin((t - s) * (2.0 * Math.PI) / ElasticP) + 1.0;
    }

    private static double ElasticInOut(double t)
    {
        if (t == 0.0) return 0.0;
        double u = t * 2.0;
        if (u == 2.0) return 1.0;

        double p = ElasticP * 1.5;
        double s = p / 4.0;

        if (u < 1.0)
        {
            u -= 1.0;
            return -0.5 * (Math.Pow(2.0, 10.0 * u) * Math.Sin((u - s) * (2.0 * Math.PI) / p));
        }

        u -= 1.0;
        return Math.Pow(2.0, -10.0 * u) * Math.Sin((u - s) * (2.0 * Math.PI) / p) * 0.5 + 1.0;
    }

    private static double BounceOut(double t)
    {
        if (t < 1.0 / 2.75) return 7.5625 * t * t;
        if (t < 2.0 / 2.75) { t -= 1.5 / 2.75;   return 7.5625 * t * t + 0.75; }
        if (t < 2.5 / 2.75) { t -= 2.25 / 2.75;  return 7.5625 * t * t + 0.9375; }
        t -= 2.625 / 2.75;
        return 7.5625 * t * t + 0.984375;
    }

    private static double SpringOut(double t)
    {
        double s = 1.0 - t;
        return (Math.Sin(t * Math.PI * (0.2 + 2.5 * t * t * t)) * Math.Pow(s, 2.2) + t)
               * (1.0 + 1.2 * s);
    }
}
