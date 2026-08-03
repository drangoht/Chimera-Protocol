using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Contrat du séquencement de <c>Tween</c> — la partie où se logent les vrais bugs d'animation.
/// L'adaptateur moteur n'y ajoute que « quel objet, quelle propriété » ; tout ce qui touche au
/// temps, à l'ordre et aux valeurs limites se vérifie ici, sans Unity.
/// </summary>
public class TweenTimelineTests
{
    /// <summary>Enregistre ce qu'émet une séquence, pour l'inspecter dans les assertions.</summary>
    private sealed class Recorder
    {
        public readonly List<(int Handle, double Value)> Values = new();
        public readonly List<int> Callbacks = new();
        public int FinishedCount;

        public Recorder(TweenTimeline t)
        {
            t.ValueUpdated  += (h, v) => Values.Add((h, v));
            t.CallbackFired += h => Callbacks.Add(h);
            t.Finished      += () => FinishedCount++;
        }

        public double Last(int handle)
        {
            for (int i = Values.Count - 1; i >= 0; i--) if (Values[i].Handle == handle) return Values[i].Value;
            throw new InvalidOperationException($"aucune valeur émise pour le handle {handle}");
        }
    }

    // ─── Séquencement ─────────────────────────────────────────────────────────

    [Fact]
    public void Append_EnchaineLesEtapes()
    {
        var t = new TweenTimeline().Append(1, 1.0).Append(2, 1.0);
        Assert.Equal(2, t.StepCount);
        Assert.Equal(2.0, t.Duration, 9);
    }

    [Fact]
    public void Join_JoueEnParallele_DansLaMemeEtape()
    {
        var t = new TweenTimeline().Append(1, 1.0).Join(2, 1.0);
        Assert.Equal(1, t.StepCount);
        Assert.Equal(1.0, t.Duration, 9);
    }

    [Fact]
    public void LaDureeDUneEtapeEstCelleDeSonEntreeLaPlusLongue()
    {
        var t = new TweenTimeline().Append(1, 0.5).Join(2, 2.0).Join(3, 1.0);
        Assert.Equal(2.0, t.Duration, 9);
    }

    [Fact]
    public void UnDelaiDecaleLEntreeDansSonEtape()
    {
        var t = new TweenTimeline().Append(1, 1.0, delay: 0.5);
        Assert.Equal(1.5, t.Duration, 9);
    }

    [Fact]
    public void LesEtapesSeJouentDansLOrdre()
    {
        var t = new TweenTimeline().Append(1, 1.0).Append(2, 1.0);
        var r = new Recorder(t);

        t.Advance(0.5);
        Assert.Contains(r.Values, v => v.Handle == 1);
        Assert.DoesNotContain(r.Values, v => v.Handle == 2);

        t.Advance(1.0);
        Assert.Contains(r.Values, v => v.Handle == 2);
    }

    // ─── Le contrat central ───────────────────────────────────────────────────

    /// <summary>
    /// Une frame longue ne doit jamais laisser une propriété à 0,98 de sa cible. C'est le bug
    /// classique du tween : invisible en test manuel, systématique en conditions de charge.
    /// </summary>
    [Fact]
    public void UneEntreeRecoitToujoursSaValeurFinaleExacte_MemeSiLePasDepasse()
    {
        var t = new TweenTimeline().Append(1, 1.0);
        var r = new Recorder(t);

        t.Advance(97.0);   // dépassement massif

        Assert.Equal(1.0, r.Last(1), 12);
    }

    [Fact]
    public void ToutesLesEtapesTraverseesParUnGrandPasSontEmises()
    {
        var t = new TweenTimeline().Append(1, 0.1).Append(2, 0.1).Append(3, 0.1);
        var r = new Recorder(t);

        t.Advance(10.0);

        Assert.Equal(1.0, r.Last(1), 12);
        Assert.Equal(1.0, r.Last(2), 12);
        Assert.Equal(1.0, r.Last(3), 12);
    }

    [Fact]
    public void LaValeurFinaleNEstEmiseQuUneFois()
    {
        var t = new TweenTimeline().Append(1, 1.0);
        var r = new Recorder(t);

        t.Advance(2.0);
        int after = r.Values.Count;
        t.Advance(2.0);

        Assert.Equal(after, r.Values.Count);
    }

    [Fact]
    public void LaProgressionEstAttenueeParLaCourbe()
    {
        var t = new TweenTimeline().Append(1, 1.0, TransType.Quad, EaseType.In);
        var r = new Recorder(t);

        t.Advance(0.5);

        Assert.Equal(0.25, r.Last(1), 6);   // Quad/In à mi-course
    }

    // ─── Rappels ──────────────────────────────────────────────────────────────

