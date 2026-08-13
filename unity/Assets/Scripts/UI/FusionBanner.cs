using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Annonce plein cadre d'une <b>fusion forgée</b> : le titre, l'icône et le nom de l'arme qui vient
/// de naître.
///
/// <para><b>Pourquoi un écran de plus.</b> Le HUD sait déjà annoncer
/// (<c>HUD.Announce</c>) — une ligne dorée de 30 px au centre, qui sert au boss imminent et à la
/// complétion du niveau. Elle ne convient pas ici pour une raison précise : une fusion <b>remplace</b>
/// une arme que le joueur possédait, et son nom seul ne dit pas laquelle. L'icône est l'information ;
/// c'est elle que le joueur retrouvera dans la liste d'arsenal trente secondes plus tard.</para>
///
/// <para><b>Il n'interrompt rien.</b> Pas de <see cref="ModalQueue"/>, pas de pause, pas de
/// <c>GraphicRaycaster</c> : la run continue dessous et aucun clic ne lui est volé. Une modale de
/// félicitations au milieu d'une nuée serait une punition déguisée en récompense — le joueur
/// vient déjà de traverser l'écran de montée de niveau.</para>
/// </summary>
/// <remarks>
/// ⚠ L'ordre d'empilement se raisonne par <b>chemin d'ouverture</b>. Une fusion se forge depuis
/// l'écran de montée de niveau (100), qui se referme juste avant ; mais le joueur peut ouvrir la
/// pause (110) pendant que l'annonce tient. À 90, elle passe au-dessus du HUD (0) et sous les deux
/// modales — jamais l'inverse.
/// </remarks>
public sealed class FusionBanner : MonoBehaviour
{
    /// <summary>Ordre d'empilement : au-dessus du HUD, sous toutes les modales.</summary>
    private const int SortingOrder = 90;

    /// <summary>Durée de la tenue à pleine opacité, en secondes réelles.</summary>
    private const float Hold = 1.9f;

    private GameObject? _root;
    private RectTransform? _group;
    private CanvasGroup? _fade;
    private Image? _icon;
    private Text? _name;
    private GTween? _sequence;

