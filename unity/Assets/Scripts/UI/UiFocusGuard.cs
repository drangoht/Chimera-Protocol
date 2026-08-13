using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Garde la sélection clavier/manette <b>vivante</b>.
///
/// <para><b>Le défaut qu'il corrige</b> : le module d'entrée d'uGUI appelle
/// <c>SetSelectedGameObject(null)</c> à chaque clic qui ne tombe sur aucun élément — l'ancien
/// <c>StandaloneInputModule</c> le faisait sans recours, et <c>InputSystemUIInputModule</c> le fait
/// toujours (<c>deselectOnBackgroundClick</c>, actif par défaut). Un joueur qui
/// clique une fois à côté d'un bouton — ou qui ferme un écran à la souris — n'a plus aucune
/// sélection, et les flèches ne font alors <b>plus rien du tout</b> jusqu'à ce qu'il reclique sur un
/// bouton. Sous Godot, le focus survit au clic dans le vide ; c'est un écart de moteur, pas un choix
/// de design, et il se manifeste exactement comme « le clavier ne marche pas ».</para>
///
/// <para>Le même garde traite le second cas : l'élément sélectionné <b>disparaît</b> (écran fermé,
/// liste reconstruite, bouton désactivé). Unity laisse alors une sélection morte, avec le même
/// résultat.</para>
///
/// <para>Il ne <b>choisit</b> jamais : il rétablit le dernier élément valide connu, et à défaut le
/// premier élément navigable de l'écran actif. Prendre l'initiative de sélectionner autre chose
/// déplacerait le curseur du joueur sans qu'il l'ait demandé.</para>
/// </summary>
public sealed class UiFocusGuard : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var go = new GameObject("[GardeFocus]", typeof(UiFocusGuard));
        DontDestroyOnLoad(go);
    }

    private GameObject? _last;

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        var current = es.currentSelectedGameObject;

        if (IsUsable(current))
        {
            _last = current;
            return;
        }

        // Ne rien faire tant qu'aucune sélection valide n'a jamais existé : au tout premier instant
        // d'une scène, les écrans n'ont pas encore posé leur focus initial, et le devancer ferait
        // porter la sélection sur un élément arbitraire.
        if (IsUsable(_last))
        {
            es.SetSelectedGameObject(_last);
            return;
        }

        var fallback = FirstNavigable();
        if (fallback != null)
        {
            _last = fallback;
            es.SetSelectedGameObject(fallback);
        }
    }

    private static bool IsUsable(GameObject? go)
    {
        if (go == null || !go.activeInHierarchy) return false;

        var selectable = go.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }

    /// <summary>
    /// Premier élément navigable de l'interface <b>la plus au-dessus</b> : une modale ouverte
    /// par-dessus un menu doit récupérer le focus, jamais l'écran qu'elle recouvre.
    /// </summary>
    private static GameObject? FirstNavigable()
    {
        Selectable? best = null;
        int bestOrder = int.MinValue;

        foreach (var selectable in Selectable.allSelectablesArray)
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy) continue;
            if (!selectable.IsInteractable()) continue;
            if (selectable.navigation.mode == Navigation.Mode.None) continue;

            var canvas = selectable.GetComponentInParent<Canvas>();
            int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;

            if (order <= bestOrder) continue;

            bestOrder = order;
            best = selectable;
        }

        return best != null ? best.gameObject : null;
    }
}