    [Fact]
    public void UnRappelSeDeclencheAuBonMomentDeLaSequence()
    {
        var t = new TweenTimeline().Append(1, 1.0).AppendCallback(7);
        var r = new Recorder(t);

        t.Advance(0.5);
        Assert.Empty(r.Callbacks);

        t.Advance(0.6);
        Assert.Equal(new[] { 7 }, r.Callbacks);
    }

    [Fact]
    public void UnRappelNeSeDeclencheQuUneFoisParPasse()
    {
        var t = new TweenTimeline().AppendCallback(7).AppendInterval(1.0);
        var r = new Recorder(t);

        t.Advance(0.1);
        t.Advance(0.1);
        t.Advance(0.1);

        Assert.Single(r.Callbacks);
    }

    [Fact]
    public void UneSequenceDeRappelsSeulsSExecuteEtSeTermine()
    {
        var t = new TweenTimeline().AppendCallback(1).AppendCallback(2);
        var r = new Recorder(t);

        t.Advance(0.0);

        Assert.Equal(new[] { 1, 2 }, r.Callbacks);
        Assert.True(t.IsFinished);
    }

    // ─── Fin et répétitions ───────────────────────────────────────────────────

    [Fact]
    public void FinishedEstSignaleUneSeuleFois()
    {
        var t = new TweenTimeline().Append(1, 1.0);
        var r = new Recorder(t);

        t.Advance(2.0);
        t.Advance(2.0);

        Assert.Equal(1, r.FinishedCount);
        Assert.True(t.IsFinished);
    }

    [Fact]
    public void SetLoops_RejoueLaSequence()
    {
        var t = new TweenTimeline().AppendCallback(9).AppendInterval(1.0).SetLoops(3);
        var r = new Recorder(t);

        t.Advance(3.5);

        Assert.Equal(3, r.Callbacks.Count);
        Assert.True(t.IsFinished);
    }

    [Fact]
    public void SetLoopsZero_NeSeTermineJamais()
    {
        var t = new TweenTimeline().AppendCallback(9).AppendInterval(0.5).SetLoops(0);
        var r = new Recorder(t);

        t.Advance(10.0);

        Assert.False(t.IsFinished);
        Assert.True(r.Callbacks.Count >= 10);
    }

    /// <summary>Sans garde, une séquence de durée nulle répétée boucle à l'infini dans Advance.</summary>
    [Fact]
    public void UneSequenceDeDureeNulleEnBoucleNeGelePas()
    {
        var t = new TweenTimeline().AppendCallback(1).SetLoops(0);
        var r = new Recorder(t);

        t.Advance(1.0);   // doit rendre la main

        Assert.True(t.IsFinished);
        Assert.Single(r.Callbacks);
    }

    [Fact]
    public void Kill_ArreteToutSansSignaler()
    {
        var t = new TweenTimeline().Append(1, 1.0);
        var r = new Recorder(t);

        t.Kill();
        t.Advance(2.0);

        Assert.Empty(r.Values);
        Assert.Equal(0, r.FinishedCount);
        Assert.True(t.IsKilled);
    }

    // ─── Robustesse ───────────────────────────────────────────────────────────

    [Fact]
    public void Advance_RefuseUnDeltaNegatif()
    {
        var t = new TweenTimeline().Append(1, 1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => t.Advance(-1.0));
    }

    [Fact]
    public void Append_RefuseUneDureeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TweenTimeline().Append(1, -1.0));
    }

    [Fact]
    public void SetLoops_RefuseUnNombreNegatif()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TweenTimeline().SetLoops(-2));
    }

    [Fact]
    public void UneSequenceVideSeTermineImmediatement()
    {
        var t = new TweenTimeline();
        var r = new Recorder(t);

        t.Advance(0.016);

        Assert.True(t.IsFinished);
        Assert.Equal(1, r.FinishedCount);
    }

    [Fact]
    public void LesEntreesParallelesProgressentEnsemble()
    {
        var t = new TweenTimeline().Append(1, 1.0).Join(2, 1.0);
        var r = new Recorder(t);

        t.Advance(0.5);

        Assert.Equal(0.5, r.Last(1), 6);
        Assert.Equal(0.5, r.Last(2), 6);
    }

    [Fact]
    public void UneEntreeRetardeeNeCommencePasAvantSonDelai()
    {
        var t = new TweenTimeline().Append(1, 1.0).Join(2, 0.5, delay: 0.5);
        var r = new Recorder(t);

        t.Advance(0.25);
        Assert.DoesNotContain(r.Values, v => v.Handle == 2);

        t.Advance(0.5);
        Assert.Contains(r.Values, v => v.Handle == 2);
    }
}
