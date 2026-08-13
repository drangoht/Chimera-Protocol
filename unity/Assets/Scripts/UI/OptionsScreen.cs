using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Options (Lot 6, remis à la parité visuelle le 2026-08-05).
///
/// <para><b>Cet écran ne propose que des réglages qui agissent.</b> Un réglage mort est le même
/// défaut qu'une arme invisible, appliqué à l'interface. C'est à ce titre que les volumes n'étaient
/// pas affichés avant que l'audio n'existe, et que l'intensité de secousse arrive maintenant —
/// branchée sur <c>ScreenShake.Intensity</c>, pas seulement écrite dans la sauvegarde.</para>
///
/// <para><b>Chaque réglage emploie le contrôle de sa nature</b>, comme le jeu publié
/// (<c>docs/ui_v1160_options.png</c>) : un <b>curseur</b> pour une valeur continue, un
/// <b>interrupteur</b> pour un état, un <b>sélecteur</b> pour un choix parmi trois. Le portage
/// empilait des boutons « Étiquette : valeur » identiques sur toute la largeur — on ne pouvait ni
/// voir où l'on se trouvait dans une course de volume, ni distinguer d'un coup d'œil ce qui se règle
/// de ce qui s'annonce.</para>
/// </summary>
public sealed class OptionsScreen : MonoBehaviour
{
    /// <summary>Émis à la fermeture.</summary>
    public event Action? Closed;

    /// <summary>Émis quand la langue change — l'appelant doit reconstruire ses libellés.</summary>
    public event Action? LanguageChanged;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Lignes de réglage — observable pour les vérifications.</summary>
    public int RowCount { get; private set; }

    /// <summary>
    /// La remise à zéro totale est-elle proposée ? <b>Faux en pleine partie</b> : l'action est
    /// destructrice, et la run en cours réécrirait par-dessus la remise à zéro en s'achevant — le
    /// joueur verrait sa progression revenir. À poser avant <see cref="Show"/>.
    /// </summary>
    public bool AllowFullReset { get; set; } = true;

    private GameObject? _root;
    private Transform? _list;
    private Button? _close;
    private Button? _first;

    /// <summary>Bouton de remise à zéro et son bloc (séparateur compris) — masqués en run.</summary>
    private GameObject? _resetRow;
    private Button? _resetButton;
    private Text? _resetLabel;

    /// <summary>La remise à zéro a-t-elle été <b>armée</b> par un premier clic ?</summary>
    private bool _resetArmed;

    /// <summary>Textes à réécrire quand une valeur change (pourcentages, nom de l'option courante).</summary>
    private readonly List<(Func<string> Text, Text Target)> _dynamic = new();

    private void Awake()
    {
        BuildUi();
        Hide();
    }

