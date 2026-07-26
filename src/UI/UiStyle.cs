using Godot;

/// <summary>
/// Fabrique centralisée des cadres d'UI (ART_BRIEF_UI_FRAMES). Avant cette classe, ~300 sites
/// dans <c>src/UI/</c> instanciaient à la main la même recette
/// (<c>SetBorderWidthAll</c> + <c>BorderColor</c> plat + <c>SetCornerRadiusAll</c>) avec des
/// rayons divergents (3, 4, 6, 8, 10) et aucune asymétrie — la signature visuelle d'un composant
/// générique, pas d'une plaque blindée. Toute nouvelle <see cref="StyleBox"/> doit venir d'ici.
///
/// Étape 1 du brief (§6.1) : les deux familles livrables en pur code, sans nouvel asset —
/// panneaux de fond d'écran (§3.3) et séparateurs/titres (§3.6).
///
/// <para><b>Instances</b> : chaque appel construit une <see cref="StyleBoxFlat"/> neuve.
/// Godot lie les <c>sub_resource</c> partagées entre contrôles et casse alors les états
/// hover/pressed (cf. <c>docs/PITFALLS.md</c> § UI) — ne jamais mémoïser le résultat.</para>
/// </summary>
public static class UiStyle
{
    // ── Panneau de fond d'écran (ART_BRIEF_UI_FRAMES §3.3) ────────────────────────────────
    //
    // Famille la plus calme de la hiérarchie (grands conteneurs passifs) : angle droit strict,
    // pas de chanfrein, pas de liseré accent saturé, 100 % StyleBoxFlat — zéro asset.
    //
    // ARBITRAGE double-bevel : le brief demande haut+gauche en SteelHighlight et bas+droite en
    // SteelShadow. Godot n'expose qu'UNE `border_color` par StyleBoxFlat ; obtenir deux teintes
    // imposerait soit deux StyleBox superposées (donc un nœud enveloppe supplémentaire par
    // panneau, sur des dizaines de lignes de liste), soit l'ombre portée floue de StyleBoxFlat
    // (`shadow_size`, anti-aliasée — interdite par la contrainte « rendu net » du brief).
    // Tranché : on ne garde que le côté éclairé (haut + gauche, 1 px SteelHighlight) et on met
    // les côtés bas/droite à 0. Justification : SteelShadow (#121223) ne se distingue pas des
    // fonds d'écran réels du jeu (BgDeep #0F0F1C ≈ 3/255 d'écart) — le côté ombré serait
    // invisible pour le coût d'un nœud par panneau. Le contraste du fill contre le fond assure
    // déjà la lecture du bord bas/droite. L'asymétrie voulue par le brief (§1.2) est préservée.

    /// <summary>Marge de contenu par défaut d'un panneau (§3.3).</summary>
    public const int PanelContentMargin = 16;

    private const int BevelWidth = 1;

    /// <summary>
    /// Panneau de fond d'écran posé sur un fond <see cref="UiPalette.BgDeep"/> (Codex, Options,
    /// écrans de menu) : angle droit, fill <see cref="UiPalette.Bg"/> à 88 %, bevel 1 px
    /// haut+gauche.
    /// </summary>
    /// <param name="contentMargin">Marge intérieure ; 16 px par défaut (§3.3). À réduire sur les
    /// lignes de liste denses, où 16 px doublerait la hauteur de chaque rangée.</param>
    public static StyleBoxFlat ScreenPanel(int contentMargin = PanelContentMargin) =>
        BuildPanel(UiPalette.Bg.Alpha(0.88f), contentMargin);

    /// <summary>
    /// Variante pour un panneau posé sur un fond déjà à <see cref="UiPalette.Bg"/> (#1A1A2E),
    /// cas du Hub. Le fill nominal du §3.3 (Bg à 88 %) s'y composite <b>exactement</b> sur la
    /// couleur du fond : le panneau redeviendrait invisible, soit précisément le symptôme que
    /// le brief corrige (§1.4). On assombrit donc le fill pour obtenir le « renfoncement »
    /// décrit, à géométrie et bevel identiques.
    /// </summary>
    public static StyleBoxFlat ScreenPanelSunken(int contentMargin = PanelContentMargin) =>
        BuildPanel(UiPalette.Bg.Darken(0.35f).Alpha(0.88f), contentMargin);

