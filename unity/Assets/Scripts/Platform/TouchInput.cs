using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Le <b>seul</b> fichier du projet qui lit une dalle tactile — troisième et dernier membre du trio
/// d'entrées, aux côtés de <see cref="InputRemap"/> (touches remappables) et <see cref="RawInput"/>
/// (Échap, clic, stick droit).
///
/// <para><b>Pourquoi le tactile ne pouvait pas se glisser dans les deux autres.</b> Un clavier et une
/// manette sont <i>sans état</i> : on demande « cette touche est-elle enfoncée ? » et la réponse est
/// complète. Une dalle tactile ne l'est pas — un joystick flottant n'existe que par la mémoire de
/// l'endroit où le doigt s'est posé, et cette mémoire doit survivre d'une image à l'autre. C'est
/// cette machine à états, et rien d'autre, qui vit ici.</para>
///
/// <para><b>Le pompage ne dépend d'aucune scène.</b> Il est installé en <c>BeforeSceneLoad</c> sur un
/// objet <c>DontDestroyOnLoad</c>, comme le préchargement des textes. La raison est une leçon déjà
/// payée : quand un invariant est porté par le cycle de vie d'un écran, <b>un tiers peut l'annuler</b>
/// — un changement de scène a tué à mi-chemin la coroutine qui chargeait la table de traduction, et
/// tout le jeu s'est affiché en clés, sans une erreur. Un stick qui cesse d'être lu au chargement du
/// premier ennemi produirait exactement le même genre de panne muette : le joueur ne bouge plus, la
/// console est vide.</para>
///
/// <para><b>Repère</b> : pixels écran, origine en bas à gauche — celui de <c>Touchscreen</c>, de
/// <c>Mouse.position</c> et des ancres uGUI. Voir <c>Rules/VirtualStick</c>.</para>
///
/// <para>La <b>géométrie</b> (rayon, zone morte, recentrage, zones de l'écran) n'est pas ici : elle
/// vit dans <c>Rules/VirtualStick</c> et <c>Rules/TouchZones</c>, purs et testés sans téléphone. Ce
/// fichier ne fait que brancher des doigts dessus.</para>
/// </summary>
public static class TouchInput
{
    // ─── Ce que le reste du jeu consulte ─────────────────────────────────────

    /// <summary>
    /// Le joueur se sert-il de ses doigts ? <b>Latché au premier vrai contact</b>, relâché dès qu'une
    /// touche ou un clic arrive.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <c>Touchscreen.current != null</c> ne répond <b>pas</b> à cette question : un portable
    /// Windows à écran tactile en déclare une alors que son propriétaire joue au clavier. S'y fier
    /// afficherait un joystick à l'écran sur une machine de bureau, et — plus grave — basculerait la
    /// visée en automatique, ce qui désarmerait la Lance Vectorielle sans qu'aucun réglage ne
    /// l'explique.</para>
    ///
    /// <para>La bascule est <b>réversible dans les deux sens</b> : sur une tablette avec clavier, le
    /// joueur passe de l'un à l'autre en cours de partie, et l'interface doit suivre. Un simple
    /// mouvement de souris ne suffit pas à relâcher — une souris posée sur un bureau qui vibre
    /// ferait alors disparaître les contrôles en pleine nuée.</para>
    /// </remarks>
    public static bool Active => _active || DebugHooks.ForceTouch;
    private static bool _active;

    /// <summary>
    /// Cet appareil a-t-il une dalle tactile qui a <b>déjà servi</b> ? <b>Jamais relâché.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>Pourquoi une seconde notion, et pas <see cref="Active"/>.</b> Les deux répondent à
    /// des questions différentes, et les confondre a coûté un défaut : <i>« le joueur se sert-il de
    /// ses doigts en ce moment ? »</i> décide de ce qu'on <b>affiche</b> (les contrôles, la source
    /// de la visée) et doit donc basculer dans les deux sens ; <i>« cet appareil est-il un appareil
    /// tactile ? »</i> décide de ce qui est <b>possible</b>, et la réponse ne redevient jamais non.
    /// </para>
    ///
    /// <para>La garde d'orientation lit celle-ci. Elle lisait <see cref="Active"/>, si bien qu'un
    /// simple contact — qui produit aussi un clic de compatibilité — la faisait <b>disparaître</b>,
    /// et le menu s'affichait en portrait. <b>Tourner son téléphone ne cesse pas d'en faire un
    /// téléphone.</b></para>
    /// </remarks>
    public static bool TouchCapable => _touchCapable || DebugHooks.ForceTouch;
    private static bool _touchCapable;

