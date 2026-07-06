using Godot;

/// <summary>
/// Écran modal « ASSIMILATION » : présente une greffe déterministe quand une jauge se remplit
/// (signal AssimilationSystem.GaugeFilled). Calqué sur LevelUpScreen mais avec une identité
/// magenta/rouille. Deux modes (§13.3) : slot libre → ASSIMILER/REJETER ; slots pleins → choisir
/// la greffe à remplacer ou CONSERVER. Partage la file modale (ModalQueue) avec le LevelUpScreen :
/// jamais affichés simultanément, un seul Paused rendu (§13.2).
///
/// Toute l'UI est PRÉ-CONSTRUITE dans _Ready (aucun AddChild à la présentation) car GaugeFilled peut
/// être émis depuis un callback physique (EnemyBase.Die) — la présentation ne fait que configurer et
/// afficher des nœuds existants.
/// </summary>
public partial class AssimilationScreen : CanvasLayer
{
    private static readonly Color Magenta = new(0.85f, 0.30f, 0.80f);
    private static readonly Color Rust    = new(0.85f, 0.45f, 0.30f);

    private Control      _root       = null!;
    private ColorRect    _background  = null!;
    private Label        _title       = null!;
    private TextureRect  _icon        = null!;
    private Label        _nameLabel   = null!;
    private Label        _tagLabel    = null!;
    private Label        _descLabel   = null!;

    private Button       _assimilateBtn = null!;
    private Button       _rejectBtn     = null!;
    private Button       _keepBtn       = null!;
    private const int    MaxSlots       = 5;
    private readonly Button[] _replaceBtns = new Button[MaxSlots];

    private string _gauge = "";
    private bool   _isFusion;

    public override void _Ready()
    {
        Layer = 60; // au-dessus du jeu, sous les overlays PostFX (90)
        ProcessMode = ProcessModeEnum.Always;
        BuildUi();
        Visible = false;

        if (AssimilationSystem.Instance != null)
            AssimilationSystem.Instance.GaugeFilled += OnGaugeFilled;
    }

    public override void _ExitTree()
    {
        if (AssimilationSystem.Instance != null)
            AssimilationSystem.Instance.GaugeFilled -= OnGaugeFilled;
    }

    // -------------------------------------------------------------------------
    // Construction de l'UI (une seule fois)
    // -------------------------------------------------------------------------

    private void BuildUi()
    {
        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _background = new ColorRect { Color = new Color(0.08f, 0.03f, 0.09f, 0.88f) };
        _background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_background);

        _title = MakeLabel("ASSIMILATION", 34, Magenta);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AnchorLeft = 0.5f; _title.AnchorRight = 0.5f;
        _title.OffsetLeft = -300; _title.OffsetRight = 300; _title.OffsetTop = 60;
        _root.AddChild(_title);

