using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Le Hub — <b>le seul endroit où les Échos servent à quelque chose</b> (Lot 6).
///
/// <para>Sans lui, la boucle de rétention est ouverte : les runs rapportent une monnaie qui
/// s'accumule sans jamais rien acheter. C'est un état que le projet a déjà rencontré <i>à l'envers</i>
/// (56 334 Échos dormants pour un arbre à 21 550), et la conclusion vaut ici : <b>une récompense qui
/// n'a rien à acheter cesse d'être une récompense</b>.</para>
///
/// <para>Il s'ouvre par-dessus le menu principal plutôt que dans une scène à lui : l'aller-retour
/// « je regarde mes Échos puis je relance » doit être immédiat.</para>
/// </summary>
public sealed class HubScreen : MonoBehaviour
{
    /// <summary>Émis à la fermeture.</summary>
    public event Action? Closed;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Lignes construites — observable pour les vérifications.</summary>
    public int RowCount { get; private set; }

    private GameObject? _root;
    private Transform? _list;
    private Text? _echoLabel;
    private Button? _firstButton;

    private readonly List<(MetaUpgradeDefinition Def, Button Button, Text Label)> _rows = new();

    private void Awake()
    {
        BuildUi();
        Hide();
    }

    /// <summary>Ouvre le Hub et rafraîchit tout ce qu'il affiche.</summary>
    public void Show()
    {
        if (_root == null) return;

        _root.SetActive(true);
        Refresh();

        // Sans focus initial, l'écran est infranchissable à la manette.
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

    /// <summary>
    /// Remet à jour le solde, les niveaux et l'état des boutons. Appelée après <b>chaque</b> achat :
    /// un prix qui reste affiché après paiement laisse croire que rien ne s'est passé.
    /// </summary>
    private void Refresh()
    {
        if (_echoLabel != null)
            _echoLabel.text = $"{MetaProgression.CurrentEchoes} ÉCHOS D'AETHER";

        foreach (var (def, button, label) in _rows)
        {
            int level = MetaProgression.LevelOf(def.Id);
            int cost = MetaProgression.NextCost(def.Id);

            string price = cost < 0 ? "MAX" : $"{cost} É";
            label.text = $"{def.Name}   {level}/{def.MaxLevel}   {price}\n{def.Description}";

            // Grisé quand c'est au maximum ou hors budget : le bouton dit ce qui est possible.
            button.interactable = MetaProgression.CanPurchase(def.Id);
        }
    }

    private void Purchase(MetaUpgradeDefinition def)
    {
        if (!MetaProgression.TryPurchase(def.Id)) return;
        Refresh();
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("HubCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;

        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Gold);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1200f, 820f);
        panelRect.anchoredPosition = Vector2.zero;

        var title = UiStyle.Label(panel.transform, "HUB", 40, UiPalette.Gold, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -74f);
        titleRect.offsetMax = new Vector2(-24f, -20f);

        _echoLabel = UiStyle.Label(panel.transform, "", 24, UiPalette.Cyan, TextAnchor.UpperCenter);
        var echoRect = _echoLabel.GetComponent<RectTransform>();
        echoRect.anchorMin = new Vector2(0f, 1f);
        echoRect.anchorMax = new Vector2(1f, 1f);
        echoRect.pivot = new Vector2(0.5f, 1f);
        echoRect.offsetMin = new Vector2(24f, -112f);
        echoRect.offsetMax = new Vector2(-24f, -78f);

        // ⚠ La liste DOIT défiler : quatorze améliorations ne tiennent pas dans un panneau, et un
        // contenu centré qui déborde sort des DEUX côtés — le défaut déjà rencontré sur l'écran de
        // pause, où « Quitter la partie » finissait hors cadre.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(28f, 88f);
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
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        _list = content.transform;

        BuildRows();

        var close = UiStyle.TextButton(panel.transform, "Retour", FrameAccent.Steel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 60f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        close.onClick.AddListener(Close);

        _firstButton = close;
    }

    private void BuildRows()
    {
        if (_list == null) return;

        foreach (var def in MetaProgression.All)
        {
            var button = UiStyle.TextButton(_list, def.Name, FrameAccent.Gold);

            var element = button.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 76f;

            var label = button.GetComponentInChildren<Text>();
            var captured = def;
            button.onClick.AddListener(() => Purchase(captured));

            _rows.Add((def, button, label));
            RowCount++;
        }
    }
}