    private static StyleBoxFlat BuildPanel(Color fill, int contentMargin)
    {
        var box = new StyleBoxFlat
        {
            BgColor      = fill,
            AntiAliasing = false,          // aucun lissage : cohérence avec texture_filter = Nearest
            BorderColor  = UiPalette.SteelHighlight,
        };
        box.SetCornerRadiusAll(0);         // §3.3 — fin de l'arrondi flou
        box.BorderWidthLeft   = BevelWidth;
        box.BorderWidthTop    = BevelWidth;
        box.BorderWidthRight  = 0;
        box.BorderWidthBottom = 0;
        box.SetContentMarginAll(contentMargin);
        return box;
    }

    // ── Séparateurs et titres (ART_BRIEF_UI_FRAMES §3.6) ──────────────────────────────────

    /// <summary>Hauteur totale occupée par <see cref="Separator"/> (ligne 2 px + gap 2 px + tick 4 px).</summary>
    public const int SeparatorHeight = 8;

    /// <summary>Hauteur totale occupée par <see cref="ScreenTitleUnderline"/>.</summary>
    public const int TitleUnderlineHeight = 5;

    private const int SeparatorInset = 12;
    private const int TickSize       = 4;

    /// <summary>
    /// Séparateur de section (§3.6) : trait de 2 px à 60 % d'alpha, en retrait de 12 px de
    /// chaque côté, plus deux ticks pleins de 4×4 px aux extrémités, 2 px sous le trait.
    /// Remplace les <c>HSeparator</c> natifs (gris neutre du thème, sans identité).
    /// Règle du brief : tout titre de section est TOUJOURS suivi de ce séparateur.
    /// </summary>
    /// <param name="accent">Accent de contexte de l'écran (cyan par défaut côté appelant).</param>
    public static Control Separator(Color accent)
    {
        var root = new Control
        {
            CustomMinimumSize = new Vector2(0, SeparatorHeight),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };

        var line = MakeRect(accent.Alpha(0.6f));
        line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        line.OffsetLeft   =  SeparatorInset;
        line.OffsetRight  = -SeparatorInset;
        line.OffsetBottom = 2;
        root.AddChild(line);

        root.AddChild(MakeTick(accent, atLeft: true));
        root.AddChild(MakeTick(accent, atLeft: false));
        return root;
    }

    /// <summary>
    /// Soulignement de titre d'écran (H1, §3.6) : double-trait « gravé » — 2 px d'accent à 90 %
    /// puis 1 px de <see cref="UiPalette.SteelShadow"/> 2 px en dessous.
    /// </summary>
    public static Control ScreenTitleUnderline(Color accent)
    {
        var root = new Control
        {
            CustomMinimumSize = new Vector2(0, TitleUnderlineHeight),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };

        var main = MakeRect(accent.Alpha(0.9f));
        main.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        main.OffsetBottom = 2;
        root.AddChild(main);

        var groove = MakeRect(UiPalette.SteelShadow);
        groove.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        groove.OffsetTop    = 4;
        groove.OffsetBottom = 5;
        root.AddChild(groove);

        return root;
    }

    private static ColorRect MakeTick(Color accent, bool atLeft)
    {
        var tick = MakeRect(accent);
        tick.SetAnchorsAndOffsetsPreset(atLeft ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.TopRight);
        tick.OffsetTop    = 4;                       // 2 px sous le trait (qui occupe y 0→2)
        tick.OffsetBottom = 4 + TickSize;
        if (atLeft)
        {
            tick.OffsetLeft  = SeparatorInset;
            tick.OffsetRight = SeparatorInset + TickSize;
        }
        else
        {
            tick.OffsetLeft  = -(SeparatorInset + TickSize);
            tick.OffsetRight = -SeparatorInset;
        }
        return tick;
    }

    /// <summary>ColorRect purement décoratif : ne doit jamais intercepter la souris.</summary>
    private static ColorRect MakeRect(Color color) =>
        new() { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };

