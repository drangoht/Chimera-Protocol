using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Écrans modaux susceptibles de s'ouvrir pendant une run, par ordre de priorité.</summary>
public enum ModalKind
{
    /// <summary>Montée de niveau — <b>prioritaire</b> : elle interrompt la run à un instant précis.</summary>
    LevelUp,
    /// <summary>Assimilation (greffe) — attend son tour.</summary>
    Assimilation,
}

/// <summary>
/// Coordonne les écrans modaux de run (Lot 5).
///
/// <para><b>Le problème que cette classe résout.</b> Une montée de niveau et un seuil de greffe
/// peuvent survenir dans la <i>même frame</i>. Ouverts ensemble, les deux écrans se superposent et
/// se disputent le focus ; ouverts sans coordination, chacun met le jeu en pause puis le relance,
/// et le second reprend une partie que le premier croyait figée.</para>
///
/// <para>D'où la règle, reprise de Godot : <b>une seule pause, une seule modale à la fois</b>, la
/// montée de niveau passant devant.</para>
///
/// <para>⚠ Sous Unity, la pause est <c>Time.timeScale = 0</c>, qui fige <i>tout</i> ce qui lit
/// <c>Time.deltaTime</c> — y compris les animations de la modale elle-même. Les écrans modaux
/// doivent donc utiliser les variantes en temps réel
/// (<c>GTween.Create(…, ignoreTimeScale: true)</c>), sinon le menu qui met le jeu en pause se fige
/// avec lui.</para>
/// </summary>
public static class ModalQueue
{
    private static readonly List<ModalKind> _pending = new();
    private static ModalKind? _current;

    /// <summary>Modale ouverte, ou <c>null</c> si le jeu tourne.</summary>
    public static ModalKind? Current => _current;

    /// <summary>Une modale est-elle ouverte ?</summary>
    public static bool IsOpen => _current.HasValue;

    /// <summary>Modales en attente d'ouverture.</summary>
    public static int PendingCount => _pending.Count;

    /// <summary>Demande l'ouverture d'une modale ; ouvre si la voie est libre.</summary>
    public static event Action<ModalKind>? Opened;

    /// <summary>Signale la fermeture effective d'une modale.</summary>
    public static event Action<ModalKind>? Closed;

    /// <summary>
    /// Demande l'affichage d'une modale. Si une autre est déjà ouverte, la demande est mise en
    /// file. Une même modale ne peut pas être demandée deux fois simultanément : sans ce garde-fou,
    /// deux montées de niveau dans la même frame ouvriraient deux écrans identiques.
    /// </summary>
    public static void Request(ModalKind kind)
    {
        if (_current == kind) return;
        if (_pending.Contains(kind)) return;

        _pending.Add(kind);
        _pending.Sort((a, b) => a.CompareTo(b));   // LevelUp (0) passe devant Assimilation (1)

        // ⚠ L'ouverture est REPORTÉE à la fin de la frame, et ce n'est pas un détail. Une montée de
        // niveau et un seuil de greffe tombent souvent dans la même frame ; ouvrir dès la première
        // demande ferait gagner celle qui arrive en premier, et non la prioritaire. Le report
        // laisse toutes les demandes de la frame arriver avant de trancher.
        if (_openScheduled) return;
        _openScheduled = true;
        SceneRoot.CallDeferred(FlushOpen);
    }

    private static bool _openScheduled;

    private static void FlushOpen()
    {
        _openScheduled = false;
        TryOpenNext();
    }

    /// <summary>Ferme la modale courante et enchaîne sur la suivante s'il y en a une.</summary>
    public static void Close(ModalKind kind)
    {
        if (_current != kind) return;

        _current = null;
        Closed?.Invoke(kind);

        if (!TryOpenNext()) SetPaused(false);
    }

    /// <summary>
    /// Vide la file et lève la pause — à appeler sur un changement de scène ou une fin de run.
    /// Sans cela, une modale en attente au moment d'un retour au menu laisserait le jeu figé.
    /// </summary>
    public static void Reset()
    {
        _pending.Clear();
        _current = null;
        _openScheduled = false;
        SetPaused(false);
    }

    private static bool TryOpenNext()
    {
        if (_current.HasValue || _pending.Count == 0) return false;

        var next = _pending[0];
        _pending.RemoveAt(0);
        _current = next;

        SetPaused(true);
        Opened?.Invoke(next);
        return true;
    }

    private static void SetPaused(bool paused) => SceneRoot.Paused = paused;
}
