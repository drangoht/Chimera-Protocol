using Godot;
using System.Collections.Generic;

/// <summary>
/// Écran Hub — dépense des Échos d'Aether en améliorations permanentes.
/// Liste les améliorations depuis MetaProgressionSystem (données provenant de meta_upgrades.json),
/// + un bouton de réinitialisation (remboursement) et le sélecteur de personnage.
/// </summary>
public partial class HubScreen : Control
{
    private Label         _echoesLabel       = null!;
    private VBoxContainer _upgradesList      = null!;
    private Button        _backButton        = null!;
    private Button        _resetButton       = null!;
    private bool          _resetArmed        = false;
    private ColorRect     _fadeOverlay       = null!;

    // Sélecteur d'arme de départ (obsolète, masqué — chaque perso a son arme de signature)
    private HBoxContainer _weaponSelector     = null!;

    // Lignes de l'UI générées dynamiquement
    private readonly List<UpgradeRow> _rows = new();

    public override void _Ready()
    {
        _echoesLabel        = GetNode<Label>("VBox/EchoesLabel");
        _upgradesList       = GetNode<VBoxContainer>("VBox/UpgradesList");
        _backButton         = GetNode<Button>("VBox/ButtonsRow/BackButton");
        _weaponSelector     = GetNode<HBoxContainer>("VBox/WeaponSelector");
        _fadeOverlay        = GetNode<ColorRect>("FadeOverlay");

        _backButton.Pressed += OnBackPressed;
        ConnectHoverEffects(_backButton, 1.04f);

        GetNode<Label>("VBox/TitleLabel").Text = Loc.T("HUB_TITLE");
        _backButton.Text = Loc.T("COMMON_BACK");

        BuildUpgradesList();
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

            var nameLabel = new Label
            {
                Text                = $"{def.Name}\n{def.Description}",
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
                Text                = "Coût : —",
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

            // Style du bouton Acheter
            var buyStyleNormal = new StyleBoxFlat();
            buyStyleNormal.BgColor = new Color(0.05f, 0.05f, 0.12f, 0.9f);
            buyStyleNormal.SetBorderWidthAll(1);
            buyStyleNormal.BorderColor = new Color(0.267f, 1f, 0.933f, 0.7f);
            buyStyleNormal.SetCornerRadiusAll(3);
            buyButton.AddThemeStyleboxOverride("normal", buyStyleNormal);

            var buyStyleHover = new StyleBoxFlat();
            buyStyleHover.BgColor = new Color(0.1f, 0.1f, 0.25f, 0.95f);
            buyStyleHover.SetBorderWidthAll(2);
            buyStyleHover.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
            buyStyleHover.SetCornerRadiusAll(3);
            buyButton.AddThemeStyleboxOverride("hover", buyStyleHover);

            var buyStylePressed = new StyleBoxFlat();
            buyStylePressed.BgColor = new Color(0.03f, 0.03f, 0.08f, 1f);
            buyStylePressed.SetBorderWidthAll(2);
            buyStylePressed.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
            buyStylePressed.SetCornerRadiusAll(3);
            buyButton.AddThemeStyleboxOverride("pressed", buyStylePressed);

            buyButton.AddThemeFontSizeOverride("font_size", 15);
            buyButton.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));

            string capturedId = def.Id;
            buyButton.Pressed += () => OnBuyPressed(capturedId);
            ConnectHoverEffects(buyButton, 1.02f);

            row.AddChild(nameLabel);
            row.AddChild(levelLabel);
            row.AddChild(costLabel);
            row.AddChild(buyButton);

            // Encapsule le row dans un PanelContainer stylé
            var panel = new PanelContainer();
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.08f, 0.08f, 0.18f, 0.7f);
            panelStyle.SetBorderWidthAll(1);
            panelStyle.BorderColor = new Color(0.267f, 1f, 0.933f, 0.2f);
            panelStyle.SetCornerRadiusAll(3);
            panel.AddThemeStyleboxOverride("panel", panelStyle);
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
    private void BuildResetButton()
    {
        _resetButton = new Button
        {
            Text              = Loc.T("HUB_RESET"),
            CustomMinimumSize = new Vector2(0, 40),
        };

        // Bordure orange « attention » pour distinguer cette action des achats.
        var normal = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.12f, 0.9f) };
        normal.SetBorderWidthAll(1); normal.BorderColor = new Color(1f, 0.55f, 0.2f, 0.7f); normal.SetCornerRadiusAll(3);
        _resetButton.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.12f, 0.08f, 0.05f, 0.95f) };
        hover.SetBorderWidthAll(2); hover.BorderColor = new Color(1f, 0.55f, 0.2f, 1f); hover.SetCornerRadiusAll(3);
        _resetButton.AddThemeStyleboxOverride("hover", hover);
        _resetButton.AddThemeStyleboxOverride("pressed", hover);

        var focus = new StyleBoxFlat { BgColor = new Color(0.12f, 0.08f, 0.05f, 0.95f) };
        focus.SetBorderWidthAll(3); focus.BorderColor = new Color(1f, 0.55f, 0.2f, 1f); focus.SetCornerRadiusAll(4);
        _resetButton.AddThemeStyleboxOverride("focus", focus);

        _resetButton.AddThemeFontSizeOverride("font_size", 16);
        _resetButton.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.4f));
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

    private void ConnectHoverEffects(Button btn, float targetScale)
    {
        btn.PivotOffset = btn.CustomMinimumSize / 2f;

        var focusStyle = new StyleBoxFlat();
        focusStyle.BgColor = new Color(0.1f, 0.1f, 0.25f, 0.95f);
        focusStyle.SetBorderWidthAll(3);
        focusStyle.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
        focusStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("focus", focusStyle);

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

        // Bouton Reset intercalé entre la dernière amélioration et le bouton Retour.
        _resetButton.FocusNeighborTop    = _resetButton.GetPathTo(_rows[^1].BuyButton);
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
