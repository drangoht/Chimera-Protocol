using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Tracé des structures de sol.
///
/// <para>Sous Godot, géométrie et rendu vivaient dans la même classe de nœuds : aucun de ces
/// invariants — « la rivière traverse vraiment l'arène », « rien ne sort de la grille », « la
/// structure reste dans la zone que la caméra montre » — ne pouvait être vérifié autrement qu'en
/// regardant une capture. Les séparer les rend testables ; c'est tout l'intérêt de la couche pure.</para>
/// </summary>
public class FloorFeatureLayoutTests
{
    private const int Cols = 60;   // 1920 px / 32
    private const int Rows = 38;   // 1216 px / 32

    /// <summary>Aléa reproductible : les tests décrivent des invariants, pas un tirage particulier.</summary>
    private static Func<float> Rand(int seed)
    {
        var rng = new Random(seed);
        return () => (float)rng.NextDouble();
    }

    private static readonly string[] Biomes = { "sanctuaire", "aether", "fournaise", "givre", "neon" };

    [Theory]
    [InlineData("sanctuaire")]
    [InlineData("aether")]
    [InlineData("fournaise")]
    [InlineData("givre")]
    [InlineData("neon")]
    public void EveryBiome_ProducesAStructure(string biome)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(seed));

            Assert.True(layout.Cells.Count > 60,
                $"{biome} (graine {seed}) : {layout.Cells.Count} cellules — structure trop maigre pour se voir");
        }
    }

    /// <summary>
    /// Aucune cellule hors grille.
    /// </summary>
    /// <remarks>
    /// Les <b>lignes</b> gardent une marge d'une cellule : une structure collée au mur haut ou bas
    /// passerait sous le liseré de l'arène. Les <b>colonnes</b> n'en gardent pas, et c'est voulu — une
    /// rivière qui s'arrête à trente-deux pixels du bord ne traverse pas l'arène, elle y flotte. Le
    /// jeu publié fait la même distinction : <c>River</c> borne ses lignes, jamais ses colonnes.
    /// </remarks>
    [Theory]
    [InlineData("sanctuaire")]
    [InlineData("aether")]
    [InlineData("fournaise")]
    [InlineData("givre")]
    [InlineData("neon")]
    public void NoCell_EscapesTheGrid(string biome)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(seed));

            foreach (var cell in layout.Cells)
            {
                Assert.InRange(cell.Row, 1, Rows - 2);
                Assert.InRange(cell.Col, 0, Cols - 1);
            }
        }
    }

    /// <summary>
    /// Une rivière et un chemin <b>traversent</b> : ils touchent les deux extrémités de leur axe.
    /// Une structure qui s'arrête au milieu se lit comme un décor abandonné en cours de route.
    /// </summary>
    [Theory]
    [InlineData("aether")]
    [InlineData("fournaise")]
    [InlineData("givre")]
    [InlineData("sanctuaire")]
    public void River_And_Path_CrossTheWholeArena(string biome)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(seed));

            var columns = layout.Cells.Select(c => c.Col).ToHashSet();

            Assert.True(columns.Contains(1) || columns.Contains(2),
                $"{biome} (graine {seed}) : ne part pas du bord gauche");
            Assert.True(columns.Contains(Cols - 2) || columns.Contains(Cols - 3),
                $"{biome} (graine {seed}) : n'atteint pas le bord droit");
        }
    }

    /// <summary>
    /// ⚠ Le rappel vers le centre est ce qui empêche une marche aléatoire de longer un mur. La
    /// caméra suit le joueur : une structure collée au bord n'est presque jamais à l'écran, et le
    /// biome perd ce qui le distingue. Le test mesure donc la <b>position moyenne</b> de l'axe.
    /// </summary>
    [Theory]
    [InlineData("aether")]
    [InlineData("fournaise")]
    [InlineData("givre")]
    public void River_StaysWithinTheVisibleBand(string biome)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(seed));
            double meanRow = layout.Center.Average(c => (double)c.Row);

            Assert.InRange(meanRow, Rows * 0.20, Rows * 0.80);
        }
    }

    /// <summary>Le parvis du Sanctuaire existe, et il est bien à l'intérieur de la structure.</summary>
    [Fact]
    public void Sanctuary_HasAHub_InsideItsPath()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build("sanctuaire", Cols, Rows, Rand(seed));

            Assert.NotNull(layout.Hub);
            Assert.Contains(layout.Hub!.Value, layout.Cells);
        }
    }

    /// <summary>
    /// Les conduits du Néon sont <b>deux</b> routes qui se croisent : sans croisement, il n'y a pas
    /// de réseau mais deux traits parallèles, et le nœud de jonction — le point que l'œil cherche —
    /// n'existe pas.
    /// </summary>
    [Fact]
    public void Neon_HasTwoAxes_AndNodes()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build("neon", Cols, Rows, Rand(seed));

            Assert.NotEmpty(layout.Center);
            Assert.NotEmpty(layout.Center2);
            Assert.NotEmpty(layout.Nodes);
        }
    }

    /// <summary>
    /// Les rives font le tour, et seulement le tour : chaque bord signalé doit donner sur une
    /// cellule <b>hors</b> de la structure. Une rive tracée à l'intérieur zébrerait la rivière.
    /// </summary>
    [Fact]
    public void Banks_OnlyFollowTheOutsideEdges()
    {
        var layout = FloorFeatureLayout.Build("givre", Cols, Rows, Rand(7));

        int banks = 0;
        foreach (var (cell, dr, dc) in FloorFeatureLayout.Banks(layout.Cells))
        {
            Assert.Contains(cell, layout.Cells);
            Assert.DoesNotContain(new FloorFeatureLayout.Cell(cell.Row + dr, cell.Col + dc), layout.Cells);
            banks++;
        }

        Assert.True(banks > 20, $"{banks} bords de rive — une structure sans contour");
    }

    /// <summary>Le sous-échantillonnage garde les deux extrémités : un cours d'eau tronqué se voit.</summary>
    [Fact]
    public void Thin_KeepsBothEnds()
    {
        var path = Enumerable.Range(0, 57).Select(i => new FloorFeatureLayout.Cell(i % 30, i)).ToList();
        var thinned = FloorFeatureLayout.Thin(path, 4);

        Assert.Equal(path[0], thinned[0]);
        Assert.Equal(path[path.Count - 1], thinned[thinned.Count - 1]);
        Assert.True(thinned.Count < path.Count);
    }

    /// <summary>
    /// Même graine, même arène. C'est ce qui permet à un banc de rejouer une run à l'identique — et
    /// la raison pour laquelle l'aléa est injecté au lieu d'être tiré d'un générateur global.
    /// </summary>
    [Fact]
    public void SameSeed_GivesSameStructure()
    {
        foreach (var biome in Biomes)
        {
            var a = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(42));
            var b = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(42));

            Assert.Equal(a.Cells.Count, b.Cells.Count);
            Assert.True(a.Cells.SetEquals(b.Cells), $"{biome} : deux tracés différents pour la même graine");
        }
    }

    /// <summary>
    /// Une structure ne doit pas <b>recouvrir</b> l'arène : elle la traverse. Au-delà d'un quart des
    /// cellules, ce n'est plus un motif de sol mais un second sol, et les obstacles comme les
    /// fenêtres vitrées n'auraient plus où se poser (les deux s'écartent des cellules occupées).
    /// </summary>
    [Theory]
    [InlineData("sanctuaire")]
    [InlineData("aether")]
    [InlineData("fournaise")]
    [InlineData("givre")]
    [InlineData("neon")]
    public void Structure_LeavesRoomForEverythingElse(string biome)
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var layout = FloorFeatureLayout.Build(biome, Cols, Rows, Rand(seed));
            double share = layout.Cells.Count / (double)(Cols * Rows);

            Assert.True(share < 0.25, $"{biome} (graine {seed}) : {share:P0} de l'arène couverte");
        }
    }
}