        // Carte de la nouvelle greffe (centrée).
        var card = new Panel
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -220, OffsetRight = 220, OffsetTop = -206, OffsetBottom = 44,
            GrowHorizontal = Control.GrowDirection.Both,
        };
        var cardStyle = new StyleBoxFlat { BgColor = new Color(0.10f, 0.05f, 0.12f, 0.95f) };
        cardStyle.SetBorderWidthAll(3); cardStyle.BorderColor = Magenta; cardStyle.SetCornerRadiusAll(10);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        _root.AddChild(card);

        _icon = new TextureRect
        {
            StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = Control.TextureFilterEnum.Nearest,
            Position      = new Vector2(180, 20),
            Size          = new Vector2(80, 80),
        };
        card.AddChild(_icon);

        _nameLabel = MakeLabel("", 24, new Color(0.95f, 0.9f, 1f));
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.Position = new Vector2(20, 108); _nameLabel.Size = new Vector2(400, 30);
        card.AddChild(_nameLabel);

        _tagLabel = MakeLabel("", 15, Rust);
        _tagLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _tagLabel.Position = new Vector2(20, 140); _tagLabel.Size = new Vector2(400, 22);
        card.AddChild(_tagLabel);

        _descLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Top,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart,
            Position            = new Vector2(24, 168),
            Size                = new Vector2(392, 76),
        };
        _descLabel.AddThemeFontSizeOverride("font_size", 14);
        _descLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.92f));
        card.AddChild(_descLabel);

        // Boutons d'action slot-libre (ASSIMILER / REJETER).
        _assimilateBtn = MakeButton(Magenta);
        _assimilateBtn.AnchorLeft = 0.5f; _assimilateBtn.AnchorRight = 0.5f; _assimilateBtn.AnchorTop = 0.5f; _assimilateBtn.AnchorBottom = 0.5f;
        _assimilateBtn.OffsetLeft = -230; _assimilateBtn.OffsetRight = -20; _assimilateBtn.OffsetTop = 68; _assimilateBtn.OffsetBottom = 120;
        _assimilateBtn.Pressed += OnAssimilate;
        _root.AddChild(_assimilateBtn);

        _rejectBtn = MakeButton(new Color(0.6f, 0.6f, 0.68f));
        _rejectBtn.AnchorLeft = 0.5f; _rejectBtn.AnchorRight = 0.5f; _rejectBtn.AnchorTop = 0.5f; _rejectBtn.AnchorBottom = 0.5f;
        _rejectBtn.OffsetLeft = 20; _rejectBtn.OffsetRight = 230; _rejectBtn.OffsetTop = 68; _rejectBtn.OffsetBottom = 120;
        _rejectBtn.Pressed += OnReject;
        _root.AddChild(_rejectBtn);

        // Boutons de remplacement (mode slots pleins) : une colonne + CONSERVER.
        var replaceBox = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -230, OffsetRight = 230, OffsetTop = 40, OffsetBottom = 300,
            GrowHorizontal = Control.GrowDirection.Both, Name = "ReplaceBox",
        };
        replaceBox.AddThemeConstantOverride("separation", 8);
        _root.AddChild(replaceBox);
        for (int i = 0; i < MaxSlots; i++)
        {
            _replaceBtns[i] = MakeButton(Rust);
            _replaceBtns[i].CustomMinimumSize = new Vector2(460, 44);
            int idx = i;
            _replaceBtns[i].Pressed += () => OnReplace(idx);
            replaceBox.AddChild(_replaceBtns[i]);
        }
        _keepBtn = MakeButton(new Color(0.6f, 0.6f, 0.68f));
        _keepBtn.CustomMinimumSize = new Vector2(460, 44);
        _keepBtn.Pressed += OnKeep;
        replaceBox.AddChild(_keepBtn);
        _replaceBox = replaceBox;

        var hint = MakeLabel("", 14, new Color(0.7f, 0.7f, 0.8f));
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AnchorLeft = 0.5f; hint.AnchorRight = 0.5f; hint.AnchorTop = 1f; hint.AnchorBottom = 1f;
        hint.OffsetLeft = -200; hint.OffsetRight = 200; hint.OffsetTop = -40;
        hint.Text = TFallback("LEVELUP_HINT", "Clic pour choisir");
        _root.AddChild(hint);
    }

    private VBoxContainer _replaceBox = null!;

    // -------------------------------------------------------------------------
    // Présentation (via la file modale partagée)
    // -------------------------------------------------------------------------

    private void OnGaugeFilled(string gaugeKey)
        => ModalQueue.Submit(GetTree(), () => Present(gaugeKey), highPriority: false);

    private void Present(string gaugeKey)
    {
        var sys = AssimilationSystem.Instance;
        if (sys == null) { Close(); return; }

        // Une jauge de fusion (§15) présente une carte de fusion (toujours 2 boutons) ; sinon greffe.
        var fusion = sys.FusionForGauge(gaugeKey);
        GraftTable.GraftDef? def = fusion ?? sys.GraftForGauge(gaugeKey);
        if (def == null) { Close(); return; }
        _isFusion = fusion != null;

        _gauge = gaugeKey;
        Visible = true;

        string idKey = def.Id.ToUpperInvariant();
        _nameLabel.Text = TFallback($"GRAFT_{idKey}_NAME", def.Name);
        _descLabel.Text = TFallback($"GRAFT_{idKey}_DESC", def.Description);
        _tagLabel.Text  = _isFusion
            ? $"{RarityLabel(def.Rarity)}  ·  {TFallback("ASSIM_FUSION_TAG", "FUSION")}"
            : $"{RarityLabel(def.Rarity)}  ·  {def.Gauge}";
        _tagLabel.AddThemeColorOverride("font_color",
            new Color(def.Tint[0], def.Tint[1], def.Tint[2]) * new Color(1, 1, 1, 1));

        var tex = LoadGraftIcon(def);
        _icon.Texture = tex;
        _icon.Visible = tex != null;

        Button? firstFocus;

        // Cas fusion : 2 boutons, jamais de remplacement (la fusion libère un slot, §15.1).
        if (_isFusion)
        {
            _assimilateBtn.Visible = true;
            _rejectBtn.Visible     = true;
            _replaceBox.Visible    = false;
            _assimilateBtn.Text = TFallback("ASSIM_FUSE", "ASSIMILER LA FUSION");
            _rejectBtn.Text     = TFallback("ASSIM_REFUSE", "REFUSER");
            _title.Text         = TFallback("ASSIM_FUSION_TITLE", "FUSION");
            firstFocus = _assimilateBtn;
            var fbtn = firstFocus;
            Callable.From(() => { if (Visible && fbtn != null && IsInstanceValid(fbtn)) fbtn.GrabFocus(); }).CallDeferred();
            return;
        }

        bool free = sys.HasFreeSlot;
        _assimilateBtn.Visible = free;
        _rejectBtn.Visible     = free;
        _replaceBox.Visible    = !free;

        if (free)
        {
            _assimilateBtn.Text = TFallback("ASSIM_ASSIMILATE", "ASSIMILER");
            _rejectBtn.Text     = TFallback("ASSIM_REJECT", "REJETER");
            firstFocus = _assimilateBtn;
            _title.Text = TFallback("ASSIM_TITLE", "ASSIMILATION");
        }
        else
        {
            _title.Text = TFallback("ASSIM_REPLACE_TITLE", "REMPLACER UNE GREFFE ?");
            var equipped = sys.EquippedGrafts;
            for (int i = 0; i < MaxSlots; i++)
            {
                bool has = i < equipped.Count;
                _replaceBtns[i].Visible = has;
                if (has)
                {
                    var od = sys.GraftById(equipped[i]);
                    string oName = od != null ? TFallback($"GRAFT_{od.Id.ToUpperInvariant()}_NAME", od.Name) : equipped[i];
                    _replaceBtns[i].Text = TFallback("ASSIM_REPLACE_WITH", "Remplacer : {0}", oName);
                }
            }
            _keepBtn.Text = TFallback("ASSIM_KEEP", "CONSERVER");
            firstFocus = _replaceBtns[0];
        }

        // GrabFocus différé (GaugeFilled peut venir d'un callback physique).
        var btn = firstFocus;
        Callable.From(() => { if (Visible && btn != null && IsInstanceValid(btn)) btn.GrabFocus(); }).CallDeferred();
    }

    private void OnAssimilate()
    {
        var sys = AssimilationSystem.Instance;
        if (_isFusion) sys?.AssimilateFusion(_gauge);
        else           sys?.Assimilate(_gauge);
        Close();
    }

    private void OnReject()
    {
        var sys = AssimilationSystem.Instance;
        if (_isFusion) sys?.RejectFusion(_gauge);
        else           sys?.Reject(_gauge);
        Close();
    }

    private void OnKeep() { AssimilationSystem.Instance?.Keep(_gauge); Close(); }

    private void OnReplace(int index)
    {
        var sys = AssimilationSystem.Instance;
        if (sys != null && index < sys.EquippedGrafts.Count)
            sys.Replace(_gauge, sys.EquippedGrafts[index]);
        Close();
    }

    private void Close()
    {
        Visible = false;
        ModalQueue.Done();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Texture2D? LoadGraftIcon(GraftTable.GraftDef def)
    {
        if (string.IsNullOrEmpty(def.HudIcon)) return null;
        return Godot.FileAccess.FileExists(def.HudIcon) ? GD.Load<Texture2D>(def.HudIcon) : null;
    }

    /// <summary>Loc.T avec repli sur un texte FR par défaut si la clé n'est pas encore traduite.</summary>
    private static string TFallback(string key, string fallback)
    {
        string t = Loc.T(key);
        return t == key ? fallback : t;
    }

    private static string TFallback(string key, string fallback, params object[] args)
    {
        string t = Loc.T(key);
        return string.Format(t == key ? fallback : t, args);
    }

    private static string RarityLabel(string rarity) => rarity switch
    {
        "common" => Loc.T("RARITY_COMMON"),
        "rare"   => Loc.T("RARITY_RARE"),
        "epic"   => Loc.T("RARITY_EPIC"),
        _        => rarity,
    };

    private static Label MakeLabel(string text, int size, Color color)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Button MakeButton(Color accent)
    {
        var btn = new Button { CustomMinimumSize = new Vector2(200, 48) };
        var normal = new StyleBoxFlat { BgColor = new Color(0.06f, 0.04f, 0.10f, 0.92f) };
        normal.SetBorderWidthAll(2); normal.BorderColor = accent * new Color(1, 1, 1, 0.85f); normal.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.12f, 0.07f, 0.18f, 0.96f) };
        hover.SetBorderWidthAll(3); hover.BorderColor = accent; hover.SetCornerRadiusAll(6);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", hover);

        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.96f));
        return btn;
    }
}
