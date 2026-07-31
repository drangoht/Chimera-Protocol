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

    // Sélecteur de saturation (cf. docs/ENDGAME_PLAN.md), UN PAR CARTE depuis le 2026-07-30 : le cran
    // se règle et se débloque par niveau. Il vit sur la carte du biome plutôt que dans un panneau en
    // tête d'écran — la liste défile, et un sélecteur global aurait permis de régler le cran d'un
    // biome sorti du champ de vision. Absent en mode assistance (challenge et accessibilité ne se
    // mélangent pas) et absent des cartes verrouillées (rien à régler sur un niveau injouable).
    //
    // Une rangée par carte : les boutons focalisables dans l'ordre horizontal (◄, ►, « Jouer ici »).
    // Sert à câbler la navigation clavier/manette, cf. SetupFocusChain.
    private readonly System.Collections.Generic.List<Button[]> _cardRows = new();

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
    /// Sélecteur de saturation d'UNE carte de biome : cran, cran maximum débloqué sur ce niveau,
    /// multiplicateur d'Échos, puis les règles actives.
    ///
    /// <para>Les règles ne sont pas décoratives : tout le parti pris du système est qu'un cran est une
    /// <b>règle nommée qu'on lit avant de jouer</b> (docs/ENDGAME_PLAN.md §2). Un sélecteur qui
    /// n'afficherait qu'un numéro reproduirait le défaut que la saturation corrige — une difficulté qui
    /// monte sans que le joueur sache ce qui a changé.</para>
    ///
    /// <para><b>Ce qui est affiché, et pourquoi pas tout</b> : les crans déjà acquis sont rappelés par
    /// leur seul <b>nom</b>, et seul le <b>dernier</b> cran — celui qu'on vient d'ajouter — est donné
    /// en toutes lettres. Le moment où lire compte est celui où l'on monte d'un cran ; répéter cinq
    /// règles complètes sur cinq cartes ferait une page de texte que plus personne ne lit.</para>
    /// </summary>
    private Button[] BuildCardSaturation(VBoxContainer into, string biomeId, Color accent)
    {
        var gs = GameSettings.Instance;
        if (gs == null) return System.Array.Empty<Button>();

        // Mode assistance : le sélecteur n'a pas de sens (« Facile » neutralise tous les crans), mais le
        // faire disparaître SANS un mot était un angle mort — un joueur en assistance ne saurait ni que
        // l'échelle existe, ni où la réactiver. Même famille de défaut que le dash sans touche annoncée
        // et l'Auto-réparation crue inactive (docs/PITFALLS.md) : invisible se lit inexistant. On garde
        // donc la ligne d'explication, et seulement elle — aucun bouton, donc rien à insérer dans la
        // chaîne de focus.
        if (gs.IsAssisted)
        {
            var assisted = new VBoxContainer();
            assisted.AddThemeConstantOverride("separation", 1);
            into.AddChild(assisted);
            AddRuleLine(assisted, Loc.T("SAT_SHORT") + " — " + Loc.T("SAT_ASSISTED"), Dim, 12);
            return System.Array.Empty<Button>();
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var caption = new Label { Text = Loc.T("SAT_SHORT"), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        caption.AddThemeFontSizeOverride("font_size", 14);
        caption.AddThemeColorOverride("font_color", Cyan);
        row.AddChild(caption);

        // ShrinkCenter sur les deux flèches : le cadre de focus s'étend de FocusExpand px et, sans
        // ancrage centré, un bouton focalisé grandit vers le bas et décale toute la ligne (cf.
        // docs/PITFALLS.md §UI — pièges StyleBox / focus).
        var down = new Button
        {
            Text = "◄",
            CustomMinimumSize = new Vector2(44, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleButton(down, Violet);
        row.AddChild(down);

        var value = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical   = SizeFlags.ShrinkCenter,
            CustomMinimumSize   = new Vector2(28, 0),
        };
        value.AddThemeFontSizeOverride("font_size", 20);
        value.AddThemeColorOverride("font_color", Text);
        row.AddChild(value);

        var up = new Button
        {
            Text = "►",
            CustomMinimumSize = new Vector2(44, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleButton(up, Violet);
        row.AddChild(up);

        var maxLbl = new Label { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        maxLbl.AddThemeFontSizeOverride("font_size", 13);
        maxLbl.AddThemeColorOverride("font_color", Dim);
        row.AddChild(maxLbl);

        var sep = new Label { Text = "·", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        sep.AddThemeFontSizeOverride("font_size", 13);
        sep.AddThemeColorOverride("font_color", Dim);
        row.AddChild(sep);

        var echoes = new Label { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        echoes.AddThemeFontSizeOverride("font_size", 14);
        echoes.AddThemeColorOverride("font_color", UiPalette.Gold);   // or : récompense
        row.AddChild(echoes);

        into.AddChild(row);

        var rules = new VBoxContainer();
        rules.AddThemeConstantOverride("separation", 1);
        into.AddChild(rules);

        void Refresh()
        {
            int rank = gs.SaturationFor(biomeId);
            int max  = gs.MaxSelectableSaturationFor(biomeId);

            value.Text   = rank.ToString();
            maxLbl.Text  = Loc.T("SAT_MAX", max.ToString());
            echoes.Text  = Loc.T("LEVELSEL_ECHO_MULT", $"{SaturationTable.EchoMult(rank):0.00}");
            down.Disabled = rank <= 0;
            up.Disabled   = rank >= max;

            foreach (var child in rules.GetChildren()) child.QueueFree();

            if (rank == 0)
            {
                AddRuleLine(rules, Loc.T("SAT_NONE"), Dim, 13);
            }
            else
            {
                // Rappel des crans acquis : noms seuls, sur une ligne.
                var names = new System.Text.StringBuilder();
                foreach (var r in SaturationTable.ActiveRanks(rank))
                {
                    if (names.Length > 0) names.Append("  ·  ");
                    names.Append($"{RomanNumeral(r.Value)} {Loc.T(r.NameKey)}");
                }
                AddRuleLine(rules, names.ToString(), accent, 13);

                // La règle du cran le plus haut, en toutes lettres : c'est la seule nouveauté par
                // rapport au cran précédent, donc la seule que le joueur doive lire.
                var top = SaturationTable.ActiveRanks(rank)[^1];
                AddRuleLine(rules, Loc.T(top.RuleKey), Text, 13);
            }

            if (rank < SaturationTable.MaxRank && rank >= max)
                AddRuleLine(rules, Loc.T("SAT_LOCKED_HINT"), Dim, 12);
        }

        void Change(int delta)
        {
            gs.SetSaturationFor(biomeId, gs.SaturationFor(biomeId) + delta);
            AudioSystem.Instance?.PlaySfx("sfx_ui_click");
            Refresh();
        }

        down.Pressed += () => Change(-1);
        up.Pressed   += () => Change(+1);
        Refresh();

        return new[] { down, up };
    }

    /// <summary>Ligne de texte d'une carte (règle de saturation), repliée si trop longue.</summary>
    private static void AddRuleLine(VBoxContainer into, string text, Color color, int size)
    {
        var line = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        line.AddThemeFontSizeOverride("font_size", size);
        line.AddThemeColorOverride("font_color", color);
        into.AddChild(line);
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

        // Cran de saturation de CE niveau : réglé et débloqué ici, sous la description du biome qu'il
        // modifie. Rien sur une carte verrouillée — il n'y a pas de cran à régler sur un niveau qu'on
        // ne peut pas encore jouer.
        var satButtons = unlocked ? BuildCardSaturation(vb, id, accent) : System.Array.Empty<Button>();
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

            // Rangée focalisable de la carte, dans l'ordre horizontal. Les flèches doivent en faire
            // partie : sans câblage explicite elles resteraient inatteignables à la manette (le focus
            // spatial de Godot ne traverse pas les PanelContainer des cartes), et le cran deviendrait
            // un réglage souris seulement.
            var rowButtons = new Button[satButtons.Length + 1];
            satButtons.CopyTo(rowButtons, 0);
            rowButtons[^1] = play;
            foreach (var b in satButtons)
                b.FocusEntered += () => _scroll.EnsureControlVisible(b);
            _cardRows.Add(rowButtons);
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
        // Chaque carte est une RANGÉE (◄ ► « Jouer ici ») : gauche/droite parcourt la rangée,
        // haut/bas saute de carte en carte. Le voisin vertical vise le PREMIER bouton de la carte
        // voisine — descendre puis remonter ramène donc au sélecteur, jamais dans un cul-de-sac.
        for (int i = 0; i < _cardRows.Count; i++)
        {
            var row = _cardRows[i];
            for (int j = 0; j < row.Length; j++)
            {
                if (j > 0)              row[j].FocusNeighborLeft  = row[j].GetPathTo(row[j - 1]);
                if (j < row.Length - 1) row[j].FocusNeighborRight = row[j].GetPathTo(row[j + 1]);

                row[j].FocusNeighborTop = i > 0
                    ? row[j].GetPathTo(_cardRows[i - 1][0])
                    : new NodePath();
                row[j].FocusNeighborBottom = i < _cardRows.Count - 1
                    ? row[j].GetPathTo(_cardRows[i + 1][0])
                    : row[j].GetPathTo(rand);
            }
        }

        if (_playButtons.Count > 0)
        {
            var last = _cardRows.Count > 0 ? _cardRows[^1][0] : _playButtons[^1];
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
