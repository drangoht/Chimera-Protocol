using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Signal de <b>focus</b> — portage d'<c>AttachFocusPulse</c> et du §3.2 de l'habillage.
///
/// <para>Le jeu se joue au clavier et à la manette : la sélection courante est la seule chose qui
/// dit au joueur « où » il est. Le portage la signalait par la variante <c>_focus</c> du cadre <b>de
/// la couleur du bouton</b>, plus ±18 % de luminosité. Relevé en jouant : la sélection <b>se
/// déplaçait bel et bien</b> à chaque flèche — et rien à l'écran ne le montrait, ce qui se lit
/// exactement comme « on ne peut pas se déplacer au clavier ». Un signal qu'on ne voit pas n'existe
/// pas.</para>
///
/// <para>Godot en superpose <b>trois</b>, et c'est délibéré (« le focus ne repose jamais sur la
/// seule teinte ») :</para>
/// <list type="number">
///   <item><b>La teinte change</b> — cadre <b>violet</b> quel que soit l'accent du bouton. Sur un
///         menu où chaque entrée porte déjà sa propre couleur, c'est le seul signal qui reste
///         lisible : « plus lumineux que son voisin » ne se compare pas entre deux teintes
///         différentes.</item>
///   <item><b>La forme change</b> — le cadre <b>déborde de 3 px</b> (<c>ExpandMargin</c> sous Godot,
///         un anneau enfant ici). Reste lisible pour qui distingue mal les couleurs.</item>
///   <item><b>Il bouge</b> — l'opacité oscille de 60 % à 100 %. Sur un écran chargé, c'est le
///         mouvement qui attire l'œil.</item>
/// </list>
/// </summary>
public sealed class UiFocusPulse : MonoBehaviour
{
    /// <summary>Période de la pulsation, en secondes — celle de Godot.</summary>
    private const float Period = 0.6f;

    /// <summary>Opacité au creux de la pulsation. Le sommet vaut 1.</summary>
    private const float MinAlpha = 0.6f;

    /// <summary>Débordement de l'anneau de focus, en pixels de référence.</summary>
    public const float Expand = 3f;

    private Button? _button;
    private Image? _image;
    private Image? _ring;

    /// <summary>
    /// Branche le signal. <paramref name="focusFrame"/> est le cadre de l'anneau ; il est <b>violet</b>
    /// pour les boutons, et de la rareté pour les cartes — dont la couleur porte une information que
    /// le focus ne doit pas écraser.
    /// </summary>
    internal void Bind(Button button, Image image, Sprite? focusFrame)
    {
        _button = button;
        _image = image;

        if (focusFrame == null) return;

        // L'anneau vit sur son PROPRE objet, derrière le contenu du bouton : peint sur l'image du
        // bouton, il subirait l'échange de sprite des états (survol, pressé) et disparaîtrait au
        // moment précis où il sert.
        var go = UiStyle.NewUiObject("FocusRing", button.transform);
        go.transform.SetAsFirstSibling();

        _ring = go.AddComponent<Image>();
        _ring.sprite = focusFrame;
        _ring.type = Image.Type.Sliced;
        _ring.fillCenter = false;    // sinon l'anneau masque le fond du bouton
        _ring.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-Expand, -Expand);
        rt.offsetMax = new Vector2(Expand, Expand);

        go.SetActive(false);
    }

    private void Update()
    {
        if (_button == null || _image == null) return;

        var es = EventSystem.current;
        bool focused = es != null && es.currentSelectedGameObject == gameObject && _button.interactable;

        if (!focused)
        {
            if (_ring != null && _ring.gameObject.activeSelf) _ring.gameObject.SetActive(false);
            if (_image.color != Color.white) _image.color = Color.white;
            return;
        }

        if (_ring != null && !_ring.gameObject.activeSelf) _ring.gameObject.SetActive(true);

        // Temps NON MIS À L'ÉCHELLE : les écrans qui portent un focus sont des modales, et une
        // modale met le jeu en pause. Avec le temps de jeu, la pulsation se figerait exactement là
        // où elle sert.
        float phase = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime / Period * Mathf.PI * 2f);
        float alpha = Mathf.Lerp(MinAlpha, 1f, phase);

        if (_ring != null) _ring.color = new Color(1f, 1f, 1f, alpha);

        // Le cadre du bouton s'éclaircit avec la pulsation : sans cela, l'anneau paraît posé à côté
        // du bouton plutôt que porté par lui.
        float k = Mathf.Lerp(1f, 1.25f, phase);
        _image.color = new Color(k, k, k, 1f);
    }
}
