using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Codex — bestiaire, arsenal et chimère (Lot 6).
///
/// <para><b>C'est un écran de découverte, pas un catalogue.</b> Une entrée jamais rencontrée reste
/// masquée : afficher d'emblée les 31 ennemis, les 21 armes et les 5 greffes retirerait au jeu la
/// seule chose que le Codex apporte — la trace de ce qu'on a effectivement croisé. Le Codex Chimère
/// masque donc les greffes non assimilées, et l'arsenal les armes jamais portées.</para>
///
/// <para>Les armes de signature font exception : elles sont considérées connues d'emblée, sans quoi
/// un joueur qui vient de finir sa première run trouverait un Codex vide alors qu'il a passé treize
/// minutes avec son arme de départ.</para>
/// </summary>
public sealed class CodexScreen : MonoBehaviour
{
    /// <summary>Onglets du Codex.</summary>
    public enum Tab { Bestiary, Arsenal, Chimera }

    /// <summary>Émis à la fermeture.</summary>
    public event Action? Closed;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Onglet affiché.</summary>
    public Tab Current { get; private set; } = Tab.Bestiary;

    /// <summary>Entrées de l'onglet courant — observable pour les vérifications.</summary>
    public int EntryCount { get; private set; }

    /// <summary>Entrées découvertes dans l'onglet courant.</summary>
    public int DiscoveredCount { get; private set; }

    private GameObject? _root;
    private Transform? _list;
    private Text? _header;
    private Button? _close;

    private Dictionary<string, EnemyTable.EnemyDef>? _bestiary;
    private Dictionary<string, WeaponTable.WeaponDef>? _weapons;

    private void Awake()
    {
        BuildUi();
        Hide();
    }

    public void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
        Rebuild();

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
        Hide();
        Closed?.Invoke();
    }

    /// <summary>Change d'onglet et reconstruit la liste.</summary>
    public void SelectTab(Tab tab)
    {
        Current = tab;
        Rebuild();
    }

    // ─── Contenu ──────────────────────────────────────────────────────────────

    private void Rebuild()
    {
        if (_list == null) return;

        foreach (Transform child in _list) Destroy(child.gameObject);
        EntryCount = 0;
        DiscoveredCount = 0;

        switch (Current)
        {
            case Tab.Bestiary: BuildBestiary(); break;
            case Tab.Arsenal:  BuildArsenal();  break;
            case Tab.Chimera:  BuildChimera();  break;
        }

        if (_header != null)
            _header.text = $"{DiscoveredCount} / {EntryCount}";
    }

    /// <summary>
    /// Bestiaire. Faute d'un registre des ennemis rencontrés, une entrée est considérée connue si son
    /// biome a déjà été joué — approximation assumée, et <b>dite ici</b> plutôt que masquée : elle
    /// vaut mieux qu'un bestiaire soit entièrement ouvert, soit entièrement vide.
    /// </summary>
    private void BuildBestiary()
    {
        _bestiary ??= EnemyTable.Parse(DataFiles.Load("enemies.json") ?? "");

        var completions = GameSettings.Current.Completions;
        var scores = GameSettings.Current.HighScores;

        foreach (var def in _bestiary.Values)
        {
            bool seen = def.Biome.Length == 0
                ? scores.Count > 0 || completions.Count > 0   // faune universelle : vue dès la 1re run
                : scores.ContainsKey(def.Biome) || completions.ContainsKey(def.Biome);

            AddEntry(seen, def.Name,
                     $"{def.Role}   {def.MaxHp:F0} PV   {def.DamagePerSecond:F0} dgt/s" +
                     (def.Biome.Length > 0 ? $"   {def.Biome}" : ""));
        }
    }

    private void BuildArsenal()
    {
        if (_weapons == null)
        {
            string? json = DataFiles.Load("weapons.json");
            _weapons = json != null ? WeaponTable.Parse(json).Weapons : new();
        }

        foreach (var def in _weapons.Values)
        {
            bool known = GameSettings.IsWeaponDiscovered(def.Id);
            var stats = WeaponTable.StatsAt(def, 1);

            AddEntry(known, def.Name,
                     $"{def.Rarity}   {stats.Damage:F0} dgt   cadence {stats.Cooldown:F2} s" +
                     $"   niveau max {def.MaxLevel}");
        }
    }

    private void BuildChimera()
    {
        foreach (var def in Assimilation.Config.Grafts)
            AddEntry(GameSettings.IsGraftDiscovered(def.Id), def.Name, def.Description);

        foreach (var fusion in Assimilation.Config.Fusions)
            AddEntry(GameSettings.IsGraftDiscovered(fusion.Id), fusion.Name, fusion.Description);
    }

    /// <summary>
    /// Une entrée. Non découverte, elle occupe <b>quand même sa place</b>, anonymisée : le joueur doit
    /// voir qu'il lui reste des choses à trouver, et combien.
    /// </summary>
    private void AddEntry(bool discovered, string name, string detail)
    {
        if (_list == null) return;

        EntryCount++;
        if (discovered) DiscoveredCount++;

        var label = UiStyle.Label(_list,
            discovered ? $"{name}\n     {detail}" : "? ? ?\n     " + Loc.T("ARSENAL_LOCKED"),
            18, discovered ? UiPalette.OffWhite : UiPalette.Steel, TextAnchor.UpperLeft);

        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 52f;
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("CodexCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;
        UiStyle.ScreenBackdrop(canvasGo.transform);

        var panel = UiStyle.NewUiObject("Panel", canvasGo.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(60f, 40f);
        panelRect.offsetMax = new Vector2(-60f, -20f);

        UiStyle.Header(panel.transform, Loc.T("MENU_CODEX"), FrameAccent.Violet);

        _header = UiStyle.Label(panel.transform, "", 22, UiPalette.Cyan, TextAnchor.UpperRight);
        var headerRect = _header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(24f, -142f);
        headerRect.offsetMax = new Vector2(-32f, -108f);

        BuildTabs(panel.transform);

        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 88f);
        scrollRect.offsetMax = new Vector2(-28f, -150f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scrollGo.AddComponent<RectMask2D>();

        var content = UiStyle.NewUiObject("Content", scrollGo.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        // ⚠ Largeur remise à ZÉRO. Un RectTransform naît en 100 × 100 : étiré entre deux ancres
        // horizontales, il vaut alors « largeur du parent + 100 » et déborde de 50 px de CHAQUE
        // côté de sa fenêtre de défilement. Le masque rogne le reste, et ce sont les premières
        // lettres de chaque ligne qui disparaissent — un défaut qu'on lit comme une faute de texte
        // et non comme un défaut de mise en page.
        contentRect.sizeDelta = Vector2.zero;

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        _list = content.transform;

        _close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = _close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        _close.onClick.AddListener(Close);
    }

    private void BuildTabs(Transform parent)
    {
        var row = UiStyle.NewUiObject("Tabs", parent);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(28f, -142f);
        rowRect.offsetMax = new Vector2(-320f, -80f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        AddTab(row.transform, Loc.T("MENU_BESTIARY"), Tab.Bestiary);
        AddTab(row.transform, Loc.T("MENU_ARSENAL"),  Tab.Arsenal);
        AddTab(row.transform, Loc.T("MENU_CHIMERA"),  Tab.Chimera);
    }

    private void AddTab(Transform parent, string label, Tab tab)
    {
        var button = UiStyle.TextButton(parent, label, FrameAccent.Cyan);
        button.onClick.AddListener(() => SelectTab(tab));
    }
}
