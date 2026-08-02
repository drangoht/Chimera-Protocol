using Godot;

/// <summary>
/// HUD reconstruit en code (2026-06-27), thématisé par la couleur d'accent du biome.
/// Cluster minimal haut-gauche (LV + PV + XP), timer haut-centre, Noyaux haut-droite,
/// notifs d'arme bas-centre. Sémantique PV fixe (cyan/orange/rouge) pour la lisibilité ;
/// tout le reste (bordures, niveau, barre XP, cadre timer, noyaux) prend l'accent du biome.
/// </summary>
public partial class HUD : CanvasLayer
{
	public static HUD? Instance { get; private set; }

	// Couleurs sémantiques PV (lisibilité — indépendantes du biome)
	private static readonly Color HpHigh = new(0.30f, 1f,   0.70f);
	private static readonly Color HpMid  = new(1f,    0.62f, 0.12f);
	private static readonly Color HpLow  = new(0.95f, 0.25f, 0.18f);
	// Réserve de régénération : le blanc cassé de la palette (UiPalette.OffWhite), volontairement NON
	// saturé. Un premier essai en cyan clair était illisible : les accents de biome Sanctuaire
	// (0.30 0.85 0.95) et Givre (0.62 0.88 0.95) sont quasi identiques, et la barre d'XP qui les porte
	// n'est qu'à 12 px sous ce liseré — deux barres de même teinte, l'une au-dessus de l'autre. Le
	// liseré ne suit PAS le thème du biome, il doit donc se distinguer des cinq accents comme des trois
	// teintes de PV.
	private static readonly Color ReserveColor = new(0.85f, 0.85f, 0.95f);
	private static readonly Color Dim    = new(0.62f, 0.66f, 0.78f);

	private Color _accent = new(0.30f, 0.85f, 0.95f);

	// Nœuds construits
	private Panel        _panel       = null!;
	private StyleBoxFlat _panelStyle  = null!;
	private ColorRect    _stripe      = null!;
	private Label        _lvLabel     = null!;
	private Panel        _hpFill      = null!;
	private StyleBoxFlat _hpFillStyle = null!;
	private Label        _hpText      = null!;
	private Panel        _xpFill      = null!;
	private StyleBoxFlat _xpFillStyle = null!;
	private Label        _timerLabel  = null!;
	private ColorRect    _timerLine   = null!;
	private Label        _coresLabel  = null!;
	private TextureRect  _coresIcon   = null!;
	private HBoxContainer _notif      = null!;
	private TextureRect  _notifIcon   = null!;
	private Label        _notifLabel  = null!;
	private Label        _biomeChip   = null!;
	private HBoxContainer _loadout    = null!;
	private int          _lastWeaponCount = -1;
	private HBoxContainer _graftRow   = null!;
	private int          _lastGraftsVersion = -1;
	// Voile de recharge posé sur le slot de la greffe de dash (Servos Érratiques / fusion Charge).
	private ColorRect?   _dashCdVeil;
	private float        _dashSlotSize;
	// Régénération PV/s, à droite de la barre de vie (masqué si nulle).
	private Label        _regenLabel = null!;
	private float        _lastRegenShown = -1f;
	private bool         _regenWasSuppressed;
	// Réserve de régénération (RegenReserve) : liseré sous la barre de vie, masqué sans régénération.
	private Panel        _reserveBar  = null!;
	private Panel        _reserveFill = null!;
	private float        _lastReserveRatio = -1f;
	private bool         _reserveWasSuppressed;
	// Rappel de la touche d'esquive, sous la rangée de greffes (masqué sans greffe de dash).
	private Label        _dashHint = null!;
	private string       _dashHintKey = "";
	// Filets de survie meta (Noyau de Secours, Plaque Adaptative) : une pastille par charge, à droite
	// de la barre de vie. Créées une fois au premier affichage, quand les maxima du Player sont connus.
	private HBoxContainer _safetyRow  = null!;
	private ColorRect[]   _lifePips   = System.Array.Empty<ColorRect>();
	private ColorRect[]   _absorbPips = System.Array.Empty<ColorRect>();
	private int           _lastLivesLeft  = -1;
	private int           _lastAbsorbLeft = -1;
	private bool          _safetyPipsBuilt;

	private const float HpBarW = 222f;
	private const float XpBarW = 296f;

	// Drain PV : gain instantané, perte lissée
	private float _displayHpRatio = 1f;
	private const float HpDrainSpeed = 2.5f;
	// XP : remplissage lissé (lerp vers la cible)
	private float _displayXpRatio = 0f;
	private int   _lastCores = 0;

	private Tween? _notifTween;
	private Tween? _hpPulseTween;
	private bool   _hpPulseActive;

	public override void _Ready()
	{
		Instance = this;
		BuildUi();
		if (XpSystem.Instance != null) XpSystem.Instance.LevelUp += OnLevelUp;
		CallDeferred(MethodName.ApplyBiomeTheme); // après GroundRenderer._Ready
	}

