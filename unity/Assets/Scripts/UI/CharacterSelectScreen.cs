using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Choix du personnage — l'écran qui n'avait jamais été porté depuis Godot.
///
/// <para><b>⚠ Tout le reste existait déjà.</b> Les douze clés <c>CHAR_*</c> dorment dans
/// <c>ui.csv</c> traduites en trois langues, les quatre jeux d'animations sont générés et importés,
/// et <c>GameSettings.SignatureWeapons</c> énumère les quatre armes de départ pour les marquer
/// « découvertes » au Codex. Seul le <i>choix</i> manquait — et la description YouTube du jeu
/// annonçait « 4 playable characters » pendant ce temps. Douzième « déclaré non consommé » du
/// projet, et le plus complet : le déclaré était du contenu fini, jusque dans sa traduction
/// espagnole.</para>
///
/// <para><b>Il montre le personnage, pas ses chiffres.</b> Une carte donne d'abord une silhouette et
/// une phrase — c'est ce qui fait choisir. Les deux nombres viennent après, en petit, parce qu'ils
/// ne veulent rien dire tant qu'on n'a pas joué : 140 PV contre 100 ne se lit pas, « encaisse mais
/// ne peut pas rompre » se lit.</para>
///
/// <para><b>Le choix est mémorisé et présélectionné.</b> Cet écran est sur le chemin de chaque run :
/// s'il redemandait à chaque fois, il deviendrait un péage. Il s'ouvre sur le personnage de la run
/// précédente, et une touche suffit à passer.</para>
/// </summary>
public sealed class CharacterSelectScreen : MonoBehaviour
{
    /// <summary>Émis quand le joueur a choisi : identifiant du personnage.</summary>
    public event Action<string>? Confirmed;

    /// <summary>Émis à la fermeture sans choisir.</summary>
    public event Action? Closed;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Cartes construites — observable pour les vérifications de banc.</summary>
    public int CardCount { get; private set; }

    /// <summary>
    /// Hauteur d'une carte.
    /// </summary>
    /// <remarks>
    /// ⚠ 132 px au premier jet, et les quatre lignes de texte en réclamaient 128 dans une carte qui
    /// n'en offrait que 100 une fois ses marges retirées : la ligne de statistiques se dessinait
    /// <b>sous le liseré</b>, hors de sa carte. C'est le piège consigné depuis l'ère Godot — une
    /// marge comptée trop juste fait sortir la dernière ligne — et il s'est présenté sur le même
    /// genre d'écran, avec le même symptôme. Il ne se voit qu'à l'image : la mise en page est
    /// « correcte », chaque ligne est là où on l'a mise.
    /// </remarks>
    private const float CardHeight = 168f;
    private const float PortraitSize = 96f;
    private const float ChooseWidth = 220f;

    private sealed class Card
    {
        public string CharacterId = "";
        public Image Portrait = null!;
        public Text Title = null!;
        public Text Tag = null!;
        public Text Description = null!;
        public Text Stats = null!;
        public Button Choose = null!;
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