    // ── Familles à texture 9-slice (ART_BRIEF_UI_FRAMES §3.1, §3.2, §3.4, §3.5) ───────────
    //
    // Chanfrein, rivets et bevel directionnel sont hors de portée de StyleBoxFlat (qui ne sait
    // qu'arrondir un coin) : ces trois familles s'appuient sur les PNG de
    // `assets/sprites/ui/frames/`, générés par `tools/generate_ui_frames.py`.

    private const string FramesDir = "res://assets/sprites/ui/frames/";

    /// <summary>Bande de cadre d'un bouton/carte (§5) : marge 9-slice de 16 px…</summary>
    private const int ButtonMargin = 16;
    /// <summary>…sauf en bas, où l'on étire plus loin dans le fill plat pour obtenir le bord
    /// « soudé » du §3.1 sans dessiner un seul pixel de plus.</summary>
    /// <remarks>
    /// Le brief propose 22 px. Ramené à 18 après essai à l'écran : 16 + 22 = 38 px de marges
    /// verticales incompressibles, or les boutons de remappage de touches ou d'achat font 40 px
    /// de haut — il ne restait que 2 px de zone étirable et la plaque paraissait écrasée, avec
    /// tout le poids en bas. À 18, il reste 6 px et l'asymétrie « monté depuis le bas » se lit
    /// encore. Les grands boutons (48 px et plus) ne voyaient pas la différence.
    /// </remarks>
    private const int ButtonWeldBottom = 18;

    private const int PopupMargin    = 20;
    private const int PopupWeldTop   = 28;

    /// <summary>Débordement du cadre de focus (§3.2) : signal de <b>forme</b>, pas de teinte.</summary>
    private const int FocusExpand = 3;

    /// <summary>Accents de cadre disponibles — un PNG est généré par entrée.</summary>
    public enum FrameAccent { Cyan, Violet, Gold, Danger }

    /// <summary>Raretés de carte — un PNG est généré par entrée.</summary>
    public enum CardRarity { Common, Rare, Epic }

    /// <summary>Modulations d'état du §3.2, appliquées sur la même texture (V ×1.15 au survol,
    /// ×0.8 à l'enfoncement) plutôt qu'en multipliant les fichiers.</summary>
    public enum FrameState { Normal, Hover, Pressed }

    /// <summary>
    /// Cadre de bouton (§3.2). <paramref name="focus"/> sélectionne la variante <c>_focus</c>
    /// (liseré « allumé ») et ajoute le débordement de forme ; la pulsation, elle, est runtime —
    /// voir <see cref="AttachFocusPulse"/>.
    /// </summary>
    public static StyleBoxTexture ButtonFrame(FrameAccent accent, FrameState state = FrameState.Normal, bool focus = false)
    {
        var box = BuildTextureBox($"ui_frame_button_{Slug(accent)}{(focus ? "_focus" : "")}",
                                  ButtonMargin, ButtonMargin, ButtonMargin, ButtonWeldBottom);
        box.ModulateColor = StateModulate(state);
        if (focus) box.SetExpandMarginAll(FocusExpand);
        return box;
    }

    /// <summary>Cadre de bouton désactivé (§3.2) : acier désaturé, liseré éteint. Un seul PNG
    /// partagé par tous les accents — un bouton grisé n'a plus de catégorie à signaler.</summary>
    public static StyleBoxTexture ButtonFrameDisabled() =>
        BuildTextureBox("ui_frame_button_disabled", ButtonMargin, ButtonMargin, ButtonMargin, ButtonWeldBottom);

    /// <summary>
    /// Cadre de carte sélectionnable (§3.5) : même anatomie que le bouton, liseré à la couleur
    /// de rareté.
    /// </summary>
    public static StyleBoxTexture CardFrame(CardRarity rarity, FrameState state = FrameState.Normal, bool focus = false)
    {
        var box = BuildTextureBox($"ui_frame_card_{Slug(rarity)}{(focus ? "_focus" : "")}",
                                  ButtonMargin, ButtonMargin, ButtonMargin, ButtonWeldBottom);
        box.ModulateColor = StateModulate(state);
        if (focus) box.SetExpandMarginAll(FocusExpand);
        return box;
    }

