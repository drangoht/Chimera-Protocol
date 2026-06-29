using Godot;

/// <summary>
/// Écran de pause — ouvert via Échap pendant le run.
/// Affiche les statistiques complètes de la run : mission, joueur, armes, passifs, fusions.
/// ProcessMode = Always — reste actif quand GetTree().Paused = true.
/// </summary>
public partial class PauseScreen : CanvasLayer
{
    // ── Palette ───────────────────────────────────────────────────────────────
    private static readonly Color _bgPanel    = new(0.102f, 0.102f, 0.18f,  1f);
    private static readonly Color _cyan       = new(0.267f, 1f,    0.933f,  1f);
    private static readonly Color _violet     = new(0.667f, 0.267f, 1f,    1f);
    private static readonly Color _gold       = new(1f,     0.8f,  0.267f,  1f);
    private static readonly Color _offWhite   = new(0.85f,  0.85f, 0.95f,   1f);
    private static readonly Color _grey       = new(0.55f,  0.55f, 0.65f,   1f);
    private static readonly Color _green      = new(0.3f,   1f,    0.5f,    1f);

    // ── Noms affichés ─────────────────────────────────────────────────────────
    private static readonly System.Collections.Generic.Dictionary<string, string> WeaponNames = new()
    {
        ["impulse_cannon"]   = "Canon à Impulsions",
        ["plasma_blade"]     = "Lame Plasma",
        ["drone_swarm"]      = "Essaim de Drones",
        ["overload_field"]   = "Champ de Surcharge",
        ["fusion_blade"]     = "Lame à Fusion",
        ["rail_overcharged"] = "Rail Surchargé",
    };

    private static readonly System.Collections.Generic.Dictionary<string, string> PassiveNames = new()
    {
        ["thermal_core"]       = "Noyau Thermique",
        ["reinforced_plating"] = "Plaque Renforcée",
        ["servo_motors"]       = "Servo-Moteurs",
        ["capacitor"]          = "Capaciteur",
    };

