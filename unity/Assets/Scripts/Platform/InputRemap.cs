using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Actions de jeu remappables — reprises telles quelles de l'<c>InputMap</c> Godot.</summary>
public enum GameAction { MoveUp, MoveDown, MoveLeft, MoveRight, Dash }

/// <summary>
/// Table des touches, remplaçant l'<c>InputMap</c> de Godot et son <c>InputRemap</c>
/// (docs/UNITY_MIGRATION_PLAN.md §4.2).
///
/// <para><b>Pourquoi ne pas coder les touches en dur.</b> Les actions de déplacement sont
/// <b>rebindables</b> depuis l'écran Options, et le projet garde volontairement ces actions
/// séparées de la navigation de menu : sans cette séparation, remapper « aller à gauche » casserait
/// aussi le déplacement du focus dans les menus.</para>
///
/// <para><b>Un piège d'ergonomie que ce fichier doit préserver</b> : le projet a déjà perdu une
/// session entière parce qu'une capacité (le dash) n'annonçait sa touche <b>nulle part</b>. Le
/// libellé affiché doit donc toujours être <b>lu depuis cette table</b>
/// (<see cref="DisplayName"/>) et jamais écrit en dur dans un texte d'interface — sinon un remap
/// rend l'aide mensongère.</para>
///
/// <para>Les défauts reprennent ceux de Godot : ZQSD <b>et</b> WASD <b>et</b> les flèches, pour
/// couvrir les dispositions AZERTY et QWERTY sans réglage.</para>
/// </summary>
public static class InputRemap
{
    private static readonly Dictionary<GameAction, List<KeyCode>> _bindings = new()
    {
        [GameAction.MoveUp]    = new() { KeyCode.Z, KeyCode.W, KeyCode.UpArrow },
        [GameAction.MoveDown]  = new() { KeyCode.S, KeyCode.DownArrow },
        [GameAction.MoveLeft]  = new() { KeyCode.Q, KeyCode.A, KeyCode.LeftArrow },
        [GameAction.MoveRight] = new() { KeyCode.D, KeyCode.RightArrow },
        [GameAction.Dash]      = new() { KeyCode.LeftShift, KeyCode.RightShift },
    };

    /// <summary>Signalé après un remap, pour que l'interface rafraîchisse ses libellés.</summary>
    public static event Action? BindingsChanged;

    /// <summary>L'action est-elle maintenue ?</summary>
    public static bool IsPressed(GameAction action)
    {
        foreach (var key in _bindings[action])
            if (Input.GetKey(key)) return true;
        return false;
    }

    /// <summary>L'action vient-elle d'être déclenchée cette frame ?</summary>
    public static bool WasPressedThisFrame(GameAction action)
    {
        foreach (var key in _bindings[action])
            if (Input.GetKeyDown(key)) return true;
        return false;
    }

    /// <summary>Vecteur de déplacement normalisé, à partir des quatre actions directionnelles.</summary>
    public static Vector2 MoveVector()
    {
        var v = new Vector2(
            (IsPressed(GameAction.MoveRight) ? 1f : 0f) - (IsPressed(GameAction.MoveLeft) ? 1f : 0f),
            (IsPressed(GameAction.MoveUp)    ? 1f : 0f) - (IsPressed(GameAction.MoveDown) ? 1f : 0f));

        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    /// <summary>
    /// Libellé de la <b>première</b> touche associée, pour l'affichage. À utiliser partout où une
    /// touche est annoncée au joueur — voir la remarque de classe.
    /// </summary>
    public static string DisplayName(GameAction action)
    {
        var keys = _bindings[action];
        return keys.Count > 0 ? keys[0].ToString() : "—";
    }

    /// <summary>Touches actuellement associées à une action.</summary>
    public static IReadOnlyList<KeyCode> Bindings(GameAction action) => _bindings[action];

    /// <summary>Remplace les touches d'une action. Une liste vide est refusée.</summary>
    public static void Rebind(GameAction action, params KeyCode[] keys)
    {
        if (keys == null || keys.Length == 0)
            throw new ArgumentException("Une action sans touche serait injouable et invisible.",
                                        nameof(keys));

        _bindings[action] = new List<KeyCode>(keys);
        BindingsChanged?.Invoke();
    }
}
