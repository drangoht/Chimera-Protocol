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

		// ── Cluster haut-gauche : panneau + LV + PV + XP ──
		_panel = new Panel { Position = new Vector2(16, 12), Size = new Vector2(326, 96),
							 MouseFilter = Control.MouseFilterEnum.Ignore };
		_panelStyle = new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.09f, 0.82f) };
		_panelStyle.SetCornerRadiusAll(6);
		_panel.AddThemeStyleboxOverride("panel", _panelStyle);
		root.AddChild(_panel);

		// Liseré d'accent (animé : respiration lente)
		_stripe = new ColorRect { Position = new Vector2(20, 18), Size = new Vector2(4, 84),
								  Color = _accent, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(_stripe);

		_lvLabel = MakeLabel(root, new Vector2(32, 16), "LV 1", 22, _accent);

		_hpText = MakeLabel(root, new Vector2(30, 18), "100 / 100", 14, HpHigh);
		_hpText.Size = new Vector2(296, 18);
		_hpText.HorizontalAlignment = HorizontalAlignment.Right;

		(_, _hpFill, _hpFillStyle) = MakeBar(root, new Vector2(30, 50), new Vector2(HpBarW, 16),
			new Color(0.08f, 0.09f, 0.14f, 0.95f), HpHigh);

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

		// ── Bandeau de loadout (armes équipées) sous le panneau ──
		_loadout = new HBoxContainer { Position = new Vector2(20, 120),
									   MouseFilter = Control.MouseFilterEnum.Ignore };
		_loadout.AddThemeConstantOverride("separation", 6);
		root.AddChild(_loadout);

		StartIdleAnimations();
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
	}

	// ── Emplacements de greffe (Assimilation) ──────────────────────────────────────
	private void RefreshGraftSlots()
	{
		if (_graftRow == null) return;
		foreach (var c in _graftRow.GetChildren()) c.QueueFree();

		var assim = AssimilationSystem.Instance;
		if (assim == null) return;

		int slots = assim.SlotCount;
		var equipped = assim.EquippedGrafts;
		for (int i = 0; i < slots; i++)
		{
			bool filled = i < equipped.Count;
			var slot = new Panel { CustomMinimumSize = new Vector2(26, 26),
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
						StretchMode   = TextureRect.StretchModeEnum.KeepAspectCentered,
						MouseFilter   = Control.MouseFilterEnum.Ignore,
					};
					// Remplit le slot (20 px) avec une marge dégageant le liseré arrondi (corner radius 4) :
					// évite que les icônes plein-cadre (ruche, œil) affleurent/mordent le bord (BUG-F04).
					tex.SetAnchorsPreset(Control.LayoutPreset.FullRect);
					tex.OffsetLeft = tex.OffsetTop = 3; tex.OffsetRight = tex.OffsetBottom = -3;
					slot.AddChild(tex);
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

	/// <summary>Charge la texture d'icône d'une greffe si le PNG existe (repli null → carré teinté).</summary>
	private static Texture2D? LoadGraftHudIcon(GraftTable.GraftDef? def)
	{
		if (def == null || string.IsNullOrEmpty(def.HudIcon)) return null;
		return Godot.FileAccess.FileExists(def.HudIcon) ? GD.Load<Texture2D>(def.HudIcon) : null;
	}

	// ── PV ─────────────────────────────────────────────────────────────────────
	private void UpdateHp(float delta)
	{
		var player = GameManager.Instance?.PlayerInstance;
		if (player == null) return;
		float cur = player.Stats.CurrentHp, max = player.Stats.MaxHp;
		float target = max > 0f ? Mathf.Clamp(cur / max, 0f, 1f) : 0f;

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
