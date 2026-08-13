using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Sonde d'entrée clavier — outil de diagnostic, activé par <c>--input-probe</c>.
///
/// <para>Elle existe pour une raison précise : au menu principal, <b>Entrée et Échap agissent, les
/// flèches non</b>. Submit et Cancel d'un côté, la navigation de l'autre — deux chemins distincts,
/// et seul le second est muet. Deviner lequel des deux ment coûte un aller-retour de build par
/// hypothèse ; le relever coûte un seul.</para>
///
/// <para>Depuis le passage au paquet Input System, elle relève aussi <b>la présence des
/// périphériques</b> : un <c>Keyboard.current</c> nul explique à lui seul un jeu qui n'obéit à rien,
/// et ne se distingue pas, à l'écran, d'un module d'entrée mal configuré.</para>
///
/// <para>Elle ne journalise que les frames où <b>quelque chose</b> est pressé : un relevé par frame
/// noierait le log sous 60 lignes par seconde.</para>
/// </summary>
public sealed class InputProbe : MonoBehaviour
{
    private const string Flag = "--input-probe";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg != Flag) continue;

            var go = new GameObject("[SondeEntree]", typeof(InputProbe));
            DontDestroyOnLoad(go);
            Debug.Log("[SONDE] sonde d'entree active.");
            return;
        }
    }

    private int _frames;

    private void Update()
    {
        var keyboard = Keyboard.current;
        var move = InputRemap.MoveVector();
        bool down = keyboard?.downArrowKey.isPressed == true;
        bool anyKey = keyboard?.anyKey.isPressed == true;

        if (!anyKey && move == Vector2.zero)
        {
            // Un relevé périodique même au repos : sans lui, un module d'entrée absent ne se
            // distingue pas d'un joueur qui n'a rien pressé.
            if (++_frames % 300 != 0) return;
        }

        var es = EventSystem.current;
        Debug.Log($"[SONDE] v={move.y:F2} h={move.x:F2} flecheBas={down} anyKey={anyKey} " +
                  $"clavier={(keyboard != null ? "oui" : "ABSENT")} " +
                  $"souris={(Mouse.current != null ? "oui" : "absente")} " +
                  $"manette={(Gamepad.current != null ? "oui" : "absente")} " +
                  $"module={(es != null ? es.currentInputModule?.GetType().Name ?? "aucun" : "PAS D'EVENTSYSTEM")} " +
                  $"focusApp={(es != null ? es.isFocused.ToString() : "?")} " +
                  $"nav={(es != null ? es.sendNavigationEvents.ToString() : "?")} " +
                  $"selection={(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "AUCUNE")}");
    }
}
