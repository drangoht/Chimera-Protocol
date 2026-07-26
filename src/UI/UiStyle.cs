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
    private const int ButtonWeldBottom = 22;

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