    /// <summary>Cadre de carte verrouillée (entrée de codex non découverte).</summary>
    public static StyleBoxTexture CardFrameDisabled() =>
        BuildTextureBox("ui_frame_card_disabled", ButtonMargin, ButtonMargin, ButtonMargin, ButtonWeldBottom);

    /// <summary>
    /// Applique le cadre de bouton à une liste déroulante, en repoussant sa flèche à l'intérieur
    /// du liseré.
    /// </summary>
    /// <remarks>
    /// Godot dessine la flèche d'un <see cref="OptionButton"/> à
    /// <c>largeur − flèche − marge_droite_du_stylebox − arrow_margin</c>. Avec la bande de 16 px
    /// du cadre, elle tombait donc pile sur le liseré accent, qui court de 12 à 16 px du bord —
    /// la flèche paraissait posée dessus. On ajoute la marge nécessaire pour la ramener dans la
    /// zone de contenu.
    /// </remarks>
    public static void ApplyDropdownFrames(OptionButton dropdown, FrameAccent accent = FrameAccent.Cyan)
    {
        dropdown.AddThemeStyleboxOverride("normal",   ButtonFrame(accent));
        dropdown.AddThemeStyleboxOverride("hover",    ButtonFrame(accent, FrameState.Hover));
        dropdown.AddThemeStyleboxOverride("pressed",  ButtonFrame(accent, FrameState.Pressed));
        dropdown.AddThemeStyleboxOverride("focus",    ButtonFrame(FrameAccent.Violet, focus: true));
        dropdown.AddThemeStyleboxOverride("disabled", ButtonFrameDisabled());
        dropdown.AddThemeConstantOverride("arrow_margin", DropdownArrowMargin);
        AttachFocusPulse(dropdown);
        // Le menu déroulant est un contrôle distinct : styliser le bouton ne l'atteint pas.
        ApplyPopupMenuStyles(dropdown.GetPopup(), AccentColor(accent));
    }

    /// <summary>
    /// Marge de la flèche, comptée depuis le bord droit du contrôle. Doit dépasser la bande du
    /// cadre (16 px) pour que la flèche passe en deçà du liseré, qui court de 12 à 16 px du bord.
    /// </summary>
    private const int DropdownArrowMargin = 24;

    /// <summary>
    /// Habille un curseur : rail creusé, partie remplie à l'accent, et poignée en petite plaque
    /// d'acier. Sans ça, <see cref="HSlider"/> garde la barre et la pastille grises de Godot,
    /// qui n'appartiennent à aucune charte.
    /// </summary>
    public static void ApplySliderStyles(HSlider slider, Color? accent = null)
    {
        var tint = accent ?? UiPalette.Cyan;

        var rail = new StyleBoxFlat { BgColor = UiPalette.Bg.Darken(0.3f), AntiAliasing = false };
        rail.SetCornerRadiusAll(0);
        rail.SetContentMarginAll(0);
        rail.SetBorderWidthAll(1);
        rail.BorderColor = UiPalette.SteelHighlight;
        rail.SetExpandMarginAll(2);
        slider.AddThemeStyleboxOverride("slider", rail);

        var filled = new StyleBoxFlat { BgColor = tint.Alpha(0.55f), AntiAliasing = false };
        filled.SetCornerRadiusAll(0);
        filled.SetExpandMarginAll(2);
        slider.AddThemeStyleboxOverride("grabber_area", filled);

        var filledHot = new StyleBoxFlat { BgColor = tint.Alpha(0.85f), AntiAliasing = false };
        filledHot.SetCornerRadiusAll(0);
        filledHot.SetExpandMarginAll(2);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", filledHot);

        LoadIcon("ui_slider_grabber", out var grabber);
        LoadIcon("ui_slider_grabber_focus", out var grabberHot);
        if (grabber is not null)
        {
            slider.AddThemeIconOverride("grabber", grabber);
            slider.AddThemeIconOverride("grabber_highlight", grabberHot ?? grabber);
            slider.AddThemeIconOverride("grabber_disabled", grabber);
        }
    }

