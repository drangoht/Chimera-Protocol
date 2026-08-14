using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
/// couvrir les dispositions AZERTY et QWERTY sans réglage. <b>C'est la position physique de la
/// touche qui est liée, pas la lettre gravée dessus</b> — <c>Key.Z</c> et <c>Key.W</c> désignent
/// deux emplacements, et un clavier AZERTY grave « z » sur celui que QWERTY appelle W. Lier les
/// deux couvre donc les deux dispositions, exactement comme le faisaient les <c>KeyCode</c>.</para>
///
/// <para><b>Migration Input System (2026-08-13).</b> Le paquet remplace l'ancien Input Manager,
/// marqué pour dépréciation. Deux écarts de comportement méritaient d'être traités ici :</para>
/// <list type="bullet">
///   <item><description><c>Keyboard.current</c> peut être <b>nul</b> — au banc headless, dans un
///   build sans périphérique, ou pendant la frame qui suit un débranchement. L'ancien
///   <c>Input.GetKey</c> ne pouvait pas échouer ; ici, l'oubli d'un test se paie en
///   <c>NullReferenceException</c> à chaque frame. Toutes les lectures passent donc par
///   <see cref="Held"/>.</description></item>
///   <item><description><see cref="DisplayName"/> lit désormais le libellé <b>réel de la
///   disposition installée</b> (<c>InputControl.displayName</c>) : sur un clavier AZERTY, la touche
///   « aller en haut » s'annonce « Z » et non « W » comme le faisait l'ancien
///   <c>KeyCode.ToString()</c>. C'est précisément ce que la remarque de classe ci-dessus
///   exige.</description></item>
/// </list>
/// </summary>
public static class InputRemap
{
    private static readonly Dictionary<GameAction, List<Key>> _bindings = new()
    {
        [GameAction.MoveUp]    = new() { Key.Z, Key.W, Key.UpArrow },
        [GameAction.MoveDown]  = new() { Key.S, Key.DownArrow },
        [GameAction.MoveLeft]  = new() { Key.Q, Key.A, Key.LeftArrow },
        [GameAction.MoveRight] = new() { Key.D, Key.RightArrow },
        [GameAction.Dash]      = new() { Key.LeftShift, Key.RightShift },
    };

    /// <summary>Signalé après un remap, pour que l'interface rafraîchisse ses libellés.</summary>
    public static event Action? BindingsChanged;

    /// <summary>
    /// Le contrôle d'une touche, ou <c>null</c> s'il n'y a pas de clavier — le seul point du
    /// fichier qui touche au périphérique.
    /// </summary>
    private static UnityEngine.InputSystem.Controls.KeyControl? Held(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && key != Key.None ? keyboard[key] : null;
    }

    /// <summary>L'action est-elle maintenue ?</summary>
    public static bool IsPressed(GameAction action)
    {
        foreach (var key in _bindings[action])
            if (Held(key)?.isPressed == true) return true;
        return false;
    }

    /// <summary>
    /// L'action vient-elle d'être déclenchée cette frame ? Clavier <b>ou</b> bouton tactile.
    /// </summary>
    public static bool WasPressedThisFrame(GameAction action)
    {
        if (action == GameAction.Dash && TouchInput.DashPressedThisFrame()) return true;

        foreach (var key in _bindings[action])
            if (Held(key)?.wasPressedThisFrame == true) return true;
        return false;
    }

    /// <summary>
    /// Vecteur de déplacement, de norme ≤ 1 : les quatre actions directionnelles, ou le joystick
    /// tactile s'il est tenu.
    /// </summary>
    /// <remarks>
    /// <para><b>Pourquoi le tactile arrive ici et non chez l'appelant.</b> Ce vecteur est lu par
    /// <c>Player</c> et par la sonde du banc. Y ajouter un « ou bien le doigt » côté appelant ferait
    /// deux endroits à tenir d'accord, et le portage a déjà montré ce que cela donne — un chemin
    /// d'entrée porté à un seul de ses sites d'appel est un chemin qui marche à la démonstration et
    /// pas en jeu (les 14 armes muettes, la visée morte). Le point d'entrée reste unique ; seule sa
    /// source s'élargit.</para>
    ///
    /// <para>Le joystick l'emporte tant qu'il est tenu, sans se mélanger au clavier : additionner les
    /// deux permettrait de dépasser la norme 1, donc la vitesse maximale, sans qu'aucun plafond ne
    /// le dise. Sur une tablette avec clavier, lâcher le doigt rend la main aux touches à l'image
    /// suivante.</para>
    /// </remarks>
    public static Vector2 MoveVector()
    {
        if (TouchInput.StickHeld) return TouchInput.MoveVector();

        var v = new Vector2(
            (IsPressed(GameAction.MoveRight) ? 1f : 0f) - (IsPressed(GameAction.MoveLeft) ? 1f : 0f),
            (IsPressed(GameAction.MoveUp)    ? 1f : 0f) - (IsPressed(GameAction.MoveDown) ? 1f : 0f));

        return v.sqrMagnitude > 1f ? v.normalized : v;
    }

    /// <summary>
    /// Libellé de la <b>première</b> touche associée, pour l'affichage. À utiliser partout où une
    /// touche est annoncée au joueur — voir la remarque de classe.
    ///
    /// <para>Sans clavier branché, on ne peut pas connaître la disposition : le nom de la touche
    /// physique fait alors office de repli, plutôt qu'un tiret qui laisserait croire que l'action
    /// n'est liée à rien.</para>
    /// </summary>
    public static string DisplayName(GameAction action)
    {
        var keys = _bindings[action];
        if (keys.Count == 0) return "—";

        string? layout = Held(keys[0])?.displayName;
        return string.IsNullOrEmpty(layout) ? keys[0].ToString() : layout!.ToUpperInvariant();
    }

    /// <summary>Remplace les touches d'une action. Une liste vide est refusée.</summary>
    public static void Rebind(GameAction action, params Key[] keys)
    {
        if (keys == null || keys.Length == 0)
            throw new ArgumentException("Une action sans touche serait injouable et invisible.",
                                        nameof(keys));

        _bindings[action] = new List<Key>(keys);
        BindingsChanged?.Invoke();
    }
}
