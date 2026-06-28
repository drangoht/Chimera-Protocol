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

    // Données des 3 cartes actuelles
    private Godot.Collections.Array _currentCards = new();

    // File d'attente des level-ups : un gros gain d'XP (XP de départ, boss…) peut
    // déclencher plusieurs montées de niveau d'un coup. On les empile et on présente
    // une carte après l'autre au lieu d'écraser/perdre les écrans intermédiaires.
    private readonly System.Collections.Generic.Queue<Godot.Collections.Array> _queue = new();
    private bool _active = false;

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

        // L'écran démarre caché
        Visible = false;

        // Connexion au signal LevelUpSystem
        LevelUpSystem.Instance.ShowLevelUpScreen += OnShowRequested;

        // Doit se processer même en pause
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>Reçoit une demande d'écran de level-up : empile et affiche si rien en cours.</summary>
    private void OnShowRequested(Godot.Collections.Array cards)
    {
        _queue.Enqueue(cards);
        if (!_active) ShowNext();
    }

    /// <summary>Affiche la prochaine carte en file, ou reprend le jeu si la file est vide.</summary>
    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            _active  = false;
            Visible  = false;
            GetTree().Paused = false;
            return;
        }
        _active = true;
        Display(_queue.Dequeue());
    }

    private void Display(Godot.Collections.Array cards)
    {
        _currentCards = cards;
        Visible = true;
        GetTree().Paused = true;

        bool isWeaponDrop = LevelUpSystem.Instance?.IsWeaponDrop ?? false;
        if (isWeaponDrop)
            _labelLevel.Text = "Butin de mini-boss !";
        else
        {
            int level = XpSystem.Instance.CurrentLevel;
            _labelLevel.Text = $"Niveau {level} !";
        }

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

        // Icône en haut de la carte (le texte centré laisse l'espace haut libre).
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

        // 4 lignes vides en tête → le texte centré reste sous l'icône.
        btn.Text = $"\n\n[{RarityLabel(rarity)}]\n{displayName}\n\n{description}";

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

        // Enchaîne sur le level-up suivant en file, ou reprend le jeu si vide.
        ShowNext();
    }

    private new void Hide()
    {
        Visible = false;
    }

    private static string RarityLabel(string rarity) => rarity switch
    {
        "common" => "Commun",
        "rare"   => "Rare",
        "epic"   => "Épique",
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
