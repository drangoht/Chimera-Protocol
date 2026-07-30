using System.Collections.Generic;
using Xunit;
using Vec = System.Numerics.Vector2;

/// <summary>
/// Tests du pilote de banc (src/Core/Rules/AutoPilotPolicy.cs).
///
/// Ce qui est vérifié ici est exactement ce qui invaliderait une mesure si ça cassait : le bot doit
/// fuir, ne pas se coincer dans un coin, ramasser ce qui passe, et surtout rester DÉTERMINISTE —
/// le banc multi-run compare des réglages entre eux, un pilote instable rendrait tout écart illisible.
/// Cf. docs/PITFALLS.md §Banc automatisé.
/// </summary>
public class AutoPilotPolicyTests
{
    // Arène jouable (Constants.ArenaWidth/Height moins les murs), en dur ici : Constants dépend de
    // Godot et n'est pas lié à l'assembly de test.
    private const float HalfW = 1920f / 2f - 32f;
    private const float HalfH = 1216f / 2f - 32f;

    private static readonly IReadOnlyList<Vec> Aucun = new List<Vec>();

    private static Vec Choose(Vec self, IReadOnlyList<Vec> threats, IReadOnlyList<Vec>? pickups = null, Vec previous = default)
        => AutoPilotPolicy.ChooseDirection(self, previous, threats, pickups ?? Aucun, HalfW, HalfH);

    // -----------------------------------------------------------------------
    // Fuite
    // -----------------------------------------------------------------------

    [Fact]
    public void Fuit_UneMenaceUnique()
    {
        // Menace à droite → le cap retenu doit avoir une composante X négative.
        var dir = Choose(Vec.Zero, new List<Vec> { new(150f, 0f) });
        Assert.True(dir.X < 0f, $"le bot n'a pas fui la menace (cap {dir})");
    }

    [Fact]
    public void Fuit_LeBarycentreDeDeuxMenaces()
    {
        // Deux menaces au nord-est et au sud-est → fuite vers l'ouest.
        var dir = Choose(Vec.Zero, new List<Vec> { new(150f, 150f), new(150f, -150f) });
        Assert.True(dir.X < 0f, $"le bot devrait partir plein ouest (cap {dir})");
    }

    [Fact]
    public void Encercle_ChoisitLeSecteurLeMoinsDense()
    {
        // Anneau complet SAUF une brèche à l'ouest : c'est le cas dominant en overtime, et celui où
        // un champ de potentiel classique s'annulerait au lieu de percer.
        var threats = new List<Vec>();
        for (int i = 0; i < 24; i++)
        {
            double a = 2.0 * System.Math.PI * i / 24;
            var p = new Vec((float)(200 * System.Math.Cos(a)), (float)(200 * System.Math.Sin(a)));
            if (p.X < -120f) continue;   // brèche
            threats.Add(p);
        }

        var dir = Choose(Vec.Zero, threats);
        Assert.True(dir.X < 0f, $"le bot n'a pas percé par la brèche (cap {dir})");
    }

    // -----------------------------------------------------------------------
    // Murs — le biais systématique à éliminer
    // -----------------------------------------------------------------------

    [Fact]
    public void NeSortJamaisDeLArene()
    {
        // Collé au bord est, menace à l'ouest : le réflexe naïf (fuir) le pousserait dans le mur.
        var self = new Vec(HalfW - 10f, 0f);
        var dir  = Choose(self, new List<Vec> { new(HalfW - 200f, 0f) });
        var probe = self + dir * AutoPilotPolicy.LookAheadPx;

        Assert.True(System.Math.Abs(probe.X) <= HalfW, $"le cap projette hors de l'arène (x={probe.X})");
    }

    [Fact]
    public void SeDegageDUnCoin()
    {
        // Coincé dans le coin sud-est avec la foule sur la diagonale : il doit longer ou percer,
        // jamais rester bloqué (cap nul).
        var self = new Vec(HalfW - 40f, HalfH - 40f);
        var threats = new List<Vec>
        {
            new(HalfW - 200f, HalfH - 200f),
            new(HalfW - 260f, HalfH - 120f),
            new(HalfW - 120f, HalfH - 260f),
        };

        var dir = Choose(self, threats);
        Assert.NotEqual(Vec.Zero, dir);

        var probe = self + dir * AutoPilotPolicy.LookAheadPx;
        Assert.True(System.Math.Abs(probe.X) <= HalfW && System.Math.Abs(probe.Y) <= HalfH);
        // Et il doit s'éloigner du coin, pas s'y enfoncer.
        Assert.True(probe.X < self.X || probe.Y < self.Y, $"le bot s'enfonce dans le coin (cap {dir})");
    }

