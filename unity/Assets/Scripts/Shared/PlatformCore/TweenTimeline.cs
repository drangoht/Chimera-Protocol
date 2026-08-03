using System;
using System.Collections.Generic;

/// <summary>Nature d'une entrée de séquence.</summary>
public enum TweenerKind
{
    /// <summary>Interpole une valeur pendant une durée.</summary>
    Interpolate,
    /// <summary>Déclenche un rappel à un instant donné (durée nulle).</summary>
    Callback,
    /// <summary>Attente pure — occupe du temps sans rien produire.</summary>
    Interval,
}

/// <summary>
/// Séquencement d'un <c>Tween</c> Godot : suite d'<b>étapes</b> jouées l'une après l'autre, chaque
/// étape pouvant contenir plusieurs entrées jouées <b>en parallèle</b>
/// (docs/UNITY_MIGRATION_PLAN.md §4.1).
///
/// <para><b>Séparation des rôles.</b> Cette classe ne connaît ni objet, ni propriété, ni moteur :
/// elle dit seulement « à cet instant, l'entrée <c>handle</c> en est à <c>t</c> ». C'est
/// l'adaptateur moteur qui traduit un handle en « la couleur de ce Label ». C'est ce qui rend tout
/// le séquencement — la partie où se logent les vrais bugs — testable sans Unity.</para>
///
/// <para><b>Le contrat qui compte le plus</b> : une entrée reçoit <b>toujours</b> sa valeur finale
/// exacte (<c>t = 1</c>), même si le pas de temps dépasse la fin de son intervalle. Sans cette
/// garantie, une frame longue laisse des propriétés à 0,98 de leur cible — le bug classique du
/// tween : un panneau qui n'arrive jamais tout à fait à sa place, et que rien ne signale.</para>
/// </summary>
public sealed class TweenTimeline
{
    private sealed class Entry
    {
        public TweenerKind Kind;
        public double      Delay;
        public double      Duration;
        public TransType   Trans;
        public EaseType    Ease;
        public int         Handle;
        public bool        FinalEmitted;
        public bool        CallbackFired;

        public double Start => Delay;
        public double End   => Delay + Duration;
    }

    private sealed class Step
    {
        public readonly List<Entry> Entries = new();
        public double StartOffset;
        public double Duration;
    }

    private readonly List<Step> _steps = new();
    private bool   _dirty = true;
    private double _totalDuration;

    private double _elapsed;
    private int    _loopsDone;

    /// <summary>Nombre de répétitions ; 0 = infini, 1 = une seule passe (défaut).</summary>
    public int Loops { get; private set; } = 1;

    /// <summary>Vrai quand la séquence est terminée (jamais si <see cref="Loops"/> vaut 0).</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Séquence tuée : elle n'avance plus et ne signale plus rien.</summary>
    public bool IsKilled { get; private set; }

    /// <summary>Durée d'une passe complète, en secondes.</summary>
    public double Duration { get { EnsureLayout(); return _totalDuration; } }

    /// <summary>Temps écoulé dans la passe courante.</summary>
    public double Elapsed => _elapsed;

    /// <summary>Étapes séquentielles.</summary>
    public int StepCount => _steps.Count;

    /// <summary>Progression d'une entrée : <c>(handle, valeur atténuée dans [0,1])</c>.</summary>
    public event Action<int, double>? ValueUpdated;

    /// <summary>Un rappel de séquence est atteint : <c>(handle)</c>.</summary>
    public event Action<int>? CallbackFired;

    /// <summary>La séquence vient de se terminer (toutes répétitions comprises).</summary>
    public event Action? Finished;

    // ─── Construction ─────────────────────────────────────────────────────────

    /// <summary>Ajoute une interpolation dans une <b>nouvelle</b> étape (comportement par défaut).</summary>
    public TweenTimeline Append(int handle, double duration, TransType trans = TransType.Linear,
                                EaseType ease = EaseType.In, double delay = 0.0)
        => AddEntry(newStep: true, TweenerKind.Interpolate, handle, duration, trans, ease, delay);

    /// <summary>
    /// Ajoute une interpolation dans <b>l'étape courante</b>, donc en parallèle de la précédente —
    /// équivalent de <c>parallel()</c> / <c>set_parallel(true)</c>.
    /// </summary>
    public TweenTimeline Join(int handle, double duration, TransType trans = TransType.Linear,
                              EaseType ease = EaseType.In, double delay = 0.0)
        => AddEntry(newStep: false, TweenerKind.Interpolate, handle, duration, trans, ease, delay);

    /// <summary>Ajoute un rappel, joué à son tour dans la séquence.</summary>
    public TweenTimeline AppendCallback(int handle, double delay = 0.0)
        => AddEntry(true, TweenerKind.Callback, handle, 0.0, TransType.Linear, EaseType.In, delay);

