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

        BuildUi();

        // La pause ne survit jamais à un retour au menu : rester à timeScale 0 produirait un menu
        // définitivement figé, sans cause visible.
        SceneRoot.Paused = false;
        ModalQueue.Reset();

        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("MenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = UiStyle.NewUiObject("Background", canvasGo.transform);
        bg.AddComponent<Image>().color = UiPalette.Bg;
        UiStyle.Stretch(bg, 0f);

        var title = UiStyle.Label(canvasGo.transform, "CHIMERA PROTOCOL", 64,
                                  UiPalette.Cyan, TextAnchor.MiddleCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(900f, 100f);
        titleRect.anchoredPosition = new Vector2(0f, -110f);

        var column = UiStyle.NewUiObject("Menu", canvasGo.transform);
        var colRect = column.GetComponent<RectTransform>();
        colRect.anchorMin = colRect.anchorMax = new Vector2(0.5f, 0.5f);
        colRect.pivot = new Vector2(0.5f, 0.5f);
        colRect.sizeDelta = new Vector2(420f, 460f);
        colRect.anchoredPosition = new Vector2(0f, -40f);

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var play = AddEntry(column.transform, "Jouer", FrameAccent.Cyan, enabled: true);
        play.onClick.AddListener(() =>
        {
            PlayRequested?.Invoke();
            OpenLevelSelect();
        });
        _firstButton = play;

        var hub = AddEntry(column.transform, "Hub", FrameAccent.Gold, enabled: true);
        hub.onClick.AddListener(OpenHub);

        var challenges = AddEntry(column.transform, Loc.T("CHALLENGES_TITLE"), FrameAccent.Violet, enabled: true);
        challenges.onClick.AddListener(OpenChallenges);

        var options = AddEntry(column.transform, Loc.T("OPTIONS_TITLE"), FrameAccent.Steel, enabled: true);
        options.onClick.AddListener(OpenOptions);

        var codex = AddEntry(column.transform, Loc.T("MENU_CODEX"), FrameAccent.Violet, enabled: true);
        codex.onClick.AddListener(OpenCodex);

        var quit = AddEntry(column.transform, "Quitter", FrameAccent.Danger, enabled: true);
        quit.onClick.AddListener(SceneRoot.Quit);

        BuildFocusChain(column.transform);
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