	// ── Construction ───────────────────────────────────────────────────────────
	private void BuildUi()
	{
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		// Overlay scanlines CRT (sous les widgets HUD, sur le jeu)
		var scan = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
		scan.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var scanShader = GD.Load<Shader>("res://assets/shaders/scanlines.gdshader");
		if (scanShader != null) scan.Material = new ShaderMaterial { Shader = scanShader };
		root.AddChild(scan);

		// ── Cluster haut-gauche : panneau + LV + PV + XP + rangée de greffes ──
		// Hauteur 122 : contient la rangée de greffes (y 92→118) avec une marge basse.
		_panel = new Panel { Position = new Vector2(16, 12), Size = new Vector2(326, 122),
							 MouseFilter = Control.MouseFilterEnum.Ignore };
		_panelStyle = new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.09f, 0.82f) };
		_panelStyle.SetCornerRadiusAll(6);
		_panel.AddThemeStyleboxOverride("panel", _panelStyle);
		root.AddChild(_panel);

		// Liseré d'accent (animé : respiration lente) — court sur toute la hauteur du panneau.
		_stripe = new ColorRect { Position = new Vector2(20, 18), Size = new Vector2(4, 110),
								  Color = _accent, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(_stripe);

		_lvLabel = MakeLabel(root, new Vector2(32, 16), "LV 1", 22, _accent);

		_hpText = MakeLabel(root, new Vector2(30, 18), "100 / 100", 14, HpHigh);
		_hpText.Size = new Vector2(296, 18);
		_hpText.HorizontalAlignment = HorizontalAlignment.Right;

		(_, _hpFill, _hpFillStyle) = MakeBar(root, new Vector2(30, 50), new Vector2(HpBarW, 16),
			new Color(0.08f, 0.09f, 0.14f, 0.95f), HpHigh);

		// Régénération continue (Auto-réparation, meta hp_regen, greffes). Sans cet indicateur, l'effet
		// est strictement invisible : il rend quelques PV par seconde sur une barre qui tombe par
		// à-coups de 100. Un testeur a cru qu'il fallait la « déclencher » et n'a plus jamais pris la
		// carte (2026-07-29). Affiché juste à droite de la barre de vie, masqué quand la régen est nulle.
		_regenLabel = MakeLabel(root, new Vector2(30 + HpBarW + 8, 50), "", 12, HpHigh);
		_regenLabel.Size = new Vector2(70, 16);
		_regenLabel.VerticalAlignment = VerticalAlignment.Center;
		_regenLabel.Visible = false;

		// Réserve de régénération : liseré fin sous la barre de vie, rempli de 0 à sa capacité. Sans
		// lui, le joueur ne verrait ni qu'il accumule un tampon en restant intact, ni qu'un coup vient
		// d'être paré — et la carte redeviendrait le « choix mort » que ce chantier corrige (GDD §33.6).
		(_reserveBar, _reserveFill, _) = MakeBar(root, new Vector2(30, 70),
			new Vector2(HpBarW, 4), new Color(0.08f, 0.09f, 0.14f, 0.6f), ReserveColor);
		_reserveBar.Visible = false;

		// Filets de survie meta : une pastille par charge achetée, allumée tant qu'elle est disponible
		// et ÉTEINTE (et non retirée) une fois dépensée — c'est la différence entre « il m'en restait
		// une » et « je n'en ai jamais eu ». Sans cet indicateur le joueur ignorait jusqu'à leur
		// existence : deux Noyaux de Secours et trois Plaques Adaptatives se consommaient en silence,
		// ce qui rendait sa propre mort inexplicable (« il a fallu que je reste immobile pour vraiment
		// mourir », 2026-08-02). Masqué entièrement s'il n'a rien acheté, ou au cran IV qui les coupe.
		_safetyRow = new HBoxContainer
		{
			Position = new Vector2(30 + HpBarW + 8, 66),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		_safetyRow.AddThemeConstantOverride("separation", 3);
		root.AddChild(_safetyRow);

		(_, _xpFill, _xpFillStyle) = MakeBar(root, new Vector2(30, 82), new Vector2(XpBarW, 6),
			new Color(0.08f, 0.09f, 0.14f, 0.95f), _accent);

		// ── Timer haut-centre ──
		_timerLabel = new Label { Text = "15:00", HorizontalAlignment = HorizontalAlignment.Center,
								  MouseFilter = Control.MouseFilterEnum.Ignore };
		_timerLabel.AnchorLeft = 0.5f; _timerLabel.AnchorRight = 0.5f;
		_timerLabel.OffsetLeft = -90; _timerLabel.OffsetRight = 90; _timerLabel.OffsetTop = 12;
		_timerLabel.AddThemeFontSizeOverride("font_size", 30);
		_timerLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.98f));
		_timerLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		_timerLabel.AddThemeConstantOverride("outline_size", 4);
		root.AddChild(_timerLabel);

		_timerLine = new ColorRect { Color = _accent, MouseFilter = Control.MouseFilterEnum.Ignore };
		_timerLine.AnchorLeft = 0.5f; _timerLine.AnchorRight = 0.5f;
		_timerLine.OffsetLeft = -48; _timerLine.OffsetRight = 48; _timerLine.OffsetTop = 50; _timerLine.OffsetBottom = 52;
		root.AddChild(_timerLine);

		// ── Noyaux haut-droite ──
		var coresBox = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		coresBox.AddThemeConstantOverride("separation", 6);
		coresBox.AnchorLeft = 1f; coresBox.AnchorRight = 1f;
		coresBox.OffsetLeft = -120; coresBox.OffsetRight = -18; coresBox.OffsetTop = 16;
		coresBox.Alignment = BoxContainer.AlignmentMode.End;
		root.AddChild(coresBox);
		_coresIcon = new TextureRect
		{
			Texture           = GD.Load<Texture2D>("res://assets/sprites/ui/ui_icon_noyau.png"),
			StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter     = Control.TextureFilterEnum.Nearest,
			CustomMinimumSize = new Vector2(26, 26),
			PivotOffset       = new Vector2(13, 13),
		};
		coresBox.AddChild(_coresIcon);
		_coresLabel = new Label { Text = "0", VerticalAlignment = VerticalAlignment.Center };
		_coresLabel.AddThemeFontSizeOverride("font_size", 24);
		_coresLabel.AddThemeColorOverride("font_color", _accent);
		coresBox.AddChild(_coresLabel);

		// ── Notif d'arme bas-centre ──
		_notif = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_notif.AddThemeConstantOverride("separation", 8);
		_notif.Alignment = BoxContainer.AlignmentMode.Center;
		_notif.AnchorLeft = 0.5f; _notif.AnchorRight = 0.5f; _notif.AnchorTop = 1f; _notif.AnchorBottom = 1f;
		_notif.OffsetLeft = -260; _notif.OffsetRight = 260; _notif.OffsetTop = -64; _notif.OffsetBottom = -34;
		_notif.Modulate = new Color(1, 1, 1, 0);
		root.AddChild(_notif);
		_notifIcon = new TextureRect { CustomMinimumSize = new Vector2(28, 28),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = Control.TextureFilterEnum.Nearest };
		_notif.AddChild(_notifIcon);
		_notifLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		_notifLabel.AddThemeFontSizeOverride("font_size", 18);
		_notif.AddChild(_notifLabel);

		// ── Chip de biome (nom + effet) sous le timer ──
		_biomeChip = new Label { HorizontalAlignment = HorizontalAlignment.Center,
								 MouseFilter = Control.MouseFilterEnum.Ignore };
		_biomeChip.AnchorLeft = 0.5f; _biomeChip.AnchorRight = 0.5f;
		_biomeChip.OffsetLeft = -200; _biomeChip.OffsetRight = 200; _biomeChip.OffsetTop = 56;
		_biomeChip.AddThemeFontSizeOverride("font_size", 13);
		_biomeChip.AddThemeColorOverride("font_color", _accent);
		root.AddChild(_biomeChip);

		// ── Rangée d'emplacements de greffe (Assimilation) sous la barre XP ──
		_graftRow = new HBoxContainer { Position = new Vector2(30, 92),
										MouseFilter = Control.MouseFilterEnum.Ignore };
		_graftRow.AddThemeConstantOverride("separation", 5);
		root.AddChild(_graftRow);

		// Rappel de la touche d'esquive, sous la rangée de greffes. Placé ICI et non sur le slot :
		// le slot est en ClipContents (garde-fou des icônes plein-cadre) et rognait « Shift » à ses
		// deux bords — le badge était présent et illisible, donc inutile. Le voile de recharge disait
		// « quand » le dash est prêt, jamais « comment » le déclencher : un testeur a joué une run
		// entière sans savoir qu'une touche existait (2026-07-29).
		_dashHint = MakeLabel(root, new Vector2(32, 120), "", 11, new Color(0.85f, 0.90f, 1f, 0.85f));
		_dashHint.Size = new Vector2(220, 14);
		_dashHint.Visible = false;

		// ── Bandeau de loadout (armes équipées) sous le panneau agrandi ──
		_loadout = new HBoxContainer { Position = new Vector2(20, 144),
									   MouseFilter = Control.MouseFilterEnum.Ignore };
		_loadout.AddThemeConstantOverride("separation", 6);
		root.AddChild(_loadout);

		BuildBossBar(root);

		StartIdleAnimations();
	}

	// ── Barre de boss (phases du boss de fin, GDD §29.5) ───────────────────────
	// Sans jauge dédiée, les trois phases du boss sont invisibles : le joueur subit une bascule
	// qu'il ne peut ni lire ni anticiper. La barre n'apparaît QUE tant qu'un boss est vivant.
	private Control       _bossBox     = null!;
	private Label         _bossName    = null!;
	private Label         _bossPhase   = null!;
	private Panel         _bossFill    = null!;
	private StyleBoxFlat  _bossFillSty = null!;
	private float         _bossDisplayRatio = 1f;
	private int           _lastBossPhase    = -1;
	private Tween?        _bossFlashTween;

	private const float BossBarW = 520f;
	private const float BossBarH = 14f;

	private void BuildBossBar(Control root)
	{
		_bossBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_bossBox.AnchorLeft = 0.5f; _bossBox.AnchorRight = 0.5f;
		_bossBox.OffsetLeft = -BossBarW / 2f; _bossBox.OffsetRight = BossBarW / 2f;
		// Sous le chip de biome (y 56 + ~18), qu'elle ne doit pas recouvrir.
		_bossBox.OffsetTop = 86; _bossBox.OffsetBottom = 86 + 46;
		root.AddChild(_bossBox);

		_bossName = MakeLabel(_bossBox, new Vector2(0, 0), "", 16, new Color(1f, 0.72f, 0.35f));
		_bossName.Size = new Vector2(BossBarW, 20);
		_bossName.HorizontalAlignment = HorizontalAlignment.Center;
		_bossName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
		_bossName.AddThemeConstantOverride("outline_size", 4);

		(_, _bossFill, _bossFillSty) = MakeBar(_bossBox, new Vector2(0, 24), new Vector2(BossBarW, BossBarH),
			new Color(0.08f, 0.05f, 0.06f, 0.92f), new Color(0.95f, 0.35f, 0.22f));

		// Crans aux seuils de phase : le joueur voit venir la bascule au lieu de la découvrir.
		foreach (float t in BossPhases.Thresholds)
		{
			var notch = new ColorRect
			{
				Position    = new Vector2(BossBarW * t - 1f, 22),
				Size        = new Vector2(2, BossBarH + 4),
				Color       = new Color(0.05f, 0.05f, 0.08f, 0.9f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_bossBox.AddChild(notch);
		}

		_bossPhase = MakeLabel(_bossBox, new Vector2(0, 24 + BossBarH + 2), "", 12, Dim);
		_bossPhase.Size = new Vector2(BossBarW, 16);
		_bossPhase.HorizontalAlignment = HorizontalAlignment.Center;
	}

	/// <summary>
	/// Retire la barre de boss immédiatement. Appelée quand un écran qui passe DERRIÈRE le HUD
	/// prend l'écran : fin de run (`RunStatsTracker.EndRun`) et écrans modaux (`ModalQueue`).
	/// Ne réinitialise PAS la phase mémorisée : au retour au jeu, la barre doit revenir telle
	/// quelle, sans rejouer son flash de bascule.
	/// </summary>
	public void HideBossBar()
	{
		if (_bossBox == null) return;
		_bossBox.Visible = false;
	}

	/// <summary>
	/// Suit le boss vivant via son groupe : le HUD n'a pas à être prévenu de son apparition, et une
	/// disparition (mort, sortie de run) fait simplement retomber la barre.
	/// </summary>
	private void UpdateBossBar(float delta)
	{
		// La run terminée, l'écran de fin prend le dessus : une barre de boss qui reste posée
		// par-dessus son titre se lit comme un bug (constaté au playtest du 2026-07-28).
		bool runOver = RunStatsTracker.Instance?.RunEnded ?? false;

		if (runOver || GetTree().GetFirstNodeInGroup("rusted_core") is not RustedCore boss || !IsInstanceValid(boss))
		{
			if (_bossBox.Visible)
			{
				_bossBox.Visible = false;
				_lastBossPhase   = -1;
			}
			return;
		}

		if (!_bossBox.Visible)
		{
			_bossBox.Visible  = true;
			_bossDisplayRatio = boss.HpRatio;
			_bossName.Text    = boss.DisplayName;
		}

		// Drain lissé, comme la barre de PV du joueur : sur 20 000 PV, une jauge qui suit au pixel
		// près saccade à chaque tick d'arme.
		float target = boss.HpRatio;
		if (target >= _bossDisplayRatio) _bossDisplayRatio = target;
		else _bossDisplayRatio = Mathf.MoveToward(_bossDisplayRatio, target, delta * HpDrainSpeed * 0.6f);
		_bossFill.Size = new Vector2(Mathf.Max(BossBarW * _bossDisplayRatio, 0f), BossBarH);

		if (boss.Phase != _lastBossPhase)
		{
			_lastBossPhase = boss.Phase;
			_bossPhase.Text = Loc.T("BOSS_PHASE", BossPhases.RomanNumeral(boss.Phase));
			FlashBossBar();
		}

		// Pendant la surcharge, la jauge pulse : elle ne descend plus et le joueur doit comprendre
		// que ce n'est pas ses dégâts qui ne passent plus, mais le boss qui bascule.
		if (boss.IsSurcharging)
			_bossFillSty.BgColor = new Color(1f, 0.95f, 0.7f);
		else
			_bossFillSty.BgColor = boss.Phase >= 2 ? new Color(1f, 0.25f, 0.30f)
								 : boss.Phase == 1 ? new Color(1f, 0.45f, 0.20f)
													: new Color(0.95f, 0.60f, 0.22f);
	}

	private void FlashBossBar()
	{
		_bossFlashTween?.Kill();
		_bossFlashTween = CreateTween();
		_bossFlashTween.TweenProperty(_bossBox, "modulate", Colors.White, 0.35)
					   .From(new Color(3f, 3f, 3f, 1f));
	}

	/// <summary>Animations discrètes en boucle : respiration du liseré et du souligné timer.</summary>
	private void StartIdleAnimations()
	{
		var s = CreateTween().SetLoops();
		s.TweenProperty(_stripe, "modulate:a", 0.5f, 1.0).SetEase(Tween.EaseType.InOut);
		s.TweenProperty(_stripe, "modulate:a", 1.0f, 1.0).SetEase(Tween.EaseType.InOut);

		var t = CreateTween().SetLoops();
		t.TweenProperty(_timerLine, "modulate:a", 0.4f, 1.2).SetEase(Tween.EaseType.InOut);
		t.TweenProperty(_timerLine, "modulate:a", 1.0f, 1.2).SetEase(Tween.EaseType.InOut);
	}

	private static Label MakeLabel(Control parent, Vector2 pos, string text, int size, Color color)
	{
		var l = new Label { Text = text, Position = pos, MouseFilter = Control.MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		parent.AddChild(l);
		return l;
	}

	private static (Panel bg, Panel fill, StyleBoxFlat fillStyle) MakeBar(
		Control parent, Vector2 pos, Vector2 size, Color bgCol, Color fillCol)
	{
		int r = (int)(size.Y / 2);
		var bg = new Panel { Position = pos, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		var bgs = new StyleBoxFlat { BgColor = bgCol }; bgs.SetCornerRadiusAll(r);
		bg.AddThemeStyleboxOverride("panel", bgs);
		parent.AddChild(bg);

		var fill = new Panel { Position = pos, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		var fs = new StyleBoxFlat { BgColor = fillCol }; fs.SetCornerRadiusAll(r);
		fill.AddThemeStyleboxOverride("panel", fs);
		parent.AddChild(fill);
		return (bg, fill, fs);
	}

	// ── Thème biome ────────────────────────────────────────────────────────────
	private void ApplyBiomeTheme()
	{
		_accent = GameManager.Instance?.BiomeAccent ?? _accent;
		_stripe.Color = _accent;
		_lvLabel.AddThemeColorOverride("font_color", _accent);
		_xpFillStyle.BgColor = _accent;
		_timerLine.Color = _accent;
		_coresLabel.AddThemeColorOverride("font_color", _accent);

		string bn = GameManager.Instance?.BiomeName ?? "";
		string be = GameManager.Instance?.BiomeEffect ?? "";
		_biomeChip.Text = bn.Length > 0 ? $"{bn}  ·  {be}" : "";
		_biomeChip.AddThemeColorOverride("font_color", new Color(_accent.R, _accent.G, _accent.B, 0.85f));
		RefreshLoadout();
	}

	public override void _Process(double delta)
	{
		UpdateHp((float)delta);
		UpdateXp((float)delta);
		UpdateTimer();
		UpdateCores();
		UpdateBossBar((float)delta);

		// Rafraîchit le loadout si le nombre d'armes a changé (ajout / fusion).
		var inv = InventorySystem.Instance;
		if (inv != null && inv.WeaponLevels.Count != _lastWeaponCount)
		{
			_lastWeaponCount = inv.WeaponLevels.Count;
			RefreshLoadout();
		}

		// Rafraîchit les emplacements de greffe si l'état a changé (équipement / remplacement / +slot).
		var assim = AssimilationSystem.Instance;
		if (assim != null && assim.GraftsVersion != _lastGraftsVersion)
		{
			_lastGraftsVersion = assim.GraftsVersion;
			RefreshGraftSlots();
		}

		UpdateDashCooldownVeil();
	}

	// ── Emplacements de greffe (Assimilation) ──────────────────────────────────────
	private const float GraftSlotSize = 26f;

	private void RefreshGraftSlots()
	{
		if (_graftRow == null) return;
		foreach (var c in _graftRow.GetChildren()) c.QueueFree();
		_dashCdVeil = null; // les slots sont recréés : on relie le voile au nouveau slot de dash

		var assim = AssimilationSystem.Instance;
		if (assim == null) return;

		int slots = assim.SlotCount;
		var equipped = assim.EquippedGrafts;
		for (int i = 0; i < slots; i++)
		{
			bool filled = i < equipped.Count;
			var slot = new Panel { CustomMinimumSize = new Vector2(GraftSlotSize, GraftSlotSize),
								   ClipContents = true, // garde-fou : rien ne peut déborder du slot (icônes plein-cadre)
								   MouseFilter = Control.MouseFilterEnum.Ignore };
			var st = new StyleBoxFlat();
			st.SetCornerRadiusAll(4); st.SetBorderWidthAll(1);

			if (filled)
			{
				var def = assim.GraftById(equipped[i]);
				var tint = def != null ? new Color(def.Tint[0], def.Tint[1], def.Tint[2]) : _accent;
				st.BgColor    = new Color(tint.R, tint.G, tint.B, 0.55f);
				st.BorderColor = tint;

				// Icône dédiée de la greffe si le PNG existe ; sinon on garde le carré teinté (fallback robuste).
				var icon = LoadGraftHudIcon(def);
				if (icon != null)
				{
					// Le carré teinté devient un liseré discret derrière l'icône (repère d'archétype).
					st.BgColor = new Color(tint.R, tint.G, tint.B, 0.25f);
					var tex = new TextureRect
					{
						Texture       = icon,
						TextureFilter = Control.TextureFilterEnum.Nearest,
						StretchMode    = TextureRect.StretchModeEnum.KeepAspectCentered,
						// IgnoreSize sinon le TextureRect prend la taille de la texture (32px) comme
						// taille minimale — elle l'emporte sur le rect d'ancrage (20px), l'icône déborde
						// du slot de 26px (ClipContents) et on n'en voit qu'un coin (BUG icônes tronquées).
						ExpandMode     = TextureRect.ExpandModeEnum.IgnoreSize,
						MouseFilter    = Control.MouseFilterEnum.Ignore,
					};
					// Remplit le slot (20 px) avec une marge dégageant le liseré arrondi (corner radius 4) :
					// évite que les icônes plein-cadre (ruche, œil) affleurent/mordent le bord (BUG-F04).
					tex.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					tex.OffsetLeft = tex.OffsetTop = 3; tex.OffsetRight = tex.OffsetBottom = -3;
					slot.AddChild(tex);
				}

				// Jauge de recharge du dash (Servos Érratiques, fusion Charge ou fusion Frappe Nova) :
				// voile sombre qui couvre le slot après usage puis se retire par le bas au fil de la
				// recharge (§dash). Les 3 partagent le système de dash du Player (DashReadyRatio).
				if (def != null && (def.HasEffect("dash") || def.HasEffect("charge") || def.HasEffect("novaDash")))
				{
					_dashCdVeil = new ColorRect
					{
						Color       = new Color(0.03f, 0.03f, 0.06f, 0.72f),
						Position    = Vector2.Zero,
						Size        = new Vector2(GraftSlotSize, 0f),
						MouseFilter = Control.MouseFilterEnum.Ignore,
						Visible     = false,
					};
					slot.AddChild(_dashCdVeil); // au-dessus de l'icône (ajouté en dernier)
					_dashSlotSize = GraftSlotSize;
				}
			}
			else
			{
				// Emplacement vide : liseré magenta discret pour identifier la rangée « Assimilation »
				// (distincte du loadout d'armes, teinté biome).
				st.BgColor    = new Color(0.16f, 0.08f, 0.16f, 0.65f);
				st.BorderColor = new Color(0.85f, 0.30f, 0.80f, 0.55f);
			}
			slot.AddThemeStyleboxOverride("panel", st);
			_graftRow.AddChild(slot);
		}
	}

	/// <summary>Anime le voile de recharge du dash : hauteur ∝ cooldown restant, se retire par le bas.</summary>
	private void UpdateDashCooldownVeil()
	{
		var dashPlayer = GameManager.Instance?.PlayerInstance;
		UpdateDashHint(dashPlayer != null && dashPlayer.DashEnabled);
		UpdateSafetyPips(dashPlayer);

		if (_dashCdVeil == null || !IsInstanceValid(_dashCdVeil)) return;
		var player = dashPlayer;
		if (player == null || !player.DashEnabled) { _dashCdVeil.Visible = false; return; }

		float ready = player.DashReadyRatio;          // 0 = juste utilisé, 1 = prêt
		if (ready >= 1f) { _dashCdVeil.Visible = false; return; }
		_dashCdVeil.Visible = true;
		_dashCdVeil.Size = new Vector2(_dashSlotSize, _dashSlotSize * (1f - ready)); // couvre par le haut
	}

	/// <summary>
	/// Dessine les filets de survie meta : une pastille par charge achetée, vive tant qu'elle est
	/// disponible, éteinte une fois dépensée.
	///
	/// <para>Les pastilles ne sont construites qu'une fois, au premier appel où le Player existe : leurs
	/// maxima sont figés au début de la run (<c>ExtraLivesMax</c>/<c>AbsorbChargesMax</c>) et le cran IV
	/// « Sans filet » les met à zéro — auquel cas la rangée reste simplement invisible, sans rien annoncer
	/// (la règle du cran est déjà lue avant de lancer, la redire ici serait du bruit).</para>
	/// </summary>
	private void UpdateSafetyPips(Player? player)
	{
		if (_safetyRow == null || !IsInstanceValid(_safetyRow)) return;
		if (player == null) { _safetyRow.Visible = false; return; }

		if (!_safetyPipsBuilt)
		{
			_safetyPipsBuilt = true;
			_lifePips   = BuildPips(player.ExtraLivesMax,    new Color(0.55f, 1f, 0.65f));
			_absorbPips = BuildPips(player.AbsorbChargesMax, new Color(0.45f, 0.72f, 1f));
			_safetyRow.Visible = _lifePips.Length > 0 || _absorbPips.Length > 0;
			if (_safetyRow.Visible)
				_safetyRow.TooltipText = Loc.T("HUD_SAFETY_NETS");
		}
		if (!_safetyRow.Visible) return;

		if (player.ExtraLivesLeft != _lastLivesLeft)
		{
			_lastLivesLeft = player.ExtraLivesLeft;
			PaintPips(_lifePips, _lastLivesLeft);
		}
		if (player.AbsorbChargesLeft != _lastAbsorbLeft)
		{
			_lastAbsorbLeft = player.AbsorbChargesLeft;
			PaintPips(_absorbPips, _lastAbsorbLeft);
		}
	}

	/// <summary>Crée <paramref name="count"/> pastilles de la teinte donnée dans la rangée des filets.</summary>
	private ColorRect[] BuildPips(int count, Color tint)
	{
		var pips = new ColorRect[Mathf.Max(0, count)];
		for (int i = 0; i < pips.Length; i++)
		{
			// Les Noyaux de Secours sont plus hauts que les Plaques : une charge qui sauve d'une mort ne
			// doit pas se lire comme une charge qui absorbe un coup, même du coin de l'œil.
			bool isLife = tint.G > tint.B;
			pips[i] = new ColorRect
			{
				CustomMinimumSize = new Vector2(6, isLife ? 12 : 8),
				Color = tint,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_safetyRow.AddChild(pips[i]);
		}
		return pips;
	}

	/// <summary>Allume les <paramref name="left"/> premières pastilles, éteint les suivantes.</summary>
	private static void PaintPips(ColorRect[] pips, int left)
	{
		for (int i = 0; i < pips.Length; i++)
		{
			if (!IsInstanceValid(pips[i])) continue;
			var c = pips[i].Color;
			// Dépensée : on la garde à l'écran, très assombrie. Elle disparaîtrait sinon, et le joueur
			// ne saurait pas qu'il vient d'en perdre une — c'est le défaut même qu'on corrige.
			pips[i].Modulate = i < left ? Colors.White : new Color(1, 1, 1, 0.18f);
			pips[i].Color = c;
		}
	}

	/// <summary>Charge la texture d'icône d'une greffe si la ressource existe (repli null → carré teinté).
	/// Utilise <see cref="ResourceLoader.Exists"/> (et non FileAccess) : en build exporté le PNG source
	/// n'est pas dans le .pck (seule la texture importée .ctex l'est) — FileExists renverrait toujours
	/// faux et masquerait toutes les icônes en jeu (BUG icônes greffes absentes du HUD).</summary>
	private static Texture2D? LoadGraftHudIcon(GraftTable.GraftDef? def)
	{
		if (def == null || string.IsNullOrEmpty(def.HudIcon)) return null;
		return ResourceLoader.Exists(def.HudIcon) ? GD.Load<Texture2D>(def.HudIcon) : null;
	}

	/// <summary>
	/// Affiche « ⇧ Shift — esquive » sous la rangée de greffes tant que le joueur dispose du dash.
	/// Le libellé est relu de l'InputMap (touche remappable), et seulement quand il change.
	/// </summary>
	private void UpdateDashHint(bool hasDash)
	{
		if (_dashHint == null || !IsInstanceValid(_dashHint)) return;
		if (!hasDash) { _dashHint.Visible = false; _dashHintKey = ""; return; }

		string key = InputRemap.DashKeyLabel();
		if (key != _dashHintKey)
		{
			_dashHintKey = key;
			_dashHint.Text = string.Format(Loc.T("HUD_DASH_HINT"), key);
		}
		_dashHint.Visible = true;
	}

	/// <summary>
	/// Affiche la régénération continue à droite de la barre de vie (« ♥ +2,4/s »), masquée si nulle.
	/// Ne se met à jour que sur changement : ce label ne bouge qu'à la prise d'une carte.
	///
	/// <para>Sous le feu, la régénération est coupée (<see cref="RegenReserve.SuppressionSeconds"/>) et
	/// le label bascule sur un décompte grisé. Sans cet affichage la règle serait invisible, donc
	/// inexistante pour le joueur — même famille que le dash qui n'annonçait pas sa touche : il croirait
	/// simplement que sa carte ne marche plus.</para>
	/// </summary>
	private void UpdateRegenLabel(PlayerStats stats)
	{
		if (_regenLabel == null || !IsInstanceValid(_regenLabel)) return;

		float regenPerSecond = stats.HpRegenPerSecond;
		bool suppressed = RegenReserve.IsSuppressed(stats.RegenSuppressLeft);
		// Le décompte bouge à chaque frame : le cache ne vaut que pour l'état stable.
		if (!suppressed && !_regenWasSuppressed && Mathf.IsEqualApprox(regenPerSecond, _lastRegenShown)) return;
		_lastRegenShown = regenPerSecond;
		_regenWasSuppressed = suppressed;

		if (regenPerSecond <= 0.01f) { _regenLabel.Visible = false; return; }
		_regenLabel.Visible = true;

		if (suppressed)
		{
			// Le débit disparaît de l'affichage — c'est bien lui qui est suspendu — et cède la place au
			// temps restant avant reprise : l'information actionnable est « dans combien de temps »,
			// pas « combien ». Glyphes volontairement limités à ceux que porte Share Tech Mono.
			_regenLabel.Text = "♥ " + stats.RegenSuppressLeft.ToString("0.0") + "s";
			_regenLabel.AddThemeColorOverride("font_color", UiPalette.Dim);
		}
		else
		{
			_regenLabel.Text = "♥ +" + regenPerSecond.ToString("0.0") + "/s";
			_regenLabel.AddThemeColorOverride("font_color", HpHigh);
		}
	}

	/// <summary>
	/// Liseré de réserve sous la barre de vie : rempli de 0 à la capacité (<see cref="RegenReserve"/>).
	/// Masqué tant que le joueur n'a aucune régénération — sans source, la réserve n'existe pas et un
	/// liseré vide en permanence ne serait que du bruit visuel.
	/// </summary>
	private void UpdateReserveBar(PlayerStats stats)
	{
		if (_reserveBar == null || !IsInstanceValid(_reserveBar)) return;

		float capacity = RegenReserve.Capacity(stats.HpRegenPerSecond, stats.MaxHp);
		bool show = capacity > 0.01f;
		if (_reserveBar.Visible != show)
		{
			_reserveBar.Visible = show;
			_reserveFill.Visible = show;
		}
		if (!show) return;

		// Grisé tant que la régénération est coupée : le liseré cesse alors de se remplir, et un
		// tampon figé sans explication se lirait comme un bug.
		bool suppressed = RegenReserve.IsSuppressed(stats.RegenSuppressLeft);
		if (suppressed != _reserveWasSuppressed)
		{
			_reserveWasSuppressed = suppressed;
			_reserveFill.Modulate = suppressed ? UiPalette.Dim : Colors.White;
		}

		float ratio = Mathf.Clamp(stats.RegenReserveCharge / capacity, 0f, 1f);
		if (Mathf.IsEqualApprox(ratio, _lastReserveRatio)) return;
		_lastReserveRatio = ratio;
		_reserveFill.Size = new Vector2(Mathf.Max(HpBarW * ratio, 0f), 4f);
	}

	// ── PV ─────────────────────────────────────────────────────────────────────
	private void UpdateHp(float delta)
	{
		var player = GameManager.Instance?.PlayerInstance;
		if (player == null) return;
		float cur = player.Stats.CurrentHp, max = player.Stats.MaxHp;
		float target = max > 0f ? Mathf.Clamp(cur / max, 0f, 1f) : 0f;

		UpdateRegenLabel(player.Stats);
		UpdateReserveBar(player.Stats);

		if (target >= _displayHpRatio) _displayHpRatio = target;
		else _displayHpRatio = Mathf.MoveToward(_displayHpRatio, target, delta * HpDrainSpeed);

		var col = HpColor(_displayHpRatio);
		_hpFill.Size = new Vector2(Mathf.Max(HpBarW * _displayHpRatio, 0f), 16f);
		_hpFillStyle.BgColor = col;
		_hpText.Text = $"{(int)cur} / {(int)max}";
		_hpText.AddThemeColorOverride("font_color", HpColor(target));

		bool pulse = target > 0f && target < 0.25f;
		if (pulse && !_hpPulseActive)
		{
			_hpPulseActive = true; _hpPulseTween?.Kill();
			_hpPulseTween = CreateTween().SetLoops();
			_hpPulseTween.TweenProperty(_hpFill, "modulate:a", 0.4f, 0.3).SetEase(Tween.EaseType.InOut);
			_hpPulseTween.TweenProperty(_hpFill, "modulate:a", 1f,   0.3).SetEase(Tween.EaseType.InOut);
		}
		else if (!pulse && _hpPulseActive)
		{
			_hpPulseActive = false; _hpPulseTween?.Kill(); _hpFill.Modulate = Colors.White;
		}
	}

	private static Color HpColor(float r) => r > 0.5f ? HpHigh : r > 0.25f ? HpMid : HpLow;

	// ── XP / niveau ──────────────────────────────────────────────────────────────
	private void UpdateXp(float delta)
	{
		var xp = XpSystem.Instance;
		if (xp == null) return;
		_lvLabel.Text = $"LV {xp.CurrentLevel}";
		float ratio = xp.XpToNextLevel > 0 ? Mathf.Clamp((float)xp.CurrentXp / xp.XpToNextLevel, 0f, 1f) : 1f;

		// Remplissage lissé : croît en douceur, snap à la baisse (reset de niveau).
		if (ratio >= _displayXpRatio) _displayXpRatio = Mathf.MoveToward(_displayXpRatio, ratio, delta * 2.5f);
		else _displayXpRatio = ratio;

		_xpFill.Size = new Vector2(Mathf.Max(XpBarW * _displayXpRatio, 0f), 6f);
	}

	private void OnLevelUp(int newLevel)
	{
		_xpFill.Modulate = new Color(3f, 3f, 3f, 1f);
		CreateTween().TweenProperty(_xpFill, "modulate", Colors.White, 0.5).SetEase(Tween.EaseType.Out);
		_lvLabel.Modulate = new Color(2.5f, 2.5f, 2.5f, 1f);
		CreateTween().TweenProperty(_lvLabel, "modulate", Colors.White, 0.5).SetEase(Tween.EaseType.Out);
	}

	// ── Timer ────────────────────────────────────────────────────────────────────
	private void UpdateTimer()
	{
		var t = RunStatsTracker.Instance;
		if (t == null) return;
		int rem = Mathf.Max(0, t.RunDurationSeconds - (int)t.ElapsedSeconds);
		_timerLabel.Text = $"{rem / 60:D2}:{rem % 60:D2}";
		_timerLabel.AddThemeColorOverride("font_color",
			rem > 120 ? new Color(0.9f, 0.92f, 0.98f) : rem > 60 ? HpMid : HpLow);
	}

	private void UpdateCores()
	{
		var t = RunStatsTracker.Instance;
		if (t == null || t.CoresCollected == _lastCores) return;

		if (t.CoresCollected > _lastCores)   // pop discret à chaque ramassage
		{
			_coresIcon.Scale = Vector2.One * 1.35f;
			CreateTween().TweenProperty(_coresIcon, "scale", Vector2.One, 0.32)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
		_lastCores = t.CoresCollected;
		_coresLabel.Text = $"{t.CoresCollected}";
	}

	// ── Loadout (armes équipées) ──────────────────────────────────────────────────
	private void RefreshLoadout()
	{
		if (_loadout == null) return;
		foreach (var c in _loadout.GetChildren()) c.QueueFree();
		var inv = InventorySystem.Instance;
		if (inv == null) return;
		foreach (var kv in inv.WeaponLevels)
			_loadout.AddChild(MakeChip(kv.Key, kv.Value));
	}

	private Control MakeChip(string id, int lvl)
	{
		var panel = new Panel { CustomMinimumSize = new Vector2(38, 38), MouseFilter = Control.MouseFilterEnum.Ignore };
		var st = new StyleBoxFlat { BgColor = new Color(0.06f, 0.07f, 0.12f, 0.9f) };
		st.SetCornerRadiusAll(5); st.SetBorderWidthAll(1);
		st.BorderColor = new Color(_accent.R, _accent.G, _accent.B, 0.7f);
		panel.AddThemeStyleboxOverride("panel", st);

		var icon = new TextureRect
		{
			Texture       = Codex.LoadIcon(id),
			StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = Control.TextureFilterEnum.Nearest,
			MouseFilter   = Control.MouseFilterEnum.Ignore,
		};
		icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		icon.OffsetLeft = 3; icon.OffsetTop = 1; icon.OffsetRight = -3; icon.OffsetBottom = -7;
		panel.AddChild(icon);

		var lbl = new Label { Text = lvl.ToString(), MouseFilter = Control.MouseFilterEnum.Ignore };
		lbl.AddThemeFontSizeOverride("font_size", 11);
		lbl.AddThemeColorOverride("font_color", _accent);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		lbl.AddThemeConstantOverride("outline_size", 3);
		lbl.AnchorTop = 1f; lbl.AnchorBottom = 1f; lbl.OffsetLeft = 4; lbl.OffsetTop = -16;
		panel.AddChild(lbl);
		return panel;
	}

	// ── Pause ─────────────────────────────────────────────────────────────────────
	private static PackedScene? _pauseScene;
	public override void _UnhandledInput(InputEvent @event)
	{
		// Ouvre la pause via l'action « pause » (Échap clavier ou Start manette).
		if (!@event.IsActionPressed("pause")) return;
		if (GetTree().Paused) return;
		GetViewport().SetInputAsHandled();
		_pauseScene ??= GD.Load<PackedScene>("res://scenes/ui/PauseScreen.tscn");
		if (_pauseScene != null) GetTree().Root.AddChild(_pauseScene.Instantiate<PauseScreen>());
	}

	// ── Notifications armes (API publique inchangée) ──────────────────────────────
	public void ShowWeaponEquipped(string id, string name)
		=> ShowNotif(id, name, new Color(1f, 0.8f, 0.267f), 1.6f, 0.5f);
	public void ShowWeaponUpgraded(string id, string name, int level)
		=> ShowNotif(id, $"{name}  LV {level}", _accent, 1.3f, 0.4f);
	public void ShowPassiveAcquired(string id, string name)
		=> ShowNotif(id, name, new Color(0.667f, 0.267f, 1f), 1.6f, 0.5f);

	private void ShowNotif(string id, string text, Color color, float hold, float fade)
	{
		var icon = Codex.LoadIcon(id);
		_notifIcon.Texture = icon;
		_notifIcon.Visible = icon != null;
		_notifLabel.Text = text;
		_notifLabel.AddThemeColorOverride("font_color", color);

		_notifTween?.Kill();
		_notif.Modulate = Colors.White;
		_notifTween = CreateTween();
		_notifTween.TweenInterval(hold);
		_notifTween.TweenProperty(_notif, "modulate:a", 0f, fade);

		RefreshLoadout(); // garde les niveaux du loadout à jour
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
		if (XpSystem.Instance != null) XpSystem.Instance.LevelUp -= OnLevelUp;
	}
}
