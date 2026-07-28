using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran Hub — dépense des Échos d'Aether en améliorations permanentes.
/// Liste les améliorations depuis MetaProgressionSystem (données provenant de meta_upgrades.json),
/// + un bouton de réinitialisation (remboursement) et le sélecteur de personnage.
/// </summary>
public partial class HubScreen : Control
{
    private Label          _echoesLabel       = null!;
    private ScrollContainer _upgradesScroll   = null!;
    private VBoxContainer  _upgradesList      = null!;
    private Button         _backButton        = null!;
    private Button        _resetButton       = null!;
    private bool          _resetArmed        = false;
    private ColorRect     _fadeOverlay       = null!;

    // Sélecteur d'arme de départ (obsolète, masqué — chaque perso a son arme de signature)
    private HBoxContainer _weaponSelector     = null!;

    // Lignes de l'UI générées dynamiquement
    private readonly List<UpgradeRow> _rows = new();

    // Sélecteurs de perk / titre (sections construites en code si ≥1 débloqué)
    private readonly List<Button> _perkChips  = new();
    private readonly List<Button> _titleChips = new();

    public override void _Ready()
    {
        _echoesLabel        = GetNode<Label>("VBox/EchoesLabel");
        _upgradesScroll     = GetNode<ScrollContainer>("VBox/UpgradesScroll");
        _upgradesList       = GetNode<VBoxContainer>("VBox/UpgradesScroll/UpgradesList");
        _backButton         = GetNode<Button>("VBox/ButtonsRow/BackButton");
        _weaponSelector     = GetNode<HBoxContainer>("VBox/WeaponSelector");
        _fadeOverlay        = GetNode<ColorRect>("FadeOverlay");

        _backButton.Pressed += OnBackPressed;
        ConnectHoverEffects(_backButton, 1.04f);

        GetNode<Label>("VBox/TitleLabel").Text = Loc.T("HUB_TITLE");
        _backButton.Text = Loc.T("COMMON_BACK");

        StyleStaticSeparators();
        BuildUpgradesList();
        BuildPerkSelector();
        BuildTitleSelector();
        BuildResetButton();
        RefreshDisplay();
        SetupFocusChain();

        // Couleurs EchoesLabel appliquées en code (complément au .tscn)
        _echoesLabel.AddThemeFontSizeOverride("font_size", 24);
        _echoesLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.267f));

        // Musique du Hub
        AudioSystem.Instance?.PlayMusic("music_hub");

        // Fade-in : opaque → transparent en 0.6 s
        var tween = CreateTween();
        tween.TweenProperty(_fadeOverlay, "color:a", 0f, 0.6)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(() => _backButton.GrabFocus()));
    }

    // ---------------------------------------------------------------------------
    // Habillage des séparateurs de la scène (ART_BRIEF_UI_FRAMES §3.6)
    // ---------------------------------------------------------------------------

    /// <summary>Pose le soulignement gravé sous le titre d'écran et remplace les
    /// <c>HSeparator</c> natifs du <c>.tscn</c> (gris neutre du thème) par le séparateur maison.</summary>
    private void StyleStaticSeparators()
    {
        var vbox  = GetNode<VBoxContainer>("VBox");
        var title = GetNode<Label>("VBox/TitleLabel");

        var underline = UiStyle.ScreenTitleUnderline(UiPalette.Cyan);
        vbox.AddChild(underline);
        vbox.MoveChild(underline, title.GetIndex() + 1);

        ReplaceSeparator(vbox, "Separator");
        ReplaceSeparator(vbox, "Separator2");
    }

    /// <summary>Substitue en place (même index dans le VBox) un <c>HSeparator</c> par
    /// <see cref="UiStyle.Separator"/>.</summary>
    private static void ReplaceSeparator(VBoxContainer parent, string nodeName)
    {
        var old = parent.GetNodeOrNull<Control>(nodeName);
        if (old == null) return;

        int index = old.GetIndex();
        parent.RemoveChild(old);
        old.QueueFree();

        var sep = UiStyle.Separator(UiPalette.Cyan);
        parent.AddChild(sep);
        parent.MoveChild(sep, index);
    }

    // ---------------------------------------------------------------------------
    // Construction de la liste
    // ---------------------------------------------------------------------------

    private void BuildUpgradesList()
    {
        var upgrades = MetaProgressionSystem.Instance.GetAllUpgrades();

        foreach (var def in upgrades)
        {
            // Ligne : [Nom + description] | [Niv X/Y] | [Coût : Z] | [Bouton Acheter]
            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string ukey = def.Id.ToUpperInvariant();
            var nameLabel = new Label
            {
                Text                = $"{Loc.T($"UPGRADE_{ukey}_NAME")}\n{Loc.T($"UPGRADE_{ukey}_DESC")}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 16);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));

            var levelLabel = new Label
            {
                Text                = "Niv 0/0",
                CustomMinimumSize   = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            levelLabel.AddThemeFontSizeOverride("font_size", 16);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.267f, 1f, 0.933f));

            var costLabel = new Label
            {
                Text                = "—", // placeholder, écrasé par RefreshDisplay (HUB_COST)
                CustomMinimumSize   = new Vector2(120, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            costLabel.AddThemeFontSizeOverride("font_size", 16);
            costLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.267f));

            var buyButton = new Button
            {
                Text              = Loc.T("HUB_BUY"),
                CustomMinimumSize = new Vector2(100, 0),
            };

            // Acheter dépense des Échos : accent or, même sémantique que le coût affiché à côté.
            buyButton.AddThemeStyleboxOverride("normal",   UiStyle.ButtonFrame(UiStyle.FrameAccent.Gold));
            buyButton.AddThemeStyleboxOverride("hover",    UiStyle.ButtonFrame(UiStyle.FrameAccent.Gold, UiStyle.FrameState.Hover));
            buyButton.AddThemeStyleboxOverride("pressed",  UiStyle.ButtonFrame(UiStyle.FrameAccent.Gold, UiStyle.FrameState.Pressed));
            buyButton.AddThemeStyleboxOverride("disabled", UiStyle.ButtonFrameDisabled());

            buyButton.AddThemeFontSizeOverride("font_size", 15);
            buyButton.AddThemeColorOverride("font_color", UiPalette.OffWhite);

            string capturedId = def.Id;
            buyButton.Pressed += () => OnBuyPressed(capturedId);
            ConnectHoverEffects(buyButton, 1.02f);

            // Liste focalisable qui déborde (18 upgrades) : le focus manette/clavier doit
            // scroller automatiquement la liste pour garder le bouton visible.
            buyButton.FocusEntered += () => _upgradesScroll.EnsureControlVisible(buyButton);

            row.AddChild(nameLabel);
            row.AddChild(levelLabel);
            row.AddChild(costLabel);
            row.AddChild(buyButton);

            // Encapsule le row dans un PanelContainer stylé (ART_BRIEF_UI_FRAMES §3.3).
            // Variante « sunken » : le fond du Hub est déjà #1A1A2E, le fill nominal du §3.3
            // s'y composite à l'identique et le panneau disparaîtrait. Marge 8 px (au lieu de
            // 16) pour ne pas doubler la hauteur des 18 rangées de la liste.
            var panel = new PanelContainer();
            var rowStyle = UiStyle.ScreenPanelSunken(8);
            // Les 8 px verticaux sont volontaires (voir ci-dessus) mais, LATÉRALEMENT, ils collaient
            // le bouton « Acheter » au bord de la liste — même gêne que les cartes de personnage et
            // de niveau, alignées à 16 + 12 (cf. docs/PITFALLS.md). On élargit donc les seuls côtés,
            // sans toucher à la hauteur des 18 rangées.
            const int rowSideMargin = 20;
            rowStyle.SetContentMargin(Side.Left,  rowSideMargin);
            rowStyle.SetContentMargin(Side.Right, rowSideMargin);
            panel.AddThemeStyleboxOverride("panel", rowStyle);
            panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            panel.AddChild(row);

            _upgradesList.AddChild(panel);
            _rows.Add(new UpgradeRow(def.Id, levelLabel, costLabel, buyButton));
        }
    }

    /// <summary>
    /// Bouton « Réinitialiser les améliorations » (rembourse l'intégralité des Échos dépensés).
    /// Confirmation en 2 temps pour éviter les clics accidentels.
    /// </summary>
    // ---------------------------------------------------------------------------
    // Sélecteurs de perk de départ / titre (débloqués via les Défis)
    // ---------------------------------------------------------------------------

    private static readonly Color PerkAccent = new(0.667f, 0.267f, 1f);   // violet

    /// <summary>Construit la section « Perk de départ » : chip « Aucun » + un chip par perk débloqué.
    /// Masquée tant qu'aucun perk n'est débloqué (reste discret pour les nouveaux joueurs).</summary>
    private void BuildPerkSelector()
    {
        var meta = MetaProgressionSystem.Instance;
        var unlocked = new List<PerkDef>();
        foreach (var p in StartingPerks.All)
            if (meta.Meta.UnlockedPerks.Contains(p.Id)) unlocked.Add(p);
        if (unlocked.Count == 0) return;

        var row = BuildChipSection(Loc.T("HUB_PERKS"), _perkChips);
        row.AddChild(MakeChip("", Loc.T("HUB_PERK_NONE"), null, OnPerkChipPressed, _perkChips));
        foreach (var p in unlocked)
            row.AddChild(MakeChip(p.Id, Loc.T(p.NameKey), p.IconPath, OnPerkChipPressed, _perkChips));

        RefreshChips(_perkChips, meta.Meta.EquippedPerk);
    }

    /// <summary>Construit la section « Titre » : chip « Aucun » + un chip par titre débloqué (flair
    /// cosmétique affiché sur le menu). Masquée tant qu'aucun titre n'est débloqué.</summary>
    private void BuildTitleSelector()
    {
        var meta = MetaProgressionSystem.Instance;
        var unlocked = new List<TitleDef>();
        foreach (var t in Titles.All)
            if (meta.Meta.UnlockedCosmetics.Contains(t.Id)) unlocked.Add(t);
        if (unlocked.Count == 0) return;

        var row = BuildChipSection(Loc.T("HUB_TITLES"), _titleChips);
        row.AddChild(MakeChip("", Loc.T("HUB_PERK_NONE"), null, OnTitleChipPressed, _titleChips));
        foreach (var t in unlocked)
            row.AddChild(MakeChip(t.Id, Loc.T(t.NameKey), null, OnTitleChipPressed, _titleChips));

        RefreshChips(_titleChips, meta.Meta.EquippedCosmetic);
    }

    /// <summary>Crée l'ossature d'une section (header + rangée de chips), l'insère avant la rangée de
    /// boutons du bas et renvoie le HBox de la rangée à remplir de chips.</summary>
    private HBoxContainer BuildChipSection(string headerText, List<Button> registry)
    {
        registry.Clear();
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);

        var header = new Label { Text = headerText };
        header.AddThemeFontSizeOverride("font_size", 18);
        header.AddThemeColorOverride("font_color", PerkAccent);
        section.AddChild(header);
        // Écart assumé au §3.6 (« tout titre de section est suivi du séparateur ») : appliqué ici,
        // il ajoutait deux séparateurs de 8 px dans la moitié basse de l'écran et faisait perdre
        // deux lignes d'améliorations à la liste, qui est le contenu principal du Hub. Ces deux
        // en-têtes sont déjà nettement identifiés par leur couleur d'accent et leur taille.

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        section.AddChild(row);

        var vbox       = GetNode<VBoxContainer>("VBox");
        var buttonsRow = GetNode<Control>("VBox/ButtonsRow");
        vbox.AddChild(section);
        vbox.MoveChild(section, buttonsRow.GetIndex());
        return row;
    }

    private Button MakeChip(string id, string label, string? iconPath, System.Action<string> onPressed, List<Button> registry)
    {
        var btn = new Button
        {
            Text              = label,
            CustomMinimumSize = new Vector2(0, 44),
            ExpandIcon        = true,
        };
        if (iconPath != null)
        {
            var tex = GD.Load<Texture2D>(iconPath);
            if (tex != null) btn.Icon = tex;
        }
        btn.AddThemeFontSizeOverride("font_size", 15);
        btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));
        btn.SetMeta("chipId", id);
        btn.Pressed += () => onPressed(id);
        ConnectHoverEffects(btn, 1.03f);
        registry.Add(btn);
        return btn;
    }

    /// <summary>Restyle chaque chip d'un registre selon l'id équipé (bordure or + fond appuyé = sélectionné).</summary>
    private static void RefreshChips(List<Button> registry, string equipped)
    {
        foreach (var chip in registry)
            ApplyChipStyle(chip, chip.GetMeta("chipId").AsString() == equipped);
    }

    /// <summary>
    /// Cadre d'une puce de perk/titre. La sélection est encodée par l'accent — or (la puce
    /// équipée, même sémantique que les Échos) contre violet (les puces disponibles) — et non
    /// plus par une épaisseur de bordure, invisible au premier coup d'œil.
    /// </summary>
    private static void ApplyChipStyle(Button btn, bool selected)
    {
        // Cadre compact : ces puces font 30 à 40 px de haut, la plaque 9-slice y placerait le
        // texte sous son liseré. La sélection est portée par l'accent — or (équipé) contre violet
        // (disponible) — et non plus par une épaisseur de bordure, invisible au premier regard.
        var accent = selected ? UiPalette.Gold : PerkAccent;

        btn.AddThemeStyleboxOverride("normal",  UiStyle.CompactFrame(accent, selected));
        btn.AddThemeStyleboxOverride("hover",   UiStyle.CompactFrame(accent, selected: true));
        btn.AddThemeStyleboxOverride("pressed", UiStyle.CompactFrame(accent, selected: true));
        btn.AddThemeStyleboxOverride("focus",   UiStyle.CompactFrame(UiPalette.Violet, selected: true));
    }

    private void OnPerkChipPressed(string perkId)
    {
        var meta = MetaProgressionSystem.Instance;
        meta.Meta.EquippedPerk = perkId;   // "" = aucun
        meta.PersistMeta();
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        RefreshChips(_perkChips, perkId);
    }

    private void OnTitleChipPressed(string titleId)
    {
        var meta = MetaProgressionSystem.Instance;
        meta.Meta.EquippedCosmetic = titleId;   // "" = aucun
        meta.PersistMeta();
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        RefreshChips(_titleChips, titleId);
    }

    private void BuildResetButton()
    {
        _resetButton = new Button
        {
            Text              = Loc.T("HUB_RESET"),
            CustomMinimumSize = new Vector2(0, 40),
        };

        // Accent ambre « attention » : distingue cette action destructrice des achats (or vif),
        // sans introduire de rouge/orange hors charte (ART_BRIEF_UI_FRAMES §3.0).
        _resetButton.AddThemeStyleboxOverride("normal",  UiStyle.ButtonFrame(UiStyle.FrameAccent.Danger));
        _resetButton.AddThemeStyleboxOverride("hover",   UiStyle.ButtonFrame(UiStyle.FrameAccent.Danger, UiStyle.FrameState.Hover));
        _resetButton.AddThemeStyleboxOverride("pressed", UiStyle.ButtonFrame(UiStyle.FrameAccent.Danger, UiStyle.FrameState.Pressed));
        _resetButton.AddThemeStyleboxOverride("focus",   UiStyle.ButtonFrame(UiStyle.FrameAccent.Danger, focus: true));

        _resetButton.AddThemeFontSizeOverride("font_size", 16);
        _resetButton.AddThemeColorOverride("font_color", UiPalette.Gold.Lighten(0.2f));
        _resetButton.Pressed += OnResetPressed;
        ConnectHoverEffects(_resetButton, 1.02f);

        // Insère le bouton juste avant la rangée Retour / Jouer.
        var vbox       = GetNode<VBoxContainer>("VBox");
        var buttonsRow = GetNode<Control>("VBox/ButtonsRow");
        vbox.AddChild(_resetButton);
        vbox.MoveChild(_resetButton, buttonsRow.GetIndex());
    }

    private void OnResetPressed()
    {
        // 1er clic : armer + demander confirmation (désarmé après 3 s).
        if (!_resetArmed)
        {
            _resetArmed       = true;
            _resetButton.Text = Loc.T("HUB_RESET_CONFIRM");
            AudioSystem.Instance?.PlaySfx("sfx_ui_button");
            var t = GetTree().CreateTimer(3.0);
            t.Timeout += DisarmReset;
            return;
        }

        // 2e clic : exécute le reset.
        DisarmReset();
        int refund = MetaProgressionSystem.Instance.ResetUpgrades();
        AudioSystem.Instance?.PlaySfx(refund > 0 ? "sfx_ui_purchase" : "sfx_ui_button");
        RefreshDisplay();
    }

    private void DisarmReset()
    {
        if (!GodotObject.IsInstanceValid(_resetButton)) return;
        _resetArmed       = false;
        _resetButton.Text = Loc.T("HUB_RESET");
    }

    // ---------------------------------------------------------------------------
    // Mise à jour affichage
    // ---------------------------------------------------------------------------

    private void RefreshDisplay()
    {
        var meta = MetaProgressionSystem.Instance;

        _echoesLabel.Text = Loc.T("HUB_ECHOES", meta.CurrentEchoes);

        var upgrades = meta.GetAllUpgrades();
        foreach (var row in _rows)
        {
            // Retrouve la définition correspondante
            MetaUpgradeDefinition? def = null;
            foreach (var u in upgrades)
                if (u.Id == row.Id) { def = u; break; }
            if (def == null) continue;

            int currentLevel = meta.GetUpgradeLevel(row.Id);
            bool isMaxed     = currentLevel >= def.MaxLevel;

            row.LevelLabel.Text = Loc.T("HUB_LEVEL", currentLevel, def.MaxLevel);

            if (isMaxed)
            {
                row.CostLabel.Text     = Loc.T("HUB_MAX");
                row.BuyButton.Disabled = true;
            }
            else
            {
                int cost               = def.CostPerLevel[currentLevel];
                row.CostLabel.Text     = Loc.T("HUB_COST", cost);
                row.BuyButton.Disabled = meta.CurrentEchoes < cost;
            }
        }

        // Sélecteur d'arme de départ
        RefreshWeaponSelector();
    }

    private void RefreshWeaponSelector()
    {
        // Le sélecteur d'arme de départ méta est obsolète : chaque personnage définit
        // sa propre arme de signature (décision design 2026-06-27). On le masque.
        _weaponSelector.Visible = false;
    }

    // ---------------------------------------------------------------------------
    // Callbacks
    // ---------------------------------------------------------------------------

    private void OnBuyPressed(string upgradeId)
    {
        bool success = MetaProgressionSystem.Instance.TryPurchase(upgradeId);
        if (success)
        {
            AudioSystem.Instance?.PlaySfx("sfx_ui_purchase");
            RefreshDisplay();
        }
        else
        {
            AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        }
    }

    private void OnBackPressed()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        TransitionTo("res://scenes/MainMenu.tscn");
    }

    // ---------------------------------------------------------------------------
    // Transition avec fade-out
    // ---------------------------------------------------------------------------

    private void TransitionTo(string scenePath)
    {
        _backButton.Disabled = true;
        var tween = CreateTween();
        tween.TweenProperty(_fadeOverlay, "color:a", 1f, 0.3f)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);
        tween.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(scenePath)));
    }

    // ---------------------------------------------------------------------------
    // Hover effects (souris + focus clavier/manette)
    // ---------------------------------------------------------------------------

    /// <param name="focusAccent">Accent du cadre de focus, ou <c>null</c> quand l'appelant pose
    /// lui-même son focus (puces, bouton de réinitialisation) — sans quoi on l'écraserait ici.</param>
    private void ConnectHoverEffects(Button btn, float targetScale, UiStyle.FrameAccent? focusAccent = UiStyle.FrameAccent.Violet)
    {
        btn.PivotOffset = btn.CustomMinimumSize / 2f;

        if (focusAccent.HasValue)
            btn.AddThemeStyleboxOverride("focus", UiStyle.ButtonFrame(focusAccent.Value, focus: true));
        UiStyle.AttachFocusPulse(btn);

        btn.MouseEntered += () => OnBtnEntered(btn, targetScale);
        btn.MouseExited  += () => OnBtnExited(btn);
        btn.FocusEntered += () => OnBtnEntered(btn, targetScale);
        btn.FocusExited  += () => OnBtnExited(btn);
    }

    private void OnBtnEntered(Button btn, float targetScale)
    {
        btn.PivotOffset = btn.Size / 2f;
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", new Vector2(targetScale, targetScale), 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    private void OnBtnExited(Button btn)
    {
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", Vector2.One, 0.12)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
    }

    private void SetupFocusChain()
    {
        if (_rows.Count == 0) return;

        _backButton.FocusNeighborBottom = _backButton.GetPathTo(_rows[0].BuyButton);

        for (int i = 0; i < _rows.Count; i++)
        {
            var btn = _rows[i].BuyButton;
            btn.FocusNeighborTop    = btn.GetPathTo(i == 0 ? _backButton : _rows[i - 1].BuyButton);
            // La dernière ligne descend vers le bouton Reset (inséré avant Retour).
            btn.FocusNeighborBottom = btn.GetPathTo(i == _rows.Count - 1 ? (Control)_resetButton : _rows[i + 1].BuyButton);
        }

        // Rangées de chips (perk, puis titre) intercalées, si présentes, entre la dernière amélioration
        // et le bouton Reset. Chaînage vertical : dernière amélio → rangée(s) de chips → Reset.
        var chipRows = new List<List<Button>>();
        if (_perkChips.Count  > 0) chipRows.Add(_perkChips);
        if (_titleChips.Count > 0) chipRows.Add(_titleChips);

        Control above = _rows[^1].BuyButton;
        for (int r = 0; r < chipRows.Count; r++)
        {
            var chips = chipRows[r];
            Control below = (r < chipRows.Count - 1) ? chipRows[r + 1][0] : _resetButton;

            if (above == _rows[^1].BuyButton)
                above.FocusNeighborBottom = above.GetPathTo(chips[0]);

            for (int i = 0; i < chips.Count; i++)
            {
                var chip = chips[i];
                chip.FocusNeighborTop    = chip.GetPathTo(above);
                chip.FocusNeighborBottom = chip.GetPathTo(below);
                if (i > 0)               chip.FocusNeighborLeft  = chip.GetPathTo(chips[i - 1]);
                if (i < chips.Count - 1) chip.FocusNeighborRight = chip.GetPathTo(chips[i + 1]);
            }
            above = chips[0];
        }

        _resetButton.FocusNeighborTop    = _resetButton.GetPathTo(above);
        _resetButton.FocusNeighborBottom = _resetButton.GetPathTo(_backButton);
        _backButton.FocusNeighborTop     = _backButton.GetPathTo(_resetButton);
    }

    // ---------------------------------------------------------------------------
    // Navigation clavier/manette
    // ---------------------------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            OnBackPressed();
        }
    }

    // ---------------------------------------------------------------------------
    // DTO ligne UI
    // ---------------------------------------------------------------------------

    private sealed record UpgradeRow(string Id, Label LevelLabel, Label CostLabel, Button BuyButton);
}