    /// <summary>
    /// Habille un interrupteur. L'état se lit à la fois à la <b>position</b> du pavé et à la
    /// couleur du liseré — jamais à la couleur seule.
    /// </summary>
    public static void ApplyToggleStyles(CheckButton toggle)
    {
        LoadIcon("ui_toggle_on", out var on);
        LoadIcon("ui_toggle_off", out var off);
        if (on is null || off is null) return;

        foreach (var slot in new[] { "checked", "checked_disabled", "checked_mirrored" })
            toggle.AddThemeIconOverride(slot, on);
        foreach (var slot in new[] { "unchecked", "unchecked_disabled", "unchecked_mirrored" })
            toggle.AddThemeIconOverride(slot, off);

        // Le fond d'un CheckButton doit rester nu : l'icône porte tout le signal.
        toggle.AddThemeStyleboxOverride("normal",  new StyleBoxEmpty());
        toggle.AddThemeStyleboxOverride("hover",   new StyleBoxEmpty());
        toggle.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        toggle.AddThemeStyleboxOverride("focus",   FocusOutline());
    }

    /// <summary>
    /// Habille le menu qui s'ouvre sous une liste déroulante — un <see cref="PopupMenu"/> est un
    /// contrôle distinct du bouton, que styliser ce dernier ne touche pas.
    /// </summary>
    public static void ApplyPopupMenuStyles(PopupMenu menu, Color? accent = null)
    {
        var tint = accent ?? UiPalette.Cyan;

        var panel = new StyleBoxFlat { BgColor = UiPalette.Bg.Alpha(0.98f), AntiAliasing = false };
        panel.SetCornerRadiusAll(0);
        panel.SetBorderWidthAll(1);
        panel.BorderColor = tint.Alpha(0.7f);
        panel.SetContentMarginAll(6);
        menu.AddThemeStyleboxOverride("panel", panel);

        var hover = new StyleBoxFlat { BgColor = tint.Alpha(0.22f), AntiAliasing = false };
        hover.SetCornerRadiusAll(0);
        menu.AddThemeStyleboxOverride("hover", hover);

        menu.AddThemeColorOverride("font_color", UiPalette.OffWhite);
        menu.AddThemeColorOverride("font_hover_color", tint);
        menu.AddThemeColorOverride("font_separator_color", UiPalette.SteelHighlight);
    }

    /// <summary>Contour de focus léger, pour les contrôles sans cadre propre (interrupteurs).</summary>
    private static StyleBoxFlat FocusOutline()
    {
        var box = new StyleBoxFlat { BgColor = Colors.Transparent, AntiAliasing = false };
        box.SetCornerRadiusAll(0);
        box.SetBorderWidthAll(2);
        box.BorderColor = UiPalette.Violet;
        return box;
    }

    private static void LoadIcon(string file, out Texture2D? texture)
    {
        var path = FramesDir + file + ".png";
        texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (texture is null)
            GD.PushWarning($"UiStyle : widget introuvable ({path}) — régénérer via tools/generate_ui_widgets.py");
    }

    /// <summary>
    /// Cadre compact, pour les éléments trop petits pour l'anatomie complète de la plaque :
    /// puces de perk/titre, boutons de drapeau, badges. La bande de 16 px d'un cadre 9-slice y
    /// laisserait le texte sous le liseré (le contenu commence avant lui) et le chanfrein
    /// deviendrait du bruit — le brief exclut d'ailleurs les rivets en dessous de 64 px (§3.1).
    /// On garde donc l'angle droit et le bevel de la charte, sans la texture.
    /// </summary>
    public static StyleBoxFlat CompactFrame(Color accent, bool selected = false)
    {
        var box = new StyleBoxFlat
        {
            BgColor      = UiPalette.Steel.Alpha(selected ? 0.95f : 0.75f),
            AntiAliasing = false,
            BorderColor  = selected ? accent : accent.Alpha(0.55f),
        };
        box.SetCornerRadiusAll(0);
        box.SetBorderWidthAll(selected ? 2 : 1);
        box.SetContentMarginAll(8);
        return box;
    }

