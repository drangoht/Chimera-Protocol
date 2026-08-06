using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unique <c>MonoBehaviour</c> qui fait tourner la couche d'adaptation : minuteries, exécution
/// différée et interpolations (docs/UNITY_MIGRATION_PLAN.md §4).
///
/// <para><b>Pourquoi un hôte unique plutôt qu'un composant par système.</b> Sous Godot, l'ordre
/// d'exécution est celui de l'arbre de scène et des AutoLoads, il est déterministe et se lit dans
/// <c>project.godot</c>. Unity ne garantit rien entre <c>MonoBehaviour</c>s sans configuration.
/// Concentrer l'ordonnancement dans une seule classe le rend <b>explicite et lisible</b>, au lieu
/// de le disperser dans des réglages d'exécution que personne ne pense à consulter.</para>
///
/// <para><b>L'ordre ci-dessous n'est pas arbitraire</b> : les minuteries et les interpolations
/// tournent en <c>Update</c>, l'exécution différée est vidée en <c>LateUpdate</c> — donc après tout
/// le code de jeu de la frame, ce qui reproduit « à la fin de la frame courante » de
/// <c>CallDeferred</c>. Vider la file en <c>Update</c> ferait s'exécuter les actions différées
/// <i>avant</i> une partie du code qui les a demandées.</para>
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class PlatformHost : MonoBehaviour
{
    private static PlatformHost? _instance;

    /// <summary>Crée l'hôte au premier besoin. Survit aux changements de scène.</summary>
    public static PlatformHost Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[PlatformHost]");
                _instance = go.AddComponent<PlatformHost>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>Minuteries soumises à <c>Time.timeScale</c> — le jeu, donc figées en pause.</summary>
    public readonly TimerWheel ScaledTimers = new();

    /// <summary>Minuteries en temps réel — l'UI, qui doit continuer à vivre pendant la pause.</summary>
    public readonly TimerWheel UnscaledTimers = new();

    /// <summary>File d'exécution différée, vidée en fin de frame.</summary>
    public readonly DeferredQueue Deferred = new();

    private readonly List<GTween> _tweens    = new();
    private readonly List<GTween> _tweenSwap = new();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        Deferred.DrainLimitReached += pending =>
            Debug.LogError($"[PlatformHost] Exécution différée interrompue : {pending} action(s) " +
                           "encore en attente après le nombre maximal de passages. Une action se " +
                           "réenfile probablement sans fin.");
    }

    internal void Register(GTween tween) => _tweens.Add(tween);

    private void Update()
    {
        float dt  = Time.deltaTime;
        float udt = Time.unscaledDeltaTime;

        ScaledTimers.Tick(dt);
        UnscaledTimers.Tick(udt);

        // ⚠ Le ralenti est avancé ICI, et non par la caméra ou par ce qui l'a déclenché : ceux-là
        // meurent pendant l'effet — le boss est détruit à l'image même où il déclenche le sien —
        // et le jeu resterait alors ralenti pour de bon.
        HitStop.Advance(udt);

        // On itère sur une copie : une interpolation peut en créer une autre depuis son rappel de
        // fin (enchaînement d'animations), ce qui modifierait la liste en cours de parcours.
        _tweenSwap.Clear();
        _tweenSwap.AddRange(_tweens);
        foreach (var t in _tweenSwap) t.Tick(dt, udt);

        _tweens.RemoveAll(t => t.IsDone);
    }

    private void LateUpdate() => Deferred.Flush();

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