        // Le curseur se pose sur le personnage COURANT, pas sur le premier de la liste : c'est ce
        // qui rend l'écran franchissable d'une touche quand on rejoue le même profil.
        var current = SelectedCard();
        var focus = current?.Choose ?? _firstButton;
        if (focus != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(focus.gameObject);
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

    private Card? SelectedCard()
    {
        string current = RunConfig.CharacterId;
        foreach (var card in _cards)
            if (string.Equals(card.CharacterId, current, StringComparison.Ordinal)) return card;

        return null;
    }

    private void RefreshAll()
    {
        string current = RunConfig.CharacterId;

        foreach (var card in _cards)
        {
            var def = Characters.Get(card.CharacterId);
            bool selected = string.Equals(card.CharacterId, current, StringComparison.Ordinal);

            card.Title.text = Loc.T(def.NameKey);
            card.Tag.text = Loc.T(def.TagKey);
            card.Description.text = Loc.T(def.DescKey);
            card.Stats.text = StatLine(def);

            // Le personnage courant se distingue par son TITRE, pas par un bouton grisé ni par une
            // clé de texte de plus : un bouton inactif se lit « indisponible », exactement le
            // contraire de « c'est celui-ci ». Le chevron est le seul signe du jeu qui ne demande
            // aucune traduction.
            if (selected) card.Title.text = "▸ " + card.Title.text;
        }
    }

    /// <summary>
    /// La ligne de chiffres : PV, vitesse, et le nom traduit de l'arme de départ.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <c>CHARSEL_STATS</c> <b>existait déjà</b> dans <c>ui.csv</c>, traduite en trois
    /// langues et paramétrée pour ces trois valeurs exactement — comme les douze clés
    /// <c>CHAR_*</c>, comme <c>CHARSEL_TITLE</c> et <c>CHARSEL_CHOOSE</c>. Le premier jet de cet
    /// écran inventait <c>CHARSEL_HP</c>, <c>CHARSEL_SPEED</c> et <c>CHARSEL_WEAPON</c> : trois clés
    /// neuves à faire traduire, pour un texte déjà écrit. <i>Chercher avant d'ajouter</i> vaut aussi
    /// pour les chaînes.</para>
    /// <para>⚠ L'arme passe par <see cref="ContentText"/> et non par son identifiant : sans quoi la
    /// carte afficherait « vector_lance » — ou son nom français dans les trois langues, ce que le
    /// repli silencieux des JSON produit et que personne ne voit venir.</para>
    /// </remarks>
    private static string StatLine(CharacterDef def)
    {
        string weapon = ContentText.WeaponName(def.WeaponId, def.WeaponId);

        return string.Format(Loc.T("CHARSEL_STATS"),
                             def.MaxHp.ToString("F0"),
                             def.MoveSpeed.ToString("F0"),
                             weapon);
    }

    private void Select(Card card)
    {
        RunConfig.ChooseCharacter(card.CharacterId);
        RefreshAll();

        Hide();
        Confirmed?.Invoke(card.CharacterId);
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var screen = UiStyle.ScreenCanvas(transform, "CharacterSelectCanvas", sortingOrder: 93);
        _root = screen.Root;
        var panel = screen.Panel;

        UiStyle.Header(panel, Loc.T("CHARSEL_TITLE"), FrameAccent.Violet);

        var list = UiStyle.VerticalList(panel,
                                        offsetMin: new Vector2(28f, 88f),
                                        offsetMax: new Vector2(-28f, -90f),
                                        spacing: 12f);
        _list = list.Content;

        foreach (var def in Characters.All) BuildCard(def);

        var close = UiStyle.TextButton(panel, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(300f, 58f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        close.onClick.AddListener(Close);
    }

    private void BuildCard(CharacterDef def)
    {
        if (_list == null) return;

        var card = new Card { CharacterId = def.Id };
        var accent = AccentOf(def.Id);

        var frame = UiStyle.Card(_list, def.Id, accent);
        var element = frame.AddComponent<LayoutElement>();
        element.minHeight = CardHeight;
        element.preferredHeight = CardHeight;

        card.Portrait = BuildPortrait(frame.transform, def, accent);

        var text = UiStyle.NewUiObject("Text", frame.transform).GetComponent<RectTransform>();
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(PortraitSize + 40f, 16f);
        text.offsetMax = new Vector2(-(ChooseWidth + 40f), -16f);

        // ⚠ Hauteurs FIXES, chacune dimensionnée pour DEUX lignes : les descriptions viennent de
        // ui.csv et l'espagnol y est régulièrement plus long que le français. Les serrer ferait se
        // chevaucher les deux derniers textes — le défaut exact déjà rencontré sur l'écran de choix
        // de niveau, où la description du Néon passait sous le liseré.
        card.Title       = Line(text, 26, TitleColorOf(accent), 0f, 32f);
        card.Tag         = Line(text, 19, UiPalette.Gold, 34f, 24f);
        card.Description = Line(text, 18, UiPalette.OffWhite, 60f, 46f);
        card.Stats       = Line(text, 17, UiPalette.Dim, 108f, 24f);

        card.Choose = UiStyle.TextButton(frame.transform, Loc.T("CHARSEL_CHOOSE"), accent);
        var chooseRect = card.Choose.GetComponent<RectTransform>();
        chooseRect.anchorMin = chooseRect.anchorMax = new Vector2(1f, 0.5f);
        chooseRect.pivot = new Vector2(1f, 0.5f);
        chooseRect.sizeDelta = new Vector2(ChooseWidth, 56f);
        chooseRect.anchoredPosition = new Vector2(-20f, 0f);
        card.Choose.onClick.AddListener(() => Select(card));

        _cards.Add(card);
        CardCount++;

        _firstButton ??= card.Choose;
    }

    /// <summary>
    /// Le portrait : la <b>première image de l'animation d'attente</b> du personnage.
    /// </summary>
    /// <remarks>
    /// <para>Aucun asset dédié n'est créé pour cet écran, et c'est délibéré : un portrait dessiné à
    /// part dériverait du sprite réel au premier changement de silhouette, et l'écran promettrait un
    /// personnage que la run ne montrerait pas. Ici la carte affiche <b>exactement</b> ce que le
    /// joueur va incarner.</para>
    /// <para>⚠ Le filtre reste au point (<c>Point</c> à l'import) et le sprite est agrandi ×3 : un
    /// personnage de 32 px affiché à 96 px en bilinéaire devient une bouillie — le pixel art est le
    /// seul style où agrandir sans précaution DÉTRUIT l'image au lieu de la flouter un peu.</para>
    /// </remarks>
    private static Image BuildPortrait(Transform parent, CharacterDef def, FrameAccent accent)
    {
        var go = UiStyle.NewUiObject("Portrait", parent);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        var frames = SpriteFramesLibrary.Get(def.FramesId);
        var idle = frames?.Find("idle");

        if (idle != null && idle.Frames.Length > 0 && idle.Frames[0] != null)
        {
            image.sprite = idle.Frames[0];
        }
        else
        {
            // Pas de silhouette : un aplat d'accent plutôt qu'un trou. Signalé, parce qu'une carte
            // sans portrait est le symptôme d'un jeu d'animations non reconstruit dans l'éditeur —
            // et cela ne se voit nulle part ailleurs.
            image.color = UiStyle.ColorOf(accent);
            Debug.LogWarning($"[CharacterSelectScreen] pas d'animation 'idle' pour '{def.FramesId}'.");
        }

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(PortraitSize, PortraitSize);
        rect.anchoredPosition = new Vector2(20f, 0f);

        return image;
    }

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
    /// Couleur de liseré par personnage — accordée à la palette de son sprite
    /// (<c>tools/generate_character_sprites.py</c>), pour que la carte et la silhouette qu'elle
    /// montre parlent de la même chose.
    /// </summary>
    private static FrameAccent AccentOf(string characterId) => characterId switch
    {
        // ⚠ Le Titan portait `Steel`, et son nom en devenait ILLISIBLE : gris sombre sur fond
        // sombre. C'est juste pour un liseré — un châssis blindé est gris — et faux pour un titre.
        // `Danger` dit la même chose (machine de combat lourde) en restant lisible.
        "titan"    => FrameAccent.Danger,
        "vagabond" => FrameAccent.Gold,
        "vecteur"  => FrameAccent.Violet,
        _          => FrameAccent.Cyan,
    };

    /// <summary>
    /// Couleur du titre. Reprend l'accent, <b>sauf</b> quand celui-ci n'est pas une couleur de
    /// texte : un accent sert à border une carte, et tous les gris qui bordent bien ne se lisent pas
    /// sur un fond sombre. Le garde-fou reste même si plus aucun personnage n'emploie `Steel` — le
    /// prochain qu'on ajoutera y viendra sans y penser.
    /// </summary>
    private static Color TitleColorOf(FrameAccent accent)
        => accent == FrameAccent.Steel ? UiPalette.OffWhite : UiStyle.ColorOf(accent);
}
