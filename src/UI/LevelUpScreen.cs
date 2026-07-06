using Godot;

/// <summary>
/// Écran de sélection de carte affiché à chaque montée de niveau.
/// - Pause le jeu (GetTree().Paused = true)
/// - Affiche 3 cartes avec scale-in 0.08 s via Tween
/// - Le clic applique l'upgrade et reprend le jeu
/// </summary>
public partial class LevelUpScreen : CanvasLayer
{
    // Nœuds (assignés dans _Ready via GetNode)
    private ColorRect _background = null!;
    private Button    _card0      = null!;
    private Button    _card1      = null!;
    private Button    _card2      = null!;
    private Label     _labelLevel = null!;

    // Boutons d'action (renouveler / passer), construits en code, débloqués par les améliorations méta.
    private Button    _rerollButton = null!;
    private Button    _skipButton   = null!;

    // Données des 3 cartes actuelles
    private Godot.Collections.Array _currentCards = new();

    // Couleurs de rareté
    private static readonly Color ColorCommon = new(0.67f, 0.67f, 0.67f);
    private static readonly Color ColorRare   = new(0.27f, 0.67f, 1.0f);
    private static readonly Color ColorEpic   = new(0.80f, 0.27f, 1.0f);

    public override void _Ready()
    {
        // Référence aux nœuds définis dans LevelUpScreen.tscn
        _background = GetNode<ColorRect>("Background");
        _card0      = GetNode<Button>("Cards/Card0");
        _card1      = GetNode<Button>("Cards/Card1");
        _card2      = GetNode<Button>("Cards/Card2");
        _labelLevel = GetNode<Label>("LevelLabel");

        _card0.Pressed += () => OnCardChosen(0);
        _card1.Pressed += () => OnCardChosen(1);
        _card2.Pressed += () => OnCardChosen(2);

        ConnectCardHover(_card0);
        ConnectCardHover(_card1);
        ConnectCardHover(_card2);

        BuildActionButtons();

        var hint = GetNodeOrNull<Label>("HintLabel");
        if (hint != null) hint.Text = Loc.T("LEVELUP_HINT");

        // L'écran démarre caché
        Visible = false;

        // Connexion au signal LevelUpSystem
        LevelUpSystem.Instance.ShowLevelUpScreen += OnShowRequested;

        // Doit se processer même en pause
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>Reçoit une demande d'écran de level-up : la soumet à la file modale partagée
    /// (prioritaire sur l'assimilation, §13.2). Un SEUL Paused est géré par ModalQueue.</summary>
    private void OnShowRequested(Godot.Collections.Array cards)
    {
        ModalQueue.Submit(GetTree(), () => Display(cards), highPriority: true);
    }

    private void Display(Godot.Collections.Array cards)
    {
        Visible = true;

        bool isWeaponDrop = LevelUpSystem.Instance?.IsWeaponDrop ?? false;
        if (isWeaponDrop)
            _labelLevel.Text = Loc.T("LEVELUP_MINIBOSS");
        else
            _labelLevel.Text = Loc.T("LEVELUP_TITLE", XpSystem.Instance.CurrentLevel);

        ApplyCards(cards);
        UpdateActionButtons();
    }

    /// <summary>Remplit les 3 cartes avec le jeu fourni + scale-in. Réutilisé par le renouvellement.</summary>
    private void ApplyCards(Godot.Collections.Array cards)
    {
        _currentCards = cards;

        PopulateCard(_card0, 0, cards);
        PopulateCard(_card1, 1, cards);
        PopulateCard(_card2, 2, cards);

        // Scale-in 0.08 s
        Button[] btns = { _card0, _card1, _card2 };
        foreach (var btn in btns) btn.Scale = Vector2.Zero;

        var scaleTween = CreateTween().SetParallel(true);
        foreach (var btn in btns)
        {
            scaleTween.TweenProperty(btn, "scale", Vector2.One, 0.08)
                      .SetTrans(Tween.TransitionType.Back)
                      .SetEase(Tween.EaseType.Out);
        }
        scaleTween.Chain().TweenCallback(Callable.From(() => _card0.GrabFocus()));
    }

    private void PopulateCard(Button btn, int index, Godot.Collections.Array cards)
    {
        if (index >= cards.Count)
        {
            btn.Visible = false;
            return;
        }

        btn.Visible = true;
        var card = (Godot.Collections.Dictionary)cards[index];

        string id           = card["id"].AsString();
        string displayName  = card["display_name"].AsString();
        string description  = card["description"].AsString();
        string rarity       = card["rarity"].AsString();

        // Icône en haut de la carte (zone réservée y=18..82).
        var icon = btn.GetNodeOrNull<TextureRect>("Icon");
        if (icon == null)
        {
            icon = new TextureRect
            {
                Name          = "Icon",
                MouseFilter   = Control.MouseFilterEnum.Ignore,
                ExpandMode    = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = Control.TextureFilterEnum.Nearest,
                Size          = new Vector2(64, 64),
                Position      = new Vector2(btn.CustomMinimumSize.X / 2f - 32f, 18f),
            };
            btn.AddChild(icon);
        }
        icon.Texture = Codex.LoadIcon(id);
        icon.Visible = icon.Texture != null;

        // Texte dans un Label enfant ANCRÉ SOUS l'icône, aligné en haut : indépendant de la
        // longueur, il ne peut donc jamais remonter sur l'icône (contrairement au texte de
        // Button centré verticalement qui débordait vers le haut pour les descriptions longues,
        // ex. fusions frost_veil/vector_beam). ClipText = garde-fou contre le débordement bas.
        var body = btn.GetNodeOrNull<Label>("Body");
        if (body == null)
        {
            body = new Label
            {
                Name                = "Body",
                MouseFilter         = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Top,
                AutowrapMode        = TextServer.AutowrapMode.WordSmart,
                ClipText            = true,
            };
            body.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            body.OffsetLeft = 12; body.OffsetRight = -12;
            body.OffsetTop  = 90; body.OffsetBottom = -14;
            body.AddThemeFontSizeOverride("font_size", 14);
            body.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));
            btn.AddChild(body);
        }
        body.Text = $"[{RarityLabel(rarity)}]\n{displayName}\n\n{description}";
        btn.Text  = "";

