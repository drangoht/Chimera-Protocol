using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Choix du niveau et de son <b>cran de saturation</b> (Lot 6).
///
/// <para>Deux décisions du projet gouvernent cet écran :</para>
/// <list type="number">
///   <item><b>Le cran se règle PAR NIVEAU</b>, sur la carte du biome — décision de la 1.25.0. Un
///         panneau global aurait laissé régler un niveau hors écran, sans rapport avec celui qu'on
///         s'apprête à lancer.</item>
///   <item><b>Un cran verrouillé reste visible.</b> Le sélecteur disparaissant, l'échelle devient
///         invisible — et invisible se lit inexistant. Le joueur doit voir qu'il y a une marche
///         au-dessus, et pourquoi elle est fermée.</item>
/// </list>
/// </summary>
public sealed class LevelSelectScreen : MonoBehaviour
{
    /// <summary>Émis quand le joueur lance une run : biome et cran choisis.</summary>
    public event Action<string, int>? RunRequested;

    /// <summary>Émis à la fermeture sans lancer.</summary>
    public event Action? Closed;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Cartes de biome construites — observable pour les vérifications.</summary>
    public int CardCount { get; private set; }

    private sealed class Card
    {
        public string BiomeId = "";
        public Text Info = null!;
        public Button Launch = null!;
        public Button RankDown = null!;
        public Button RankUp = null!;
        public int Rank;
    }

    private readonly List<Card> _cards = new();
    private GameObject? _root;
    private Transform? _list;
    private Button? _firstButton;

    private void Awake()
    {
        BuildUi();
        Hide();
    }

    public void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
        RefreshAll();

        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
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

    private void RefreshAll()
    {
        var settings = GameSettings.Current;

        foreach (var card in _cards)
        {
            bool unlocked = BiomeUnlock.IsUnlocked(card.BiomeId, settings.Completions);
            int maxRank = BiomeUnlock.MaxSelectableRank(card.BiomeId, settings.SaturationBeatenByLevel);

            card.Rank = Mathf.Clamp(GameSettings.SaturationFor(card.BiomeId), 0, maxRank);

            card.Launch.interactable = unlocked;
            card.RankDown.interactable = unlocked && card.Rank > 0;
            card.RankUp.interactable = unlocked && card.Rank < maxRank;

            card.Info.text = Describe(card, unlocked, maxRank, settings);
        }
    }

    private static string Describe(Card card, bool unlocked, int maxRank, SettingsData settings)
    {
        string name = card.BiomeId.ToUpperInvariant();

        if (!unlocked)
        {
            string blocker = BiomeUnlock.BlockedBy(card.BiomeId, settings.Completions) ?? "";
            return $"{name}   VERROUILLÉ\nTerminer {blocker.ToUpperInvariant()} pour l'ouvrir";
        }

        int best = GameSettings.HighScoreFor(card.BiomeId);
        string record = best > 0 ? $"record {best / 60:00}:{best % 60:00}" : "aucun record";

        // La RÈGLE du cran, pas son numéro : un cran est une règle nommée, lisible avant de lancer,
        // et non un multiplicateur. C'est ce qui permet de savoir ce qu'on accepte de perdre.
        //
        // ⚠ Ranks est indexée à partir de 0 pour des crans numérotés à partir de 1 : lire
        // Ranks[card.Rank] afficherait la règle du cran SUIVANT — un décalage qui ne se voit qu'en
        // lisant l'écran, jamais à la compilation.
        string rankText;
        if (card.Rank <= 0)
        {
            rankText = Loc.T("SAT_NONE");
        }
        else
        {
            var rank = SaturationTable.Ranks[Mathf.Min(card.Rank, SaturationTable.Ranks.Count) - 1];
            rankText = $"Cran {card.Rank} — {Loc.T(rank.NameKey)} : {Loc.T(rank.RuleKey)}";
        }

        string ceiling = card.Rank >= maxRank && maxRank < SaturationTable.MaxRank
            ? "   (battre ce cran pour ouvrir le suivant)"
            : "";

        return $"{name}   palier {LevelThreat.TierOf(card.BiomeId)}   {record}\n{rankText}{ceiling}";
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("LevelSelectCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 92;

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

        UiStyle.Header(panel.transform, Loc.T("LEVELSEL_TITLE"), FrameAccent.Cyan);

        // Cinq biomes ne tiennent pas sans défilement dès que les cartes portent leur sélecteur.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 88f);
        scrollRect.offsetMax = new Vector2(-28f, -90f);

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
        layout.spacing = 12f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        _list = content.transform;

        foreach (string biome in LevelThreat.Order) BuildCard(biome);

        var close = UiStyle.TextButton(panel.transform, "Retour", FrameAccent.Steel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        close.onClick.AddListener(Close);
    }

    private void BuildCard(string biomeId)
    {
        if (_list == null) return;

        var row = UiStyle.NewUiObject("Card_" + biomeId, _list);
        var element = row.AddComponent<LayoutElement>();
        element.minHeight = 104f;

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;

        var card = new Card { BiomeId = biomeId };

        card.Launch = UiStyle.TextButton(row.transform, biomeId.ToUpperInvariant(), FrameAccent.Cyan);
        var launchElement = card.Launch.gameObject.AddComponent<LayoutElement>();
        launchElement.flexibleWidth = 1f;

        // ⚠ Largeur préférée forcée à ZÉRO. Sans elle, la carte réclame la largeur de son texte —
        // la règle du cran fait deux lignes et plus de 1 500 px — et pousse les deux boutons de
        // sélection hors du cadre : le joueur ne peut plus changer de cran, donc plus régler la
        // difficulté du niveau qu'il s'apprête à lancer.
        launchElement.preferredWidth = 0f;
        launchElement.minWidth = 320f;
        card.Info = card.Launch.GetComponentInChildren<Text>();
        card.Launch.onClick.AddListener(() => Launch(card));

        card.RankDown = BuildRankButton(row.transform, "◀", card, -1);
        card.RankUp   = BuildRankButton(row.transform, "▶", card, +1);

        _cards.Add(card);
        CardCount++;

        _firstButton ??= card.Launch;
    }

    private Button BuildRankButton(Transform parent, string label, Card card, int delta)
    {
        var button = UiStyle.TextButton(parent, label, FrameAccent.Gold);

        var element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 88f;

        button.onClick.AddListener(() =>
        {
            int maxRank = BiomeUnlock.MaxSelectableRank(
                card.BiomeId, GameSettings.Current.SaturationBeatenByLevel);

            card.Rank = Mathf.Clamp(card.Rank + delta, 0, maxRank);
            GameSettings.Current.SaturationByLevel[card.BiomeId] = card.Rank;
            GameSettings.Save();

            RefreshAll();
        });

        return button;
    }

    private void Launch(Card card)
    {
        if (!BiomeUnlock.IsUnlocked(card.BiomeId, GameSettings.Current.Completions)) return;

        RunConfig.Choose(card.BiomeId, card.Rank);
        Hide();

        RunRequested?.Invoke(card.BiomeId, card.Rank);
        SceneRoot.ChangeScene(GameScenes.Game);
    }
}