    // ── Construction ──────────────────────────────────────────────────────────

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100;
        BuildLayout();
        GetTree().Paused = true;
    }

    private void BuildLayout()
    {
        // Fond sombre semi-transparent
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);

        // Centrage
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        // Panel principal
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(920f, 0f);
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = _bgPanel;
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = _violet;
        panelStyle.SetCornerRadiusAll(6);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   28);
        margin.AddThemeConstantOverride("margin_right",  28);
        margin.AddThemeConstantOverride("margin_top",    22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        // Titre
        root.AddChild(Lbl("⏸  " + Loc.T("PAUSE_TITLE"), 26, _cyan, center: true));
        root.AddChild(Sep(_violet));

        // Corps 2 colonnes
        var cols = new HBoxContainer();
        cols.AddThemeConstantOverride("separation", 24);
        root.AddChild(cols);

        var left  = NewCol(cols);
        var vsep  = new VSeparator();
        vsep.AddThemeColorOverride("color", _violet);
        cols.AddChild(vsep);
        var right = NewCol(cols);

        BuildMissionSection(left);
        BuildPlayerSection(left);
        BuildInventorySection(right);

        // Boutons : reprendre + quitter la partie
        root.AddChild(Sep(_violet));
        var btn = MakeButton("▶  " + Loc.T("PAUSE_RESUME"));
        btn.Pressed += Close;
        root.AddChild(btn);

        var quitBtn = MakeButton(Loc.T("PAUSE_QUIT"));
        quitBtn.Pressed += QuitToMenu;
        root.AddChild(quitBtn);

        // Navigation clavier/manette entre les deux boutons
        btn.FocusNeighborBottom     = btn.GetPathTo(quitBtn);
        quitBtn.FocusNeighborTop    = quitBtn.GetPathTo(btn);

        // Focus sur le bouton reprendre après le layout
        CreateTween().TweenCallback(Callable.From(() => btn.GrabFocus()));
    }

    // ── Section MISSION ───────────────────────────────────────────────────────

    private static void BuildMissionSection(VBoxContainer col)
    {
        col.AddChild(SectionLbl(Loc.T("PAUSE_MISSION")));
        var grid = AddGrid(col);

        var tracker = RunStatsTracker.Instance;
        var xp      = XpSystem.Instance;

        int elapsed = tracker != null ? (int)tracker.ElapsedSeconds : 0;
        int mm = elapsed / 60, ss = elapsed % 60;

        int remaining = 0;
        if (tracker != null)
            remaining = Mathf.Max(0, tracker.RunDurationSeconds - elapsed);
        int rm = remaining / 60, rs = remaining % 60;

        StatRow(grid, Loc.T("PAUSE_TIME_SURVIVED"), $"{mm:D2}:{ss:D2}");
        StatRow(grid, Loc.T("PAUSE_TIME_LEFT"),     $"{rm:D2}:{rs:D2}");
        StatRow(grid, Loc.T("PAUSE_LEVEL"),         $"{xp?.CurrentLevel ?? 1}");
        StatRow(grid, Loc.T("PAUSE_XP"),            $"{xp?.CurrentXp ?? 0} / {xp?.XpToNextLevel ?? 0}");
        StatRow(grid, Loc.T("PAUSE_KILLS"),         $"{tracker?.KillCount ?? 0}");
        StatRow(grid, Loc.T("PAUSE_CORES"),         $"{tracker?.CoresCollected ?? 0}");
    }

    // ── Section JOUEUR ────────────────────────────────────────────────────────

    private static void BuildPlayerSection(VBoxContainer col)
    {
        col.AddChild(ThinSep());
        col.AddChild(SectionLbl(Loc.T("PAUSE_PLAYER")));
        var grid = AddGrid(col);

        var s = GameManager.Instance?.PlayerInstance?.Stats;
        if (s == null) { StatRow(grid, "—", Loc.T("PAUSE_UNAVAILABLE")); return; }

        float hpRatio = s.MaxHp > 0f ? s.CurrentHp / s.MaxHp : 0f;
        Color hpColor = hpRatio > 0.5f ? new Color(0.267f, 1f, 0.933f)
                      : hpRatio > 0.25f ? new Color(1f, 0.6f, 0.1f)
                      : new Color(0.8f, 0.2f, 0.067f);

        StatRow(grid, Loc.T("PAUSE_HP"),         $"{(int)s.CurrentHp} / {(int)s.MaxHp}", hpColor);
        StatRow(grid, Loc.T("PAUSE_SPEED"),      $"{(int)s.Speed} px/s");
        StatRow(grid, Loc.T("PAUSE_DMG_MULT"),   $"×{s.DamageMultiplier:F2}",
                      s.DamageMultiplier > 1f ? _gold : _offWhite);
        StatRow(grid, Loc.T("PAUSE_DMG_REDUC"),  $"{(int)(s.DamageReduction * 100)} %",
                      s.DamageReduction > 0f ? _green : _offWhite);
        StatRow(grid, Loc.T("PAUSE_CD_REDUC"),   $"{(int)(s.CooldownReduction * 100)} %",
                      s.CooldownReduction > 0f ? _gold : _offWhite);
    }

    // ── Section INVENTAIRE ────────────────────────────────────────────────────

    private static void BuildInventorySection(VBoxContainer col)
    {
        var inv    = InventorySystem.Instance;
        var player = GameManager.Instance?.PlayerInstance;
        if (inv == null || player == null) return;

        // ── Armes ──────────────────────────────────────────────────────────────
        col.AddChild(SectionLbl(Loc.T("PAUSE_WEAPONS")));

        if (inv.WeaponLevels.Count == 0)
        {
            col.AddChild(Lbl(Loc.T("PAUSE_NONE"), 13, _grey));
        }
        else
        {
            foreach (var (id, lvl) in inv.WeaponLevels)
            {
                bool isFusion = inv.AppliedFusions.Contains(id);
                int  maxLvl   = inv.GetWeaponMaxLevel(id);
                string name   = WeaponNames.GetValueOrDefault(id, id);
                string prefix = isFusion ? "✦ " : "  ";

                // Nom + niveau
                var nameRow = new HBoxContainer();
                nameRow.AddThemeConstantOverride("separation", 8);
                col.AddChild(nameRow);
                nameRow.AddChild(Lbl(prefix + name, 15, isFusion ? _violet : _gold));
                nameRow.AddChild(Lbl($"Niv. {lvl}/{maxLvl}", 13, _grey));

                // Stats spécifiques selon le type d'arme
                WeaponBase? wb = FindWeaponNode(id, player);
                if (wb != null)
                {
                    var details = WeaponDetails(wb);
                    col.AddChild(Lbl("    " + details, 12, _offWhite));
                }
            }
        }

        // ── Passifs ────────────────────────────────────────────────────────────
        if (inv.PassiveLevels.Count > 0)
        {
            col.AddChild(ThinSep());
            col.AddChild(SectionLbl(Loc.T("PAUSE_PASSIVES")));
            var grid = AddGrid(col);
            foreach (var (id, lvl) in inv.PassiveLevels)
            {
                int maxLvl = inv.GetPassiveMaxLevel(id);
                string pname = PassiveNames.GetValueOrDefault(id, id);
                StatRow(grid, pname, $"Niv. {lvl}/{maxLvl}", _gold);
            }
        }
    }

    // ── Détails arme ──────────────────────────────────────────────────────────

    private static string WeaponDetails(WeaponBase wb) => wb switch
    {
        ImpulseCannon ic =>
            $"{ic.Damage:F0} dmg | {ic.Cooldown:F2}s cd | {ic.ProjectileCount} projectile(s)"
            + (ic.IsPiercing ? " | perforant" : ""),

        PlasmaBlade pb =>
            $"{pb.Damage:F0} dmg | {pb.Cooldown:F2}s cd | arc {pb.ArcAngleDeg:F0}° | r={pb.ArcRadius:F0}px",

        DroneSwarm ds =>
            $"{ds.Damage:F0} dmg/tick | {ds.DroneCount} drone(s) | {ds.DamageInterval:F2}s tick"
            + $" → {ds.Damage * ds.DroneCount / ds.DamageInterval:F0} dps",

        OverloadField of =>
            $"{of.Damage:F0} dmg | {of.Cooldown:F2}s cd | r={of.Radius:F0}px | {of.Knockback:F0}px knockback",

        FusionBlade =>
            "55 dps continu | r=130px | anneau 360°",

        RailOvercharged =>
            "22 dmg × 3 proj. | 0.60s cd | perforation infinie | 600 px/s",

        _ =>
            $"{wb.Damage:F0} dmg | {wb.Cooldown:F2}s cd"
    };

    // ── Recherche nœud d'arme ─────────────────────────────────────────────────

    private static WeaponBase? FindWeaponNode(string id, Player player)
    {
        foreach (var child in player.GetChildren())
        {
            if (child is WeaponBase wb && MatchId(child, id))
                return wb;
        }
        return null;
    }

    private static bool MatchId(Node n, string id) => id switch
    {
        "impulse_cannon"   => n is ImpulseCannon,
        "plasma_blade"     => n is PlasmaBlade,
        "drone_swarm"      => n is DroneSwarm,
        "overload_field"   => n is OverloadField,
        "fusion_blade"     => n is FusionBlade,
        "rail_overcharged" => n is RailOvercharged,
        _                  => false,
    };

    // ── Fermeture ─────────────────────────────────────────────────────────────

    private void Close()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        GetTree().Paused = false;
        QueueFree();
    }

    /// <summary>Abandonne la run en cours et revient au menu principal.</summary>
    private void QuitToMenu()
    {
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        // Le tree était en pause : impératif de le relancer, sinon le menu reste figé.
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        // Le PauseScreen est enfant de la racine (pas de la scène déchargée) → on le retire.
        QueueFree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Ferme la pause via Échap/B (ui_cancel) OU Start manette (pause) → Start fait bascule.
        if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    // ── Helpers UI ────────────────────────────────────────────────────────────

    private static Label Lbl(string text, int size, Color color, bool center = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        if (center) l.HorizontalAlignment = HorizontalAlignment.Center;
        return l;
    }

    private static Label SectionLbl(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 15);
        l.AddThemeColorOverride("font_color", _violet);
        return l;
    }

    private static HSeparator Sep(Color color)
    {
        var s = new HSeparator();
        s.AddThemeColorOverride("color", color);
        return s;
    }

    private static HSeparator ThinSep() => Sep(new Color(0.3f, 0.3f, 0.5f));

    private static VBoxContainer NewCol(HBoxContainer parent)
    {
        var v = new VBoxContainer();
        v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AddThemeConstantOverride("separation", 5);
        parent.AddChild(v);
        return v;
    }

    private static GridContainer AddGrid(VBoxContainer col)
    {
        var g = new GridContainer { Columns = 2 };
        g.AddThemeConstantOverride("h_separation", 12);
        g.AddThemeConstantOverride("v_separation", 3);
        col.AddChild(g);
        return g;
    }

    private static void StatRow(GridContainer grid, string key, string value, Color? color = null)
    {
        var k = new Label { Text = key };
        k.AddThemeFontSizeOverride("font_size", 13);
        k.AddThemeColorOverride("font_color", _grey);
        k.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(k);

        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        v.AddThemeColorOverride("font_color", color ?? _offWhite);
        v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(v);
    }

    private static Button MakeButton(string text)
    {
        var btn = new Button { Text = text };
        btn.CustomMinimumSize    = new Vector2(300f, 44f);
        btn.SizeFlagsHorizontal  = Control.SizeFlags.ShrinkCenter;
        btn.AddThemeFontSizeOverride("font_size", 16);

        var normal = new StyleBoxFlat();
        normal.BgColor = new Color(0.08f, 0.06f, 0.2f);
        normal.SetBorderWidthAll(2);
        normal.BorderColor = _cyan;
        normal.SetCornerRadiusAll(4);

        var hover = new StyleBoxFlat();
        hover.BgColor = new Color(0.12f, 0.08f, 0.25f);
        hover.SetBorderWidthAll(3);
        hover.BorderColor = _violet;
        hover.SetCornerRadiusAll(4);

        btn.AddThemeStyleboxOverride("normal",  normal);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus",   hover);
        return btn;
    }
}
