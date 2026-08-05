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

    /// <summary>
    /// Une ligne d'amélioration. Elle porte <b>quatre</b> textes distincts et non un seul bloc :
    /// sous Godot, le nom et la description sont à gauche, le niveau et le coût alignés à droite, et
    /// l'achat se fait par un bouton propre. Tout empiler dans le libellé d'un grand bouton — ce que
    /// faisait le portage — supprime l'alignement des colonnes : l'œil ne peut plus comparer deux
    /// prix ni repérer d'un coup ce qui est au maximum.
    /// </summary>
    private sealed class Row
    {
        public MetaUpgradeDefinition Def = null!;
        public Text Name = null!;
        public Text Description = null!;
        public Text Level = null!;
        public Text Cost = null!;
        public Button Buy = null!;
    }

    private readonly List<Row> _rows = new();

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
            _echoLabel.text = Loc.T("HUB_ECHOES", MetaProgression.CurrentEchoes);

        RefreshPerkRow();
        RefreshTitleRow();

        foreach (var row in _rows)
        {
            int level = MetaProgression.LevelOf(row.Def.Id);
            int cost = MetaProgression.NextCost(row.Def.Id);
            bool maxed = cost < 0;

            row.Name.text = row.Def.Name;
            row.Description.text = row.Def.Description;
            row.Level.text = Loc.T("HUB_LEVEL", level, row.Def.MaxLevel);
            row.Cost.text = maxed ? Loc.T("HUB_MAX") : Loc.T("HUB_COST", cost);

            // Le coût s'éteint quand il n'y a plus rien à payer : un prix affiché sur une ligne au
            // maximum se lit comme un achat encore possible.
            row.Cost.color = maxed ? UiPalette.Dim : UiPalette.Gold;

            // Grisé quand c'est au maximum ou hors budget : le bouton dit ce qui est possible.
            row.Buy.interactable = MetaProgression.CanPurchase(row.Def.Id);
        }
    }

    private void Purchase(MetaUpgradeDefinition def)
    {
        if (!MetaProgression.TryPurchase(def.Id)) return;

        AudioSystem.PlaySfx("sfx_ui_purchase");
        Refresh();
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("HubCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 90);

        _root = canvasGo;

        UiStyle.ScreenBackdrop(canvasGo.transform);

        var panel = UiStyle.NewUiObject("Panel", canvasGo.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(60f, 40f);
        panelRect.offsetMax = new Vector2(-60f, -20f);

        float headerBottom = UiStyle.Header(panel.transform, Loc.T("HUB_TITLE"));

        // Le solde est en OR et juste sous le titre : c'est la seule information de cet écran qui
        // décide de ce que le joueur peut faire, et il doit la voir avant de lire les lignes.
        _echoLabel = UiStyle.Label(panel.transform, "", 28, UiPalette.Gold, TextAnchor.UpperCenter);
        var echoRect = _echoLabel.GetComponent<RectTransform>();
        echoRect.anchorMin = new Vector2(0f, 1f);
        echoRect.anchorMax = new Vector2(1f, 1f);
        echoRect.pivot = new Vector2(0.5f, 1f);
        echoRect.offsetMin = new Vector2(36f, -(headerBottom + 40f));
        echoRect.offsetMax = new Vector2(-36f, -headerBottom);

        headerBottom += 52f;

        // ⚠ La liste DOIT défiler : quatorze améliorations ne tiennent pas dans un panneau, et un
        // contenu centré qui déborde sort des DEUX côtés — le défaut déjà rencontré sur l'écran de
        // pause, où « Quitter la partie » finissait hors cadre.
        var scrollGo = UiStyle.NewUiObject("Scroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(28f, 96f);
        scrollRect.offsetMax = new Vector2(-28f, -headerBottom);

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
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;
        _list = content.transform;

        // Les améliorations D'ABORD, le perk et le titre ensuite — l'ordre du jeu publié. Ce qui
        // s'achète est la raison d'être de l'écran ; ce qui s'équipe se règle une fois puis ne bouge
        // plus. Le portage ouvrait sur deux lignes d'équipement, qui repoussaient hors écran ce que
        // le joueur venait faire.
        BuildRows();
        BuildPerkRow();
        BuildTitleRow();

        // Réinitialisation : elle REMBOURSE les Échos dépensés. C'est ce qui rend un arbre
        // d'améliorations réversible, donc explorable — sans elle, un achat regretté est définitif
        // et le joueur n'ose plus rien acheter.
        _reset = UiStyle.TextButton(panel.transform, Loc.T("HUB_RESET"), FrameAccent.Danger);
        var resetRect = _reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0f, 0f);
        resetRect.anchorMax = new Vector2(0f, 0f);
        resetRect.pivot = new Vector2(0f, 0f);
        resetRect.sizeDelta = new Vector2(460f, 60f);
        resetRect.anchoredPosition = new Vector2(28f, 16f);
        _reset.onClick.AddListener(ResetUpgrades);

        var close = UiStyle.TextButton(panel.transform, Loc.T("COMMON_BACK"), FrameAccent.Steel);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.sizeDelta = new Vector2(320f, 60f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        close.onClick.AddListener(Close);

        _firstButton = close;
    }

    private Button? _reset;
    private bool _resetArmed;

    /// <summary>
    /// Réinitialise les améliorations, <b>en deux temps</b>. Une confirmation est indispensable : le
    /// bouton est à portée de manette sur un écran qu'on parcourt vite, et l'action défait des
    /// heures de jeu — même remboursée, elle remet le personnage à zéro.
    /// </summary>
    private void ResetUpgrades()
    {
        var label = _reset?.GetComponentInChildren<Text>();

        if (!_resetArmed)
        {
            _resetArmed = true;
            if (label != null) label.text = Loc.T("HUB_RESET_CONFIRM");
            return;
        }

        _resetArmed = false;
        if (label != null) label.text = Loc.T("HUB_RESET");

        MetaProgression.ResetUpgrades();
        AudioSystem.PlaySfx("sfx_ui_purchase");
        Refresh();
    }

    /// <summary>
    /// Perk de départ : un seul à la fois, choisi ici parmi ceux que les défis ont débloqués.
    ///
    /// <para>Sans ce bouton, la récompense d'un défi se débloque et <b>ne sert jamais</b> — la boucle
    /// « accomplir → gagner → jouer autrement » reste ouverte sur son dernier maillon.</para>
    /// </summary>
    private void BuildPerkRow()
    {
        if (_list == null) return;

        _perkButton = UiStyle.TextButton(_list, "", FrameAccent.Violet);

        var element = _perkButton.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 72f;

        _perkLabel = _perkButton.GetComponentInChildren<Text>();
        _perkButton.onClick.AddListener(CyclePerk);
        RowCount++;
    }

    /// <summary>
    /// Fait défiler les perks débloqués, « aucun » compris. Un cycle plutôt qu'une liste : ils sont
    /// au plus trois, et un sous-écran pour trois entrées coûterait plus au joueur qu'il ne rapporte.
    /// </summary>
    private void CyclePerk()
    {
        var available = new List<string> { "" };
        foreach (var perk in StartingPerks.All)
            if (MetaProgression.HasPerk(perk.Id)) available.Add(perk.Id);

        int index = available.IndexOf(MetaProgression.EquippedPerk);
        MetaProgression.EquipPerk(available[(index + 1) % available.Count]);

        Refresh();
    }

    private void RefreshPerkRow()
    {
        if (_perkLabel == null || _perkButton == null) return;

        int unlocked = 0;
        foreach (var perk in StartingPerks.All)
            if (MetaProgression.HasPerk(perk.Id)) unlocked++;

        if (unlocked == 0)
        {
            _perkLabel.text = $"{Loc.T("HUB_PERKS")} : {Loc.T("HUB_PERK_NONE")}\n" +
                              "Accomplir des défis pour en débloquer";
            _perkButton.interactable = false;
            return;
        }

        var def = StartingPerks.ById(MetaProgression.EquippedPerk);
        _perkLabel.text = def == null
            ? $"{Loc.T("HUB_PERKS")} : {Loc.T("HUB_PERK_NONE")}   ({unlocked} disponible(s))"
            : $"{Loc.T("HUB_PERKS")} : {Loc.T(def.NameKey)}\n{Loc.T(def.DescKey)}";

        _perkButton.interactable = true;
    }

    private Button? _perkButton;
    private Text? _perkLabel;

    private void BuildRows()
    {
        if (_list == null) return;

        foreach (var def in MetaProgression.All)
        {
            // ⚠ Cadre de BOUTON et non de panneau. Le cadre de panneau porte un second liseré
            // intérieur : répété sur quatorze lignes, il donnait un empilement de cadres dans des
            // cadres où plus rien ne ressortait — alors qu'une ligne d'amélioration doit se lire
            // comme une ligne, pas comme une fenêtre.
            var panel = UiStyle.Card(_list, "Row_" + def.Id, FrameAccent.Cyan);

            var element = panel.AddComponent<LayoutElement>();
            element.minHeight = 96f;

            // Colonnes en DISPOSITION EXPLICITE, et non par ancres proportionnelles. Dans un
            // conteneur défilant dont la largeur est elle-même calculée, une ancre en pourcentage se
            // résout contre une largeur qui n'est pas encore connue : le texte débordait des deux
            // côtés du panneau et se faisait rogner par le masque de défilement — les premières
            // lettres de chaque amélioration manquaient.
            var rowLayout = panel.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(26, 26, 14, 14);
            rowLayout.spacing = 14f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            // Colonne de texte : elle prend TOUTE la place restante, d'où `flexibleWidth`. Sa
            // largeur préférée est mise à zéro, sinon celle du texte — potentiellement une ligne de
            // 1 500 px — pousserait les colonnes de droite hors du cadre.
            var textColumn = UiStyle.NewUiObject("Text", panel.transform);
            var textElement = textColumn.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
            textElement.preferredWidth = 0f;
            textElement.minWidth = 240f;

            var textLayout = textColumn.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 4f;
            textLayout.childForceExpandHeight = false;
            textLayout.childControlHeight = true;
            textLayout.childControlWidth = true;

            var name = UiStyle.Label(textColumn.transform, def.Name, 24, UiPalette.OffWhite);
            name.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            var description = UiStyle.Label(textColumn.transform, def.Description, 19, UiPalette.Dim);
            description.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var level = Column(panel.transform, 190f, UiPalette.Cyan);
            var cost = Column(panel.transform, 230f, UiPalette.Gold);

            var buy = UiStyle.TextButton(panel.transform, Loc.T("HUB_BUY"), FrameAccent.Gold);
            var buyElement = buy.gameObject.AddComponent<LayoutElement>();
            buyElement.preferredWidth = 190f;
            buyElement.preferredHeight = 58f;

            var captured = def;
            buy.onClick.AddListener(() => Purchase(captured));

            _rows.Add(new Row
            {
                Def = def,
                Name = name,
                Description = description,
                Level = level,
                Cost = cost,
                Buy = buy,
            });

            RowCount++;
        }
    }

    /// <summary>
    /// Colonne de droite à largeur fixe. C'est cette largeur constante d'une ligne à l'autre qui
    /// rend les niveaux et les prix <b>comparables d'un coup d'œil</b> — sans elle, chaque valeur
    /// se place où le texte de gauche la laisse, et l'écran redevient une liste de phrases.
    /// </summary>
    private static Text Column(Transform parent, float width, Color color)
    {
        var label = UiStyle.Label(parent, "", 21, color, TextAnchor.MiddleCenter);
        var element = label.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.minWidth = width;
        return label;
    }

    private Button? _titleButton;
    private Text? _titleLabel;

    /// <summary>
    /// Choix du <b>titre</b> cosmétique, dernier maillon de la boucle des défis.
    ///
    /// <para>Il manquait entièrement au portage : un titre se débloquait, et rien nulle part ne
    /// permettait de le porter — donc rien ne le montrait jamais. Un cosmétique qu'on ne peut pas
    /// équiper n'est pas une récompense, c'est une ligne dans une sauvegarde.</para>
    /// </summary>
    private void BuildTitleRow()
    {
        if (_list == null) return;

        _titleButton = UiStyle.TextButton(_list, "", FrameAccent.Violet);

        var element = _titleButton.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 72f;

        _titleLabel = _titleButton.GetComponentInChildren<Text>();
        _titleButton.onClick.AddListener(CycleTitle);
        RowCount++;
    }

    private void CycleTitle()
    {
        var available = new List<string> { "" };
        foreach (var title in Titles.All)
            if (MetaProgression.HasCosmetic(title.Id)) available.Add(title.Id);

        int index = available.IndexOf(MetaProgression.EquippedCosmetic);
        MetaProgression.EquipCosmetic(available[(index + 1) % available.Count]);

        Refresh();
    }

    private void RefreshTitleRow()
    {
        if (_titleLabel == null || _titleButton == null) return;

        int unlocked = 0;
        foreach (var title in Titles.All)
            if (MetaProgression.HasCosmetic(title.Id)) unlocked++;

        if (unlocked == 0)
        {
            _titleLabel.text = $"{Loc.T("HUB_TITLES")} : {Loc.T("HUB_PERK_NONE")}";
            _titleButton.interactable = false;
            return;
        }

        var def = Titles.ById(MetaProgression.EquippedCosmetic);
        _titleLabel.text = def == null
            ? $"{Loc.T("HUB_TITLES")} : {Loc.T("HUB_PERK_NONE")}   ({unlocked})"
            : $"{Loc.T("HUB_TITLES")} : {Loc.T(def.NameKey)}";

        _titleButton.interactable = true;
    }
}
