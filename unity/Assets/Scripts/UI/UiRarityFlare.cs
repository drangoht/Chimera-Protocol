using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aura de <b>rareté</b> autour d'une carte de montée de niveau — le signal qui dit « celle-ci n'est
/// pas comme les autres » avant même qu'on l'ait lue.
///
/// <para><b>Pourquoi elle manquait.</b> La rareté n'était portée que par deux choses immobiles : la
/// variante du cadre (<c>ui_frame_card_epic</c>) et l'étiquette de texte. Or l'écran s'ouvre au pire
/// moment — le jeu figé au milieu d'une nuée — et le joueur arbitre en quelques secondes : deux
/// cadres qui ne diffèrent que par leur teinte se comparent mal, et l'étiquette suppose qu'on lise.
/// <b>Ce qui attire l'œil, c'est le mouvement</b> ; c'est déjà la raison pour laquelle
/// <see cref="UiFocusPulse"/> superpose trois signaux au lieu d'un.</para>
///
/// <para><b>L'aura vit chez le PARENT, pas chez la carte.</b> Enfant de la carte, elle serait dessinée
/// <i>par-dessus</i> son fond — donc une nappe de couleur sous l'icône et le texte, exactement là où
/// la lisibilité compte. Posée à côté d'elle et mise en premier frère, elle passe dessous et seul son
/// débordement se voit. Le prix à payer est qu'elle doit être exclue de la mise en page
/// (<see cref="LayoutElement.ignoreLayout"/>, sans quoi elle compterait comme une quatrième carte) et
/// qu'elle recopie le rectangle de la carte à chaque image, le <c>HorizontalLayoutGroup</c> ne
/// calculant les tailles qu'en fin de frame.</para>
/// </summary>
/// <remarks>
/// ⚠ Le temps est <b>non mis à l'échelle</b> : l'écran de montée de niveau tourne à
/// <c>Time.timeScale = 0</c>, et une pulsation asservie au temps de jeu resterait parfaitement
/// immobile — c'est-à-dire indiscernable de l'absence d'effet.
/// </remarks>
public sealed class UiRarityFlare : MonoBehaviour
{
    /// <summary>Période de la respiration, en secondes. Plus lente que le focus (0,6 s), qui doit rester le signal le plus vif.</summary>
    private const float Period = 1.6f;

    /// <summary>
    /// Marge au creux de la respiration, en fraction de la marge au repos.
    /// </summary>
    /// <remarks>
    /// ⚠ La respiration ne va jamais <b>au-delà</b> de la marge demandée, elle ne fait que rentrer.
    /// Dépasser <see cref="UiPrimitives.GlowBoxBorder"/> ouvrirait un vide entre la lueur et la carte,
    /// la zone centrale du 9-slice n'étant pas peinte.
    /// </remarks>
    private const float BreathFloor = 0.80f;

    private RectTransform? _card;
    private RectTransform? _halo;
    private Image? _image;
    private Color _color;
    private float _minAlpha;
    private float _maxAlpha;
    private float _margin;
    private float _phase;

    /// <summary>
    /// Pose une aura autour de <paramref name="card"/>. Sans effet si la carte n'a pas de parent —
    /// l'aura n'aurait alors nulle part où se placer <i>derrière</i>.
    /// </summary>
    /// <param name="margin">Débordement au repos, en pixels d'interface.</param>
    public static void Attach(RectTransform card, Color color,
                              float minAlpha, float maxAlpha, float margin)
    {
        if (card.parent == null) return;

        var flare = card.gameObject.AddComponent<UiRarityFlare>();
        flare.Build(card, color, minAlpha, maxAlpha, margin);
    }

    private void Build(RectTransform card, Color color, float minAlpha, float maxAlpha, float margin)
    {
        _card = card;
        _color = color;
        _minAlpha = minAlpha;
        _maxAlpha = maxAlpha;
        _margin = Mathf.Min(margin, UiPrimitives.GlowBoxBorder);

        var go = UiStyle.NewUiObject("RarityFlare", card.parent);
        go.transform.SetAsFirstSibling();   // dessiné SOUS les cartes

        // Sans cela, l'aura serait traitée comme une carte de plus par le HorizontalLayoutGroup :
        // trois cartes deviendraient quatre colonnes, dont une vide.
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        _image = go.AddComponent<Image>();

        // ⚠ Halo RECTANGULAIRE, pas le dégradé radial des effets. Le premier essai employait
        // `VfxPrimitives.Glow` : étiré sur une carte, son bord tombe là où le dégradé est déjà éteint,
        // et l'aura n'existait qu'au centre — sous la carte qui la cache. Voir `UiPrimitives.GlowBox`.
        _image.sprite = UiPrimitives.GlowBox;
        _image.type = Image.Type.Sliced;
        _image.fillCenter = false;          // la zone centrale est couverte : ne pas la peindre
        _image.raycastTarget = false;       // elle couvre la carte : elle intercepterait le clic
        _image.color = new Color(color.r, color.g, color.b, minAlpha);

        _halo = go.GetComponent<RectTransform>();

        // Les trois cartes ne doivent pas respirer à l'unisson : un battement synchronisé se lit
        // comme un clignotement de l'écran entier, pas comme une propriété de chaque carte.
        _phase = card.GetSiblingIndex() * 0.7f;

        MatchCard(_margin);
    }

    private void OnDestroy()
    {
        if (_halo != null) Destroy(_halo.gameObject);
    }

    // L'aura ne vit pas sous la carte : sans ces deux relais, masquer la carte laisserait son aura
    // seule à l'écran, figée sur sa dernière opacité — une tache de couleur sans objet.
    private void OnEnable()  { if (_halo != null) _halo.gameObject.SetActive(true); }
    private void OnDisable() { if (_halo != null) _halo.gameObject.SetActive(false); }

    private void LateUpdate()
    {
        if (_card == null || _halo == null || _image == null) return;

        _phase += Time.unscaledDeltaTime * (2f * Mathf.PI / Period);

        float wave = 0.5f + 0.5f * Mathf.Sin(_phase);

        _image.color = new Color(_color.r, _color.g, _color.b,
                                 Mathf.Lerp(_minAlpha, _maxAlpha, wave));

        MatchCard(_margin * Mathf.Lerp(BreathFloor, 1f, wave));
    }

    /// <summary>
    /// Recopie le rectangle de la carte, élargi de <paramref name="margin"/> <b>sans déplacer son
    /// centre</b>.
    /// </summary>
    /// <remarks>
    /// La compensation de position n'est pas une précaution théorique : un enfant de
    /// <c>HorizontalLayoutGroup</c> reçoit un pivot qui n'est pas centré, et agrandir <c>sizeDelta</c>
    /// seul ferait alors grandir l'aura <b>vers un coin</b>. Le centre vaut
    /// <c>anchoredPosition + (0,5 − pivot) × taille</c> : le conserver impose de retirer
    /// <c>(0,5 − pivot) × 2 × marge</c> à la position.
    /// </remarks>
    private void MatchCard(float margin)
    {
        if (_card == null || _halo == null) return;

        _halo.anchorMin = _card.anchorMin;
        _halo.anchorMax = _card.anchorMax;
        _halo.pivot = _card.pivot;
        _halo.sizeDelta = _card.sizeDelta + new Vector2(margin * 2f, margin * 2f);

        var offset = (new Vector2(0.5f, 0.5f) - _card.pivot) * (margin * 2f);
        _halo.anchoredPosition = _card.anchoredPosition - offset;
    }
}
