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

    /// <summary>Touche clavier de dash par défaut (rebindable).</summary>
    public const Key DefaultDashKey = Key.Shift;

    /// <summary>Actions rebindables, dans l'ordre d'affichage (haut/bas/gauche/droite).</summary>
    public static readonly string[] Actions = { Up, Down, Left, Right };

    /// <summary>Action <c>ui_*</c> de navigation menu miroir de chaque action de déplacement.
    /// Permet de naviguer dans les menus/modals avec les touches de déplacement (ZQSD) en plus
    /// des flèches : la nav focus de Godot lit les <c>ui_*</c>, qui n'étaient bindées qu'aux flèches.</summary>
    private static readonly Dictionary<string, string> UiNav = new()
    {
        { Up,    "ui_up" },
        { Down,  "ui_down" },
        { Left,  "ui_left" },
        { Right, "ui_right" },
    };

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

    /// <summary>(Re)construit une action de déplacement <b>et</b> sa nav menu miroir (<c>ui_*</c>) :
    /// touche clavier principale + flèche + manette (d-pad &amp; stick).</summary>
    public static void SetKey(string action, Key key)
    {
        BuildDirectional(action, action, key);   // action de déplacement (Player)
        BuildDirectional(UiNav[action], action, key); // nav menu miroir (focus des Control)
    }

    /// <summary>Reconstruit une action directionnelle <paramref name="target"/> avec les événements
    /// de <paramref name="action"/> : touche clavier principale rebindable + flèche + d-pad + stick.</summary>
    private static void BuildDirectional(string target, string action, Key key)
    {
        if (!InputMap.HasAction(target)) InputMap.AddAction(target);
        InputMap.ActionEraseEvents(target);

        // Touche clavier principale (rebindable, par label pour respecter l'AZERTY).
        InputMap.ActionAddEvent(target, new InputEventKey { Keycode = key });

        // Flèche directionnelle (secondaire, toujours dispo).
        InputMap.ActionAddEvent(target, new InputEventKey { Keycode = ArrowKeys[action] });

        // Manette : D-pad + stick gauche.
        InputMap.ActionAddEvent(target, new InputEventJoypadButton { ButtonIndex = DpadButtons[action] });
        var (axis, value) = StickAxes[action];
        InputMap.ActionAddEvent(target, new InputEventJoypadMotion { Axis = axis, AxisValue = value });
    }

    /// <summary>Enregistre les actions hors déplacement (dash) avec leurs bindings par défaut.
    /// Idempotent (n'écrase pas des bindings déjà présents). À appeler au boot (GameManager).</summary>
    public static void EnsureExtraActions()
    {
        if (!InputMap.HasAction(Dash)) InputMap.AddAction(Dash);
        if (InputMap.ActionGetEvents(Dash).Count == 0)
            SetDashKey(DefaultDashKey);
    }

    /// <summary>(Re)construit l'action de dash : touche clavier principale (rebindable) + RB manette (fixe).</summary>
    public static void SetDashKey(Key key)
    {
        if (!InputMap.HasAction(Dash)) InputMap.AddAction(Dash);
        InputMap.ActionEraseEvents(Dash);
        InputMap.ActionAddEvent(Dash, new InputEventKey { Keycode = key });
        InputMap.ActionAddEvent(Dash, new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder });
    }

    /// <summary>Nom lisible de la touche clavier principale d'une action (pour l'UI Options).</summary>
    public static string KeyName(Key key) => OS.GetKeycodeString(key);
}
