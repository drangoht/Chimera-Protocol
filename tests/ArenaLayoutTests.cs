using Xunit;

/// <summary>
/// Vérifie le placement des obstacles : ce qui rend une arène jouable n'est pas leur nombre mais
/// leur <b>disposition</b> — assez d'espace pour circuler, aucun coin condamné.
/// </summary>
public class ArenaLayoutTests
{
    private const float HalfW = 960f;
    private const float HalfH = 608f;

    private static System.Collections.Generic.List<ArenaLayout.Spot> Layout(ulong seed)
    {
        var rng = new Pcg32(seed);
        return ArenaLayout.Positions(rng.NextFloat, HalfW, HalfH);
    }

    [Fact]
    public void ChaqueGabaritPlaceEntreSixEtDixObstacles()
    {
        for (ulong seed = 1; seed <= 30; seed++)
        {
            int count = Layout(seed).Count;
            Assert.True(count >= 6 && count <= 10, $"graine {seed} : {count} obstacles");
        }
    }

    /// <summary>
    /// Aucun obstacle collé au mur : sans cette marge, un encerclement contre une paroi devient une
    /// mort sans issue.
    /// </summary>
    [Fact]
    public void AucunObstacleNeTouchLeBord()
    {
        for (ulong seed = 1; seed <= 30; seed++)
            foreach (var spot in Layout(seed))
            {
                Assert.True(System.Math.Abs(spot.X) <= HalfW - ArenaLayout.EdgeMargin + 0.01f,
                    $"graine {seed} : obstacle à x={spot.X}");
                Assert.True(System.Math.Abs(spot.Y) <= HalfH - ArenaLayout.EdgeMargin + 0.01f,
                    $"graine {seed} : obstacle à y={spot.Y}");
            }
    }

    /// <summary>
    /// Le centre reste dégagé : c'est là que le joueur commence, et démarrer coincé dans un pilier
    /// serait la pire entrée en matière possible.
    /// </summary>
    [Fact]
    public void LeCentreResteDegage()
    {
        for (ulong seed = 1; seed <= 30; seed++)
            foreach (var spot in Layout(seed))
                Assert.True(spot.X * spot.X + spot.Y * spot.Y > 150f * 150f,
                    $"graine {seed} : obstacle à {spot.X};{spot.Y}, trop près du point de départ");
    }

    /// <summary>Même graine, même arène — condition d'une mesure de banc reproductible.</summary>
    [Fact]
    public void MemeGraineMemeArene()
    {
        var a = Layout(4242);
        var b = Layout(4242);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].X, b[i].X, 4);
            Assert.Equal(a[i].Y, b[i].Y, 4);
        }
    }

    [Fact]
    public void DesGrainesDifferentesDonnentDesArenesDifferentes()
    {
        var seen = new System.Collections.Generic.HashSet<string>();

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var layout = Layout(seed);
            seen.Add($"{layout.Count}:{layout[0].X:F0},{layout[0].Y:F0}");
        }

        Assert.True(seen.Count > 3, $"seulement {seen.Count} dispositions distinctes sur 20 graines");
    }

    // ─── Écartement ───────────────────────────────────────────────────────────

    [Fact]
    public void UneEntiteQuiSEnfonceEstRepousseeAuBordDeLObstacle()
    {
        var (x, y) = ArenaLayout.PushOut(105f, 100f, 100f, 100f, 30f);

        Assert.Equal(130f, x, 3);   // repoussée le long de l'axe d'entrée
        Assert.Equal(100f, y, 3);
    }

    [Fact]
    public void UneEntiteHorsDeLObstacleNEstPasDeplacee()
    {
        var (x, y) = ArenaLayout.PushOut(200f, 100f, 100f, 100f, 30f);

        Assert.Equal(200f, x, 3);
        Assert.Equal(100f, y, 3);
    }

    /// <summary>
    /// Pile au centre : la normalisation diviserait par zéro et l'entité partirait en <c>NaN</c> —
    /// c'est-à-dire disparaîtrait de la carte, sans erreur.
    /// </summary>
    [Fact]
    public void PileAuCentreLEcartementResteFini()
    {
        var (x, y) = ArenaLayout.PushOut(100f, 100f, 100f, 100f, 30f);

        Assert.False(float.IsNaN(x) || float.IsNaN(y));
        Assert.Equal(130f, x, 3);
        Assert.Equal(100f, y, 3);
    }
}
