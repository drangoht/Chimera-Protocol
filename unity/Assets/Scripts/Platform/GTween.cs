using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adaptateur d'interpolation reproduisant la <b>forme</b> de l'API <c>Tween</c> de Godot
/// (docs/UNITY_MIGRATION_PLAN.md §4.1).
///
/// <para><b>Pourquoi un shim plutôt que DOTween.</b> Le projet compte <b>502 sites d'appel</b> à
/// <c>Tween</c>. Adopter une bibliothèque tierce reviendrait à réécrire ces 502 sites dans un autre
/// idiome ; reproduire la forme de l'API Godot les rend portables presque sans édition. C'est un
/// travail d'infrastructure borné plutôt qu'un travail de traduction diffus sur cinq dossiers.</para>
///
/// <para><b>Une différence assumée avec Godot : les propriétés sont désignées par un
/// <i>lambda</i>, pas par une chaîne.</b> Godot écrit <c>TweenProperty(node, "modulate:a", …)</c>,
/// ce qui suppose de la réflexion à l'exécution — fragile sous IL2CPP, et non vérifié par le
/// compilateur. Ici, <c>v =&gt; img.color = v</c> est vérifié à la compilation et fonctionne en AOT.
/// Le renommage d'un champ devient une erreur de compilation au lieu d'une animation qui cesse
/// silencieusement de fonctionner.</para>
///
/// <para><b>Fidélités conservées</b> :</para>
/// <list type="bullet">
///   <item>une interpolation <b>meurt avec son propriétaire</b>, comme un Tween lié à son nœud sous
///         Godot — sinon un rappel toucherait un objet détruit ;</item>
///   <item>le séquencement (étapes, parallèle, boucles, valeur finale exacte) est délégué à
///         <see cref="TweenTimeline"/>, testé sans moteur ;</item>
///   <item>les courbes sont celles de <see cref="Easing"/>, relevées sur le moteur.</item>
/// </list>
/// </summary>
public sealed class GTween
{
    private readonly TweenTimeline _timeline = new();
    private readonly List<Action<double>> _appliers = new();
    private readonly List<Action> _callbacks = new();

    private readonly UnityEngine.Object? _owner;
    private readonly bool _boundToOwner;
    private readonly bool _ignoreTimeScale;

    private GTween(UnityEngine.Object? owner, bool ignoreTimeScale)
    {
        _owner           = owner;
        _boundToOwner    = owner != null;
        _ignoreTimeScale = ignoreTimeScale;

        _timeline.ValueUpdated  += (h, v) => _appliers[h](v);
        _timeline.CallbackFired += h => _callbacks[h]();
        _timeline.Finished      += () => Finished?.Invoke();
    }

    /// <summary>Signalé quand la séquence s'achève (jamais si elle boucle indéfiniment).</summary>
    public event Action? Finished;

    /// <summary>Vrai quand l'interpolation peut être oubliée par l'hôte.</summary>
    public bool IsDone => _timeline.IsKilled || _timeline.IsFinished || OwnerIsGone;

    /// <summary>
    /// Le propriétaire a-t-il été détruit ? Repose sur la comparaison à <c>null</c> d'Unity, qui
    /// vaut vrai pour un objet détruit — c'est précisément le cas qu'on veut intercepter.
    /// </summary>
    private bool OwnerIsGone => _boundToOwner && _owner == null;

    /// <summary>
    /// Crée une interpolation — équivalent de <c>CreateTween()</c>.
    /// </summary>
    /// <param name="owner">
    /// Objet auquel lier la durée de vie (typiquement le <c>MonoBehaviour</c> appelant). S'il est
    /// détruit, l'interpolation s'arrête sans toucher à quoi que ce soit.
    /// </param>
    /// <param name="ignoreTimeScale">
    /// À mettre à vrai pour toute animation d'<b>interface</b>. La pause du jeu passe par
    /// <c>Time.timeScale = 0</c> ; une interpolation d'UI qui suivrait le temps mis à l'échelle se
    /// figerait avec le jeu — donc le menu de pause lui-même serait figé. C'est l'équivalent du
    /// <c>ProcessMode.Always</c> de Godot.
    /// </param>
    public static GTween Create(UnityEngine.Object? owner = null, bool ignoreTimeScale = false)
    {
        var t = new GTween(owner, ignoreTimeScale);
        PlatformHost.Instance.Register(t);
        return t;
    }

    // ─── Construction de la séquence ──────────────────────────────────────────

    /// <summary>Interpole un réel, dans une nouvelle étape.</summary>
    public GTween TweenFloat(Action<float> setter, float from, float to, float duration,
                             TransType trans = TransType.Linear, EaseType ease = EaseType.In,
                             float delay = 0f)
        => AddInterpolation(v => setter(Mathf.LerpUnclamped(from, to, (float)v)),
                            duration, trans, ease, delay, parallel: false);

    /// <summary>Ajoute une attente — équivalent de <c>tween_interval</c>.</summary>
    public GTween AppendInterval(float seconds)
    {
        _timeline.AppendInterval(seconds);
        return this;
    }

    /// <summary>Ajoute un rappel joué à son tour — équivalent de <c>tween_callback</c>.</summary>
    public GTween AppendCallback(Action callback)
    {
        _callbacks.Add(callback);
        _timeline.AppendCallback(_callbacks.Count - 1);
        return this;
    }

    /// <summary>Fixe le nombre de répétitions (0 = infini) — équivalent de <c>set_loops</c>.</summary>
    public GTween SetLoops(int loops)
    {
        _timeline.SetLoops(loops);
        return this;
    }

    /// <summary>Arrête l'interpolation — équivalent de <c>kill()</c>.</summary>
    public void Kill() => _timeline.Kill();

    private GTween AddInterpolation(Action<double> applier, float duration, TransType trans,
                                    EaseType ease, float delay, bool parallel)
    {
        _appliers.Add(applier);
        int handle = _appliers.Count - 1;

        if (parallel) _timeline.Join(handle, duration, trans, ease, delay);
        else          _timeline.Append(handle, duration, trans, ease, delay);

        return this;
    }

    // ─── Pilotage ─────────────────────────────────────────────────────────────

    internal void Tick(float scaledDt, float unscaledDt)
    {
        // Un propriétaire détruit doit interrompre l'interpolation AVANT toute application :
        // le setter capturerait sinon un objet Unity déjà détruit.
        if (OwnerIsGone) { _timeline.Kill(); return; }

        _timeline.Advance(_ignoreTimeScale ? unscaledDt : scaledDt);
    }
}
