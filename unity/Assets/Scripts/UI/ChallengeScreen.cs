using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Écran des défis : ce qui reste à accomplir, et ce que ça rapporte (Lot 6).
///
/// <para><b>Un défi qu'on ne peut pas lire n'existe pas.</b> Les récompenses — Échos, perks de départ,
/// titres — ne sont un levier de rétention que si le joueur sait <i>ce qu'il vise</i> avant de lancer
/// la run. C'est la raison d'être de cet écran : il n'ajoute aucune mécanique, il rend visible celle
/// qui tourne déjà en fin de partie.</para>
/// </summary>
public sealed class ChallengeScreen : MonoBehaviour
{
    /// <summary>Émis à la fermeture.</summary>
    public event Action? Closed;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Lignes construites — observable pour les vérifications.</summary>
    public int RowCount { get; private set; }

    private GameObject? _root;
    private Text? _header;
    private Button? _close;

    private void Awake()
    {
        BuildUi();
        Hide();
    }

    public void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
        if (_header != null)
            _header.text = Loc.T("CHALLENGES_PROGRESS",
                                 ChallengeSystem.UnlockedCount(), ChallengeSystem.All.Count);

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

    private void BuildUi()
    {
        var canvasGo = new GameObject("ChallengeCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 94);

        _root = canvasGo;
        UiStyle.ScreenBackdrop(canvasGo.transform);

        var panel = UiStyle.NewUiObject("Panel", canvasGo.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(60f, 40f);
        panelRect.offsetMax = new Vector2(-60f, -20f);

        UiStyle.Header(panel.transform, Loc.T("CHALLENGES_TITLE"), FrameAccent.Gold);

        // Ce que sont les défis, dit AVANT la liste : un joueur qui découvre cet écran voit sinon
        // treize objectifs sans savoir qu'ils se valident en fin de run ni ce qu'ils rapportent.
        var intro = UiStyle.Label(panel.transform, Loc.T("CHALLENGES_INTRO"), 19,
                                  UiPalette.Dim, TextAnchor.UpperLeft);
        var introRect = intro.GetComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0f, 1f);
        introRect.anchorMax = new Vector2(1f, 1f);
        introRect.pivot = new Vector2(0.5f, 1f);
        introRect.offsetMin = new Vector2(28f, -128f);
        introRect.offsetMax = new Vector2(-28f, -92f);

        _header = UiStyle.Label(panel.transform, "", 22, UiPalette.Cyan, TextAnchor.UpperLeft);
        var headerRect = _header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(28f, -164f);
        headerRect.offsetMax = new Vector2(-28f, -132f);

        // Treize défis ne tiennent pas dans un panneau : la liste défile, comme au Hub.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 86f);
        scrollRect.offsetMax = new Vector2(-28f, -176f);

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
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;

        foreach (var def in ChallengeSystem.All) BuildRow(content.transform, def);

        _close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = _close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        _close.onClick.AddListener(Close);
    }

    /// <summary>
    /// Un défi : <b>une carte</b> portant l'icône de sa récompense, son nom, son état et ce qu'il
    /// demande.
    ///
    /// <para>Le portage empilait treize lignes de texte nu, alignées à gauche, sur un tiers de
    /// l'écran. La liste se lisait comme un journal de bord : rien ne séparait un défi du suivant,
    /// et rien ne distinguait « +50 Échos » d'« un titre » — alors que c'est exactement ce que le
    /// joueur vient chercher ici. La carte, l'icône et le liseré viennent du jeu publié
    /// (<c>docs/ui_v1160_challenges.png</c>).</para>
    /// </summary>
    private void BuildRow(Transform parent, ChallengeTable.ChallengeDef def)
    {
        bool done = ChallengeSystem.IsUnlocked(def.Id);

        // Clés de la table du jeu — les inventer afficherait leur nom brut à l'écran.
        string reward = def.RewardType switch
        {
            ChallengeTable.RewardKind.Echoes   => Loc.T("CHAL_REWARD_ECHOES", def.RewardEchoes),
            ChallengeTable.RewardKind.Perk     => Loc.T("CHAL_REWARD_PERK"),
            ChallengeTable.RewardKind.Cosmetic => Loc.T("CHAL_REWARD_COSMETIC"),
            _ => "",
        };

        // Un défi accompli reste affiché, coché : masquer les acquis effacerait le sentiment de
        // progression, qui est tout l'objet de cet écran. Le liseré ACQUIS est doré, l'autre acier —
        // la couleur dit l'état avant que le mot ne soit lu.
        var card = UiStyle.Card(parent, def.Id, done ? FrameAccent.Gold : FrameAccent.Cyan);

        var element = card.AddComponent<LayoutElement>();
        element.minHeight = CardHeight;
        element.preferredHeight = CardHeight;

        // L'icône dit la NATURE de la récompense : un Noyau pour les Échos, un emplacement pour un
        // perk, un titre pour un cosmétique. Trois pictogrammes déjà dessinés pour cet usage.
        var iconSprite = UiIcons.For(RewardIconId(def.RewardType));
        if (iconSprite != null)
        {
            var iconGo = UiStyle.NewUiObject("Icon", card.transform);
            var image = iconGo.AddComponent<Image>();
            image.sprite = iconSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // Éteinte tant que le défi n'est pas accompli : la récompense se voit, sans se donner.
            image.color = done ? Color.white : new Color(0.30f, 0.30f, 0.42f, 1f);

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconRect.anchoredPosition = new Vector2(20f, 0f);
        }

        float textLeft = iconSprite != null ? IconSize + 36f : 24f;

        // Les clés d'état sont celles du jeu — les inventer afficherait leur nom brut à l'écran.
        var title = UiStyle.Label(card.transform,
            $"{Loc.T(def.NameKey)}   {Loc.T(done ? "CHAL_STATUS_DONE" : "CHAL_STATUS_TODO")}",
            24, done ? UiPalette.Gold : UiPalette.OffWhite, TextAnchor.LowerLeft);

        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(textLeft, 0f);
        titleRect.offsetMax = new Vector2(-24f, -14f);

        var body = UiStyle.Label(card.transform,
            $"{Loc.T(def.DescKey)}   ·   {reward}",
            19, done ? UiPalette.OffWhite : UiPalette.Dim, TextAnchor.UpperLeft);

        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.offsetMin = new Vector2(textLeft, 14f);
        bodyRect.offsetMax = new Vector2(-24f, 0f);

        RowCount++;
    }

    /// <summary>Icône associée à une nature de récompense.</summary>
    private static string RewardIconId(ChallengeTable.RewardKind kind) => kind switch
    {
        ChallengeTable.RewardKind.Perk     => "extra_slot",
        ChallengeTable.RewardKind.Cosmetic => "title",
        _                                  => "echo",
    };

    /// <summary>Hauteur d'une carte de défi, en pixels de référence.</summary>
    private const float CardHeight = 96f;

    /// <summary>Côté de l'icône de récompense.</summary>
    private const float IconSize = 56f;
}
