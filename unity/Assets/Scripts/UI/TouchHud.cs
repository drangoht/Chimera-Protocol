using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Les contrôles tactiles <b>visibles</b> : joystick flottant, bouton d'esquive, bouton de pause.
///
/// <para><b>Celui qui montre est celui qui écoute.</b> Ce composant ne se contente pas de dessiner :
/// c'est lui qui ouvre et referme la capture des doigts (<see cref="TouchInput.SetGameControls"/>).
/// Les deux ne peuvent donc pas diverger — et la divergence est <i>le</i> défaut du tactile : un
/// bouton qui se voit et ne répond pas, ou pire, une zone invisible qui avale les appuis destinés à
/// un menu. Séparer le dessin de la lecture, ce serait recréer à la main l'invariant que ce
/// couplage rend gratuit.</para>
///
/// <para><b>Rien ne s'affiche tant qu'aucun doigt n'a touché la dalle</b>
/// (<see cref="TouchInput.Active"/>) : un portable Windows à écran tactile déclare une dalle sans
/// que son propriétaire s'en serve, et un joystick posé au milieu de l'écran d'un joueur au clavier
/// serait un défaut visible immédiatement.</para>
/// </summary>
/// <remarks>
/// ⚠ <b>Ce canevas est le seul du jeu à ne PAS passer par <see cref="UiCanvas.Configure"/>.</b> Tout
/// le reste de l'interface est mis à l'échelle d'une maquette 1920 × 1080 ; ces contrôles-ci sont
/// mesurés en <b>pouces</b>, pas en pixels de maquette. Un bouton d'esquive à l'échelle de la
/// maquette ferait 44 px de large sur un téléphone et 130 sur une tablette, alors que le pouce, lui,
/// a la même taille sur les deux. Le canevas est donc en <c>ConstantPixelSize</c> à l'échelle 1 :
/// une unité uGUI y vaut exactement un pixel écran, ce qui permet de poser tel quel ce que rend
/// <c>Rules/TouchZones</c> — et supprime la conversion où les deux couches auraient dérivé.
/// </remarks>
public sealed class TouchHud : MonoBehaviour
{
    /// <summary>Ordre d'empilement : au-dessus du HUD (0), sous l'annonce de fusion (90) et les modales.</summary>
    private const int SortingOrder = 60;

    /// <summary>Taille du pommeau du joystick, en fraction du rayon de la base.</summary>
    private const float KnobFraction = 0.46f;

    private GameObject? _root;
    private RectTransform? _stickBase;
    private RectTransform? _stickKnob;
    private RectTransform? _dash;
    private Image? _dashRing;
    private Image? _dashFill;
    private RectTransform? _pause;
    private PauseScreen? _pauseScreen;

    private void OnEnable()  => Refresh();
    private void OnDisable() => TouchInput.SetGameControls(false);
    private void OnDestroy() => TouchInput.SetGameControls(false);

    private void LateUpdate() => Refresh();

    /// <summary>
    /// Décide, à chaque image, si les doigts pilotent le jeu — et redessine en conséquence.
    /// </summary>
    /// <remarks>
    /// <para><c>LateUpdate</c> : la file de modales et la pause se décident pendant <c>Update</c>.
    /// Lue plus tôt, la porte se refermerait une image en retard, et le stick volerait l'appui qui
    /// vient d'ouvrir le menu de montée de niveau — c'est-à-dire qu'il choisirait une carte.</para>
    ///
    /// <para>La porte se ferme dès qu'une modale s'ouvre : sans cela, un pouce resté posé sur le
    /// joystick continuerait de pousser <b>pendant la pause</b>, et le joueur repartirait dans une
    /// direction qu'il n'a pas demandée à la reprise.</para>
    /// </remarks>
    private void Refresh()
    {
        // ⚠ La pause N'EST PAS dans la file de modales — elle est un simple écran à bascule, et
        // `ModalQueue.IsOpen` reste donc à `false` pendant qu'elle est ouverte. S'en tenir à la file
        // laissait les contrôles tactiles actifs par-dessus : un pouce resté sur le joystick
        // continuait de pousser pendant la pause, et le joueur repartait dans cette direction à la
        // reprise sans avoir rien touché. Les deux conditions, pas l'une des deux.
        _pauseScreen ??= GetComponent<PauseScreen>();
        bool paused = _pauseScreen != null && _pauseScreen.IsVisible;

        bool wanted = isActiveAndEnabled && TouchInput.Active && !ModalQueue.IsOpen && !paused;
        TouchInput.SetGameControls(wanted);

        if (!wanted)
        {
            if (_root != null) _root.SetActive(false);
            return;
        }

        if (_root == null) BuildUi();
        if (_root == null) return;

        _root.SetActive(true);
        LayoutButtons();
        LayoutStick();
    }