    /// <summary>
    /// Cadre de carte dont le liseré porte une <b>couleur de catégorie</b> libre plutôt qu'une
    /// rareté — cas explicitement prévu par le §3.5 (« couleur de catégorie » pour les chips) et
    /// seul cas possible pour les entrées de codex, les biomes et les personnages, dont l'accent
    /// est une teinte de données, pas un membre de <see cref="CardRarity"/>.
    ///
    /// <para>Réutilise les textures d'accent : les familles <c>button</c> et <c>card</c> ont la
    /// <b>même géométrie</b> (<c>tools/generate_ui_frames.py</c> : canvas 48, bande 16, chanfrein
    /// 10, rivets TL/BR) — seule la palette du liseré diffère. Passer par
    /// <see cref="CardRarity"/> ici écraserait tous les accents sur trois teintes
    /// (gris/bleu/violet) et reperdrait la catégorie que le liseré doit justement rendre.</para>
    /// </summary>
    public static StyleBoxTexture CardFrame(Color accent, FrameState state = FrameState.Normal, bool focus = false) =>
        ButtonFrame(NearestAccent(accent), state, focus);

    /// <summary>
    /// Câble d'un coup les cinq états d'un bouton (§3.2) et la pulsation de focus. Remplace les
    /// fabriques locales <c>MakeBtnStyle</c>/<c>BtnStyle</c>/<c>StyleButton</c> qui étaient
    /// recopiées à l'identique d'un écran à l'autre.
    ///
    /// <para>Chaque état reçoit sa <b>propre instance</b> de <see cref="StyleBoxTexture"/> :
    /// Godot lie les instances partagées et casse alors hover/pressed
    /// (cf. <c>docs/PITFALLS.md</c> § UI).</para>
    /// </summary>
    /// <param name="accent">Accent de catégorie du bouton au repos.</param>
    public static void ApplyButtonFrames(Button button, FrameAccent accent = FrameAccent.Cyan)
    {
        button.AddThemeStyleboxOverride("normal",   ButtonFrame(accent));
        button.AddThemeStyleboxOverride("hover",    ButtonFrame(accent, FrameState.Hover));
        button.AddThemeStyleboxOverride("pressed",  ButtonFrame(accent, FrameState.Pressed));
        // Le focus se superpose à l'état courant : violet Aether (§3.0) + débordement de forme.
        button.AddThemeStyleboxOverride("focus",    ButtonFrame(FrameAccent.Violet, FrameState.Hover, focus: true));
        button.AddThemeStyleboxOverride("disabled", ButtonFrameDisabled());
        AttachFocusPulse(button);
    }

    /// <summary>Variante à accent dynamique (teinte de biome, de personnage, de greffe).</summary>
    public static void ApplyButtonFrames(Button button, Color accent) =>
        ApplyButtonFrames(button, NearestAccent(accent));

    /// <summary>
    /// Cadre de popup/modale (§3.4) : bande élargie à 20 px et bord soudé <b>en haut</b> — une
    /// modale se lit comme un panneau suspendu, titre en tête.
    /// </summary>
    /// <param name="cyan">Variante cyan (level-up, modales non-Aether) ; violet par défaut.</param>
    public static StyleBoxTexture PopupFrame(bool cyan = false) =>
        BuildTextureBox($"ui_frame_popup_{(cyan ? "cyan" : "violet")}",
                        PopupMargin, PopupWeldTop, PopupMargin, PopupMargin);

