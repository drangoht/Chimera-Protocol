using Godot;
using System.Collections.Generic;

/// <summary>
/// Actions de déplacement rebindables (<c>move_up/down/left/right</c>). Enregistre
/// les actions dans l'<see cref="InputMap"/> au démarrage avec des bindings par
/// défaut (ZQSD + flèches + manette), puis applique les touches personnalisées
/// persistées par <see cref="GameSettings"/>. Le <c>Player</c> lit ces actions via
/// <c>Input.GetVector</c> plutôt que les <c>ui_*</c> (que les menus utilisent pour
/// la navigation focus). Séparer les deux évite qu'un remap casse la nav clavier.
/// </summary>
public static class InputRemap
{
    public const string Up    = "move_up";
    public const string Down  = "move_down";
    public const string Left  = "move_left";
    public const string Right = "move_right";

    /// <summary>Action de ruade (greffe Servos Erratiques) : Maj gauche (clavier) / RB (manette).</summary>
    public const string Dash  = "dash";

    /// <summary>Actions rebindables, dans l'ordre d'affichage (haut/bas/gauche/droite).</summary>
    public static readonly string[] Actions = { Up, Down, Left, Right };

    /// <summary>Touche clavier principale par défaut (ZQSD, layout AZERTY par label).</summary>
    public static readonly Dictionary<string, Key> DefaultKeys = new()
    {
        { Up,    Key.Z },
        { Down,  Key.S },
        { Left,  Key.Q },
        { Right, Key.D },
    };

    /// <summary>Flèche directionnelle secondaire (toujours disponible, non rebindable).</summary>
    private static readonly Dictionary<string, Key> ArrowKeys = new()
    {
        { Up,    Key.Up },
        { Down,  Key.Down },
        { Left,  Key.Left },
        { Right, Key.Right },
    };

    /// <summary>Bouton D-pad manette (secondaire, fixe).</summary>
    private static readonly Dictionary<string, JoyButton> DpadButtons = new()
    {
        { Up,    JoyButton.DpadUp },
        { Down,  JoyButton.DpadDown },
        { Left,  JoyButton.DpadLeft },
        { Right, JoyButton.DpadRight },
    };

    /// <summary>Axe du stick gauche + signe (secondaire, fixe).</summary>
    private static readonly Dictionary<string, (JoyAxis Axis, float Value)> StickAxes = new()
    {
        { Up,    (JoyAxis.LeftY, -1f) },
        { Down,  (JoyAxis.LeftY,  1f) },
        { Left,  (JoyAxis.LeftX, -1f) },
        { Right, (JoyAxis.LeftX,  1f) },
    };

    /// <summary>Applique toutes les actions depuis les réglages (touches perso ou défauts).</summary>
    public static void ApplyAll(GameSettings settings)
    {
        foreach (var action in Actions)
            SetKey(action, settings.MoveKey(action));
    }

    /// <summary>(Re)construit une action : touche clavier principale + flèche + manette (d-pad & stick).</summary>
    public static void SetKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        InputMap.ActionEraseEvents(action);

        // Touche clavier principale (rebindable, par label pour respecter l'AZERTY).
        InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });

        // Flèche directionnelle (secondaire, toujours dispo).
        InputMap.ActionAddEvent(action, new InputEventKey { Keycode = ArrowKeys[action] });

        // Manette : D-pad + stick gauche.
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = DpadButtons[action] });
        var (axis, value) = StickAxes[action];
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
    }

    /// <summary>Enregistre les actions hors déplacement (dash) avec leurs bindings par défaut.
    /// Idempotent (n'écrase pas des bindings déjà présents). À appeler au boot (GameManager).</summary>
    public static void EnsureExtraActions()
    {
        if (!InputMap.HasAction(Dash)) InputMap.AddAction(Dash);
        if (InputMap.ActionGetEvents(Dash).Count == 0)
        {
            InputMap.ActionAddEvent(Dash, new InputEventKey { Keycode = Key.Shift });
            InputMap.ActionAddEvent(Dash, new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder });
        }
    }

    /// <summary>Nom lisible de la touche clavier principale d'une action (pour l'UI Options).</summary>
    public static string KeyName(Key key) => OS.GetKeycodeString(key);
}
