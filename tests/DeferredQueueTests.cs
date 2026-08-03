using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Contrat de la file d'exécution différée qui remplace <c>CallDeferred</c> (57 sites d'appel).
/// L'ordre d'exécution et le traitement des ajouts en cours de drainage sont la raison d'être de
/// cette classe : s'ils sont faux, les symptômes seront des bugs d'une frame, très coûteux à
/// diagnostiquer plus tard.
/// </summary>
public class DeferredQueueTests
{
    [Fact]
    public void EnqueueNExecutePasImmediatement()
    {
        var q = new DeferredQueue();
        bool ran = false;
        q.Enqueue(() => ran = true);

        Assert.False(ran);
        Assert.Equal(1, q.Count);
    }

    [Fact]
    public void Flush_ExecuteDansLOrdreDAjout()
    {
        var q = new DeferredQueue();
        var order = new List<int>();
        for (int i = 0; i < 5; i++) { int n = i; q.Enqueue(() => order.Add(n)); }

        int executed = q.Flush();

        Assert.Equal(5, executed);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, order);
        Assert.Equal(0, q.Count);
    }

    /// <summary>
    /// Cœur du contrat : une action ajoutée pendant le drainage s'exécute dans le MÊME passage.
    /// Le reporter à la frame suivante introduirait une latence d'une image dans les chaînes du
    /// type « l'ennemi meurt → l'orbe apparaît ».
    /// </summary>
    [Fact]
    public void Flush_ExecuteAussiCeQuiEstAjoutePendantLeDrainage()
    {
        var q = new DeferredQueue();
        var order = new List<string>();

        q.Enqueue(() =>
        {
            order.Add("a");
            q.Enqueue(() => order.Add("b"));
        });

        int executed = q.Flush();

        Assert.Equal(2, executed);
        Assert.Equal(new[] { "a", "b" }, order);
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void Flush_SurFileVide_NeFaitRien()
    {
        var q = new DeferredQueue();
        Assert.Equal(0, q.Flush());
    }

    /// <summary>
    /// Une action qui se réenfile sans fin gèlerait le jeu <b>sans aucun message</b> — le pire mode
    /// de défaillance. Le drainage doit s'arrêter et le signaler.
    /// </summary>
    [Fact]
    public void Flush_InterrompUneBoucleInfinie_EtLaSignale()
    {
        var q = new DeferredQueue();
        int? reported = null;
        q.DrainLimitReached += pending => reported = pending;

        void Loop() => q.Enqueue(Loop);
        q.Enqueue(Loop);

        q.Flush();   // ne doit pas boucler indéfiniment

        Assert.NotNull(reported);
        Assert.Equal(0, q.Count);
    }

    /// <summary>
    /// Un <c>Flush</c> appelé depuis une action différée doit être ignoré : deux drainages imbriqués
    /// casseraient l'ordre d'exécution, et le drainage en cours prendra de toute façon la suite.
    /// </summary>
    [Fact]
    public void Flush_ReentrantEstIgnore()
    {
        var q = new DeferredQueue();
        var order = new List<string>();
        int nested = -1;

        q.Enqueue(() =>
        {
            order.Add("externe");
            q.Enqueue(() => order.Add("interne"));
            nested = q.Flush();          // réentrant
        });

        q.Flush();

        Assert.Equal(0, nested);
        Assert.Equal(new[] { "externe", "interne" }, order);
    }

    [Fact]
    public void IsDraining_EstVraiPendantLExecutionSeulement()
    {
        var q = new DeferredQueue();
        bool during = false;
        q.Enqueue(() => during = q.IsDraining);

        Assert.False(q.IsDraining);
        q.Flush();

        Assert.True(during);
        Assert.False(q.IsDraining);
    }

    [Fact]
    public void Clear_AbandonneSansExecuter()
    {
        var q = new DeferredQueue();
        bool ran = false;
        q.Enqueue(() => ran = true);

        q.Clear();
        q.Flush();

        Assert.False(ran);
    }

    [Fact]
    public void Enqueue_RefuseUneActionNulle()
    {
        var q = new DeferredQueue();
        Assert.Throws<ArgumentNullException>(() => q.Enqueue(null!));
    }
}
