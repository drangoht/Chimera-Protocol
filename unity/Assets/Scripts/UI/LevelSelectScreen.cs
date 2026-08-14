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
        public Text Title = null!;
        public Text Effect = null!;
        public Text Description = null!;
        public Text Saturation = null!;
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
        if (IsVisible && RawInput.EscapePressedThisFrame()) Close();
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

            Describe(card, unlocked, maxRank, settings);
        }
    }

    /// <summary>
    /// Remplit les quatre textes d'une carte : titre, effet du biome, description, règle du cran.
    ///
    /// <para>Le portage réunissait tout dans un unique libellé sur le bouton de lancement. Trois
    /// conséquences : le <b>nom</b> du biome se réduisait à son identifiant en capitales
    /// (« SANCTUAIRE »), son <b>effet</b> et sa <b>description</b> — la raison même de choisir ce
    /// niveau plutôt qu'un autre — n'apparaissaient nulle part, et la règle du cran <b>débordait</b>
    /// hors du cadre.</para>
    /// </summary>
    private static void Describe(Card card, bool unlocked, int maxRank, SettingsData settings)
    {
        string slug = card.BiomeId.ToUpperInvariant();
        string name = Loc.T($"BIOME_{slug}_NAME");

        // Verrouillé : la carte garde son nom, son effet et sa description — c'est ce qui donne
        // envie de l'ouvrir. Seul le badge change, et le bouton se grise. Le jeu publié fait de
        // même : masquer le contenu d'un niveau fermé n'en dirait pas la valeur.
        if (!unlocked)
        {
            card.Title.text = $"{name}   {Loc.T("LEVELSEL_LOCKED")}";
            card.Effect.text = Loc.T($"BIOME_{slug}_EFFECT");
            card.Description.text = Loc.T($"BIOME_{slug}_DESC");
            card.Saturation.text = "";
            return;
        }

        int best = GameSettings.HighScoreFor(card.BiomeId);
        bool beaten = settings.Completions.ContainsKey(card.BiomeId);

        // Le badge VAINCU et le record disent l'histoire du joueur avec ce niveau : c'est ce qui
        // transforme une liste de cinq entrées en une carte de progression.
        string record = best > 0 ? $"{best / 60:00}:{best % 60:00}" : "";
        string badge = beaten ? $"   {Loc.T("LEVELSEL_DEFEATED")}" : "";
        string time = record.Length > 0 ? $"   ⏱ {record}" : "";

        card.Title.text = $"{name}{badge}{time}   {Loc.T("LEVELSEL_THREAT")} {LevelThreat.TierOf(card.BiomeId)}";
        card.Effect.text = Loc.T($"BIOME_{slug}_EFFECT");
        card.Description.text = Loc.T($"BIOME_{slug}_DESC");

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
            rankText = $"{Loc.T("SAT_SHORT")} {card.Rank} — {Loc.T(rank.NameKey)} : {Loc.T(rank.RuleKey)}";
        }

        if (card.Rank >= maxRank && maxRank < SaturationTable.MaxRank)
            rankText += "   " + Loc.T("SAT_LOCKED_HINT");

        card.Saturation.text = rankText;
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var screen = UiStyle.ScreenCanvas(transform, "LevelSelectCanvas", sortingOrder: 92);
        _root = screen.Root;
        var panel = screen.Panel;

        UiStyle.Header(panel, Loc.T("LEVELSEL_TITLE"), FrameAccent.Cyan);

        // Cinq biomes ne tiennent pas sans défilement dès que les cartes portent leur sélecteur.
        var list = UiStyle.VerticalList(panel,
                                        offsetMin: new Vector2(28f, 88f),
                                        offsetMax: new Vector2(-28f, -90f),
                                        spacing: 12f);
        _list = list.Content;

        foreach (string biome in LevelThreat.Order) BuildCard(biome);

        // « Aléatoire » à côté de « Retour », comme le jeu publié : il évite au joueur qui n'a pas
        // d'avis de relire cinq cartes pour rejouer.
        var random = UiStyle.TextButton(panel, Loc.T("LEVELSEL_RANDOM"), FrameAccent.Cyan);
        var randomRect = random.GetComponent<RectTransform>();
        randomRect.anchorMin = randomRect.anchorMax = new Vector2(0.5f, 0f);
        randomRect.pivot = new Vector2(1f, 0f);
        randomRect.sizeDelta = new Vector2(300f, 58f);
        randomRect.anchoredPosition = new Vector2(-14f, 16f);
        random.onClick.AddListener(LaunchRandom);

        var close = UiStyle.TextButton(panel, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0f, 0f);
        closeRect.sizeDelta = new Vector2(300f, 58f);
        closeRect.anchoredPosition = new Vector2(14f, 16f);
        close.onClick.AddListener(Close);
    }

    /// <summary>
    /// Une <b>carte de biome</b> : vignette, nom, effet, description, règle du cran et bouton de
    /// lancement — la disposition du jeu publié (<c>docs/ui_v1160_levelselect.png</c>).
    ///
    /// <para>Le liseré prend la <b>couleur du biome</b>. Ce n'est pas un ornement : c'est la même
    /// teinte que le sol de l'arène qu'on s'apprête à lancer, et elle fait le lien entre l'écran de
    /// choix et le lieu choisi.</para>
    /// </summary>
    private void BuildCard(string biomeId)
    {
        if (_list == null) return;

        var card = new Card { BiomeId = biomeId };
        var accent = AccentOf(biomeId);

        // ⚠ Sur une petite dalle, la maquette est rétrécie pour que l'interface reste touchable au
        // doigt : le canevas y fait jusqu'à deux fois moins d'unités de large, donc chaque texte
        // prend jusqu'à deux fois plus de lignes. Les hauteurs ci-dessous suivent ce facteur, faute
        // de quoi les deux derniers textes se chevauchent — ce qui est arrivé du premier coup.
        float wrap = UiCanvas.Narrowing();

        var frame = UiStyle.Card(_list, biomeId, accent);
        var element = frame.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = CardHeight + TextBlockHeight * (wrap - 1f);

        BuildThumbnail(frame.transform, biomeId, accent);

        // Colonne de texte, entre la vignette et la colonne de boutons.
        var text = UiStyle.NewUiObject("Text", frame.transform).GetComponent<RectTransform>();
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(ThumbnailSize + 40f, 16f);
        text.offsetMax = new Vector2(-(LaunchWidth + RankWidth + 48f), -16f);

        // ⚠ Les quatre lignes sont posées à des hauteurs FIXES, donc chacune doit avoir la place de
        // ses deux lignes possibles : la description du Secteur Néon et la règle d'un cran passent
        // toutes deux à la ligne, et les serrer faisait se chevaucher les deux derniers textes.
        // ⚠ Les QUATRE lignes suivent `wrap`, y compris le titre. Le premier essai l'en exemptait —
        // « un nom tient toujours sur une ligne » — en oubliant que le titre porte aussi le palier de
        // menace : « Sanctuaire Rouillé   Menace 0 » passe à la ligne dès que le canevas se rétrécit,
        // et le « 0 » tombait alors sur « Terrain neutre ». Une exception à une règle d'espacement se
        // paie toujours sur le cas qu'on n'avait pas en tête.
        card.Title = Line(text, 26, UiStyle.ColorOf(accent), 0f, 34f * wrap);
        card.Effect = Line(text, 20, UiPalette.Gold, 36f * wrap, 26f * wrap);
        card.Description = Line(text, 19, UiPalette.OffWhite, 64f * wrap, 52f * wrap);
        card.Saturation = Line(text, 17, UiPalette.Dim, 118f * wrap, 52f * wrap);

        // Colonne de droite : lancer en haut, réglage du cran dessous. Les deux appartiennent à la
        // même décision — où jouer, et à quelle dureté — donc au même endroit de la carte.
        card.Launch = UiStyle.TextButton(frame.transform, Loc.T("LEVELSEL_PLAY_HERE"), accent);
        var launchRect = card.Launch.GetComponent<RectTransform>();
        launchRect.anchorMin = new Vector2(1f, 1f);
        launchRect.anchorMax = new Vector2(1f, 1f);
        launchRect.pivot = new Vector2(1f, 1f);
        launchRect.sizeDelta = new Vector2(LaunchWidth, 56f);
        launchRect.anchoredPosition = new Vector2(-20f, -18f);
        card.Launch.onClick.AddListener(() => Launch(card));

        card.RankDown = BuildRankButton(frame.transform, "◀", card, -1, -(20f + RankWidth + 8f));
        card.RankUp   = BuildRankButton(frame.transform, "▶", card, +1, -20f);

        _cards.Add(card);
        CardCount++;

        _firstButton ??= card.Launch;
    }

    /// <summary>Une ligne de texte de la colonne centrale, posée à une hauteur donnée depuis le haut.</summary>
    private static Text Line(RectTransform parent, int size, Color color, float top, float height)
    {
        var text = UiStyle.Label(parent, "", size, color, TextAnchor.UpperLeft);

        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -(top + height));
        rect.offsetMax = new Vector2(0f, -top);

        return text;
    }

    /// <summary>
    /// Vignette du biome : la <b>tuile de sol</b> répétée et teintée, exactement comme l'arène. Elle
    /// ne décore pas, elle montre le lieu — c'est le seul aperçu que le joueur ait avant de lancer.
    /// </summary>
    private static void BuildThumbnail(Transform parent, string biomeId, FrameAccent accent)
    {
        var go = UiStyle.NewUiObject("Thumbnail", parent);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;

        var tile = Resources.Load<Sprite>("Environment/tile_floor_stone");
        if (tile != null)
        {
            image.sprite = tile;

            // ⚠ Volontairement AGRANDIE, pas répétée. Le mode `Tiled` d'une Image uGUI dimensionne
            // sa tuile d'après `referencePixelsPerUnit / spritePixelsPerUnit` : à 100 pour 1 — les
            // valeurs du projet —, une tuile de 32 px se dessine sur 3 200 et la vignette n'affiche
            // qu'un aplat uni. Agrandie au filtre point, la même tuile montre son motif, ce qui est
            // précisément ce que la vignette doit dire.
            image.type = Image.Type.Simple;
            image.color = FloorTintOf(biomeId);
        }
        else
        {
            image.color = UiStyle.ColorOf(accent);
        }

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(ThumbnailSize, ThumbnailSize);
        rect.anchoredPosition = new Vector2(20f, 0f);

        // Liseré d'accent autour de la vignette, comme le jeu publié : sans lui, la tuile paraît
        // collée sur la carte plutôt que sertie dedans.
        var border = UiStyle.NewUiObject("Border", go.transform);
        var borderImage = border.AddComponent<Image>();
        borderImage.sprite = UiStyle.ButtonFrame(accent);
        borderImage.type = Image.Type.Sliced;
        borderImage.fillCenter = false;
        borderImage.raycastTarget = false;
        UiStyle.Stretch(border, -2f);
    }

    /// <summary>Teinte du sol du biome — la même table que l'arène, pour que l'aperçu ne mente pas.</summary>
    private static Color FloorTintOf(string biomeId) => biomeId switch
    {
        "fournaise" => new Color(1.00f, 0.72f, 0.58f),
        "givre"     => new Color(0.72f, 0.88f, 1.00f),
        "neon"      => new Color(0.86f, 0.70f, 1.00f),
        "aether"    => new Color(0.72f, 1.00f, 0.94f),
        _           => new Color(0.88f, 0.90f, 1.00f),
    };

    /// <summary>Accent d'un biome, repris de sa couleur de bordure d'arène.</summary>
    private static FrameAccent AccentOf(string biomeId) => biomeId switch
    {
        "fournaise" => FrameAccent.Danger,
        "neon"      => FrameAccent.Violet,
        "aether"    => FrameAccent.Violet,
        _           => FrameAccent.Cyan,
    };

    private const float CardHeight = 196f;

    /// <summary>
    /// Part de <see cref="CardHeight"/> occupée par le bloc de texte — celle qui grandit quand le
    /// canevas se rétrécit. Les 26 px restants sont les marges haute et basse de la carte.
    /// </summary>
    private const float TextBlockHeight = 170f;
    private const float ThumbnailSize = 136f;
    private const float LaunchWidth = 220f;
    private const float RankWidth = 72f;

    private Button BuildRankButton(Transform parent, string label, Card card, int delta, float right)
    {
        var button = UiStyle.TextButton(parent, label, FrameAccent.Gold);

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(RankWidth, 52f);
        rect.anchoredPosition = new Vector2(right, 18f);

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

    /// <summary>
    /// Lance un biome <b>débloqué</b> tiré au sort, avec le cran déjà réglé pour lui. Le tirage passe
    /// par <see cref="Gd.Randf"/> comme tout l'aléatoire du jeu : une graine fixée doit rejouer la
    /// même partie, y compris le choix du niveau.
    /// </summary>
    private void LaunchRandom()
    {
        var open = new List<Card>();
        foreach (var card in _cards)
            if (BiomeUnlock.IsUnlocked(card.BiomeId, GameSettings.Current.Completions)) open.Add(card);

        if (open.Count == 0) return;

        Launch(open[Mathf.Min((int)(Gd.Randf() * open.Count), open.Count - 1)]);
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
