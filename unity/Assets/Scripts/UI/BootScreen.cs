using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Premier écran du jeu : il charge les données, puis s'efface au profit de la cinématique.
///
/// <para><b>Pourquoi une scène pour ça.</b> En WebGL, <c>StreamingAssets</c> est une URL : les
/// tables d'armes, d'ennemis, de greffes et la table de traduction ne peuvent arriver que par le
/// réseau, donc de façon asynchrone. Or tout le jeu les lit de façon synchrone, au premier besoin —
/// seize sites pour les seules données de tuning, et chaque <c>Loc.T</c> pour les libellés. Réécrire
/// ces appels en asynchrone contaminerait l'intégralité du code d'écran pour une contrainte qui
/// n'existe que sur une plateforme.</para>
///
/// <para>L'attente est donc <b>concentrée ici</b>, avant que quiconque puisse lire quoi que ce soit.
/// Le reste du jeu ignore la question.</para>
///
/// <para>⚠ <b>Cet écran ne peut afficher aucun texte traduit</b> : la table de traduction est
/// précisément ce qu'il attend. Il ne montre donc qu'une forme et une couleur — un texte en dur
/// serait un texte non traduit de plus, et le projet en a déjà corrigé une pleine cargaison
/// (2.0.1, 2.0.2).</para>
/// </summary>
public sealed class BootScreen : MonoBehaviour
{
    /// <summary>Barre de progression : le seul retour visuel possible sans libellés.</summary>
    private Image? _bar;

    private IEnumerator Start()
    {
        Build();

        // ⚠ On ATTEND le chargement, on ne le porte pas. Il est lancé avant la première scène, par un
        // objet qui leur survit (StreamingText.Install) : porté ici, il était interrompu par le
        // premier changement de scène venu — et `--auto-play` en provoque un dès la première image.
        while (!StreamingText.Preloaded) yield return null;

        // Un jeu sans ses données n'est pas un jeu dégradé, c'est un jeu faux : armes sans paliers,
        // ennemis absents, interface affichant ses clés. On le dit fort plutôt que d'enchaîner.
        if (StreamingText.Count == 0)
            Debug.LogError("[BootScreen] aucune donnée chargée — le jeu va démarrer sur des tables vides.");

        SceneRoot.ChangeScene(GameScenes.Intro);
    }

    /// <summary>
    /// Fond plein et barre de progression, construits à la main.
    /// </summary>
    /// <remarks>
    /// Sans passer par <c>UiStyle.ScreenCanvas</c> : les fabriques d'écran posent des libellés et des
    /// cadres qui, eux, dépendent de ressources et de traductions. Cet écran doit pouvoir s'afficher
    /// alors que rien n'est encore chargé.
    /// </remarks>
    private void Build()
    {
        var canvasGo = new GameObject("BootCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var bgGo = new GameObject("Fond", typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        bgGo.GetComponent<Image>().color = UiPalette.BgDeep;
        Stretch(bgGo.GetComponent<RectTransform>());

        var trackGo = new GameObject("Piste", typeof(Image));
        trackGo.transform.SetParent(canvasGo.transform, false);
        trackGo.GetComponent<Image>().color = UiPalette.Steel;
        Bar(trackGo.GetComponent<RectTransform>(), 1f);

        var fillGo = new GameObject("Jauge", typeof(Image));
        fillGo.transform.SetParent(canvasGo.transform, false);
        _bar = fillGo.GetComponent<Image>();
        _bar.color = UiPalette.Cyan;
        Bar(_bar.rectTransform, 0f);
    }

    /// <summary>
    /// Anime la jauge sans jamais l'immobiliser.
    /// </summary>
    /// <remarks>
    /// Elle ne mesure rien : le nombre de fichiers restants n'est pas connu d'avance côté web, et une
    /// jauge qui saute de 0 à 100 % ne dit pas si le jeu charge ou s'il est figé. Une pulsation dit au
    /// moins « ça vit », ce qui est exactement l'information utile pendant deux secondes de réseau.
    /// </remarks>
    private void Update()
    {
        if (_bar == null) return;

        float t = Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);
        Bar(_bar.rectTransform, Mathf.SmoothStep(0.08f, 1f, t));
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Bande horizontale centrée, occupant <paramref name="fill"/> de la largeur utile.</summary>
    private static void Bar(RectTransform rt, float fill)
    {
        const float Width = 520f;
        const float Height = 6f;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(-Width * 0.5f, 0f);
        rt.sizeDelta = new Vector2(Width * Mathf.Clamp01(fill), Height);
    }
}