    // ─── Mise en place ───────────────────────────────────────────────────────

    private void LayoutStick()
    {
        if (_stickBase == null || _stickKnob == null) return;

        bool held = TouchInput.StickHeld;
        _stickBase.gameObject.SetActive(held);
        if (!held) return;

        float radius = VirtualStick.RadiusFor(Screen.height);

        _stickBase.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        _stickBase.anchoredPosition = TouchInput.StickOrigin;

        // Le pommeau suit le doigt, borné au rayon : au-delà, le stick est déjà recentré côté
        // TouchInput, mais l'image ne doit jamais sortir de sa base — un pommeau qui s'en échappe
        // se lit comme un bug, alors que le déplacement, lui, est juste.
        Vector2 offset = Vector2.ClampMagnitude(TouchInput.StickFinger - TouchInput.StickOrigin, radius);

        float knob = radius * KnobFraction;
        _stickKnob.sizeDelta = new Vector2(knob * 2f, knob * 2f);
        _stickKnob.anchoredPosition = offset;
    }

    private void LayoutButtons()
    {
        if (_dash != null && _dashRing != null && _dashFill != null)
        {
            var player = Player.Instance;
            bool available = player != null && player.DashEnabled;

            // L'esquive n'est accordée que par une greffe : montrer un bouton mort pendant les dix
            // premières minutes d'une run apprendrait au joueur à l'ignorer, et il l'ignorerait
            // encore une fois obtenue.
            _dash.gameObject.SetActive(available);

            if (available)
            {
                float r = TouchZones.DashRadius(Screen.height);
                var (cx, cy) = TouchZones.DashCenter(Screen.width, Screen.height);

                _dash.sizeDelta = new Vector2(r * 2f, r * 2f);
                _dash.anchoredPosition = new Vector2(cx, cy);

                // Le remplissage EST la recharge : la même information que la jauge du HUD, mais
                // là où le pouce regarde. Radial et non horizontal, pour qu'elle se lise du coin de
                // l'œil sans rien mesurer.
                float ratio = player!.DashReadyRatio;
                _dashFill.fillAmount = ratio;
                _dashRing.color = UiPalette.WithAlpha(UiPalette.Cyan,
                                                      TouchInput.DashHeld ? 0.95f : 0.55f);
            }
        }

        if (_pause != null)
        {
            float r = TouchZones.PauseRadius(Screen.height);
            var (px, py) = TouchZones.PauseCenter(Screen.width, Screen.height);

            _pause.sizeDelta = new Vector2(r * 2f, r * 2f);
            _pause.anchoredPosition = new Vector2(px, py);
        }
    }

    // ─── Construction ────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("TouchHudCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = SortingOrder;

        // Voir la remarque de classe : un pixel uGUI = un pixel écran, sinon les zones lues et les
        // boutons dessinés ne parlent pas la même langue.
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        // ⚠ Pas de GraphicRaycaster, et raycastTarget à false partout : les doigts sont lus par
        // TouchZones, pas par uGUI. Un raycaster ici mangerait les appuis destinés aux modales qui
        // s'ouvrent par-dessus — le menu de montée de niveau cesserait de répondre.
        _root = canvasGo;

        _stickBase = NewCircle(canvasGo.transform, "StickBase", UiPrimitives.Ring,
                               UiPalette.WithAlpha(UiPalette.OffWhite, 0.42f));
        _stickKnob = NewCircle(_stickBase.transform, "StickKnob", UiPrimitives.Disc,
                               UiPalette.WithAlpha(UiPalette.Cyan, 0.55f));

