using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu principal (Lot 5).
///
/// <para>Reprend l'organisation du jeu publié : <b>Jouer · Hub · Codex · Options · Quitter</b>. Les
/// écrans d'information vivent sous un sous-menu <b>Codex</b> — un regroupement adopté parce que le
/// menu principal était devenu illisible à force d'y ajouter des entrées (Bestiaire, Arsenal,
/// Chimère, Défis, Perks).</para>
///
/// <para>⚠ Les entrées non encore portées sont <b>désactivées et visibles</b>, jamais masquées.
/// Le projet a déjà appris cette leçon deux fois — un sélecteur de saturation qui disparaissait
/// sans un mot, une capacité de dash qui n'annonçait sa touche nulle part : <b>invisible se lit
/// inexistant</b>. Une entrée grisée dit « pas encore » ; une entrée absente dit « jamais ».</para>
/// </summary>
public sealed class MainMenuScreen : MonoBehaviour
{
    /// <summary>Émis quand le joueur lance une partie.</summary>
    public event Action? PlayRequested;

    /// <summary>Boutons construits, dans l'ordre d'affichage — observable pour les tests.</summary>
    public int ButtonCount { get; private set; }

    private Button? _firstButton;

    private void Start()
    {
        // Premier accès aux réglages : c'est lui qui déclenche, une fois pour toutes, la reprise
        // d'une installation Godot. Le faire ici — avant toute scène de jeu — garantit qu'aucune
        // écriture n'a pu créer un fichier vierge et condamner la migration.
        _ = GameSettings.Current;

        // Le menu a sa propre piste : c'est le premier son du jeu, et le laisser muet donne
        // l'impression que rien n'a démarré.
        MusicDirector.Instance?.PlaySingle("music_menu");

        BuildUi();

        // La pause ne survit jamais à un retour au menu : rester à timeScale 0 produirait un menu
        // définitivement figé, sans cause visible.
        SceneRoot.Paused = false;
        ModalQueue.Reset();

        // Le statut Discord redevient « dans les menus » : posé seulement à l'entrée en run, il
        // laisserait les contacts du joueur le croire en partie longtemps après sa mort.
        DiscordPresence.Instance?.SetInMenus();

        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("MenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo);

        var bg = UiStyle.NewUiObject("Background", canvasGo.transform);
        bg.AddComponent<Image>().color = UiPalette.Bg;
        UiStyle.Stretch(bg, 0f);

        BuildSplashArt(canvasGo.transform);
        BuildTitleFlair(canvasGo.transform);

        var column = UiStyle.NewUiObject("Menu", canvasGo.transform);
        var colRect = column.GetComponent<RectTransform>();
        colRect.anchorMin = colRect.anchorMax = new Vector2(0.5f, 0.5f);
        colRect.pivot = new Vector2(0.5f, 0.5f);
        colRect.sizeDelta = new Vector2(460f, 460f);
        colRect.anchoredPosition = new Vector2(0f, -20f);

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        // ⚠ Toutes les entrées en CYAN, sauf « Quitter » — c'est la disposition du jeu publié
        // (docs/ui_v1160_menu.png), et elle est ce qui rend le focus lisible : le focus s'annonce en
        // violet, et une teinte ne ressort que sur un fond uniforme. Le portage donnait une couleur
        // différente à chaque entrée ; le menu paraissait plus riche et la sélection s'y perdait.
        var play = AddEntry(column.transform, "Jouer", FrameAccent.Cyan, enabled: true);
        play.onClick.AddListener(() =>
        {
            PlayRequested?.Invoke();
            OpenLevelSelect();
        });
        _firstButton = play;

        var hub = AddEntry(column.transform, "Hub", FrameAccent.Cyan, enabled: true);
        hub.onClick.AddListener(OpenHub);

        var challenges = AddEntry(column.transform, Loc.T("CHALLENGES_TITLE"), FrameAccent.Cyan, enabled: true);
        challenges.onClick.AddListener(OpenChallenges);

        var options = AddEntry(column.transform, Loc.T("OPTIONS_TITLE"), FrameAccent.Cyan, enabled: true);
        options.onClick.AddListener(OpenOptions);

        var codex = AddEntry(column.transform, Loc.T("MENU_CODEX"), FrameAccent.Cyan, enabled: true);
        codex.onClick.AddListener(OpenCodex);

        var quit = AddEntry(column.transform, "Quitter", FrameAccent.Gold, enabled: true);
        quit.onClick.AddListener(SceneRoot.Quit);

        BuildFocusChain(column.transform);
        BuildLanguageRow(canvasGo.transform);
        BuildVersionStamp(canvasGo.transform);
    }

    /// <summary>
    /// L'illustration de couverture, plein écran.
    ///
    /// <para><b>Le titre du jeu est peint dedans</b> — il n'y a pas de libellé « CHIMERA PROTOCOL »
    /// à afficher par-dessus. Le portage en dessinait un en police monospace cyan, ce qui donnait un
    /// menu correct mais anonyme : le logo néon, la Chimère et la ville sont l'identité du jeu, et
    /// c'est le premier écran que voit un joueur.</para>
    ///
    /// <para>Cadrage en <b>aspect couvert</b> : l'illustration remplit l'écran quitte à déborder,
    /// jamais déformée. Étirée à un autre format, la silhouette de la Chimère grossit ou s'écrase —
    /// et c'est immédiatement visible sur un personnage.</para>
    /// </summary>
    private static void BuildSplashArt(Transform parent)
    {
        var sprite = Resources.Load<Sprite>("Ui/splash_art");
        if (sprite == null)
        {
            Debug.LogWarning("[MainMenu] Ui/splash_art introuvable — fond uni.");
            return;
        }

        var go = UiStyle.NewUiObject("SplashArt", parent);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        UiStyle.Stretch(go, 0f);

        // preserveAspect d'uGUI fait TENIR l'image dans le cadre (contain), là où il faut la faire
        // le RECOUVRIR (cover). D'où le calcul : on agrandit le rectangle du facteur qui manque.
        var rect = go.GetComponent<RectTransform>();
        float artRatio = sprite.rect.width / sprite.rect.height;
        float screenRatio = 1920f / 1080f;

        if (artRatio > screenRatio) rect.offsetMin = rect.offsetMax = Vector2.zero;
        else
        {
            float grow = (screenRatio / artRatio - 1f) * 1080f * 0.5f;
            rect.offsetMin = new Vector2(0f, -grow);
            rect.offsetMax = new Vector2(0f, grow);
        }

        // Vignette : l'illustration est claire en son centre, et les boutons s'y perdaient. Sous
        // Godot c'est un shader ; ici un simple dégradé radial suffit — il n'y a rien à animer.
        var vignette = UiStyle.NewUiObject("Vignette", parent);
        var vig = vignette.AddComponent<Image>();
        vig.sprite = UiVignette.Sprite;
        vig.color = new Color(0f, 0f, 0f, 0.72f);
        vig.raycastTarget = false;
        UiStyle.Stretch(vignette, 0f);
    }

    /// <summary>
    /// Titre cosmétique équipé, sous le logo. Masqué si aucun n'est porté — c'est un flair, pas une
    /// ligne d'interface : afficher « aucun titre » reviendrait à rappeler au joueur ce qu'il n'a pas.
    /// </summary>
    private void BuildTitleFlair(Transform parent)
    {
        string id = MetaProgression.EquippedCosmetic;
        var def = id.Length > 0 ? Titles.ById(id) : null;
        if (def == null) return;

        var flair = UiStyle.Label(parent, $"— {Loc.T(def.NameKey)} —", 26,
                                  UiPalette.Gold, TextAnchor.MiddleCenter);

        var rect = flair.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(800f, 34f);
        rect.anchoredPosition = new Vector2(0f, -272f);
    }

    /// <summary>
    /// Sélecteur de langue en drapeaux, en haut à droite.
    ///
    /// <para>Des <b>drapeaux</b> et non des libellés : l'écran est le premier du jeu, et un joueur
    /// qui ne lit pas la langue affichée doit pouvoir en changer sans la comprendre.</para>
    /// </summary>
    private void BuildLanguageRow(Transform parent)
    {
        var row = UiStyle.NewUiObject("Languages", parent);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(1f, 1f);
        rowRect.sizeDelta = new Vector2(240f, 46f);
        rowRect.anchoredPosition = new Vector2(-28f, -24f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childForceExpandWidth = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        foreach (string lang in LocTable.Languages)
        {
            string code = lang;
            var button = UiStyle.TextButton(row.transform, "", FrameAccent.Steel);

            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 62f;
            element.preferredHeight = 42f;

            var flag = Resources.Load<Sprite>("Ui/flag_" + code);
            if (flag != null)
            {
                var icon = UiStyle.NewUiObject("Flag", button.transform);
                var image = icon.AddComponent<Image>();
                image.sprite = flag;
                image.raycastTarget = false;
                UiStyle.Stretch(icon, 12f);

                // Le libellé de repli disparaît : sans cela, le code de langue se superpose au drapeau.
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.text = "";
            }
            else
            {
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.text = code.ToUpperInvariant();
            }

            button.onClick.AddListener(() => ChooseLanguage(code));
        }
    }

    private void ChooseLanguage(string language)
    {
        AudioSystem.PlaySfx("sfx_ui_button");

        var settings = GameSettings.Current;
        if (settings.Language == language) return;

        settings.Language = language;
        Loc.Language = language;
        Loc.Reset();       // sans cela la table reste celle de l'ancienne langue
        GameSettings.Save();

        // Recharger la scène plutôt que ré-appliquer les libellés un par un : chaque écran se
        // reconstruit avec la nouvelle table, sans qu'aucun n'ait à connaître le mécanisme.
        SceneRoot.ChangeScene(GameScenes.MainMenu);
    }

    /// <summary>
    /// Tampon de version, en bas à droite. Il ne sert pas au joueur mais au <b>rapport de bug</b> :
    /// sans lui, une capture d'écran ne dit pas quelle version elle montre.
    /// </summary>
    private static void BuildVersionStamp(Transform parent)
    {
        var stamp = UiStyle.Label(parent, "v" + Application.version, 16,
                                  UiPalette.WithAlpha(UiPalette.Dim, 0.7f), TextAnchor.LowerRight);

        var rect = stamp.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(320f, 24f);
        rect.anchoredPosition = new Vector2(-16f, 12f);
    }

    private HubScreen? _hub;
    private LevelSelectScreen? _levelSelect;

    /// <summary>
    /// Ouvre le choix du niveau. « Jouer » ne lance plus directement : le biome décide du palier de
    /// menace, de la faune et de l'incarnation du boss — le choisir <b>est</b> une décision de jeu.
    /// </summary>
    private void OpenLevelSelect()
    {
        if (_levelSelect == null)
        {
            _levelSelect = gameObject.AddComponent<LevelSelectScreen>();
            _levelSelect.Closed += RestoreFocus;
        }

        _levelSelect.Show();
    }

    private CodexScreen? _codex;

    private void OpenCodex()
    {
        if (_codex == null)
        {
            _codex = gameObject.AddComponent<CodexScreen>();
            _codex.Closed += RestoreFocus;
        }

        _codex.Show();
    }

    private OptionsScreen? _options;

    private void OpenOptions()
    {
        if (_options == null)
        {
            _options = gameObject.AddComponent<OptionsScreen>();
            _options.Closed += RestoreFocus;

            // Changer de langue doit se voir IMMÉDIATEMENT, y compris sur le menu qui est dessous.
            // Recharger la scène est le moyen le plus sûr : chaque écran se reconstruit avec la
            // nouvelle table, sans qu'aucun d'eux n'ait à connaître le mécanisme.
            _options.LanguageChanged += () => SceneRoot.ChangeScene(GameScenes.MainMenu);
        }

        _options.Show();
    }

    private ChallengeScreen? _challenges;

    private void OpenChallenges()
    {
        if (_challenges == null)
        {
            _challenges = gameObject.AddComponent<ChallengeScreen>();
            _challenges.Closed += RestoreFocus;
        }

        _challenges.Show();
    }

    private void RestoreFocus()
    {
        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    /// <summary>
    /// Ouvre le Hub par-dessus le menu. Il est créé à la première demande : le construire d'emblée
    /// ferait lire la sauvegarde et le catalogue avant même que le joueur ne s'y intéresse.
    /// </summary>
    private void OpenHub()
    {
        if (_hub == null)
        {
            _hub = gameObject.AddComponent<HubScreen>();
            _hub.Closed += RestoreFocus;
        }

        _hub.Show();
    }

    private Button AddEntry(Transform parent, string label, FrameAccent accent, bool enabled)
    {
        var button = UiStyle.TextButton(parent, label, accent);
        button.interactable = enabled;

        var le = button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 64f;

        ButtonCount++;
        return button;
    }

    /// <summary>
    /// Chaîne de focus explicite, ne parcourant que les entrées <b>actives</b>. La navigation
    /// automatique d'Unity s'arrête sur un bouton désactivé : au clavier, le menu serait bloqué à la
    /// première entrée grisée.
    /// </summary>
    private static void BuildFocusChain(Transform column)
    {
        var all = column.GetComponentsInChildren<Button>();
        var usable = new System.Collections.Generic.List<Button>();
        foreach (var b in all) if (b.interactable) usable.Add(b);
        if (usable.Count == 0) return;

        for (int i = 0; i < usable.Count; i++)
        {
            var nav = usable[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp   = usable[(i - 1 + usable.Count) % usable.Count];
            nav.selectOnDown = usable[(i + 1) % usable.Count];
            usable[i].navigation = nav;
        }
    }
}