    // -----------------------------------------------------------------------
    // Ramassage
    // -----------------------------------------------------------------------

    [Fact]
    public void VaChercherUnOrbeQuandLaVoieEstLibre()
    {
        var dir = Choose(Vec.Zero, Aucun, new List<Vec> { new(120f, 0f) });
        Assert.True(dir.X > 0f, $"le bot ignore un orbe accessible (cap {dir})");
    }

    [Fact]
    public void NePlongePasDansLaFoulePourUnOrbe()
    {
        // Orbe à l'est, mais gardé par un paquet d'ennemis : la survie prime sur le ramassage.
        var threats = new List<Vec> { new(110f, 0f), new(130f, 40f), new(130f, -40f) };
        var dir = Choose(Vec.Zero, threats, new List<Vec> { new(150f, 0f) });
        Assert.True(dir.X < 0f, $"le bot a plongé dans la foule pour un orbe (cap {dir})");
    }

    [Fact]
    public void NeTraversePasUnMurDEnnemisPourRejoindreUneZoneDegagee()
    {
        // Rideau d'ennemis à mi-couloir vers l'est, vide au-delà. Si le score ne regardait que le
        // point d'arrivée, le cap est semblerait parfait — et le bot foncerait dans le rideau.
        var wall = new List<Vec>();
        for (int i = -3; i <= 3; i++)
            wall.Add(new Vec(AutoPilotPolicy.LookAheadPx / 2f, i * 45f));

        var dir = Choose(Vec.Zero, wall);
        Assert.True(dir.X <= 0f, $"le bot a traversé le rideau d'ennemis (cap {dir})");
    }

    // -----------------------------------------------------------------------
    // Stabilité — la propriété qui rend le banc comparable d'un réglage à l'autre
    // -----------------------------------------------------------------------

    [Fact]
    public void EstDeterministe()
    {
        var threats = new List<Vec> { new(150f, 20f), new(-90f, 200f) };
        var pickups = new List<Vec> { new(40f, -160f) };

        var a = AutoPilotPolicy.ChooseDirection(Vec.Zero, new Vec(1f, 0f), threats, pickups, HalfW, HalfH);
        var b = AutoPilotPolicy.ChooseDirection(Vec.Zero, new Vec(1f, 0f), threats, pickups, HalfW, HalfH);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RetourneToujoursUnCapNormalise()
    {
        var dir = Choose(new Vec(200f, -300f), new List<Vec> { new(250f, -250f) });
        Assert.Equal(1f, dir.Length(), 3);
    }

    [Fact]
    public void LInertieDepartageDeuxCapsEquivalents()
    {
        // Sans menace ni orbe, tous les caps se valent : c'est l'inertie qui doit trancher, sans quoi
        // le bot vibre sur place et sa « mortalité » ne mesure plus que cet artefact.
        var previous = new Vec(1f, 0f);
        var dir = AutoPilotPolicy.ChooseDirection(Vec.Zero, previous, Aucun, Aucun, HalfW, HalfH);
        Assert.True(Vec.Dot(dir, previous) > 0.9f, $"l'inertie n'a pas maintenu le cap (cap {dir})");
    }

    [Fact]
    public void IgnoreLesMenacesHorsPortee()
    {
        // Une menace à l'autre bout de l'arène ne doit pas décider du cap (sinon le bot fuit en
        // permanence une foule qui ne le menace pas, et ne ramasse plus rien).
        var loin = new Vec(AutoPilotPolicy.ThreatRadiusPx + 400f, 0f);
        var avec = Choose(Vec.Zero, new List<Vec> { loin }, new List<Vec> { new(120f, 0f) });
        var sans = Choose(Vec.Zero, Aucun, new List<Vec> { new(120f, 0f) });
        Assert.Equal(sans, avec);
    }
}
