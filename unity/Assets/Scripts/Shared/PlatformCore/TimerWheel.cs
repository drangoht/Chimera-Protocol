using System;
using System.Collections.Generic;

/// <summary>
/// Minuteries à retard, remplaçant <c>GetTree().CreateTimer(x).Timeout += …</c> et les nœuds
/// <c>Timer</c> de Godot (163 sites d'appel — docs/UNITY_MIGRATION_PLAN.md §4.2).
///
/// <para><b>Pourquoi pas des coroutines Unity.</b> Une coroutine est attachée à un
/// <c>MonoBehaviour</c> et meurt avec lui, ce qui est souvent l'inverse du besoin ici (un délai qui
/// doit survivre à la mort de l'ennemi qui l'a déclenché). Une file centralisée reproduit le
/// comportement Godot, se met en pause d'un seul point, et — surtout — reste <b>testable sans
/// moteur</b>.</para>
///
/// <para><b>Trois décisions de sémantique</b>, chacune motivée par un bug qu'elle évite :</para>
/// <list type="number">
///   <item>Une minuterie ajoutée <i>pendant</i> <see cref="Tick"/> ne se déclenche <b>pas</b> dans
///         le même passage. Sans cette règle, une minuterie répétitive de délai nul boucle à
///         l'infini dès la première frame.</item>
///   <item>Une minuterie répétitive se déclenche <b>au plus une fois par passage</b>, le reliquat
///         étant reporté. Sinon un pic de latence (chargement, point d'arrêt) produirait une rafale
///         de déclenchements rattrapant le retard — visuellement, une salve d'effets.</item>
///   <item>Un délai négatif ou nul se déclenche au <b>prochain</b> passage, jamais immédiatement :
///         c'est ce qui distingue une minuterie d'un appel direct.</item>
/// </list>
///
/// <para>Logique pure : testable par la suite xUnit.</para>
/// </summary>
public sealed class TimerWheel
{
    private sealed class Entry
    {
        public int      Id;
        public double   Remaining;
        public double   Interval;
        public bool     Repeat;
        public Action   Callback = null!;
        public bool     Cancelled;
    }

    private readonly List<Entry> _entries = new();
    private readonly List<Entry> _added   = new();
    private int  _nextId = 1;
    private bool _ticking;

    /// <summary>Minuteries actives (celles ajoutées pendant un <see cref="Tick"/> comprises).</summary>
    public int Count
    {
        get
        {
            int n = 0;
            foreach (var e in _entries) if (!e.Cancelled) n++;
            foreach (var e in _added)   if (!e.Cancelled) n++;
            return n;
        }
    }

    /// <summary>
    /// Programme <paramref name="callback"/> dans <paramref name="delaySeconds"/> secondes.
    /// Renvoie un identifiant utilisable avec <see cref="Cancel"/>.
    /// </summary>
    /// <param name="repeat">Si vrai, se reprogramme indéfiniment au même intervalle.</param>
    public int Add(double delaySeconds, Action callback, bool repeat = false)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));
        if (repeat && delaySeconds <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(delaySeconds),
                "Une minuterie répétitive exige un intervalle strictement positif — sinon elle " +
                "monopoliserait chaque passage.");

        var e = new Entry
        {
            Id        = _nextId++,
            Remaining = delaySeconds,
            Interval  = delaySeconds,
            Repeat    = repeat,
            Callback  = callback,
        };

        // Pendant un Tick, on met de côté : se déclencher dans le passage courant serait une
        // exécution immédiate déguisée (cf. règle 1).
        (_ticking ? _added : _entries).Add(e);
        return e.Id;
    }

    /// <summary>Annule une minuterie. Sans effet si elle a déjà expiré ou n'existe pas.</summary>
    public bool Cancel(int id)
    {
        foreach (var e in _entries) if (e.Id == id && !e.Cancelled) { e.Cancelled = true; return true; }
        foreach (var e in _added)   if (e.Id == id && !e.Cancelled) { e.Cancelled = true; return true; }
        return false;
    }

    /// <summary>Annule toutes les minuteries — pour un changement de scène.</summary>
    public void Clear()
    {
        foreach (var e in _entries) e.Cancelled = true;
        foreach (var e in _added)   e.Cancelled = true;
    }

    /// <summary>
    /// Avance le temps de <paramref name="deltaSeconds"/> et déclenche ce qui est dû, dans l'ordre
    /// de programmation. Renvoie le nombre de déclenchements.
    /// </summary>
    public int Tick(double deltaSeconds)
    {
        if (_ticking) return 0;          // pas de Tick imbriqué
        if (deltaSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        _ticking = true;
        int fired = 0;
        try
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Cancelled) continue;

                e.Remaining -= deltaSeconds;
                if (e.Remaining > 0.0) continue;

                if (e.Repeat)
                {
                    // Reliquat reporté, sans rattrapage : au plus un déclenchement par passage
                    // (cf. règle 2). On borne pour éviter une dérive après une très longue frame.
                    e.Remaining += e.Interval;
                    if (e.Remaining <= 0.0) e.Remaining = e.Interval;
                }
                else
                {
                    e.Cancelled = true;
                }

                e.Callback.Invoke();
                fired++;
            }
        }
        finally
        {
            _ticking = false;
            _entries.RemoveAll(x => x.Cancelled);
            if (_added.Count > 0) { _entries.AddRange(_added); _added.Clear(); }
        }

        return fired;
    }
}
