using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Écran de montée de niveau — la seule modale qui interrompt le jeu <b>au milieu de l'action</b>
/// (Lot 5).
///
/// <para>Trois contraintes gouvernent ce fichier, chacune reprise d'un problème réel du projet :</para>
/// <list type="number">
///   <item><b>Il passe par <see cref="ModalQueue"/></b> : une montée de niveau et un seuil de
///         greffe peuvent tomber dans la même frame, et deux écrans ouverts ensemble se disputent
///         le focus et la pause.</item>
///   <item><b>Il s'anime en temps réel</b> : la pause est <c>Time.timeScale = 0</c>, donc une
///         animation asservie au temps de jeu se figerait avec le jeu — l'écran qui met en pause
///         resterait figé.</item>
///   <item><b>Il est jouable au clavier et à la manette</b> : le premier bouton reçoit le focus à
///         l'ouverture. Sans cela, une manette ne peut littéralement pas passer l'écran, et la run
///         est bloquée.</item>
/// </list>
/// </summary>
public sealed class LevelUpScreen : MonoBehaviour
{
    /// <summary>Émis quand le joueur choisit une carte.</summary>
    public event Action<LevelUpCard>? CardChosen;

    /// <summary>
    /// Émis quand le joueur renouvelle la main. L'écran ne sait pas tirer des cartes — c'est
    /// <see cref="RunHud"/> qui possède l'inventaire — il demande donc, et attend un
    /// <see cref="Replace"/>.
    /// </summary>
    public event Action? RerollRequested;

    /// <summary>Émis quand le joueur passe son tour sans rien prendre.</summary>
    public event Action? Skipped;

    /// <summary>
    /// Charges de Renouveler / Passer de la run. Posées par <see cref="RunHud"/> au démarrage depuis
    /// les améliorations du Hub ; sans achat, les deux boutons n'existent pas.
    /// </summary>
    public LevelUpCharges Charges { get; set; } = new(0, 0);

    /// <summary>Cartes actuellement affichées.</summary>
    public IReadOnlyList<LevelUpCard> Cards => _cards;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Hauteur réservée à la rangée d'actions, en pixels d'interface.</summary>
    private const float ActionRowHeight = 62f;

    private readonly List<LevelUpCard> _cards = new();
    private GameObject? _root;
    private Transform? _cardRow;
    private Button? _firstButton;
    private Button? _rerollButton;
    private Button? _skipButton;
    private GameObject? _actionRow;

    private void Awake()
    {
        BuildUi();
        Hide();

        ModalQueue.Opened += OnModalOpened;
    }

    private void OnDestroy() => ModalQueue.Opened -= OnModalOpened;

    private void OnModalOpened(ModalKind kind)
    {
        if (kind == ModalKind.LevelUp) Show();
    }

    /// <summary>Prépare le choix et demande l'ouverture. L'affichage effectif dépend de la file.</summary>
    public void Present(IReadOnlyList<LevelUpCard> cards)
    {
        _cards.Clear();
        _cards.AddRange(cards);
        ModalQueue.Request(ModalKind.LevelUp);
    }

    /// <summary>
    /// Remplace la main affichée sans refermer l'écran — la réponse attendue à
    /// <see cref="RerollRequested"/>.
    /// </summary>
    public void Replace(IReadOnlyList<LevelUpCard> cards)
    {
        _cards.Clear();
        _cards.AddRange(cards);

        if (IsVisible) BuildCards();
    }

    private void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
        RefreshActions();
        BuildCards();

