using Godot;

/// <summary>
/// Écran de choix du niveau (biome) après « Jouer ». Liste les biomes avec aperçu
/// (tuile en damier), nom, effet et description. Sélectionner un biome le force pour
/// la run ; « Aléatoire » laisse le tirage au sort. UI construite en code.
/// </summary>
public partial class LevelSelectScreen : Control
{
    private static readonly Color Bg     = UiPalette.BgDeep;
    private static readonly Color Cyan   = UiPalette.Cyan;
    private static readonly Color Violet = UiPalette.Violet;
    private static readonly Color Text   = UiPalette.OffWhite;
    private static readonly Color Dim    = UiPalette.Dim;

    private ColorRect _fade   = null!;
    private bool      _leaving = false;
    private Button?   _firstPlay;   // bouton « Jouer ici » du 1er biome (présélectionné)
    private ScrollContainer _scroll = null!;
    private readonly System.Collections.Generic.List<Button> _playButtons = new();

    // Sélecteur d'ascension (cf. docs/ENDGAME_PLAN.md). Null en mode assistance : l'échelle de
    // challenge et l'accessibilité ne se mélangent pas.
    private Button? _ascDown;
    private Button? _ascUp;
    private Label?  _ascValue;
    private Label?  _ascEchoes;
    private VBoxContainer? _ascRules;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Bg };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 12);
        root.OffsetLeft = 80; root.OffsetRight = -80; root.OffsetTop = 30; root.OffsetBottom = -24;
        AddChild(root);

        var title = new Label { Text = Loc.T("LEVELSEL_TITLE"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", Cyan);
        root.AddChild(title);

        // Sélecteur d'ascension, AVANT la liste : c'est un choix qui s'applique à toute la run, pas au
        // niveau. Masqué en mode assistance (« Facile »), où l'ascension est forcée à 0.
        var ascPanel = BuildAscensionSelector();
        if (ascPanel != null) root.AddChild(ascPanel);

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(list);

        foreach (var b in BiomeCatalog.All)
        {
            string k = b.Id.ToUpperInvariant();
            list.AddChild(BuildCard(b.Id, Loc.T($"BIOME_{k}_NAME"), Loc.T($"BIOME_{k}_EFFECT"),
                                    Loc.T($"BIOME_{k}_DESC"), b.Accent, b.PreviewPath));
        }

        // Boutons bas : Aléatoire + Retour
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        var rand = new Button { Text = Loc.T("LEVELSEL_RANDOM"), CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(rand, Cyan);
        rand.Pressed += StartRandomUnlocked;   // ne tire que parmi les niveaux débloqués
        var back = new Button { Text = Loc.T("COMMON_BACK"), CustomMinimumSize = new Vector2(200, 46) };
        StyleButton(back, Violet);
        back.Pressed += GoBack;
        row.AddChild(rand);
        row.AddChild(back);
        root.AddChild(row);

        // Chaîne de focus explicite (le focus spatial ne traverse pas fiablement les PanelContainer
        // des cartes) + auto-scroll : sans ça, en bas le focus sautait directement à Random/Back et
        // la liste ne défilait pas pour suivre la carte sélectionnée (Néon hors écran).
        SetupFocusChain(rand, back);

        _fade = new ColorRect { Color = new Color(0, 0, 0, 1) };
        _fade.SetAnchorsPreset(LayoutPreset.FullRect);
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_fade);
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 0f, 0.4);
        // Présélectionne le 1er niveau (fallback « Retour » s'il n'y a aucun biome).
        t.TweenCallback(Callable.From(() => (_firstPlay ?? back).GrabFocus()));
    }

    /// <summary>
    /// Panneau de choix du cran d'ascension : valeur, multiplicateur d'Échos et <b>liste des règles
    /// actives</b>. Renvoie null en mode assistance.
    ///
    /// <para>La liste des règles n'est pas décorative : tout le parti pris du système est qu'un cran
    /// est une <b>règle nommée qu'on lit avant de jouer</b> (docs/ENDGAME_PLAN.md §2). Un sélecteur qui
    /// n'afficherait qu'un numéro reproduirait le défaut que l'ascension corrige — une difficulté qui
    /// monte sans que le joueur sache ce qui a changé.</para>
    /// </summary>
    private Control? BuildAscensionSelector()
    {
        var gs = GameSettings.Instance;
        if (gs == null || gs.IsAssisted) return null;

        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        // Cadre « plaque blindée » à l'or de la palette : la même couleur que les récompenses d'Échos,
        // puisque c'est le contrat de ce panneau — plus dur, mieux payé.
        panel.AddThemeStyleboxOverride("panel", UiStyle.CardFrame(UiPalette.Gold));

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        panel.AddChild(vb);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);

        var caption = new Label { Text = Loc.T("ASC_TITLE"), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        caption.AddThemeFontSizeOverride("font_size", 18);
        caption.AddThemeColorOverride("font_color", Cyan);
        row.AddChild(caption);

        // ShrinkCenter sur les deux flèches : le cadre de focus s'étend de FocusExpand px et, sans
        // ancrage centré, un bouton focalisé grandit vers le bas et décale toute la ligne (cf.
        // docs/PITFALLS.md §UI — pièges StyleBox / focus).
        _ascDown = new Button
        {
            Text = "◄",
            CustomMinimumSize = new Vector2(52, 40),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleButton(_ascDown, Violet);
        _ascDown.Pressed += () => ChangeAscension(-1);
        row.AddChild(_ascDown);

        _ascValue = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(46, 0),
        };
        _ascValue.AddThemeFontSizeOverride("font_size", 24);
        _ascValue.AddThemeColorOverride("font_color", Text);
        row.AddChild(_ascValue);

        _ascUp = new Button
        {
            Text = "►",
            CustomMinimumSize = new Vector2(52, 40),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleButton(_ascUp, Violet);
        _ascUp.Pressed += () => ChangeAscension(+1);
        row.AddChild(_ascUp);

        _ascEchoes = new Label { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _ascEchoes.AddThemeFontSizeOverride("font_size", 15);
        _ascEchoes.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.27f));   // or : récompense
        row.AddChild(_ascEchoes);

        vb.AddChild(row);

        _ascRules = new VBoxContainer();
        _ascRules.AddThemeConstantOverride("separation", 2);
        vb.AddChild(_ascRules);

        RefreshAscension();
        return panel;
    }

    /// <summary>Décale le cran choisi (borné par la progression) et rafraîchit l'affichage.</summary>
    private void ChangeAscension(int delta)
    {
        var gs = GameSettings.Instance;
        if (gs == null) return;
        gs.SetAscension(gs.Ascension + delta);
        AudioSystem.Instance?.PlaySfx("sfx_ui_click");
        RefreshAscension();
    }

    /// <summary>Met à jour valeur, Échos, règles actives et l'état des deux flèches.</summary>
    private void RefreshAscension()
    {
        var gs = GameSettings.Instance;
        if (gs == null || _ascValue == null || _ascRules == null) return;

        int rank = gs.Ascension;
        _ascValue.Text = rank.ToString();
        if (_ascEchoes != null)
            _ascEchoes.Text = Loc.T("LEVELSEL_ECHO_MULT", $"{gs.AscensionEchoMult:0.00}");

        if (_ascDown != null) _ascDown.Disabled = rank <= 0;
        if (_ascUp   != null) _ascUp.Disabled   = rank >= gs.MaxSelectableAscension;

        foreach (var child in _ascRules.GetChildren()) child.QueueFree();

        if (rank == 0)
        {
            var none = new Label { Text = Loc.T("ASC_NONE"), HorizontalAlignment = HorizontalAlignment.Center };
            none.AddThemeFontSizeOverride("font_size", 13);
            none.AddThemeColorOverride("font_color", Dim);
            _ascRules.AddChild(none);
            return;
        }

        foreach (var r in AscensionTable.ActiveRanks(rank))
        {
            var line = new Label
            {
                Text = $"{RomanNumeral(r.Value)} · {Loc.T(r.NameKey)} — {Loc.T(r.RuleKey)}",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            line.AddThemeFontSizeOverride("font_size", 13);
            line.AddThemeColorOverride("font_color", Text);
            _ascRules.AddChild(line);
        }

        if (rank < AscensionTable.MaxRank && rank >= gs.MaxSelectableAscension)
        {
            var next = new Label
            {
                Text = Loc.T("ASC_LOCKED_HINT"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            next.AddThemeFontSizeOverride("font_size", 12);
            next.AddThemeColorOverride("font_color", Dim);
            _ascRules.AddChild(next);
        }
    }

    /// <summary>Chiffre romain d'un cran (I..V). Table courte : MaxRank vaut 5.</summary>
    private static string RomanNumeral(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString(),
    };

    /// <summary>
    /// Jauge de menace du palier, en étoiles (palier 0 → ★, palier 4 → ★★★★★). Seul le glyphe plein
    /// est utilisé : ☆ n'est pas garanti dans Share Tech Mono, ★ l'est (déjà employé sur cet écran et
    /// à la fin de run). Partagée avec <see cref="RunEndScreen"/>.
    /// </summary>
    public static string ThreatStars(int tier)
        => new('★', Mathf.Clamp(tier + 1, 1, LevelThreat.MaxTier + 1));

    private Control BuildCard(string id, string name, string effect, string desc, Color accent, string preview)
    {
        bool unlocked  = GameSettings.Instance?.IsUnlocked(id) ?? true;
        bool completed = GameSettings.Instance?.HasCompletedAny(id) == true;

        // Carte du §3.5 : plaque chanfreinée 9-slice, liseré à l'accent du biome. Niveau non
        // débloqué → plaque d'acier éteinte (le liseré de catégorie est une information de biome
        // qu'on ne révèle pas tant qu'il est verrouillé).
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var cardStyle = unlocked ? UiStyle.CardFrame(accent) : UiStyle.CardFrameDisabled();
        // Sans marge explicite, le contenu se cale sur la bande du cadre (16 px) et vient donc
        // butter contre le liseré d'accent, qui court de 12 à 16 px du bord : 4 à 5 px mesurés
        // entre la plaque du bouton « Jouer ici » et ce liseré, soit un bouton perçu comme collé.
        // 16 + 12 de respiration (même valeur que les cartes de personnage, cf. docs/PITFALLS.md).
        // Seuls les CÔTÉS sont élargis : la hauteur est déjà comptée (la liste déborde et défile).
        const int sideMargin = UiStyle.PanelContentMargin + 12;
        cardStyle.SetContentMargin(Side.Left,  sideMargin);
        cardStyle.SetContentMargin(Side.Right, sideMargin);
        panel.AddThemeStyleboxOverride("panel", cardStyle);
        if (!unlocked) panel.Modulate = new Color(1f, 1f, 1f, 0.45f);   // carte grisée si verrouillée

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hb);

        var prev = new TextureRect
        {
            Texture           = GD.Load<Texture2D>(preview),
            StretchMode       = TextureRect.StretchModeEnum.Tile,
            TextureFilter     = TextureFilterEnum.Nearest,
            CustomMinimumSize = new Vector2(96, 96),
        };
        // Cadre accent autour de l'aperçu : même plaque que la carte (§3.5), en hublot. Instance
        // distincte de celle du panneau — Godot lie les StyleBox partagées.
        var prevWrap = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        prevWrap.AddThemeStyleboxOverride("panel", UiStyle.CardFrame(accent));
        prevWrap.AddChild(prev);
        hb.AddChild(prevWrap);

        var vb = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vb.AddThemeConstantOverride("separation", 2);
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 10);
        var lblName = new Label { Text = name };
        lblName.AddThemeFontSizeOverride("font_size", 22);
        lblName.AddThemeColorOverride("font_color", accent);
        nameRow.AddChild(lblName);
        // Badge : « VAINCU » si complété, sinon « VERROUILLÉ » si non débloqué.
        if (completed || !unlocked)
        {
            var badge = new Label
            {
                Text = Loc.T(completed ? "LEVELSEL_DEFEATED" : "LEVELSEL_LOCKED"),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            badge.AddThemeFontSizeOverride("font_size", 13);
            badge.AddThemeColorOverride("font_color", completed ? new Color(1f, 0.8f, 0.27f) : Dim);
            nameRow.AddChild(badge);
        }
        // Record de temps survécu (high score du niveau) + difficulté du record.
        int best = GameSettings.Instance?.BestTime(id) ?? 0;
        if (best > 0)
        {
            string diff = Loc.T(GameSettings.DifficultyKey(GameSettings.Instance!.BestDifficulty(id)));
            var rec = new Label
            {
                Text = $"⏱ {best / 60:D2}:{best % 60:D2} · {diff}",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            rec.AddThemeFontSizeOverride("font_size", 13);
            rec.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
            nameRow.AddChild(rec);
        }
        var lblEffect = new Label { Text = effect };
        lblEffect.AddThemeFontSizeOverride("font_size", 14);
        lblEffect.AddThemeColorOverride("font_color", Cyan);
        // Palier de menace : contrat lisible avant de lancer la run — les niveaux tardifs sont plus
        // durs (le Hub a rendu le joueur plus fort) mais paient plus d'Échos (cf. LevelThreat).
        int tier = LevelThreat.TierOf(id);
        var lblThreat = new Label
        {
            Text = $"{Loc.T("LEVELSEL_THREAT")} {ThreatStars(tier)}   ·   {Loc.T("LEVELSEL_ECHO_MULT", $"{LevelThreat.EchoMult(tier):0.00}")}",
        };
        lblThreat.AddThemeFontSizeOverride("font_size", 14);
        lblThreat.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.27f));   // or : récompense
        var lblDesc = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lblDesc.AddThemeFontSizeOverride("font_size", 14);
        lblDesc.AddThemeColorOverride("font_color", Dim);
        vb.AddChild(nameRow); vb.AddChild(lblEffect); vb.AddChild(lblThreat); vb.AddChild(lblDesc);
        hb.AddChild(vb);

        var play = new Button
        {
            Text = unlocked ? Loc.T("LEVELSEL_PLAY_HERE") : "🔒",
            CustomMinimumSize = new Vector2(130, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Disabled = !unlocked,
        };
        StyleButton(play, accent);
        if (unlocked)
        {
            play.Pressed += () => StartRun(id);
            // Auto-scroll : quand ce bouton prend le focus (clavier/manette), défile pour le rendre visible.
            play.FocusEntered += () => _scroll.EnsureControlVisible(play);
            hb.AddChild(play);
            _playButtons.Add(play);          // seuls les débloqués entrent dans la chaîne de focus
            _firstPlay ??= play;             // 1er niveau jouable = présélection
        }
        else
        {
            hb.AddChild(play);               // visible mais désactivé (pas dans le focus)
        }

        return panel;
    }

    /// <summary>
    /// Câble la navigation verticale : carte[0] → … → carte[N] → (Aléatoire / Retour), et l'inverse.
    /// Indispensable car l'algo de focus spatial de Godot ne traverse pas fiablement les PanelContainer
    /// des cartes (même piège que HubScreen.SetupFocusChain).
    /// </summary>
    private void SetupFocusChain(Button rand, Button back)
    {
        // Les flèches d'ascension sont en TÊTE de chaîne : sans câblage explicite elles resteraient
        // inatteignables à la manette (le focus spatial ne traverse pas les PanelContainer — c'est déjà
        // la raison d'être de cette méthode), et le cran d'ascension serait un réglage souris seulement.
        if (_ascDown != null && _ascUp != null)
        {
            _ascDown.FocusNeighborRight = _ascDown.GetPathTo(_ascUp);
            _ascUp.FocusNeighborLeft    = _ascUp.GetPathTo(_ascDown);
            if (_playButtons.Count > 0)
            {
                _ascDown.FocusNeighborBottom = _ascDown.GetPathTo(_playButtons[0]);
                _ascUp.FocusNeighborBottom   = _ascUp.GetPathTo(_playButtons[0]);
            }
        }

        for (int i = 0; i < _playButtons.Count; i++)
        {
            var cur = _playButtons[i];
            if (i > 0)
                cur.FocusNeighborTop = cur.GetPathTo(_playButtons[i - 1]);
            else if (_ascUp != null)
                cur.FocusNeighborTop = cur.GetPathTo(_ascUp);   // remonter au sélecteur depuis la 1re carte
            cur.FocusNeighborBottom = (i < _playButtons.Count - 1)
                ? cur.GetPathTo(_playButtons[i + 1])
                : cur.GetPathTo(rand);
        }

        if (_playButtons.Count > 0)
        {
            var last = _playButtons[^1];
            rand.FocusNeighborTop = rand.GetPathTo(last);
            back.FocusNeighborTop = back.GetPathTo(last);
        }
        rand.FocusNeighborRight = rand.GetPathTo(back);
        back.FocusNeighborLeft  = back.GetPathTo(rand);
    }

    private void StyleButton(Button btn, Color accent)
    {
        var frame = UiStyle.NearestAccent(accent);
        btn.AddThemeStyleboxOverride("normal",  UiStyle.ButtonFrame(frame));
        btn.AddThemeStyleboxOverride("hover",   UiStyle.ButtonFrame(frame, UiStyle.FrameState.Hover));
        btn.AddThemeStyleboxOverride("pressed", UiStyle.ButtonFrame(frame, UiStyle.FrameState.Pressed));
        btn.AddThemeStyleboxOverride("focus",   UiStyle.ButtonFrame(UiStyle.FrameAccent.Violet, focus: true));
        UiStyle.AttachFocusPulse(btn);
        btn.AddThemeColorOverride("font_color", UiPalette.OffWhite);
    }

    private void StartRun(string? biomeId)
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        if (GameManager.Instance != null) GameManager.Instance.SelectedBiomeId = biomeId;
        Transition("res://scenes/Game.tscn");
    }

    /// <summary>« Aléatoire » : tire un biome parmi les niveaux débloqués uniquement.</summary>
    private void StartRandomUnlocked()
    {
        var unlocked = new System.Collections.Generic.List<string>();
        foreach (var b in BiomeCatalog.All)
            if (GameSettings.Instance?.IsUnlocked(b.Id) ?? true) unlocked.Add(b.Id);
        if (unlocked.Count == 0) { StartRun(null); return; }
        StartRun(unlocked[(int)(GD.Randi() % (uint)unlocked.Count)]);
    }

    private void GoBack()
    {
        if (_leaving) return;
        _leaving = true;
        AudioSystem.Instance?.PlaySfx("sfx_ui_button");
        // Étape précédente du flux « Jouer » : le choix du personnage.
        Transition("res://scenes/ui/CharacterSelectScreen.tscn");
    }

    private void Transition(string path)
    {
        var t = CreateTween();
        t.TweenProperty(_fade, "color:a", 1f, 0.3);
        t.TweenCallback(Callable.From(() => GetTree().ChangeSceneToFile(path)));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            GoBack();
        }
    }
}
