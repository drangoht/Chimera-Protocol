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

    private GameObject? _root;
    private Transform? _list;
    private Button? _close;
    private Button? _first;

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
        if (IsVisible && Input.GetKeyDown(KeyCode.Escape)) Close();
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

        UiCanvas.Configure(canvasGo, 96);

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

        var column = UiStyle.NewUiObject("Rows", panel.transform);
        var columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = Vector2.zero;
        columnRect.anchorMax = Vector2.one;
        // Le bas laisse la place au bouton de retour : sans cette marge, la derniere ligne
        // passe DESSOUS et les deux se chevauchent.
        columnRect.offsetMin = new Vector2(56f, 100f);
        columnRect.offsetMax = new Vector2(-56f, -headerBottom);

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

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
        AddSwitch(Loc.T("OPTIONS_VSYNC"), GameSettings.Current.Vsync, SetVsync);
        AddSwitch(Loc.T("OPTIONS_SHOW_FPS"), GameSettings.Current.ShowFps, SetFps);

        AddSelector(Loc.T("OPTIONS_DIFFICULTY"),
                    () => DifficultyName(GameSettings.Current.Difficulty), CycleDifficulty);
        AddSelector(Loc.T("OPTIONS_LANGUAGE"),
                    () => GameSettings.Current.Language.ToUpperInvariant(), CycleLanguage);

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