        // Couleur du bouton selon rareté
        var stylebox = new StyleBoxFlat();
        stylebox.BgColor    = RarityColor(rarity) * new Color(1, 1, 1, 0.25f);
        stylebox.BorderColor = RarityColor(rarity);
        stylebox.SetBorderWidthAll(2);
        stylebox.SetCornerRadiusAll(8);
        btn.AddThemeStyleboxOverride("normal", stylebox);
    }

    private void OnCardChosen(int index)
    {
        if (index >= _currentCards.Count) return;

        var card     = (Godot.Collections.Dictionary)_currentCards[index];
        string id    = card["id"].AsString();
        string type  = card["card_type"].AsString();

        var inv = InventorySystem.Instance;

        switch (type)
        {
            case "weapon":
                AudioSystem.Instance?.PlaySfx("sfx_card_select");
                inv.AddOrUpgradeWeapon(id);
                break;
            case "passive":
                AudioSystem.Instance?.PlaySfx("sfx_card_select");
                inv.AddOrUpgradePassive(id);
                break;
            case "fusion":
                // SFX marque pour la fusion (evenement rare et important)
                AudioSystem.Instance?.PlaySfx("sfx_fusion_evolve");
                inv.ApplyFusion(id);
                // Flash blanc 0.35 s — "reset de l'oeil" GDD §12
                FusionFlash.Instance?.TriggerFlash();
                break;
            case "xp_bonus":
                AudioSystem.Instance?.PlaySfx("sfx_card_select");
                XpSystem.Instance.AddXp(50);
                break;
        }

        // Réinitialise l'état weapon drop dans LevelUpSystem
        LevelUpSystem.Instance?.ResolveWeaponDrop();

        // Ferme cette présentation ; ModalQueue enchaîne (level-up suivant, assimilation en attente,
        // ou reprise du jeu si la file est vide).
        Visible = false;
        ModalQueue.Done();
    }

    // -------------------------------------------------------------------------
    // Boutons d'action : Renouveler (reroll) / Passer (skip)
    // -------------------------------------------------------------------------

    private void BuildActionButtons()
    {
        var row = new HBoxContainer
        {
            Name           = "Actions",
            Alignment      = BoxContainer.AlignmentMode.Center,
            AnchorLeft     = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft     = -260f, OffsetRight = 260f, OffsetTop = 230f, OffsetBottom = 282f,
            GrowHorizontal = Control.GrowDirection.Both,
        };
        row.AddThemeConstantOverride("separation", 24);

        _rerollButton = MakeActionButton("Renouveler", new Color(0.27f, 0.67f, 1f));
        _skipButton   = MakeActionButton("Passer",     new Color(1f, 0.55f, 0.2f));
        _rerollButton.Name = "RerollButton";
        _skipButton.Name   = "SkipButton";
        _rerollButton.Pressed += OnRerollPressed;
        _skipButton.Pressed   += OnSkipPressed;

        row.AddChild(_rerollButton);
        row.AddChild(_skipButton);
        AddChild(row);
    }

    private static Button MakeActionButton(string text, Color accent)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(200, 48) };

        var normal = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.12f, 0.9f) };
        normal.SetBorderWidthAll(2); normal.BorderColor = accent * new Color(1, 1, 1, 0.8f); normal.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.22f, 0.95f) };
        hover.SetBorderWidthAll(3); hover.BorderColor = accent; hover.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", hover);

        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));
        return btn;
    }

    private void OnRerollPressed()
    {
        var sys = LevelUpSystem.Instance;
        if (sys == null || !sys.TryConsumeReroll()) return;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        ApplyCards(sys.RerollCurrentCards());
        UpdateActionButtons();
    }

    private void OnSkipPressed()
    {
        var sys = LevelUpSystem.Instance;
        if (sys == null || !sys.TryConsumeSkip()) return;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        sys.ResolveWeaponDrop();
        Visible = false;
        ModalQueue.Done();
    }

    /// <summary>Met à jour libellés/visibilité/état des boutons selon les consommables restants.</summary>
    private void UpdateActionButtons()
    {
        var sys  = LevelUpSystem.Instance;
        var meta = MetaProgressionSystem.Instance;
        int rerolls = sys?.RerollsLeft ?? 0;
        int skips   = sys?.SkipsLeft   ?? 0;

        // Visible uniquement si l'amélioration est possédée (sinon l'écran reste épuré).
        _rerollButton.Visible  = (meta?.GetUpgradeLevel("reroll") ?? 0) > 0;
        _rerollButton.Text     = Loc.T("LEVELUP_REROLL", rerolls);
        _rerollButton.Disabled = rerolls <= 0;

        _skipButton.Visible  = (meta?.GetUpgradeLevel("skip") ?? 0) > 0;
        _skipButton.Text     = Loc.T("LEVELUP_SKIP", skips);
        _skipButton.Disabled = skips <= 0;

        SetupActionFocus();
    }

    /// <summary>
    /// Câble la navigation clavier/manette entre les 3 cartes et les boutons Renouveler/Passer.
    /// La navigation spatiale par défaut de Godot ne franchit pas le conteneur d'actions séparé :
    /// on relie donc explicitement (en ne ciblant que les boutons visibles ET activés).
    /// </summary>
    private void SetupActionFocus()
    {
        bool rerollOk = _rerollButton.Visible && !_rerollButton.Disabled;
        bool skipOk   = _skipButton.Visible   && !_skipButton.Disabled;

        Button? left  = rerollOk ? _rerollButton : (skipOk ? _skipButton : null);
        Button? right = skipOk   ? _skipButton   : (rerollOk ? _rerollButton : null);

        // Bas des cartes → bouton d'action (gauche pour Card0/1, droite pour Card2).
        Button[] cards = { _card0, _card1, _card2 };
        foreach (var c in cards)
            c.FocusNeighborBottom = new NodePath();
        if (left != null)
        {
            _card0.FocusNeighborBottom = _card0.GetPathTo(left);
            _card1.FocusNeighborBottom = _card1.GetPathTo(left);
            _card2.FocusNeighborBottom = _card2.GetPathTo(right!);
        }

        // Haut des boutons → cartes ; et entre eux (gauche/droite).
        if (rerollOk)
        {
            _rerollButton.FocusNeighborTop   = _rerollButton.GetPathTo(_card0);
            _rerollButton.FocusNeighborRight = skipOk ? _rerollButton.GetPathTo(_skipButton) : new NodePath();
        }
        if (skipOk)
        {
            _skipButton.FocusNeighborTop  = _skipButton.GetPathTo(_card2);
            _skipButton.FocusNeighborLeft = rerollOk ? _skipButton.GetPathTo(_rerollButton) : new NodePath();
        }
    }

    private new void Hide()
    {
        Visible = false;
    }

    private static string RarityLabel(string rarity) => rarity switch
    {
        "common" => Loc.T("RARITY_COMMON"),
        "rare"   => Loc.T("RARITY_RARE"),
        "epic"   => Loc.T("RARITY_EPIC"),
        _        => rarity,
    };

    private static Color RarityColor(string rarity) => rarity switch
    {
        "common" => ColorCommon,
        "rare"   => ColorRare,
        "epic"   => ColorEpic,
        _        => ColorCommon,
    };

    private void ConnectCardHover(Button btn)
    {
        var focusStyle = new StyleBoxFlat();
        focusStyle.BgColor = new Color(0.15f, 0.1f, 0.3f, 0.4f);
        focusStyle.SetBorderWidthAll(3);
        focusStyle.BorderColor = new Color(0.667f, 0.267f, 1f, 1f);
        focusStyle.SetCornerRadiusAll(8);
        btn.AddThemeStyleboxOverride("focus", focusStyle);

        btn.MouseEntered  += () => OnCardEntered(btn);
        btn.MouseExited   += () => OnCardExited(btn);
        btn.FocusEntered  += () => OnCardEntered(btn);
        btn.FocusExited   += () => OnCardExited(btn);
    }

    private void OnCardEntered(Button btn)
    {
        btn.PivotOffset = btn.Size.X > 0 ? btn.Size / 2f : btn.CustomMinimumSize / 2f;
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", new Vector2(1.03f, 1.03f), 0.1)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);
    }

    private void OnCardExited(Button btn)
    {
        var tween = CreateTween();
        tween.TweenProperty(btn, "scale", Vector2.One, 0.1)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);
    }

    public override void _ExitTree()
    {
        if (LevelUpSystem.Instance != null)
            LevelUpSystem.Instance.ShowLevelUpScreen -= OnShowRequested;
    }
}
