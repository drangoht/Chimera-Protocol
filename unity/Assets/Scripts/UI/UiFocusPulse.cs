using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pulsation du cadre de l'élément qui a le <b>focus</b> — portage d'<c>AttachFocusPulse</c>.
///
/// <para>Le jeu se joue au clavier et à la manette : la sélection courante est la seule chose qui
/// dit au joueur « où » il est dans un écran. La variante <c>_focus</c> du cadre allume déjà le
/// liseré, mais sur un écran chargé — quatorze améliorations du Hub, une grille de codex — une
/// nuance fixe se perd. Une <b>pulsation</b> attire l'œil sans rien ajouter à l'écran.</para>
///
/// <para>Elle module la <b>luminosité</b> du cadre et non sa teinte : la couleur porte la catégorie
/// du bouton (cyan action, doré récompense, danger), et la faire varier brouillerait cette
/// lecture.</para>
/// </summary>
public sealed class UiFocusPulse : MonoBehaviour
{
    /// <summary>Battements par seconde. Assez lent pour ne pas fatiguer, assez net pour se voir.</summary>
    private const float Frequency = 1.6f;

    /// <summary>Amplitude de la modulation de luminosité, autour de 1.</summary>
    private const float Amplitude = 0.18f;

    private Button? _button;
    private Image? _image;

    internal void Bind(Button button, Image image)
    {
        _button = button;
        _image = image;
    }

    private void Update()
    {
        if (_button == null || _image == null) return;

        bool focused = EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == gameObject
                    && _button.interactable;

        if (!focused)
        {
            if (_image.color != Color.white) _image.color = Color.white;
            return;
        }

        // Temps NON MIS À L'ÉCHELLE : les écrans qui portent un focus sont des modales, et une
        // modale met le jeu en pause. Avec le temps de jeu, la pulsation se figerait exactement là
        // où elle sert.
        float k = 1f + Amplitude * Mathf.Sin(Time.unscaledTime * Frequency * Mathf.PI * 2f);
        _image.color = new Color(k, k, k, 1f);
    }
}
