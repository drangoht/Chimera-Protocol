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
    public enum Tab { Bestiary, Arsenal, Chimera, Perks }

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
    private Text? _intro;
    private RectTransform? _scrollRect;
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
            case Tab.Perks:    BuildPerks();    break;
        }

        if (_header != null)
            _header.text = $"{DiscoveredCount} / {EntryCount}";

        // L'intro n'apparaît que là où elle dit quelque chose. Les perks en ont besoin : rien
        // n'indique autrement qu'ils se débloquent par les Défis et s'équipent au Hub — deux écrans
        // différents, ni l'un ni l'autre visible d'ici.
        if (_intro != null)
        {
            _intro.text = Current == Tab.Perks ? Loc.T("PERKS_INTRO") : "";

            bool hasIntro = _intro.text.Length > 0;
            _intro.gameObject.SetActive(hasIntro);

            // La liste recule d'autant : réserver la place en permanence laisserait un trou de
            // 50 px en haut des trois onglets qui n'ont rien à introduire.
            if (_scrollRect != null)
                _scrollRect.offsetMax = new Vector2(-28f, -(hasIntro ? _listTop + IntroHeight : _listTop));
        }
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
                     (def.Biome.Length > 0 ? $"   {def.Biome}" : ""),
                     icon: PortraitOf(def));
        }
    }

    /// <summary>
    /// Portrait d'un ennemi : la <b>première image de son animation d'attente</b>.
    ///
    /// <para>Les ennemis n'ont pas d'icône dessinée — et n'en ont pas besoin : leur sprite EST leur
    /// portrait, et c'est celui que le joueur a vu à l'écran. Le bestiaire n'en montrait aucun,
    /// alors que les jeux d'animations étaient déjà chargés pour le jeu : une colonne de trente
    /// lignes de texte où l'on ne reconnaît rien de ce qu'on a combattu.</para>
    /// </summary>
    private static Sprite? PortraitOf(EnemyTable.EnemyDef def)
    {
        var frames = SpriteFramesLibrary.ForEnemy(def.Id, def.FramesPath);
        if (frames == null) return null;

        // « idle » d'abord — un ennemi se reconnaît au repos, pas au milieu d'une attaque. À défaut,
        // la première animation venue vaut mieux que rien.
        var animation = frames.Find("idle") ?? frames.Find("move");
        if (animation == null && frames.Animations.Length > 0) animation = frames.Animations[0];

        return animation != null && animation.Frames.Length > 0 ? animation.Frames[0] : null;
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
                     $"   niveau max {def.MaxLevel}",
                     def.Id);
        }
    }

    private void BuildChimera()
    {
        foreach (var def in Assimilation.Config.Grafts)
            AddEntry(GameSettings.IsGraftDiscovered(def.Id), def.Name, def.Description, def.Id);

        foreach (var fusion in Assimilation.Config.Fusions)
            AddEntry(GameSettings.IsGraftDiscovered(fusion.Id), fusion.Name, fusion.Description, fusion.Id);
    }

    /// <summary>
    /// Perks de départ — bonus de début de run, débloqués par les Défis et équipés au Hub.
    ///
    /// <para>⚠ <b>Aucune entrée n'est masquée ici</b>, contrairement aux trois autres onglets. Un
    /// bestiaire cache ce qu'on n'a pas croisé parce que la <i>découverte</i> est son objet ; un perk
    /// verrouillé, lui, est un <b>but</b> : anonymiser sa description reviendrait à cacher au joueur
    /// ce qu'il gagnerait à aller chercher. C'est la règle du jeu publié (<c>PerksScreen</c>), et
    /// c'est pour cela que cet onglet ne se contente pas d'appeler <see cref="AddEntry"/>.</para>
    /// </summary>
    private void BuildPerks()
    {
        foreach (var perk in StartingPerks.All)
        {
            bool unlocked = MetaProgression.HasPerk(perk.Id);

            AddEntry(unlocked,
                     $"{Loc.T(perk.NameKey)}   {Loc.T(unlocked ? "CHAL_STATUS_DONE" : "PERK_STATUS_LOCKED")}",
                     Loc.T(perk.DescKey),
                     perk.Id,
                     hideWhenLocked: false,
                     // Doré une fois acquis, comme les défis : le jeu emploie cette couleur pour
                     // « obtenu » partout, et un perk EST une récompense de défi.
                     tint: unlocked ? UiPalette.Gold : null);
        }
    }

    /// <summary>
    /// Une entrée. Non découverte, elle occupe <b>quand même sa place</b>, anonymisée : le joueur doit
    /// voir qu'il lui reste des choses à trouver, et combien.
    /// </summary>
    /// <param name="hideWhenLocked">
    /// Faux pour les entrées qui décrivent un <b>but</b> plutôt qu'une découverte : le texte reste
    /// alors lisible, seul le ton s'éteint.
    /// </param>
    private void AddEntry(bool discovered, string name, string detail, string? id = null,
                          bool hideWhenLocked = true, Color? tint = null, Sprite? icon = null)
    {
        if (_list == null) return;

        EntryCount++;
        if (discovered) DiscoveredCount++;

        var row = UiStyle.NewUiObject("Entry", _list);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = IconSize + 8f;

        // ⚠ Un TRAIT sous chaque entrée. Trente lignes de deux textes empilées sans séparation se
        // lisent comme un bloc continu : l'œil ne sait plus où finit une créature et où commence la
        // suivante, et les deux lignes d'une même entrée paraissent appartenir à deux entrées
        // différentes. C'est ce que le jeu publié règle par un cadre autour de chaque carte ; ici,
        // sur des listes de trente entrées, un filet coûte moins cher à l'œil qu'un cadre.
        var rule = UiStyle.NewUiObject("Rule", row.transform);
        rule.AddComponent<Image>().color = UiPalette.WithAlpha(UiPalette.Steel, 0.9f);
        rule.GetComponent<Image>().raycastTarget = false;

        var ruleRect = rule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 0f);
        ruleRect.anchorMax = new Vector2(1f, 0f);
        ruleRect.pivot = new Vector2(0.5f, 0f);
        ruleRect.sizeDelta = new Vector2(0f, 1f);
        ruleRect.anchoredPosition = Vector2.zero;

        // L'icône fournie l'emporte : le bestiaire passe le sprite de la créature, que la table
        // d'identifiants ne connaît pas — et n'a pas à connaître.
        icon ??= id != null ? UiIcons.For(id) : null;

        if (icon != null)
        {
            var go = UiStyle.NewUiObject("Icon", row.transform);
            var image = go.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // ⚠ Une entrée non découverte garde son icône, mais ÉTEINTE — presque noire, opaque.
            // La retirer laisserait une colonne en dents de scie où la place des entrées inconnues
            // ne se lit plus ; l'afficher pleine révélerait ce que le Codex a pour rôle de cacher.
            image.color = discovered ? Color.white : new Color(0.16f, 0.16f, 0.24f, 1f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(4f, 0f);
        }

        string text = discovered || !hideWhenLocked
            ? $"{name}\n{detail}"
            : "? ? ?\n" + Loc.T("ARSENAL_LOCKED");

        var label = UiStyle.Label(row.transform, text, 18,
            tint ?? (discovered ? UiPalette.OffWhite : UiPalette.Dim), TextAnchor.MiddleLeft);

        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(icon != null ? IconSize + 16f : 8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);
    }

    /// <summary>Côté d'une icône d'entrée, en pixels de référence.</summary>
    private const float IconSize = 44f;

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("CodexCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 95);

        _root = canvasGo;
        UiStyle.ScreenBackdrop(canvasGo.transform);

        var panel = UiStyle.NewUiObject("Panel", canvasGo.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(60f, 40f);
        panelRect.offsetMax = new Vector2(-60f, -20f);

        // ⚠ La valeur RENVOYÉE par Header donne le bas du liseré. L'ignorer — ce que faisait le
        // portage — pose les onglets à une ordonnée devinée : ils chevauchaient le trait, qui
        // passait derrière eux. Tout ce qui suit se cale désormais sur cette mesure.
        float headerBottom = UiStyle.Header(panel.transform, Loc.T("MENU_CODEX"), FrameAccent.Violet);

        BuildTabs(panel.transform, headerBottom);

        // Le compteur « découverts / total » est aligné sur la rangée d'onglets, à sa droite : les
        // deux disent l'état de l'onglet courant, ils appartiennent à la même ligne de lecture.
        _header = UiStyle.Label(panel.transform, "", 22, UiPalette.Cyan, TextAnchor.MiddleRight);
        var headerRect = _header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(1f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(1f, 1f);
        headerRect.sizeDelta = new Vector2(260f, TabHeight);
        headerRect.anchoredPosition = new Vector2(-28f, -headerBottom);

        // Sous les onglets, jamais dessus : la liste commence là où la rangée finit.
        _listTop = headerBottom + TabHeight + 12f;

        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 88f);
        scrollRect.offsetMax = new Vector2(-28f, -_listTop);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        UiStyle.ConfigureScroll(scroll);
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
        _scrollRect = scrollRect;

        // Bandeau d'introduction, entre les onglets et la liste. Il n'occupe la place que lorsqu'il
        // a quelque chose à dire — d'où l'objet désactivé plutôt qu'une chaîne vide, qui laisserait
        // un trou de 40 px en haut des trois autres onglets.
        _intro = UiStyle.Label(panel.transform, "", 18, UiPalette.Dim, TextAnchor.UpperLeft);
        var introRect = _intro.GetComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0f, 1f);
        introRect.anchorMax = new Vector2(1f, 1f);
        introRect.pivot = new Vector2(0.5f, 1f);
        introRect.offsetMin = new Vector2(28f, -196f);
        introRect.offsetMax = new Vector2(-28f, -150f);
        _intro.gameObject.SetActive(false);

        _close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = _close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        _close.onClick.AddListener(Close);
    }

    /// <summary>Hauteur de la rangée d'onglets, en pixels de référence.</summary>
    private const float TabHeight = 58f;

    /// <summary>Hauteur réservée au bandeau d'introduction, quand l'onglet en a un.</summary>
    private const float IntroHeight = 52f;

    /// <summary>Ordonnée du haut de la liste, mesurée depuis le haut du panneau.</summary>
    private float _listTop = 150f;

    private void BuildTabs(Transform parent, float top)
    {
        var row = UiStyle.NewUiObject("Tabs", parent);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(28f, -(top + TabHeight));
        rowRect.offsetMax = new Vector2(-300f, -top);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        AddTab(row.transform, Loc.T("MENU_BESTIARY"), Tab.Bestiary);
        AddTab(row.transform, Loc.T("MENU_ARSENAL"),  Tab.Arsenal);
        AddTab(row.transform, Loc.T("MENU_CHIMERA"),  Tab.Chimera);
        AddTab(row.transform, Loc.T("MENU_PERKS"),    Tab.Perks);
    }

    private void AddTab(Transform parent, string label, Tab tab)
    {
        var button = UiStyle.TextButton(parent, label, FrameAccent.Cyan);
        button.onClick.AddListener(() => SelectTab(tab));
    }
}