    /// <summary>
    /// Les contrôles de jeu (stick, esquive, pause) doivent-ils capter les doigts ?
    /// </summary>
    /// <remarks>
    /// <b>Ferme par défaut</b>, et c'est délibéré : hors d'une run, tout doigt appartient à uGUI. Un
    /// stick qui resterait actif dans les menus volerait les appuis destinés aux boutons — un menu
    /// qui ne répond pas est le pire symptôme possible sur mobile, le joueur n'a alors aucun recours.
    /// C'est <c>UI/TouchHud</c>, l'objet qui <i>dessine</i> ces contrôles, qui ouvre et referme cette
    /// porte : celui qui montre est celui qui écoute, ils ne peuvent donc pas diverger.
    /// </remarks>
    public static bool GameControlsEnabled { get; private set; }

    /// <summary>Ouvre ou referme la capture des doigts par les contrôles de jeu.</summary>
    /// <remarks>La refermer <b>relâche immédiatement</b> les doigts en cours : sans cela, un stick
    /// poussé au moment où le menu de montée de niveau s'ouvre resterait poussé, et le joueur
    /// repartirait dans cette direction à la reprise, sans avoir rien touché.</remarks>
    public static void SetGameControls(bool enabled)
    {
        GameControlsEnabled = enabled;
        if (!enabled) ReleaseAll();
    }

    /// <summary>Déplacement demandé par le joystick, dosé, de norme ≤ 1.</summary>
    public static Vector2 MoveVector() => _move;

    /// <summary>L'esquive vient-elle d'être demandée ? <b>Consommé à la lecture.</b></summary>
    public static bool DashPressedThisFrame() => Consume(ref _dashPressedFrame);

    /// <summary>La pause vient-elle d'être demandée ? <b>Consommé à la lecture.</b></summary>
    public static bool PausePressedThisFrame() => Consume(ref _pausePressedFrame);

    /// <summary>
    /// Relève un appui en attente et l'efface, s'il date de moins de <see cref="EventLifetime"/>
    /// images.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Un appui ne peut PAS être publié comme « cette image-ci ».</b> Le pompage tactile
    /// vit sur un objet créé en <c>BeforeSceneLoad</c> ; l'ordre des <c>Update</c> entre objets
    /// n'est pas garanti par Unity. Le <c>RunHud</c> qui interroge la pause s'exécute donc, une fois
    /// sur deux, <b>avant</b> le pompage : il lit une image trop tôt, et à l'image suivante
    /// l'événement est déjà périmé. L'appui disparaît — et c'est ce qui s'est passé au premier essai,
    /// bouton parfaitement placé, zone parfaitement calculée, aucune erreur.</para>
    ///
    /// <para>La parade tient en deux parties : l'événement <b>survit</b> quelques images, et il est
    /// <b>consommé</b> par son lecteur pour ne pas se déclencher deux fois. Chaque appui n'a qu'un
    /// lecteur (la pause pour le HUD de run, l'esquive pour le joueur) — un second lecteur dans la
    /// même image verrait <c>false</c>, ce qui est le comportement voulu pour une action, pas pour
    /// un état.</para>
    /// </remarks>
    private static bool Consume(ref int frame)
    {
        if (frame < 0) return false;

        bool fresh = Time.frameCount - frame <= EventLifetime;
        frame = -1;

        return fresh;
    }

    /// <summary>
    /// Nombre d'images pendant lesquelles un appui reste relevable. Deux : de quoi couvrir n'importe
    /// quel ordre d'exécution, sans jamais retarder une action d'assez pour que ça se sente.
    /// </summary>
    private const int EventLifetime = 2;

    /// <summary>Le joystick est-il tenu ? (pour le dessiner)</summary>
    public static bool StickHeld => _stickFinger != NoFinger;

    /// <summary>Centre du joystick à l'écran, en pixels — n'a de sens que si <see cref="StickHeld"/>.</summary>
    public static Vector2 StickOrigin => _stickOrigin;

    /// <summary>Position du doigt qui tient le joystick, en pixels écran.</summary>
    public static Vector2 StickFinger => _stickFinger == NoFinger ? _stickOrigin : _stickTip;

    /// <summary>L'esquive est-elle maintenue ? (pour l'enfoncer visuellement)</summary>
    public static bool DashHeld => _dashFinger != NoFinger;

    // ─── État ────────────────────────────────────────────────────────────────

    private const int NoFinger = int.MinValue;

    private static Vector2 _move;
    private static Vector2 _stickOrigin;
    private static Vector2 _stickTip;
    private static int _stickFinger = NoFinger;
    private static int _dashFinger = NoFinger;
    private static int _dashPressedFrame = -1;
    private static int _pausePressedFrame = -1;

    // ─── Installation ────────────────────────────────────────────────────────

