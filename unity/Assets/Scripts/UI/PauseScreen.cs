using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Écran de pause (Lot 5).
///
/// <para><b>Un piège documenté du projet est reproduit ici volontairement.</b> Sous Godot, cet écran
/// a fini par devenir <i>inutilisable</i> en fin de run : titre, corps et boutons vivaient dans un
/// seul conteneur centré, <b>sans défilement ni plafond</b>. Avec 5 armes niveau 20, 4 passifs et
/// 5 greffes multilignes, le panneau dépassait la fenêtre — et, <i>parce qu'il était centré</i>,
/// débordait des <b>deux</b> côtés. Le bouton « Quitter la partie » se retrouvait hors cadre : plus
/// aucun moyen d'abandonner.</para>
///
/// <para>D'où la structure retenue : <b>seul le corps défile</b>. Le titre et les boutons vivent en
/// dehors de la zone de défilement, donc restent atteignables quelle que soit la longueur du
/// contenu.</para>
/// </summary>
public sealed class PauseScreen : MonoBehaviour
{
    /// <summary>Le joueur demande la reprise.</summary>
    public event Action? Resumed;

    /// <summary>Le joueur demande à quitter la partie.</summary>
    public event Action? QuitRequested;

    /// <summary>L'écran est-il affiché ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    private GameObject? _root;
    private Text? _body;
    private Button? _firstButton;

    private void Awake()
    {
        BuildUi();
        SetVisible(false);
    }

    /// <summary>Bascule pause / reprise.</summary>
    public void Toggle()
    {
        if (IsVisible) Resume();
        else Open();
    }

    /// <summary>Ouvre l'écran et met le jeu en pause.</summary>
    public void Open(string? bodyText = null)
    {
        if (_body != null && bodyText != null) _body.text = bodyText;

        SetVisible(true);
        SceneRoot.Paused = true;

        // Focus initial : sans lui, l'écran est infranchissable à la manette — et c'est l'écran par
        // lequel on quitte la partie.
        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    /// <summary>Ferme l'écran et relance le jeu.</summary>
    public void Resume()
    {
        SetVisible(false);
        SceneRoot.Paused = false;
        Resumed?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("PauseCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 110);   // au-dessus du HUD et des effets plein écran

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Steel);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 700f);
        rect.anchoredPosition = Vector2.zero;

        // ─── Titre : HORS zone de défilement ──────────────────────────────────
        var title = UiStyle.Label(panel.transform, "PAUSE", 40, UiPalette.Cyan, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -90f);
        titleRect.offsetMax = new Vector2(-24f, -24f);

        // ─── Corps : SEUL élément qui défile ──────────────────────────────────
        var scrollGo = UiStyle.NewUiObject("BodyScroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(24f, 120f);   // laisse la place aux boutons, en bas
        scrollRect.offsetMax = new Vector2(-24f, -100f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
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
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;

        _body = UiStyle.Label(content.transform, "", 20, UiPalette.OffWhite);

        // ─── Boutons : HORS zone de défilement, donc toujours atteignables ────
        var buttonRow = UiStyle.NewUiObject("Buttons", panel.transform);
        var rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.offsetMin = new Vector2(24f, 24f);
        rowRect.offsetMax = new Vector2(-24f, 96f);

        var layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var resume = UiStyle.TextButton(buttonRow.transform, "Reprendre", FrameAccent.Cyan);
        resume.onClick.AddListener(Resume);
        _firstButton = resume;

        var quit = UiStyle.TextButton(buttonRow.transform, "Quitter la partie", FrameAccent.Danger);
        quit.onClick.AddListener(() =>
        {
            SetVisible(false);
            SceneRoot.Paused = false;   // ne jamais quitter en laissant le temps figé
            QuitRequested?.Invoke();
        });

        Navigation.Mode explicitMode = Navigation.Mode.Explicit;
        var navA = resume.navigation; navA.mode = explicitMode; navA.selectOnRight = quit; resume.navigation = navA;
        var navB = quit.navigation;   navB.mode = explicitMode; navB.selectOnLeft = resume; quit.navigation = navB;
    }
}