        // Focus initial : sans lui, l'écran est infranchissable à la manette et bloque la run.
        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);

        // Apparition en temps réel — asservie au temps de jeu, elle ne jouerait jamais (timeScale 0).
        var canvasGroup = _root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        GTween.Create(this, ignoreTimeScale: true)
              .TweenFloat(v => canvasGroup.alpha = v, 0f, 1f, 0.18f, TransType.Quad, EaseType.Out);
    }

    private void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void Choose(LevelUpCard card)
    {
        AudioSystem.PlaySfx("sfx_card_select");
        Hide();
        ModalQueue.Close(ModalKind.LevelUp);
        CardChosen?.Invoke(card);
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("LevelUpCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 100);

        _root = canvasGo;

        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Cyan);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        // Proportions du jeu publié (docs/ui_v1160_levelup.png) : les cartes y occupent près de la
        // moitié de la hauteur. Le portage les tenait dans 520 px, et la description de
        // l'Auto-réparation — la plus longue du jeu — débordait par le bas une fois l'icône posée.
        panelRect.sizeDelta = new Vector2(1420f, 680f);
        panelRect.anchoredPosition = Vector2.zero;

        var title = UiStyle.Label(panel.transform, "MONTÉE DE NIVEAU", 34, UiPalette.Gold, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -80f);
        titleRect.offsetMax = new Vector2(-24f, -24f);

        var row = UiStyle.NewUiObject("CardRow", panel.transform);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.offsetMin = new Vector2(32f, 32f);
        rowRect.offsetMax = new Vector2(-32f, -100f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        _cardRow = row.transform;

        BuildActionRow(panel.transform);
    }

    /// <summary>
    /// Rangée « Renouveler / Passer », sous les cartes.
    ///
    /// <para><b>Les deux améliorations du Hub qu'elle rend enfin réelles.</b> <c>reroll</c> et
    /// <c>skip</c> étaient achetables, coûtaient des Échos, et ne faisaient <i>rien</i> — le portage
    /// n'avait tout simplement pas ces boutons. Une amélioration payée sans effet est indiscernable
    /// d'un équilibrage raté : le joueur conclut qu'elle est faible, pas qu'elle est absente.</para>
    ///
    /// <para>Non acheté = <b>bouton absent</b>, pas grisé. Un bouton grisé qu'on ne peut jamais
    /// activer se lit comme une fonction cassée ; l'absence se lit comme un déblocage à venir.
    /// À l'inverse, dépenser sa dernière charge le laisse visible et grisé — le faire disparaître
    /// donnerait à croire que l'achat a été perdu.</para>
    /// </summary>
    private void BuildActionRow(Transform panel)
    {
        var row = UiStyle.NewUiObject("ActionRow", panel);
        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(560f, ActionRowHeight);
        rect.anchoredPosition = new Vector2(0f, 18f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        _rerollButton = UiStyle.TextButton(row.transform, "", FrameAccent.Cyan);
        _rerollButton.onClick.AddListener(OnRerollPressed);

        _skipButton = UiStyle.TextButton(row.transform, "", FrameAccent.Gold);
        _skipButton.onClick.AddListener(OnSkipPressed);

        _actionRow = row;
    }

    private void OnRerollPressed()
    {
        if (!Charges.TryReroll()) return;

        AudioSystem.PlaySfx("sfx_ui_button");
        RefreshActions();
        RerollRequested?.Invoke();
    }

    private void OnSkipPressed()
    {
        if (!Charges.TrySkip()) return;

        AudioSystem.PlaySfx("sfx_ui_button");
        Hide();
        ModalQueue.Close(ModalKind.LevelUp);
        Skipped?.Invoke();
    }

    /// <summary>Aligne l'existence, le libellé et l'état des deux boutons sur les charges restantes.</summary>
    private void RefreshActions()
    {
        if (_rerollButton != null)
        {
            _rerollButton.gameObject.SetActive(Charges.RerollUnlocked);
            _rerollButton.interactable = Charges.RerollsLeft > 0;
            SetLabel(_rerollButton, Loc.T("LEVELUP_REROLL", Charges.RerollsLeft));
        }

        if (_skipButton != null)
        {
            _skipButton.gameObject.SetActive(Charges.SkipUnlocked);
            _skipButton.interactable = Charges.SkipsLeft > 0;
            SetLabel(_skipButton, Loc.T("LEVELUP_SKIP", Charges.SkipsLeft));
        }

        // La rangée entière disparaît quand rien n'est acheté : sans quoi elle réserverait sa
        // hauteur sous les cartes, et le panneau paraîtrait mal centré sans raison visible.
        if (_actionRow != null)
            _actionRow.SetActive(Charges.RerollUnlocked || Charges.SkipUnlocked);
    }

    private static void SetLabel(Button button, string text)
    {
        var label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = text;
    }

    private void BuildCards()
    {
        if (_cardRow == null) return;

        foreach (Transform child in _cardRow) Destroy(child.gameObject);
        _firstButton = null;

        foreach (var card in _cards)
        {
            var captured = card;

            // Le cadre porte la RARETÉ, comme sous Godot : c'est ce qui dit la valeur d'une carte
            // avant même qu'on l'ait lue, dans un écran qui met le jeu en pause au pire moment.
            var button = UiStyle.CardButton(_cardRow, card.Id, RarityOf(card));
            button.onClick.AddListener(() => Choose(captured));

            // L'icône EN PREMIER, et grande : c'est elle qu'on reconnaît sans lire. Le jeu est en
            // pause au milieu d'une nuée et le joueur arbitre entre trois cartes en quelques
            // secondes — trois pavés de texte identiques lui coûtent ce temps-là.
            float textTop = AddIcon(button.transform, card.Id) ? 150f : 26f;

            var text = UiStyle.Label(button.transform, Describe(card), 20,
                                     UiPalette.OffWhite, TextAnchor.UpperCenter);

            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(26f, 26f);
            textRect.offsetMax = new Vector2(-26f, -textTop);

            _firstButton ??= button;
        }

        // La rangée d'actions réserve sa place SOUS les cartes, et seulement si elle existe : sans
        // ce recalcul, les boutons se superposeraient au bas des cartes les plus longues.
        bool hasActions = _actionRow != null && _actionRow.activeSelf;
        var rowRect = _cardRow.GetComponent<RectTransform>();
        rowRect.offsetMin = new Vector2(32f, hasActions ? ActionRowHeight + 30f : 32f);

        // Chaîne de focus explicite : la navigation automatique d'Unity se perd dès qu'un élément
        // est reconstruit à chaque ouverture.
        var buttons = _cardRow.GetComponentsInChildren<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft  = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnRight = buttons[(i + 1) % buttons.Length];

            // Descendre depuis une carte doit atteindre les actions, sinon elles sont inaccessibles
            // à la manette — et un bouton qu'on ne peut pas atteindre n'existe pas.
            nav.selectOnDown = FirstUsableAction();
            buttons[i].navigation = nav;
        }

        WireActionNavigation(buttons.Length > 0 ? buttons[0] : null);
    }

    /// <summary>Premier bouton d'action réellement activable, ou <c>null</c> s'il n'y en a aucun.</summary>
    private Button? FirstUsableAction()
    {
        if (_rerollButton != null && _rerollButton.gameObject.activeSelf && _rerollButton.interactable)
            return _rerollButton;

        if (_skipButton != null && _skipButton.gameObject.activeSelf && _skipButton.interactable)
            return _skipButton;

        return null;
    }

    private void WireActionNavigation(Button? firstCard)
    {
        void Wire(Button? button, Button? other)
        {
            if (button == null) return;

            var nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = firstCard;
            nav.selectOnLeft = other;
            nav.selectOnRight = other;
            button.navigation = nav;
        }

        Wire(_rerollButton, _skipButton);
        Wire(_skipButton, _rerollButton);
    }

    /// <summary>
    /// Pose l'icône de la carte, en haut. Renvoie <c>false</c> si l'identifiant n'en a pas — le
    /// texte reprend alors toute la carte plutôt que de laisser un vide qui se lirait comme une
    /// image cassée.
    /// </summary>
    private static bool AddIcon(Transform card, string id)
    {
        var sprite = UiIcons.For(id);
        if (sprite == null) return false;

        var go = UiStyle.NewUiObject("Icon", card);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;   // les icônes ne sont pas toutes carrées
        image.raycastTarget = false;   // sinon elle intercepte le clic destiné à la carte

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(104f, 104f);
        rect.anchoredPosition = new Vector2(0f, -26f);

        return true;
    }

    /// <summary>
    /// Ce que la carte annonce : sa rareté, son nom et ce qu'elle fait.
    ///
    /// <para>Le portage affichait l'<b>identifiant technique</b> (<c>overload_plating</c>) et rien
    /// d'autre. C'est le pire endroit possible pour ce défaut : le jeu est en pause, le joueur doit
    /// arbitrer entre trois options, et aucune ne dit ce qu'elle fait. Une carte illisible transforme
    /// le choix — cœur de la progression — en tirage au hasard.</para>
    /// </summary>
    private static string Describe(LevelUpCard card)
    {
        // L'étiquette porte la RARETÉ, comme le jeu publié — « [Rare] Lance Cryo ». C'est elle qui
        // hiérarchise trois cartes d'un coup d'œil ; le type de la carte, lui, se déduit du texte.
        string tag = Loc.T(RarityOf(card) switch
        {
            "epic" => "RARITY_EPIC",
            "rare" => "RARITY_RARE",
            _      => "RARITY_COMMON",
        });

        string slug = card.Id.ToUpperInvariant();

        // Trois familles, trois préfixes de clé. La recherche s'arrête à la première qui répond :
        // une clé absente renvoie son propre nom, ce qui se repère immédiatement à l'écran.
        string name = FirstTranslated($"WPN_{slug}_NAME", $"PAS_{slug}_NAME", $"CARD_{slug}", card.Id);
        string desc = FirstTranslated($"WPN_{slug}_DESC", $"PAS_{slug}_DESC", $"CARD_{slug}_DESC", "");

        // Le niveau visé n'est pas un détail : « Lance Cryo » au niveau 1 et au niveau 12 sont deux
        // décisions très différentes.
        bool leveled = card.Kind is LevelUpCardKind.WeaponUpgrade or LevelUpCardKind.Passive;
        if (leveled) name += Loc.T("CARD_LEVEL", card.NextLevel);

        return desc.Length > 0 ? $"[{tag}]\n{name}\n\n{desc}" : $"[{tag}]\n{name}";
    }

    /// <summary>
    /// Rareté d'une carte. Une seule source pour le cadre <b>et</b> l'étiquette : les voir diverger
    /// (cadre épique, texte « Commun ») ne se remarque pas au code et saute aux yeux à l'écran.
    /// </summary>
    private static string RarityOf(LevelUpCard card) => card.Kind switch
    {
        LevelUpCardKind.Fusion   => "epic",
        LevelUpCardKind.Overload => "epic",
        LevelUpCardKind.Passive  => "rare",
        _                        => "common",
    };

    /// <summary>Première clé qui a une traduction, sinon le repli fourni en dernier argument.</summary>
    private static string FirstTranslated(params string[] keysThenFallback)
    {
        for (int i = 0; i < keysThenFallback.Length - 1; i++)
        {
            string translated = Loc.T(keysThenFallback[i]);
            if (translated != keysThenFallback[i]) return translated;
        }

        return keysThenFallback[^1];
    }
}
