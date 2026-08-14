using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Le jeu se joue en <b>paysage</b>. En portrait, un panneau plein écran le dit et met tout en
/// attente.
///
/// <para><b>Pourquoi refuser le portrait plutôt que s'y adapter.</b> L'arène est en 16/9 et la caméra
/// montre 1920 × 1080 unités de terrain. En portrait, le joueur verrait environ un tiers de la
/// largeur : les nuées arriveraient hors champ, et un survivor dont on ne voit pas venir la vague
/// n'est pas un jeu difficile, c'est un jeu injuste. Ce n'est pas un défaut d'interface qu'on
/// corrige en réagençant des panneaux — c'est le champ de vision, qui est une règle de jeu.</para>
///
/// <para><b>Elle vit hors des scènes</b>, installée en <c>BeforeSceneLoad</c> sur un objet
/// <c>DontDestroyOnLoad</c>, pour la même raison que <see cref="TouchInput"/> : un joueur tourne son
/// téléphone n'importe quand, y compris pendant un chargement, et une garde portée par un écran
/// s'éteindrait avec lui — sans une erreur.</para>
/// </summary>
public sealed class OrientationGate : MonoBehaviour
{
    /// <summary>Au-dessus de tout, y compris la pause (110) et l'annonce de fusion (90).</summary>
    private const int SortingOrder = 500;

    private static GameObject? _host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (_host != null) return;

        _host = new GameObject("[OrientationGate]", typeof(OrientationGate));
        DontDestroyOnLoad(_host);
    }

    private GameObject? _panel;
    private Text? _title;
    private Text? _body;
    private bool _blocking;
    private float _restoreTimeScale = 1f;

    /// <summary>
    /// La garde doit-elle s'interposer ?
    /// </summary>
    /// <remarks>
    /// <para>La forme du canevas fait foi, et non <c>Screen.orientation</c> : en WebGL, le navigateur
    /// rapporte l'orientation du <i>système</i>, qui ment dès que l'utilisateur a verrouillé la
    /// rotation — le canevas, lui, dit toujours ce que le joueur voit
    /// (<c>Rules/TouchZones.IsPortrait</c>).</para>
    ///
    /// <para>Réservée au tactile : une fenêtre de bureau plus haute que large est un choix de
    /// l'utilisateur, pas une erreur de tenue de téléphone, et la bloquer serait une régression sur
    /// la plateforme qui marche.</para>
    ///
    /// <para>⚠ <b><c>TouchCapable</c> et non <c>Active</c>.</b> Le second dit « le joueur se sert de
    /// ses doigts <i>en ce moment</i> » et bascule dans les deux sens — or un appui produit aussi un
    /// clic de compatibilité, qui le faisait retomber. Résultat : <b>toucher l'écran refermait la
    /// garde</b>, et le menu s'affichait en portrait. Ce qu'il faut savoir ici n'est pas ce que le
    /// joueur fait, c'est ce que l'appareil <i>est</i> — et cette réponse ne redevient jamais non.
    /// </para>
    /// </remarks>
    private static bool ShouldBlock()
        => (Application.isMobilePlatform || TouchInput.TouchCapable) &&
           TouchZones.IsPortrait(Screen.width, Screen.height);

    private void Update()
    {
        bool block = ShouldBlock();

        // ⚠ Tant que la table de traduction n'est pas chargée, cette garde afficherait ses CLÉS —
        // « ROTATE_TITLE » en travers de l'écran, ce qui est précisément le défaut trouvé au premier
        // essai navigateur. On laisse alors passer : le préchargement dure une fraction de seconde,
        // et la garde s'interpose à l'image suivante. Bloquer le temps ici serait pire encore, la
        // séquence d'amorçage étant elle-même comptée en secondes.
        if (block && !StreamingText.Preloaded) return;

        if (block == _blocking) { if (block) Fit(); return; }

        _blocking = block;

        if (block)
        {
            if (_panel == null) BuildUi();

            // Relus à chaque affichage : la langue se change depuis les Options, et un panneau
            // construit une fois garderait la langue du premier basculement.
            if (_title != null) _title.text = Loc.T("ROTATE_TITLE");
            if (_body != null)  _body.text  = Loc.T("ROTATE_BODY");

            _panel?.SetActive(true);
            Fit();

            // ⚠ On mémorise l'échelle de temps COURANTE au lieu de rendre 1 au retour : si le joueur
            // tourne son téléphone alors que le jeu est déjà en pause, remettre 1 le relancerait
            // sous l'écran de pause — le joueur mourrait derrière une modale, sans rien voir.
            _restoreTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            _panel?.SetActive(false);
            Time.timeScale = _restoreTimeScale;
        }
    }

    /// <summary>Rendre l'échelle de temps si l'objet disparaît malgré tout : sinon le jeu reste figé.</summary>
    private void OnDisable()
    {
        if (!_blocking) return;

        _blocking = false;
        Time.timeScale = _restoreTimeScale;
    }

    /// <summary>
    /// Le pictogramme se met à l'échelle de l'écran — la maquette 1920 × 1080 n'a pas de sens ici :
    /// en portrait, la largeur disponible peut être de 360 px.
    /// </summary>
    private void Fit()
    {
        float unit = Mathf.Min(Screen.width, Screen.height);

        if (_title != null) _title.fontSize = Mathf.Max(16, Mathf.RoundToInt(unit * 0.075f));
        if (_body != null)  _body.fontSize  = Mathf.Max(12, Mathf.RoundToInt(unit * 0.045f));
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("OrientationGateCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // ⚠ Le GraphicRaycaster et le fond OPAQUE ne sont pas décoratifs : ils avalent les appuis.
        // Sans eux, le joueur toucherait les boutons du menu à travers le panneau, et se retrouverait
        // à lancer une run qu'il ne voit pas.
        var backdrop = UiStyle.NewUiObject("Backdrop", canvasGo.transform);
        var bg = backdrop.AddComponent<Image>();
        bg.sprite = UiPrimitives.White;
        bg.color = UiPalette.Bg;
        bg.raycastTarget = true;
        Stretch(backdrop.GetComponent<RectTransform>());

        _title = UiStyle.Label(backdrop.transform, Loc.T("ROTATE_TITLE"), 48, UiPalette.Cyan,
                               TextAnchor.LowerCenter);
        var titleRect = _title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.offsetMin = new Vector2(24f, 6f);
        titleRect.offsetMax = new Vector2(-24f, 90f);

        _body = UiStyle.Label(backdrop.transform, Loc.T("ROTATE_BODY"), 28, UiPalette.OffWhite,
                              TextAnchor.UpperCenter);
        var bodyRect = _body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.offsetMin = new Vector2(24f, -90f);
        bodyRect.offsetMax = new Vector2(-24f, -10f);

        _panel = canvasGo;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