    /// <summary>
    /// Ombre portée dure d'une modale (§3.4) : pas de flou gaussien — un simple doublon
    /// décalé de +6 px, à insérer <b>avant</b> le panneau dans l'ordre des enfants.
    /// </summary>
    public static StyleBoxFlat PopupHardShadow()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.45f), AntiAliasing = false };
        box.SetCornerRadiusAll(0);
        return box;
    }

    /// <summary>Décalage de <see cref="PopupHardShadow"/>, en pixels.</summary>
    public static readonly Vector2 PopupShadowOffset = new(6, 6);

    /// <summary>
    /// Branche la pulsation de focus du §3.2 : l'alpha oscille de 60 % à 100 % sur 0,6 s tant que
    /// le contrôle a le focus. Troisième signal, avec le débordement de forme et l'opacité du
    /// liseré — le focus ne repose ainsi jamais sur la seule teinte.
    /// </summary>
    /// <remarks>
    /// Le tween est créé en mode <see cref="Tween.TweenPauseMode.Process"/> : les modales
    /// (PauseScreen, LevelUpScreen, AssimilationScreen) tournent avec l'arbre en pause, où un
    /// tween par défaut serait gelé (cf. <c>docs/PITFALLS.md</c> § Scènes / cycle de vie).
    /// </remarks>
    public static void AttachFocusPulse(Control control)
    {
        Tween? pulse = null;

        control.FocusEntered += () =>
        {
            pulse?.Kill();
            pulse = control.CreateTween();
            pulse.SetPauseMode(Tween.TweenPauseMode.Process);
            pulse.SetLoops();
            pulse.TweenProperty(control, "self_modulate:a", 0.6f, 0.3f);
            pulse.TweenProperty(control, "self_modulate:a", 1.0f, 0.3f);
        };

        control.FocusExited += () =>
        {
            pulse?.Kill();
            pulse = null;
            control.SelfModulate = new Color(control.SelfModulate, 1f);
        };

        control.TreeExiting += () => pulse?.Kill();
    }

    /// <summary>Accent le plus proche d'une couleur libre — pour les appelants dont l'accent est
    /// dynamique (teinte de personnage, de biome, de greffe) et qui ne peuvent pas choisir un
    /// membre de <see cref="FrameAccent"/> à la compilation.</summary>
    public static FrameAccent NearestAccent(Color color)
    {
        var best = FrameAccent.Cyan;
        var bestDistance = float.MaxValue;
        foreach (var (candidate, reference) in new[]
                 {
                     (FrameAccent.Cyan,   UiPalette.Cyan),
                     (FrameAccent.Violet, UiPalette.Violet),
                     (FrameAccent.Gold,   UiPalette.Gold),
                     (FrameAccent.Danger, UiPalette.Amber),
                 })
        {
            var distance = Mathf.Abs(color.H - reference.H);
            if (distance > 0.5f) distance = 1f - distance;   // la teinte est circulaire
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = candidate;
        }
        return best;
    }

    private static Color StateModulate(FrameState state) => state switch
    {
        FrameState.Hover   => new Color(1.15f, 1.15f, 1.15f),   // §3.2 — léger éclat
        FrameState.Pressed => new Color(0.80f, 0.80f, 0.80f),   // §3.2 — plaque enfoncée
        _                  => Colors.White,
    };

    private static StyleBoxTexture BuildTextureBox(string file, int left, int top, int right, int bottom)
    {
        var box = new StyleBoxTexture
        {
            TextureMarginLeft   = left,
            TextureMarginTop    = top,
            TextureMarginRight  = right,
            TextureMarginBottom = bottom,
        };

        // ResourceLoader.Exists, jamais FileAccess.FileExists : en build exporté le PNG source
        // n'est pas dans le .pck, seule la texture importée l'est (cf. docs/PITFALLS.md § Assets).
        var path = FramesDir + file + ".png";
        if (ResourceLoader.Exists(path))
            box.Texture = GD.Load<Texture2D>(path);
        else
            GD.PushWarning($"UiStyle : cadre introuvable ({path}) — régénérer via tools/generate_ui_frames.py");

        return box;
    }

    /// <summary>Couleur de la palette correspondant à une famille de cadre.</summary>
    public static Color AccentColor(FrameAccent accent) => accent switch
    {
        FrameAccent.Violet => UiPalette.Violet,
        FrameAccent.Gold   => UiPalette.Gold,
        FrameAccent.Danger => UiPalette.Amber,
        _                  => UiPalette.Cyan,
    };

    private static string Slug(FrameAccent accent) => accent switch
    {
        FrameAccent.Violet => "violet",
        FrameAccent.Gold   => "or",
        FrameAccent.Danger => "danger",
        _                  => "cyan",
    };

    private static string Slug(CardRarity rarity) => rarity switch
    {
        CardRarity.Rare => "rare",
        CardRarity.Epic => "epic",
        _               => "common",
    };
}
