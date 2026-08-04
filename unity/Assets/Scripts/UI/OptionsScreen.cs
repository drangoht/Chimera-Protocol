using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Options (Lot 6).
///
/// <para><b>Cet écran ne propose que des réglages qui agissent.</b> Les curseurs de volume et
/// l'intensité de secousse existent dans la sauvegarde — ils viennent de la version Godot — mais ni
/// l'audio ni la secousse d'écran ne sont portés : les afficher donnerait des réglages morts, c'est-à-dire
/// exactement le défaut des armes invisibles, appliqué à l'interface. Ils reviendront avec ce qu'ils
/// pilotent.</para>
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

    private readonly List<(Func<string> Label, Button Button, Text Text)> _rows = new();

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

        if (_close != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_close.gameObject);
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
        foreach (var (label, _, text) in _rows) text.text = label();
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

    private void ToggleFullscreen()
    {
        var settings = GameSettings.Current;
        settings.DisplayMode = settings.DisplayMode == 2 ? 0 : 2;

        Screen.fullScreenMode = settings.DisplayMode == 2
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        Refresh();
    }

    private void ToggleVsync()
    {
        var settings = GameSettings.Current;
        settings.Vsync = !settings.Vsync;
        QualitySettings.vSyncCount = settings.Vsync ? 1 : 0;
        Refresh();
    }

    private void ToggleFps()
    {
        GameSettings.Current.ShowFps = !GameSettings.Current.ShowFps;
        Refresh();
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

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 96;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Steel);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1000f, 880f);
        panelRect.anchoredPosition = Vector2.zero;

        var title = UiStyle.Label(panel.transform, Loc.T("OPTIONS_TITLE"), 38,
                                  UiPalette.Cyan, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -80f);
        titleRect.offsetMax = new Vector2(-24f, -24f);

        var column = UiStyle.NewUiObject("Rows", panel.transform);
        var columnRect = column.GetComponent<RectTransform>();
        columnRect.anchorMin = Vector2.zero;
        columnRect.anchorMax = Vector2.one;
        // Le bas laisse la place au bouton de retour : sans cette marge, la derniere ligne
        // passe DESSOUS et les deux se chevauchent.
        columnRect.offsetMin = new Vector2(40f, 100f);
        columnRect.offsetMax = new Vector2(-40f, -100f);

        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        _list = column.transform;

        AddRow(() => $"{Loc.T("OPTIONS_LANGUAGE")} : {GameSettings.Current.Language.ToUpperInvariant()}",
               CycleLanguage);
        AddRow(() => $"{Loc.T("OPTIONS_DIFFICULTY")} : {DifficultyName(GameSettings.Current.Difficulty)}",
               CycleDifficulty);
        AddRow(() => $"{Loc.T("OPTIONS_DISPLAY_MODE")} : " +
                     Loc.T(GameSettings.Current.DisplayMode == 2
                         ? "OPTIONS_DISPLAY_FULLSCREEN" : "OPTIONS_DISPLAY_WINDOWED"),
               ToggleFullscreen);
        AddRow(() => $"{Loc.T("OPTIONS_VSYNC")} : {OnOff(GameSettings.Current.Vsync)}", ToggleVsync);
        AddRow(() => $"{Loc.T("OPTIONS_SHOW_FPS")} : {OnOff(GameSettings.Current.ShowFps)}", ToggleFps);

        // Les volumes ne sont revenus qu'avec l'audio : tant que rien ne jouait, ces trois lignes
        // auraient été des réglages morts.
        AddRow(() => $"{Loc.T("OPTIONS_MASTER")} : {Percent(GameSettings.Current.MasterVolume)}",
               () => CycleVolume(v => GameSettings.Current.MasterVolume = v, GameSettings.Current.MasterVolume));
        AddRow(() => $"{Loc.T("OPTIONS_MUSIC")} : {Percent(GameSettings.Current.MusicVolume)}",
               () => CycleVolume(v => GameSettings.Current.MusicVolume = v, GameSettings.Current.MusicVolume));
        AddRow(() => $"{Loc.T("OPTIONS_SFX")} : {Percent(GameSettings.Current.SfxVolume)}",
               () => CycleVolume(v => GameSettings.Current.SfxVolume = v, GameSettings.Current.SfxVolume));

        _close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = _close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 18f);
        _close.onClick.AddListener(Close);
    }

    private static string OnOff(bool value) => value ? Loc.T("COMMON_ON") : Loc.T("COMMON_OFF");

    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)} %";

    /// <summary>
    /// Fait défiler un volume par paliers de 25 %, jusqu'à zéro puis retour au maximum.
    ///
    /// <para>Des paliers plutôt qu'un curseur : un curseur uGUI se règle mal à la manette, et le
    /// projet n'a pas d'écart audible entre 62 % et 65 %. Le réglage est <b>appliqué immédiatement</b>
    /// — un volume qui ne changerait qu'à la fermeture de l'écran ne se règle pas à l'oreille.</para>
    /// </summary>
    private void CycleVolume(Action<float> setter, float current)
    {
        float next = current <= 0.001f ? 1f : Mathf.Max(0f, current - 0.25f);

        setter(next);
        GameSettings.ApplyVolumes(GameSettings.Current);

        // Un repère sonore à chaque cran : c'est la seule façon d'entendre ce qu'on règle.
        AudioSystem.PlaySfx("sfx_ui_button", pitchVariation: 0f);
        Refresh();
    }

    private void AddRow(Func<string> label, UnityEngine.Events.UnityAction action)
    {
        if (_list == null) return;

        var button = UiStyle.TextButton(_list, label(), FrameAccent.Cyan);

        var element = button.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 62f;

        button.onClick.AddListener(action);
        _rows.Add((label, button, button.GetComponentInChildren<Text>()));
        RowCount++;
    }
}
