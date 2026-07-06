using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Coordinateur d'écrans modaux qui mettent le jeu en pause (LevelUpScreen, AssimilationScreen).
/// Garantit qu'UN SEUL écran est présenté à la fois et que <c>GetTree().Paused</c> n'est remis à
/// <c>false</c> QUE lorsque la file est vide (piège critique §13.2 : deux systèmes qui togglent
/// Paused indépendamment se marchent dessus et gèlent/dégèlent la physique à contretemps).
///
/// Chaque écran soumet une « présentation » (callback qui affiche son contenu) via <see cref="Submit"/>
/// et signale la résolution du joueur via <see cref="Done"/>. Le level-up est prioritaire : sa file
/// est vidée avant celle de l'assimilation. Statique (pas de nœud) : réinitialisé par run via
/// <see cref="Reset"/> pour ne pas fuir un état bloqué entre deux parties.
/// </summary>
public static class ModalQueue
{
    private static readonly Queue<Action> _highPriority = new(); // level-up
    private static readonly Queue<Action> _lowPriority  = new(); // assimilation
    private static bool _busy;
    private static SceneTree? _tree;

    /// <summary>Soumet une présentation modale. <paramref name="highPriority"/> = level-up (prioritaire).</summary>
    public static void Submit(SceneTree tree, Action show, bool highPriority)
    {
        _tree = tree;
        (highPriority ? _highPriority : _lowPriority).Enqueue(show);
        if (!_busy) Advance();
    }

    /// <summary>À appeler quand l'écran courant a été résolu (carte choisie, greffe assimilée/refusée…).</summary>
    public static void Done()
    {
        _busy = false;
        Advance();
    }

    /// <summary>Y a-t-il un écran modal ouvert (ou en attente) ?</summary>
    public static bool AnyOpen => _busy || _highPriority.Count > 0 || _lowPriority.Count > 0;

    /// <summary>Réinitialise l'état (début de run / changement de scène). Ne touche pas à Paused.</summary>
    public static void Reset()
    {
        _highPriority.Clear();
        _lowPriority.Clear();
        _busy = false;
    }

    private static void Advance()
    {
        var next = _highPriority.Count > 0 ? _highPriority.Dequeue()
                 : _lowPriority.Count  > 0 ? _lowPriority.Dequeue()
                 : null;

        if (next == null)
        {
            if (_tree != null && GodotObject.IsInstanceValid(_tree.Root)) _tree.Paused = false;
            return;
        }

        _busy = true;
        if (_tree != null) _tree.Paused = true;
        next();
    }
}
