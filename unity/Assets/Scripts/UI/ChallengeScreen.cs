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
            _header.text = $"{ChallengeSystem.UnlockedCount()} / {ChallengeSystem.All.Count} accomplis";

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

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 94;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Violet);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1200f, 820f);
        panelRect.anchoredPosition = Vector2.zero;

        var title = UiStyle.Label(panel.transform, Loc.T("CHALLENGES_TITLE"), 38,
                                  UiPalette.Violet, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -78f);
        titleRect.offsetMax = new Vector2(-24f, -22f);

        _header = UiStyle.Label(panel.transform, "", 22, UiPalette.Cyan, TextAnchor.UpperCenter);
        var headerRect = _header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(24f, -112f);
        headerRect.offsetMax = new Vector2(-24f, -80f);

        // Treize défis ne tiennent pas dans un panneau : la liste défile, comme au Hub.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 86f);
        scrollRect.offsetMax = new Vector2(-28f, -120f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scrollGo.AddComponent<RectMask2D>();

        var content = UiStyle.NewUiObject("Content", scrollGo.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

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
        // progression, qui est tout l'objet de cet écran.
        var label = UiStyle.Label(parent,
            $"{(done ? "✔" : "○")}  {Loc.T(def.NameKey)}   —   {reward}\n     {Loc.T(def.DescKey)}",
            18, done ? UiPalette.Gold : UiPalette.OffWhite, TextAnchor.UpperLeft);

        var element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 56f;

        RowCount++;
    }
}
