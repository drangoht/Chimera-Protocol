using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sonde d'entrée clavier — outil de diagnostic, activé par <c>--input-probe</c>.
///
/// <para>Elle existe pour une raison précise : au menu principal, <b>Entrée et Échap agissent, les
/// flèches non</b>. Submit et Cancel passent par <c>Input.GetButtonDown</c>, la navigation par
/// <c>Input.GetAxisRaw</c> — deux chemins distincts, et seul le second est muet. Deviner lequel des
/// deux ment coûte un aller-retour de build par hypothèse ; le relever coûte un seul.</para>
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
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        bool down = Input.GetKey(KeyCode.DownArrow);
        bool anyKey = Input.anyKey;

        if (!anyKey && Mathf.Approximately(v, 0f) && Mathf.Approximately(h, 0f))
        {
            // Un relevé périodique même au repos : sans lui, un module d'entrée absent ne se
            // distingue pas d'un joueur qui n'a rien pressé.
            if (++_frames % 300 != 0) return;
        }

        var es = EventSystem.current;
        Debug.Log($"[SONDE] v={v:F2} h={h:F2} flecheBas={down} anyKey={anyKey} " +
                  $"module={(es != null ? es.currentInputModule?.GetType().Name ?? "aucun" : "PAS D'EVENTSYSTEM")} " +
                  $"focusApp={(es != null ? es.isFocused.ToString() : "?")} " +
                  $"nav={(es != null ? es.sendNavigationEvents.ToString() : "?")} " +
                  $"selection={(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "AUCUNE")}");
    }
}