    private static GameObject? _host;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // ⚠ Rejoué à chaque entrée en mode Play dans l'éditeur, où les statiques survivent d'une
        // session à l'autre : sans cette remise à zéro, un stick tenu à l'arrêt du jeu resterait
        // poussé au lancement suivant.
        ReleaseAll();
        _active = false;
        _touchCapable = false;
        _lastTouchFrame = int.MinValue / 2;
        GameControlsEnabled = false;
        _dashPressedFrame = _pausePressedFrame = -1;

        if (_host != null) return;

        _host = new GameObject("[TouchInput]");
        Object.DontDestroyOnLoad(_host);
        _host.AddComponent<TouchInputPump>();

        EnableSimulationIfAsked();
    }

    /// <summary>
    /// Sous <c>--touch</c>, fait passer la souris pour un doigt — un vrai
    /// <c>Touchscreen</c>, alimenté par le paquet Input System lui-même.
    /// </summary>
    /// <remarks>
    /// <para><b>C'est ce qui rend le joystick vérifiable sans téléphone.</b> La solution facile
    /// aurait été de dessiner un stick de démonstration à une position fictive : elle aurait montré
    /// une image et validé <i>autre chose</i> que le code du jeu — le projet a déjà vu un outil de
    /// contrôle photographier la main précédente et déclarer bonne une hiérarchie qu'il n'avait pas
    /// regardée. Ici, la souris crée un doigt <b>réel</b> : le chemin parcouru est exactement celui
    /// d'un joueur, recentrage compris.</para>
    ///
    /// <para>Ce que cela ne couvre toujours pas : le multi-touch. Tenir le stick <i>et</i> presser
    /// l'esquive demande deux doigts, donc un vrai écran ou l'émulation du navigateur.</para>
    /// </remarks>
    private static void EnableSimulationIfAsked()
    {
        if (!DebugHooks.ForceTouch) return;

        UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
        Debug.Log("[TOUCH] --touch : mode tactile force, souris simulee en doigt.");
    }

    // ─── Pompage ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Une image de lecture de la dalle. Appelée par <see cref="TouchInputPump"/>, jamais ailleurs.
    /// </summary>
    internal static void Poll()
    {
        var screen = Touchscreen.current;

        // Pas de dalle : l'état pendant doit être relâché, sinon un débranchement (ou le passage
        // d'un émulateur mobile à la souris dans les outils du navigateur) laisserait le joueur
        // courir tout seul.
        if (screen == null)
        {
            if (_stickFinger != NoFinger || _dashFinger != NoFinger) ReleaseAll();
            UpdateActiveLatch(sawTouch: false);
            return;
        }

        bool sawTouch = false;
        bool stickStillDown = false;
        bool dashStillDown = false;

        float w = Screen.width;
        float h = Screen.height;
        float radius = VirtualStick.RadiusFor(h);
        float deadZone = VirtualStick.DeadZoneFor(h);

        foreach (TouchControl touch in screen.touches)
        {
            // ⚠ L'ARRIVÉE se teste AVANT le maintien, et jamais l'inverse. Un appui posé et relevé
            // entre deux images se présente ici avec `isPressed` déjà à false : filtrer là-dessus
            // d'abord **avale le tapotement**, qui est pourtant le geste le plus naturel sur un
            // bouton. Le défaut ne se voit pas au code et ne se reproduit pas à tous les coups — il
            // se signale « le bouton ne répond pas une fois sur dix », c'est-à-dire de la façon la
            // plus coûteuse à instruire.
            bool arrived = touch.press.wasPressedThisFrame;
            bool held = touch.press.isPressed;
            if (!arrived && !held) continue;

            sawTouch = true;
            if (!GameControlsEnabled) continue;

            int id = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();

            if (held && id == _stickFinger)
            {
                stickStillDown = true;
                _stickTip = position;

                var reading = VirtualStick.Read(_stickOrigin.x, _stickOrigin.y,
                                                position.x, position.y, radius, deadZone);
                _stickOrigin = new Vector2(reading.OriginX, reading.OriginY);
                _move = new Vector2(reading.X, reading.Y);
                continue;
            }

            if (held && id == _dashFinger) { dashStillDown = true; continue; }

            // Reste les doigts qui viennent d'arriver. `wasPressedThisFrame` plutôt qu'un test de
            // phase : la phase `Began` n'est visible que l'image où elle arrive, et une image sautée
            // — chose banale au chargement d'une vague — ferait perdre l'appui sans rien signaler.
            if (!arrived) continue;

            // L'ordre suit celui de TouchZones : boutons d'abord, stick en dernier recours.
            if (TouchZones.IsPauseButton(position.x, position.y, w, h))
            {
                _pausePressedFrame = Time.frameCount;
            }
            else if (_dashFinger == NoFinger && TouchZones.IsDashButton(position.x, position.y, w, h))
            {
                _dashFinger = id;
                _dashPressedFrame = Time.frameCount;
                dashStillDown = true;
            }
            else if (_stickFinger == NoFinger &&
                     TouchZones.IsStickZone(position.x, position.y, w, h))
            {
                _stickFinger = id;
                _stickOrigin = position;
                _stickTip = position;
                _move = Vector2.zero;   // poser le pouce ne déplace pas : voir la zone morte
                stickStillDown = true;
            }
        }

        if (_stickFinger != NoFinger && !stickStillDown) ReleaseStick();
        if (_dashFinger != NoFinger && !dashStillDown) _dashFinger = NoFinger;

        UpdateActiveLatch(sawTouch);
    }

    /// <summary>
    /// Bascule entre « le joueur a des doigts » et « le joueur a un clavier ».
    /// </summary>
    private static void UpdateActiveLatch(bool sawTouch)
    {
        if (sawTouch)
        {
            if (!_active) WidenDragThreshold();
            _active = true;
            _touchCapable = true;
            _lastTouchFrame = Time.frameCount;
            return;
        }

        if (!_active) return;

        // ⚠ **Un appui du doigt produit AUSSI un clic de souris** sur la plupart des navigateurs et
        // des systèmes : c'est l'événement de compatibilité, hérité du web d'avant le tactile. Le
        // relâchement se déclenchait donc sur le tapotement lui-même — le joueur touchait l'écran,
        // et le jeu en concluait qu'il était revenu à la souris. Les contrôles disparaissaient à
        // l'appui, et la garde d'orientation se refermait sur un simple contact.
        //
        // On ignore donc tout clic qui suit de près un vrai contact. Une frappe clavier, elle, n'a
        // pas d'équivalent tactile : elle tranche immédiatement.
        bool clickAfterTouch = Time.frameCount - _lastTouchFrame <= CompatibilityClickFrames;

        if ((RawInput.PrimaryClickThisFrame() && !clickAfterTouch) ||
            Keyboard.current?.anyKey.wasPressedThisFrame == true)
            _active = false;
    }

    /// <summary>
    /// Fenêtre, en images, pendant laquelle un clic est tenu pour l'écho d'un contact tactile.
    /// </summary>
    /// <remarks>
    /// Une vingtaine d'images, soit un tiers de seconde : les navigateurs émettent leur clic de
    /// compatibilité jusqu'à ~300 ms après le relâchement du doigt.
    /// </remarks>
    private const int CompatibilityClickFrames = 20;

    private static int _lastTouchFrame = int.MinValue / 2;

    /// <summary>
    /// Élargit le seuil au-delà duquel uGUI requalifie un appui en glissement.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Le défaut classique du tactile sur uGUI, et il se signale « les boutons ne
    /// marchent pas ».</b> Le seuil par défaut est de 10 pixels, calibré pour une souris — qui ne
    /// bouge pas quand on clique. Un doigt, lui, roule de deux ou trois millimètres pendant l'appui :
    /// sur une dalle où un pixel logique vaut 0,2 mm, le seuil est franchi presque à chaque fois.
    /// uGUI conclut alors à un glissement, la liste défile de quelques pixels, et <b>le bouton ne
    /// reçoit jamais son clic</b>. Aucune erreur, aucun symptôme dans un journal : le menu paraît
    /// simplement mort.</para>
    ///
    /// <para>24 pixels, soit environ 4 mm, laisse passer le tremblement d'un pouce sans empêcher un
    /// vrai geste de défilement, qui parcourt plusieurs centimètres. Posé une seule fois, au premier
    /// contact : sur une machine sans dalle, le seuil de la souris reste intact.</para>
    /// </remarks>
    private static void WidenDragThreshold()
    {
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null && events.pixelDragThreshold < TouchDragThreshold)
            events.pixelDragThreshold = TouchDragThreshold;
    }

    /// <summary>Seuil de glissement au doigt, en pixels — voir <see cref="WidenDragThreshold"/>.</summary>
    private const int TouchDragThreshold = 24;

    private static void ReleaseStick()
    {
        _stickFinger = NoFinger;
        _move = Vector2.zero;
    }

    private static void ReleaseAll()
    {
        ReleaseStick();
        _dashFinger = NoFinger;
    }

    /// <summary>
    /// L'objet qui appelle <see cref="Poll"/> une fois par image, hors de toute scène.
    /// </summary>
    /// <remarks>
    /// <c>Update</c> et non <c>FixedUpdate</c> : les entrées se lisent au rythme de l'affichage, et
    /// un <c>wasPressedThisFrame</c> lu à une autre cadence manque des appuis brefs — un tapotement
    /// d'esquive dure moins qu'un pas de physique.
    /// </remarks>
    private sealed class TouchInputPump : MonoBehaviour
    {
        private void Update() => Poll();
    }
}