    public void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);

        // Le bloc de remise à zéro apparaît ou disparaît À L'OUVERTURE, et non à la construction :
        // le même composant sert au menu principal et à la pause, et c'est l'appelant qui sait
        // laquelle des deux situations on est en train de vivre.
        if (_resetRow != null) _resetRow.SetActive(AllowFullReset);
        DisarmReset();

        Refresh();

        // Le focus va sur le PREMIER réglage, pas sur « Retour » : un écran d'options qui s'ouvre
        // avec la sortie sélectionnée demande un aller-retour avant de pouvoir régler quoi que ce
        // soit, et la première touche pressée referme l'écran.
        var target = _first != null ? _first.gameObject : _close?.gameObject;
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Update()
    {
        if (!IsVisible) return;

        // La capture d'une touche passe AVANT la fermeture : sinon Échap, qui sert à annuler un
        // remap en cours, refermerait l'écran d'un coup.
        if (_awaiting != null) { CaptureBinding(); return; }

        if (RawInput.EscapePressedThisFrame()) Close();
    }

    private void Close()
    {
        // Écrit à la fermeture, une seule fois : sauvegarder à chaque clic multiplierait les
        // écritures disque pour un réglage que le joueur est peut-être en train de faire défiler.
        GameSettings.Save();
        Hide();
        Closed?.Invoke();
    }

    private void Refresh()
    {
        foreach (var (source, target) in _dynamic) target.text = source();
    }

    // ─── Réglages ─────────────────────────────────────────────────────────────

    private void CycleLanguage()
    {
        var settings = GameSettings.Current;

        int index = Array.IndexOf(LocTable.Languages, settings.Language);
        settings.Language = LocTable.Languages[(index + 1) % LocTable.Languages.Length];

        // La table est relue : sans cela, la langue change dans la sauvegarde et l'écran continue
        // d'afficher l'ancienne — un réglage qui paraît sans effet.
        Loc.Language = settings.Language;
        Loc.Reset();

        GameSettings.Save();
        Refresh();
        LanguageChanged?.Invoke();
    }

    private void CycleDifficulty()
    {
        var settings = GameSettings.Current;
        settings.Difficulty = (settings.Difficulty + 1) % 3;
        Refresh();
    }

    /// <summary>
    /// Bascule le plein écran.
    /// </summary>
    /// <remarks>
    /// ⚠ En web, <b>c'est le seul endroit d'où l'appel puisse aboutir</b> : un navigateur n'accorde le
    /// plein écran que pendant un geste de l'utilisateur, et un clic sur cet interrupteur en est un.
    /// Le même appel rejoué au chargement des réglages est refusé et arrête le moteur — d'où son
    /// retrait de <c>GameSettings.ApplyDisplay</c>.
    /// </remarks>
    private static void SetFullscreen(bool on)
    {
        GameSettings.Current.DisplayMode = on ? 2 : 0;

        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    private static void SetVsync(bool on)
    {
        GameSettings.Current.Vsync = on;
        QualitySettings.vSyncCount = on ? 1 : 0;
    }

    private static void SetFps(bool on) => GameSettings.Current.ShowFps = on;

    /// <summary>
    /// Présence Discord. <b>Elle s'applique tout de suite</b> : ce réglage était enregistré et migré
    /// depuis Godot sans que rien ne le lise — un interrupteur mort, dans l'écran qui s'interdit d'en
    /// avoir. Le système entier manquait derrière lui.
    /// </summary>
    private static void SetDiscord(bool on)
    {
        GameSettings.Current.Discord = on;
        GameSettings.ApplyPresence(GameSettings.Current);
    }

    /// <summary>
    /// Secousse d'écran. <b>Elle agit tout de suite</b> : le champ existait dans la sauvegarde depuis
    /// la reprise des réglages Godot, mais <c>ScreenShake.Intensity</c> n'était affecté par personne —
    /// le réglage aurait été un interrupteur mort, exactement ce que cet écran s'interdit.
    /// </summary>
    private static void SetShake(bool on)
    {
        GameSettings.Current.ShakeIntensity = on ? 1f : 0f;
        ScreenShake.Intensity = GameSettings.Current.ShakeIntensity;
    }

    private static string DifficultyName(int difficulty) => difficulty switch
    {
        0 => Loc.T("DIFF_EASY"),
        2 => Loc.T("DIFF_HARD"),
        _ => Loc.T("DIFF_NORMAL"),
    };

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("OptionsCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        // ⚠ AU-DESSUS de l'écran de pause (110). Les options s'ouvrent depuis deux endroits : le
        // menu principal, où rien ne les recouvre, et la PAUSE — et là, un ordre inférieur les
        // faisait apparaître derrière elle. Le joueur voyait le voile s'assombrir sans savoir que
        // l'écran demandé s'était bien ouvert, dessous.
        UiCanvas.Configure(canvasGo, 115);

        _root = canvasGo;
        UiStyle.ScreenBackdrop(canvasGo.transform);

        // Panneau CENTRÉ et borné, non plein écran — comme le jeu publié : une colonne de réglages
        // étalée sur 1920 px sépare tellement le libellé de son contrôle qu'on ne les relie plus.
        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Cyan);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 860f);
        panelRect.anchoredPosition = Vector2.zero;

        float headerBottom = UiStyle.Header(panel.transform, Loc.T("OPTIONS_TITLE"));

        // ⚠ La liste DOIT défiler depuis que la section Contrôles existe : quinze lignes ne tiennent
        // pas dans un panneau, et un contenu centré qui déborde sort des DEUX côtés — le défaut déjà
        // rencontré sur l'écran de pause, où « Quitter la partie » finissait hors cadre.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        // Le bas laisse la place au bouton de retour : sans cette marge, la derniere ligne
        // passe DESSOUS et les deux se chevauchent.
        scrollRect.offsetMin = new Vector2(56f, 100f);
        scrollRect.offsetMax = new Vector2(-56f, -headerBottom);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        UiStyle.ConfigureScroll(scroll);
        scrollGo.AddComponent<RectMask2D>();

        var column = UiStyle.NewUiObject("Rows", scrollGo.transform);
        var columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = new Vector2(0f, 1f);
        columnRect.anchorMax = new Vector2(1f, 1f);
        columnRect.pivot = new Vector2(0.5f, 1f);

        // ⚠ Largeur remise à ZÉRO. Un RectTransform naît en 100 × 100 : étiré entre deux ancres
        // horizontales, il vaudrait « largeur du parent + 100 » et déborderait de 50 px de chaque
        // côté de sa fenêtre de défilement.
        columnRect.sizeDelta = Vector2.zero;

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        column.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = columnRect;
        scroll.viewport = scrollRect;

        _list = column.transform;

        // Ordre du jeu publié : d'abord ce qui s'entend, puis ce qui se voit, puis ce qui se joue.
        AddSlider(Loc.T("OPTIONS_MASTER"), () => GameSettings.Current.MasterVolume,
                  v => GameSettings.Current.MasterVolume = v);
        AddSlider(Loc.T("OPTIONS_MUSIC"), () => GameSettings.Current.MusicVolume,
                  v => GameSettings.Current.MusicVolume = v);
        AddSlider(Loc.T("OPTIONS_SFX"), () => GameSettings.Current.SfxVolume,
                  v => GameSettings.Current.SfxVolume = v);

        // « Plein écran » et non « Mode d'affichage » : le contrôle est devenu un interrupteur, et un
        // interrupteur doit nommer ce qu'il ACTIVE — « Mode d'affichage : allumé » ne veut rien dire.
        AddSwitch(Loc.T("OPTIONS_DISPLAY_FULLSCREEN"), GameSettings.Current.DisplayMode == 2, SetFullscreen);
        AddSwitch(Loc.T("OPTIONS_SHAKE"), GameSettings.Current.ShakeIntensity > 0.01f, SetShake);
        // ⚠ Pas de synchronisation verticale dans un navigateur : la cadence d'affichage y est
        // imposée par `requestAnimationFrame`, et `QualitySettings.vSyncCount` y est purement et
        // simplement ignoré. Même raison que pour Discord — un interrupteur qui ne pilote rien.
        if (Application.platform != RuntimePlatform.WebGLPlayer)
            AddSwitch(Loc.T("OPTIONS_VSYNC"), GameSettings.Current.Vsync, SetVsync);
        AddSwitch(Loc.T("OPTIONS_SHOW_FPS"), GameSettings.Current.ShowFps, SetFps);
        // ⚠ L'interrupteur n'existe QUE là où la présence existe. Sur le web, la bibliothèque Discord
        // est retirée du build — un canal local vers un client installé n'a pas d'équivalent dans un
        // navigateur — et l'offrir quand même donnerait le réglage mort que cet écran s'interdit
        // depuis qu'il en a eu un. Le champ reste dans la sauvegarde : elle est commune aux
        // plateformes, et un joueur qui joue aux deux ne doit pas perdre son choix côté bureau.
        if (DiscordPresence.Available)
            AddSwitch(Loc.T("OPTIONS_DISCORD"), GameSettings.Current.Discord, SetDiscord);

        AddSelector(Loc.T("OPTIONS_DIFFICULTY"),
                    () => DifficultyName(GameSettings.Current.Difficulty), CycleDifficulty);
        AddSelector(Loc.T("OPTIONS_LANGUAGE"),
                    () => GameSettings.Current.Language.ToUpperInvariant(), CycleLanguage);

        AddSectionTitle(Loc.T("OPTIONS_CONTROLS"));

        AddBinding(Loc.T("OPTIONS_MOVE_UP"),    GameAction.MoveUp);
        AddBinding(Loc.T("OPTIONS_MOVE_DOWN"),  GameAction.MoveDown);
        AddBinding(Loc.T("OPTIONS_MOVE_LEFT"),  GameAction.MoveLeft);
        AddBinding(Loc.T("OPTIONS_MOVE_RIGHT"), GameAction.MoveRight);
        AddBinding(Loc.T("OPTIONS_DASH"),       GameAction.Dash);

        AddResetRow();

        _close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = _close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 18f);
        _close.onClick.AddListener(Close);
    }

    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)} %";

    // ─── Lignes ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Ligne à curseur : libellé, course, et le <b>pourcentage à droite</b>. Le chiffre n'est pas
    /// redondant avec la course — l'un se compare d'un coup d'œil, l'autre se dit.
    /// </summary>
    private void AddSlider(string label, Func<float> get, Action<float> set)
    {
        if (_list == null) return;

        var control = UiStyle.SettingRow(_list, label);

        var readout = UiStyle.Label(control, Percent(get()), 22, UiPalette.Cyan, TextAnchor.MiddleRight);
        var readoutRect = readout.GetComponent<RectTransform>();
        readoutRect.anchorMin = new Vector2(1f, 0f);
        readoutRect.anchorMax = new Vector2(1f, 1f);
        readoutRect.pivot = new Vector2(1f, 0.5f);
        readoutRect.sizeDelta = new Vector2(120f, 0f);
        readoutRect.anchoredPosition = Vector2.zero;

        var slider = UiStyle.HSlider(control, get(), value =>
        {
            set(value);

            // Appliqué IMMÉDIATEMENT : un volume qui ne changerait qu'à la fermeture de l'écran ne
            // se règle pas à l'oreille.
            GameSettings.ApplyVolumes(GameSettings.Current);
            readout.text = Percent(value);
        });

        // Un repère sonore quand on lâche le curseur — pas à chaque frame de glissement, sinon le
        // réglage se fait sous une mitraillette de clics.
        var trigger = slider.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => AudioSystem.PlaySfx("sfx_ui_button", pitchVariation: 0f));
        trigger.triggers.Add(entry);

        _dynamic.Add((() => Percent(get()), readout));
        _first ??= null;   // un curseur n'est pas un Button : le focus initial reste sur le 1er bouton
        RowCount++;
    }

    private void AddSwitch(string label, bool value, Action<bool> set)
    {
        if (_list == null) return;

        var control = UiStyle.SettingRow(_list, label);
        UiStyle.Switch(control, value, on =>
        {
            set(on);
            AudioSystem.PlaySfx("sfx_ui_button", pitchVariation: 0f);
            Refresh();
        });

        RowCount++;
    }

    /// <summary>
    /// Titre de section : un séparateur d'accent et un intitulé, comme le jeu publié. Sans lui, les
    /// cinq lignes de touches se lisent comme cinq réglages de plus.
    /// </summary>
    private void AddSectionTitle(string title)
    {
        if (_list == null) return;

        var row = UiStyle.NewUiObject("Section_" + title, _list);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 56f;
        element.preferredHeight = 56f;

        var label = UiStyle.Label(row.transform, title, 24, UiPalette.Cyan, TextAnchor.LowerCenter);
        UiStyle.Stretch(label.gameObject, 0f);

        var rule = UiStyle.NewUiObject("Rule", row.transform);
        rule.AddComponent<Image>().color = UiPalette.WithAlpha(UiPalette.Cyan, 0.5f);

        var ruleRect = rule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 0f);
        ruleRect.anchorMax = new Vector2(1f, 0f);
        ruleRect.pivot = new Vector2(0.5f, 0f);
        ruleRect.sizeDelta = new Vector2(0f, 2f);
        ruleRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>Action dont on attend la nouvelle touche, s'il y en a une.</summary>
    private GameAction? _awaiting;

    /// <summary>
    /// Ligne de remappage : le bouton affiche la touche courante et, une fois pressé, <b>attend la
    /// suivante</b>.
    ///
    /// <para>⚠ Le libellé se lit toujours depuis <see cref="InputRemap"/> et n'est jamais écrit en
    /// dur — c'est la règle qui empêche l'aide de devenir mensongère après un remap. Le projet a
    /// déjà perdu une session entière sur une capacité dont la touche n'était annoncée nulle
    /// part.</para>
    /// </summary>
    private void AddBinding(string label, GameAction action)
    {
        if (_list == null) return;

        var control = UiStyle.SettingRow(_list, label);
        var button = UiStyle.TextButton(control, InputRemap.DisplayName(action), FrameAccent.Steel);

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 46f);
        rect.anchoredPosition = Vector2.zero;

        var text = button.GetComponentInChildren<Text>();

        button.onClick.AddListener(() =>
        {
            _awaiting = action;
            if (text != null) text.text = Loc.T("OPTIONS_CONTROLS_PRESS");
        });

        if (text != null) _dynamic.Add((() => InputRemap.DisplayName(action), text));
        RowCount++;
    }

    /// <summary>
    /// Capture la touche suivante et l'affecte à l'action en attente.
    ///
    /// <para><b>Échap annule</b> plutôt que de se lier : une action liée à Échap rendrait le menu
    /// impossible à fermer, et le joueur n'aurait plus aucun moyen de revenir en arrière.</para>
    /// </summary>
    private void CaptureBinding()
    {
        if (_awaiting == null) return;

        if (RawInput.EscapePressedThisFrame())
        {
            _awaiting = null;
            Refresh();
            return;
        }

        var key = RawInput.FirstKeyPressedThisFrame();
        if (key == UnityEngine.InputSystem.Key.None) return;

        InputRemap.Rebind(_awaiting.Value, key);
        _awaiting = null;
        AudioSystem.PlaySfx("sfx_ui_button", pitchVariation: 0f);
        Refresh();
    }

    // ─── Remise à zéro totale ─────────────────────────────────────────────────

    /// <summary>
    /// Bouton « Tout réinitialiser », en bas de la liste, sous son propre séparateur.
    ///
    /// <para><b>Il manquait entièrement au portage</b> — le jeu publié le propose depuis les options
    /// (<c>OptionsScreen.AddResetButton</c>), et c'est le seul moyen pour un joueur de repartir d'un
    /// jeu vierge : la sauvegarde vit dans un dossier système qu'on ne lui demandera pas d'aller
    /// ouvrir à la main.</para>
    ///
    /// <para><b>Deux clics, pas de modale.</b> Le premier arme, le second efface — l'intitulé change
    /// entre les deux et dit ce qui va disparaître. Une fenêtre de confirmation supplémentaire ne
    /// protégerait pas mieux (on y clique « oui » aussi vite) et demanderait une chaîne de focus de
    /// plus dans un écran qui en compte déjà quinze.</para>
    /// </summary>
    private void AddResetRow()
    {
        if (_list == null) return;

        var row = UiStyle.NewUiObject("ResetRow", _list);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 108f;
        element.preferredHeight = 108f;

        var rule = UiStyle.NewUiObject("Rule", row.transform);
        rule.AddComponent<Image>().color = UiPalette.WithAlpha(UiPalette.Danger, 0.45f);

        var ruleRect = rule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(1f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.sizeDelta = new Vector2(0f, 2f);
        ruleRect.anchoredPosition = Vector2.zero;

        // Accent « danger » : l'action destructrice se distingue par son LISERÉ autant que par la
        // couleur de son texte — c'est la règle du jeu publié, et elle vaut aussi pour qui ne
        // distingue pas les couleurs.
        _resetButton = UiStyle.TextButton(row.transform, Loc.T("OPTIONS_RESET"), FrameAccent.Danger);

        var buttonRect = _resetButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        // Largeur dictée par le libellé le PLUS LONG — celui de l'armement, qui porte
        // l'avertissement — et non par celui du repos : un cadre calé sur « Tout réinitialiser »
        // coupait « irréversible — efface Échos & progression » (667 px de texte pour 536 utiles,
        // relevé au banc). Le bouton demanderait alors une confirmation sans dire de quoi.
        buttonRect.sizeDelta = new Vector2(740f, 56f);
        buttonRect.anchoredPosition = new Vector2(0f, 10f);

        _resetLabel = _resetButton.GetComponentInChildren<Text>();
        if (_resetLabel != null)
        {
            _resetLabel.color = UiPalette.Danger;

            // Le libellé suit la langue comme les autres, mais il dépend AUSSI de l'état du bouton :
            // le relire depuis une clé fixe rendrait son armement au premier changement de langue.
            _dynamic.Add((ResetLabelText, _resetLabel));
        }

        _resetButton.onClick.AddListener(OnResetPressed);

        _resetRow = row;
        RowCount++;
    }

    /// <summary>
    /// Premier clic : arme. Second : efface les deux sauvegardes.
    /// </summary>
    /// <remarks>
    /// ⚠ Le bouton se <b>désactive</b> ensuite au lieu de disparaître, et annonce « ✓ Réinitialisé ».
    /// Un bouton qui reprend son intitulé de départ après un effacement laisse croire que rien ne
    /// s'est produit — et invite à recommencer.
    /// </remarks>
    private void OnResetPressed()
    {
        AudioSystem.PlaySfx("sfx_ui_button", pitchVariation: 0f);

        if (!_resetArmed)
        {
            _resetArmed = true;
            if (_resetLabel != null) _resetLabel.text = Loc.T("OPTIONS_RESET_CONFIRM");
            return;
        }

        GameSettings.ResetEverything();

        _resetArmed = false;
        if (_resetLabel != null) _resetLabel.text = Loc.T("OPTIONS_RESET_DONE");
        if (_resetButton != null) _resetButton.interactable = false;
    }

    /// <summary>
    /// Désarme à chaque ouverture : un bouton laissé armé effacerait tout au premier clic d'une
    /// visite ultérieure, sans que le joueur ait rien confirmé cette fois-là.
    /// </summary>
    private void DisarmReset()
    {
        _resetArmed = false;

        if (_resetButton != null) _resetButton.interactable = true;
        if (_resetLabel != null)
        {
            _resetLabel.text = ResetLabelText();
            _resetLabel.color = UiPalette.Danger;
        }
    }

    /// <summary>Intitulé du bouton dans son état courant : au repos, armé, ou déjà exécuté.</summary>
    private string ResetLabelText()
    {
        if (_resetButton != null && !_resetButton.interactable) return Loc.T("OPTIONS_RESET_DONE");
        return _resetArmed ? Loc.T("OPTIONS_RESET_CONFIRM") : Loc.T("OPTIONS_RESET");
    }

    private void AddSelector(string label, Func<string> value, Action next)
    {
        if (_list == null) return;

        var control = UiStyle.SettingRow(_list, label);
        var button = UiStyle.Selector(control, value(), () => { next(); Refresh(); });

        var text = button.GetComponentInChildren<Text>();
        if (text != null) _dynamic.Add((value, text));

        _first ??= button;
        RowCount++;
    }
}