    /// <summary>Affiche l'annonce pour <paramref name="fusionId"/>. Un second appel écrase le premier.</summary>
    public void Show(string fusionId)
    {
        if (_root == null) BuildUi();
        if (_root == null || _group == null || _fade == null) return;

        // Copies locales : les champs nullables ne restent pas « prouvés non nuls » à l'intérieur des
        // lambdas de la séquence, et le compilateur a raison de s'en méfier — la séquence survit à
        // l'appel.
        var root = _root;
        var group = _group;
        var fade = _fade;

        // Deux fusions ne peuvent pas se forger dans la même frame, mais une seconde peut tomber
        // pendant que la première tient encore. Sans ce `Kill`, l'ancienne séquence rendrait
        // l'opacité à zéro au milieu de la nouvelle annonce.
        _sequence?.Kill();

        if (_name != null) _name.text = UiNames.Of(fusionId);

        if (_icon != null)
        {
            var sprite = UiIcons.For(fusionId);
            _icon.sprite = sprite;
            // Une icône absente laisse un carré vide qui se lit comme une image cassée : mieux vaut
            // le titre et le nom seuls, qui suffisent à porter l'événement.
            _icon.gameObject.SetActive(sprite != null);
        }

        root.SetActive(true);
        fade.alpha = 0f;
        group.localScale = Vector3.one * 0.72f;

        void SetScale(float v) => group.localScale = new Vector3(v, v, 1f);

        // ⚠ Temps réel de bout en bout. L'annonce naît à la seconde même où `FusionFanfare` met le
        // jeu au ralenti : comptée en temps de jeu, son apparition durerait presque deux secondes et
        // le joueur verrait un panneau grandir lentement pendant qu'il se fait encercler.
        _sequence = GTween.Create(this, ignoreTimeScale: true);

        _sequence.TweenFloat(v => fade.alpha = v, 0f, 1f, 0.22f, TransType.Quad, EaseType.Out)
                 .AppendInterval(Hold)
                 .TweenFloat(v => fade.alpha = v, 1f, 0f, 0.45f, TransType.Quad, EaseType.In)
                 .AppendCallback(() => root.SetActive(false));

        // Le dépassement de `Back` fait « claquer » l'arrivée. Séquence séparée : celle de l'opacité
        // enchaîne trois étapes, et `GTween` n'expose pas de jonction parallèle.
        GTween.Create(this, ignoreTimeScale: true)
              .TweenFloat(SetScale, 0.72f, 1f, 0.34f, TransType.Back, EaseType.Out);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("FusionBannerCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        // Pas de GraphicRaycaster : l'annonce est un décor, elle ne doit intercepter aucun clic.
        UiCanvas.Configure(canvasGo, SortingOrder);

        _root = canvasGo;
        _fade = canvasGo.GetComponent<CanvasGroup>();
        _fade.blocksRaycasts = false;
        _fade.interactable = false;

        // Un conteneur porte l'échelle pour que le halo grandisse AVEC le panneau : mis à l'échelle
        // séparément, les deux se décolleraient pendant l'arrivée.
        var group = UiStyle.NewUiObject("Group", canvasGo.transform);
        _group = group.GetComponent<RectTransform>();
        _group.anchorMin = _group.anchorMax = new Vector2(0.5f, 1f);
        _group.pivot = new Vector2(0.5f, 1f);
        _group.sizeDelta = new Vector2(660f, 200f);
        // Sous le panneau de boss (qui descend à -110), au-dessus du bandeau central du HUD.
        _group.anchoredPosition = new Vector2(0f, -140f);

        BuildHalo(group.transform);

        var panel = UiStyle.Panel(group.transform, "Panel", FrameAccent.Violet);
        UiStyle.Stretch(panel, 0f);

        var title = UiStyle.Label(panel.transform, Loc.T("FUSION_FORGED"), 26, UiPalette.Gold,
                                  TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -62f);
        titleRect.offsetMax = new Vector2(-24f, -22f);

        var iconGo = UiStyle.NewUiObject("Icon", panel.transform);
        _icon = iconGo.AddComponent<Image>();
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        var iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0f);
        iconRect.pivot = new Vector2(0.5f, 0f);
        iconRect.sizeDelta = new Vector2(76f, 76f);
        iconRect.anchoredPosition = new Vector2(-206f, 26f);

        // ⚠ Le nom n'est pas centré sur le PANNEAU mais sur la place qui reste à droite de l'icône.
        // Centré sur le panneau, il l'était géométriquement et paraissait décalé : l'œil centre le
        // bloc « icône + nom », pas le nom seul, et l'icône tirait tout l'ensemble vers la gauche.
        _name = UiStyle.Label(panel.transform, "", 32, UiPalette.Violet, TextAnchor.MiddleCenter);
        var nameRect = _name.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.offsetMin = new Vector2(130f, 24f);
        nameRect.offsetMax = new Vector2(-50f, -70f);

        canvasGo.SetActive(false);
    }

    /// <summary>
    /// Lueur violette derrière le panneau : c'est elle qui détache l'annonce de l'arène.
    /// </summary>
    /// <remarks>
    /// Un panneau seul, posé sur un sol clair au milieu d'une nuée, se confond avec le HUD. Le halo
    /// est le dégradé radial des effets — pas un cadre de plus, qui ajouterait un second contour là
    /// où le panneau en porte déjà un.
    /// </remarks>
    private static void BuildHalo(Transform parent)
    {
        var go = UiStyle.NewUiObject("Halo", parent);

        var image = go.AddComponent<Image>();

        // ⚠ Halo RECTANGULAIRE. Le premier essai employait le dégradé radial des effets : étiré sur
        // un panneau large, il dessinait une **ellipse** dont le bord franchissait le cadre en
        // diagonale — visible à la capture comme un trait oblique traversant le bord gauche du
        // panneau. Un halo qui borde un rectangle se fait en 9-slice, pas en radial.
        image.sprite = UiPrimitives.GlowBox;
        image.type = Image.Type.Sliced;
        image.fillCenter = false;
        image.raycastTarget = false;
        image.color = UiPalette.WithAlpha(UiPalette.Violet, 0.75f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        // Exactement la bordure du sprite : au-delà, un vide s'ouvrirait entre la lueur et le cadre.
        float margin = UiPrimitives.GlowBoxBorder;
        rect.offsetMin = new Vector2(-margin, -margin);
        rect.offsetMax = new Vector2(margin, margin);
    }
}
