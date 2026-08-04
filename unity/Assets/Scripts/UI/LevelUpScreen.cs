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

    /// <summary>Cartes actuellement affichées.</summary>
    public IReadOnlyList<LevelUpCard> Cards => _cards;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    private readonly List<LevelUpCard> _cards = new();
    private GameObject? _root;
    private Transform? _cardRow;
    private Button? _firstButton;

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

    private void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
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

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Au-dessus du HUD et de tout effet plein écran : une modale assombrie par la vignette
        // avait déjà été un défaut visible du jeu.
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;

        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Cyan);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1100f, 520f);
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
    }

    private void BuildCards()
    {
        if (_cardRow == null) return;

        foreach (Transform child in _cardRow) Destroy(child.gameObject);
        _firstButton = null;

        foreach (var card in _cards)
        {
            var captured = card;
            var accent = card.Kind switch
            {
                LevelUpCardKind.Fusion   => FrameAccent.Gold,
                LevelUpCardKind.Overload => FrameAccent.Danger,
                LevelUpCardKind.Passive  => FrameAccent.Violet,
                _                        => FrameAccent.Cyan,
            };

            var button = UiStyle.TextButton(_cardRow, Describe(card), accent);
            button.onClick.AddListener(() => Choose(captured));

            _firstButton ??= button;
        }

        // Chaîne de focus explicite : la navigation automatique d'Unity se perd dès qu'un élément
        // est reconstruit à chaque ouverture.
        var buttons = _cardRow.GetComponentsInChildren<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft  = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnRight = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = nav;
        }
    }

    private static string Describe(LevelUpCard card) => card.Kind switch
    {
        LevelUpCardKind.NewWeapon     => $"{card.Id}\nNOUVELLE ARME",
        LevelUpCardKind.WeaponUpgrade => $"{card.Id}\nNIVEAU {card.NextLevel}",
        LevelUpCardKind.Passive       => $"{card.Id}\nPASSIF {card.NextLevel}",
        LevelUpCardKind.Fusion        => $"{card.Id}\nFUSION",
        LevelUpCardKind.Overload      => $"{card.Id}\nSURCHARGE",
        _                             => card.Id,
    };
}