        // ⚠ Le pommeau s'ancre au CENTRE de sa base, pas au coin. NewCircle ancre en bas à gauche —
        // c'est le repère de TouchZones, juste pour les éléments posés directement sur l'écran. Le
        // laisser tel quel décalerait le pommeau d'un rayon, en diagonale, et le stick paraîtrait
        // désaxé sans qu'aucun calcul de déplacement ne soit faux.
        _stickKnob.anchorMin = _stickKnob.anchorMax = new Vector2(0.5f, 0.5f);

        BuildDash(canvasGo.transform);
        BuildPause(canvasGo.transform);
    }

    private void BuildDash(Transform parent)
    {
        _dash = NewCircle(parent, "DashButton", UiPrimitives.Disc,
                          UiPalette.WithAlpha(UiPalette.BgDeep, 0.55f));

        // Deux anneaux superposés, et ⚠ l'ORDRE compte : uGUI peint dans l'ordre des enfants, donc
        // l'anneau de fond doit venir EN PREMIER. Créé après, il recouvrirait exactement le
        // remplissage — la recharge serait calculée, colorée, et parfaitement invisible.
        var ring = NewCircle(_dash.transform, "DashRing", UiPrimitives.Ring,
                             UiPalette.WithAlpha(UiPalette.Cyan, 0.55f));
        StretchToParent(ring);
        _dashRing = ring.GetComponent<Image>();

        var fill = NewCircle(_dash.transform, "DashFill", UiPrimitives.Ring,
                             UiPalette.WithAlpha(UiPalette.Cyan, 0.95f));
        StretchToParent(fill);
        _dashFill = fill.GetComponent<Image>();
        _dashFill.type = Image.Type.Filled;
        _dashFill.fillMethod = Image.FillMethod.Radial360;
        _dashFill.fillOrigin = (int)Image.Origin360.Top;
        _dashFill.fillClockwise = true;

        // 16 et non le corps courant : le libellé doit tenir dans le plus petit bouton possible
        // (88 px de diamètre sur une dalle basse), et un texte qui déborde de son cercle se lit
        // comme une erreur de mise en page.
        var label = UiStyle.Label(_dash.transform, Loc.T("TOUCH_DASH"), 16, UiPalette.OffWhite,
                                  TextAnchor.MiddleCenter);
        label.raycastTarget = false;
        StretchToParent(label.GetComponent<RectTransform>());
    }

    /// <summary>
    /// Le bouton de pause : un cercle et <b>deux barres</b>.
    /// </summary>
    /// <remarks>
    /// Deux barres et non un mot : c'est le seul glyphe que tout le monde lit sans traduction, et
    /// c'est aussi le seul dont on est certain qu'il existe — la police du jeu (Share Tech Mono) n'a
    /// pas les caractères de commande d'Unicode, et un glyphe absent se dessine en carré vide.
    /// </remarks>
    private void BuildPause(Transform parent)
    {
        _pause = NewCircle(parent, "PauseButton", UiPrimitives.Disc,
                           UiPalette.WithAlpha(UiPalette.BgDeep, 0.55f));

        var ring = NewCircle(_pause.transform, "PauseRing", UiPrimitives.Ring,
                             UiPalette.WithAlpha(UiPalette.OffWhite, 0.6f));
        StretchToParent(ring);

        foreach (float x in new[] { -0.11f, 0.11f })
        {
            var bar = UiStyle.NewUiObject("Bar", _pause.transform);

            var image = bar.AddComponent<Image>();
            image.sprite = UiPrimitives.White;
            image.color = UiPalette.OffWhite;
            image.raycastTarget = false;

            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f + x - 0.055f, 0.32f);
            rect.anchorMax = new Vector2(0.5f + x + 0.055f, 0.68f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    // ─── Petits outils ───────────────────────────────────────────────────────

    /// <summary>Une image circulaire ancrée <b>en bas à gauche</b> — le repère de TouchZones.</summary>
    private static RectTransform NewCircle(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = UiStyle.NewUiObject(name, parent);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        return rect;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