    /// <summary>Ajoute une attente.</summary>
    public TweenTimeline AppendInterval(double duration)
        => AddEntry(true, TweenerKind.Interval, -1, duration, TransType.Linear, EaseType.In, 0.0);

    /// <summary>Fixe le nombre de répétitions (0 = infini).</summary>
    public TweenTimeline SetLoops(int loops)
    {
        if (loops < 0) throw new ArgumentOutOfRangeException(nameof(loops));
        Loops = loops;
        return this;
    }

    private TweenTimeline AddEntry(bool newStep, TweenerKind kind, int handle, double duration,
                                   TransType trans, EaseType ease, double delay)
    {
        if (duration < 0.0) throw new ArgumentOutOfRangeException(nameof(duration));
        if (delay    < 0.0) throw new ArgumentOutOfRangeException(nameof(delay));

        if (newStep || _steps.Count == 0) _steps.Add(new Step());

        _steps[^1].Entries.Add(new Entry
        {
            Kind = kind, Handle = handle, Duration = duration,
            Trans = trans, Ease = ease, Delay = delay,
        });

        _dirty = true;
        return this;
    }

    private void EnsureLayout()
    {
        if (!_dirty) return;

        double offset = 0.0;
        foreach (var step in _steps)
        {
            double longest = 0.0;
            foreach (var e in step.Entries) if (e.End > longest) longest = e.End;

            step.StartOffset = offset;
            step.Duration    = longest;
            offset          += longest;
        }

        _totalDuration = offset;
        _dirty = false;
    }

    // ─── Lecture ──────────────────────────────────────────────────────────────

    /// <summary>Arrête définitivement la séquence, sans émettre de valeur finale ni de signal.</summary>
    public void Kill() => IsKilled = true;

    /// <summary>
    /// Avance de <paramref name="deltaSeconds"/>. Émet les mises à jour de valeur et les rappels
    /// atteints, dans l'ordre chronologique, y compris si le pas traverse plusieurs étapes.
    /// </summary>
    public void Advance(double deltaSeconds)
    {
        if (IsKilled || IsFinished) return;
        if (deltaSeconds < 0.0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        EnsureLayout();

        // Séquence vide ou instantanée : on émet ce qu'il y a (des rappels, typiquement) puis on
        // termine. Boucler sur une durée nulle tournerait à l'infini.
        if (_totalDuration <= 0.0)
        {
            EmitWindow(0.0, double.MaxValue);
            Complete();
            return;
        }

        double remaining = deltaSeconds;
        while (remaining > 0.0 && !IsFinished && !IsKilled)
        {
            double before = _elapsed;
            double room   = _totalDuration - before;
            double step   = Math.Min(remaining, room);

            _elapsed += step;
            remaining -= step;

            EmitWindow(before, _elapsed);

            if (_elapsed < _totalDuration) break;

            // Fin d'une passe.
            _loopsDone++;
            if (Loops != 0 && _loopsDone >= Loops) { Complete(); return; }

            ResetForLoop();
            if (remaining <= 0.0) break;
        }
    }

    /// <summary>
    /// Émet tout ce qui tombe dans l'intervalle de temps ]from, to], plus les valeurs finales des
    /// entrées désormais dépassées.
    /// </summary>
    private void EmitWindow(double from, double to)
    {
        foreach (var stepItem in _steps)
        {
            foreach (var e in stepItem.Entries)
            {
                double start = stepItem.StartOffset + e.Start;
                double end   = stepItem.StartOffset + e.End;

                switch (e.Kind)
                {
                    case TweenerKind.Interval:
                        break;

                    case TweenerKind.Callback:
                        // Un rappel ne se déclenche qu'une fois par passe, dès que son instant est
                        // atteint — y compris à t = 0 (d'où le >= sur la borne basse).
                        if (!e.CallbackFired && to >= start && (from < start || start == 0.0))
                        {
                            e.CallbackFired = true;
                            CallbackFired?.Invoke(e.Handle);
                        }
                        break;

                    case TweenerKind.Interpolate:
                        if (to < start) break;               // pas encore commencé
                        if (e.FinalEmitted) break;           // déjà achevé

                        if (to >= end)
                        {
                            // Valeur finale EXACTE, quoi qu'il arrive au pas de temps.
                            e.FinalEmitted = true;
                            ValueUpdated?.Invoke(e.Handle, Easing.Evaluate(e.Trans, e.Ease, 1.0));
                        }
                        else
                        {
                            double raw = e.Duration <= 0.0 ? 1.0 : (to - start) / e.Duration;
                            ValueUpdated?.Invoke(e.Handle, Easing.Evaluate(e.Trans, e.Ease, raw));
                        }
                        break;
                }
            }
        }
    }

    private void ResetForLoop()
    {
        _elapsed = 0.0;
        foreach (var s in _steps)
            foreach (var e in s.Entries) { e.FinalEmitted = false; e.CallbackFired = false; }
    }

    private void Complete()
    {
        IsFinished = true;
        Finished?.Invoke();
    }
}
