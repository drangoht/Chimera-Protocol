using UnityEngine;
using UnityEngine.UI;

/// <summary>Accent d'un cadre — détermine sa couleur de liseré.</summary>
public enum FrameAccent { Cyan, Violet, Gold, Steel, Danger }

/// <summary>
/// Fabrique unique des éléments d'interface — équivalent d'<c>UiStyle</c> (Lot 5).
///
/// <para>Règle du projet : <b>aucun style ad hoc</b>. Tout panneau, bouton ou séparateur passe par
/// ici. C'est ce qui a permis, côté Godot, de refondre l'habillage complet de 18 écrans sans les
/// rouvrir un par un.</para>
///
/// <para><b>Les cadres « plaque blindée » sont les textures d'origine</b> — chanfreins, biseau,
/// rivets — découpées en neuf zones (bordures réglées à l'import, cf.
/// <c>UiFrameImportPostprocessor</c>). Le rectangle plat n'est plus qu'un <b>repli</b>.</para>
///
/// <para>⚠ Ces textures étaient importées, découpées et <b>vérifiées au banc</b> depuis le lot 5 —
/// mais <b>aucun écran ne les utilisait</b> : la fabrique dessinait un liseré plat. Le banc
/// contrôlait que les sprites existaient et portaient une bordure, pas qu'ils soient <i>affichés</i>.
/// C'est exactement le mode de défaillance déjà rencontré avec le compteur de sons qui montait sans
/// qu'aucun son ne sorte : <b>une vérification qui n'observe pas le résultat final ne prouve
/// rien</b>.</para>
///
/// <para>⚠ Le repli sur le rendu plat subsiste, et il est <b>volontaire</b> : une texture absente
/// donnerait sinon des panneaux invisibles — c'est-à-dire des écrans qui paraissent vides.</para>
/// </summary>
public static class UiStyle
{
    /// <summary>Épaisseur du liseré du repli plat, en pixels de référence.</summary>
    public const float BorderWidth = 2f;

    /// <summary>Marge intérieure standard d'un panneau.</summary>
    public const float PanelPadding = 24f;

    /// <summary>Marge entre le texte d'un bouton et son liseré.</summary>
    public const float ButtonTextPadding = 14f;

    /// <summary>Sprite blanc partagé — voir <see cref="UiPrimitives.White"/>.</summary>
    public static Sprite WhiteSprite => UiPrimitives.White;

    private static readonly System.Collections.Generic.Dictionary<string, Sprite?> FrameCache = new();

    /// <summary>
    /// Charge un cadre par son nom de fichier. Le résultat est mis en cache, y compris l'échec :
    /// un cadre manquant est demandé à chaque bouton de chaque écran, et retenter le chargement
    /// coûterait un accès disque par bouton.
    /// </summary>
    public static Sprite? Frame(string name)
    {
        if (FrameCache.TryGetValue(name, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>("UiFrames/" + name);
        if (sprite == null)
            Debug.LogWarning($"[UiStyle] cadre introuvable : UiFrames/{name} — repli en liseré plat.");

        FrameCache[name] = sprite;
        return sprite;
    }

    /// <summary>Cadre de bouton d'un accent — utile pour border un élément qui n'est pas un bouton.</summary>
    public static Sprite? ButtonFrame(FrameAccent accent, bool focus = false)
        => Frame($"ui_frame_button_{Slug(accent)}{(focus ? "_focus" : "")}");

    /// <summary>Suffixe de fichier d'un accent. Le doré s'écrit « or », comme sous Godot.</summary>
    private static string Slug(FrameAccent accent) => accent switch
    {
        FrameAccent.Violet => "violet",
        FrameAccent.Gold   => "or",
        FrameAccent.Danger => "danger",
        _                  => "cyan",
    };

    /// <summary>
    /// Applique un cadre 9 zones à une image. Renvoie <c>false</c> si la texture manque, à charge
    /// de l'appelant de dessiner son repli.
    /// </summary>
    /// <remarks>
    /// La teinte reste <b>blanche</b> : ces PNG portent déjà leur couleur d'accent. Les teinter une
    /// seconde fois donnait des cadres saturés et faux — l'un des deux défauts qui avaient fait
    /// abandonner les textures au lot 5.
    /// </remarks>
    private static bool ApplyFrame(Image image, string frameName)
    {
        var sprite = Frame(frameName);
        if (sprite == null) return false;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        // Sans cela, une Image dont le sprite est plus petit que ses bordures cumulées disparaît
        // purement et simplement — c'est le cas de tout bouton de moins de 32 px de large.
        image.fillCenter = true;
        return true;
    }

    /// <summary>Couleur associée à un accent.</summary>
    public static Color ColorOf(FrameAccent accent) => accent switch
    {
        FrameAccent.Cyan   => UiPalette.Cyan,
        FrameAccent.Violet => UiPalette.Violet,
        FrameAccent.Gold   => UiPalette.Gold,
        FrameAccent.Danger => UiPalette.Danger,
        _                  => UiPalette.SteelHighlight,
    };

    /// <summary>
    /// Panneau : un fond sombre bordé d'un liseré d'accent. Le liseré est un enfant étiré, et non
    /// un contour dessiné : il reste net à toute échelle.
    /// </summary>
    public static GameObject Panel(Transform parent, string name, FrameAccent accent = FrameAccent.Steel)
    {
        var go = NewUiObject(name, parent);
        var image = go.AddComponent<Image>();

        // Deux cadres de panneau seulement, cyan et violet, comme sous Godot : un panneau annonce
        // un contexte (jeu / chimère), pas une catégorie d'action. Les accents doré et danger
        // appartiennent aux boutons.
        string frame = accent == FrameAccent.Violet ? "ui_frame_popup_violet" : "ui_frame_popup_cyan";

        if (ApplyFrame(image, frame)) return go;

        // Repli plat : liseré d'accent + fond sombre.
        image.color = ColorOf(accent);

        var fill = NewUiObject("Fill", go.transform);
        fill.AddComponent<Image>().color = UiPalette.PanelBg;
        Stretch(fill, BorderWidth);

        return go;
    }

    /// <summary>Bouton stylé, avec ses états de survol, de pression et de focus.</summary>
    public static Button TextButton(Transform parent, string label, FrameAccent accent = FrameAccent.Cyan)
    {
        var go = NewUiObject("Button_" + label, parent);
        var image = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        if (ApplyFrame(image, $"ui_frame_button_{Slug(accent)}"))
        {
            button.transition = Selectable.Transition.SpriteSwap;

            var state = button.spriteState;
            state.highlightedSprite = Frame($"ui_frame_button_{Slug(accent)}_focus");
            state.selectedSprite    = state.highlightedSprite;
            state.pressedSprite     = state.highlightedSprite;
            state.disabledSprite    = Frame("ui_frame_button_disabled");
            button.spriteState = state;

            // ⚠ Le focus s'annonce en VIOLET, quel que soit l'accent du bouton — c'est la règle du
            // jeu publié (§3.2), et elle n'est pas décorative : dans un menu où chaque entrée porte
            // déjà sa couleur, « ma variante _focus est plus lumineuse que ma variante normale » ne
            // se compare pas d'un bouton à l'autre. Le portage avait gardé la couleur du bouton, et
            // le déplacement du focus en devenait invisible.
            AttachFocusPulse(button, image, Frame("ui_frame_button_violet_focus"));
        }
        else
        {
            image.color = ColorOf(accent);

            var fill = NewUiObject("Fill", go.transform);
            fill.AddComponent<Image>().color = UiPalette.PanelBg;
            Stretch(fill, BorderWidth);
            button.targetGraphic = fill.GetComponent<Image>();

            var colors = button.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor    = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.selectedColor    = colors.highlightedColor;
            button.colors = colors;
        }

        // Le texte respire : sans cette marge, un libellé long touche le liseré et paraît coupé.
        var text = Label(go.transform, label, 20, UiPalette.OffWhite, TextAnchor.MiddleCenter);
        Stretch(text.gameObject, ButtonTextPadding);

        return button;
    }

    /// <summary>
    /// Carte <b>non cliquable</b> — une entrée de liste qui porte un liseré d'état : défi accompli
    /// ou non, niveau vaincu ou non.
    ///
    /// <para>Elle emprunte le cadre des <b>boutons</b> et non celui des panneaux, pour une raison
    /// concrète : les panneaux n'existent qu'en cyan et violet (ils annoncent un contexte), alors
    /// qu'une carte d'état a besoin du <b>doré</b> — c'est la couleur que le jeu publié emploie pour
    /// « acquis ».</para>
    /// </summary>
    public static GameObject Card(Transform parent, string name, FrameAccent accent)
    {
        var go = NewUiObject("Card_" + name, parent);
        var image = go.AddComponent<Image>();

        if (ApplyFrame(image, $"ui_frame_button_{Slug(accent)}")) return go;

        // Repli plat : liseré d'accent + fond sombre.
        image.color = ColorOf(accent);

        var fill = NewUiObject("Fill", go.transform);
        fill.AddComponent<Image>().color = UiPalette.PanelBg;
        Stretch(fill, BorderWidth);

        return go;
    }

    /// <summary>
    /// Carte sélectionnable (montée de niveau, codex) : même anatomie qu'un bouton, liseré à la
    /// couleur de <b>rareté</b>. C'est ce liseré qui dit la valeur d'une carte avant qu'on l'ait lue.
    /// </summary>
    public static Button CardButton(Transform parent, string name, string rarity)
    {
        var go = NewUiObject("Card_" + name, parent);
        var image = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        string slug = rarity switch
        {
            "epic" or "legendary" => "epic",
            "rare" or "uncommon"  => "rare",
            _                     => "common",
        };

        if (ApplyFrame(image, $"ui_frame_card_{slug}"))
        {
            button.transition = Selectable.Transition.SpriteSwap;

            var state = button.spriteState;
            state.highlightedSprite = Frame($"ui_frame_card_{slug}_focus");
            state.selectedSprite    = state.highlightedSprite;
            state.pressedSprite     = state.highlightedSprite;
            state.disabledSprite    = Frame("ui_frame_card_disabled");
            button.spriteState = state;

            // Une carte garde sa RARETÉ jusque dans son anneau de focus : cette couleur dit la
            // valeur de la carte, et l'écraser en violet effacerait l'information au moment même où
            // le joueur arbitre.
            AttachFocusPulse(button, image, Frame($"ui_frame_card_{slug}_focus"));
        }
        else
        {
            image.color = UiPalette.ForRarity(rarity);
        }

        return button;
    }

    /// <summary>
    /// Branche le signal de focus — teinte, débordement et pulsation. Voir <see cref="UiFocusPulse"/>
    /// pour la raison des trois signaux cumulés.
    /// </summary>
    private static void AttachFocusPulse(Button button, Image image, Sprite? focusFrame)
        => button.gameObject.AddComponent<UiFocusPulse>().Bind(button, image, focusFrame);

    /// <summary>Texte courant. Le corps par défaut correspond à celui du jeu publié.</summary>
    public static Text Label(Transform parent, string content, int size = 20,
                             Color? color = null, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        var go = NewUiObject("Label", parent);
        var text = go.AddComponent<Text>();

        // ⚠ Police intégrée : la police définitive (Share Tech Mono) demande un asset TextMeshPro,
        // travail identifié au §7.6 du plan. Tout le reste — corps, couleur, alignement — est déjà
        // à sa valeur finale.
        text.font = UiFonts.Main;
        text.fontSize = size;
        text.color = color ?? UiPalette.OffWhite;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = content;

        return text;
    }

    /// <summary>
    /// Ligne de réglage : <b>libellé à gauche, contrôle à droite</b>. C'est la disposition du jeu
    /// publié, et elle n'est pas cosmétique — un écran d'options se parcourt en lisant la colonne de
    /// gauche, puis en ne regardant à droite que la ligne qu'on veut changer. Le portage empilait des
    /// boutons « Étiquette : valeur » pleine largeur : rien ne distinguait ce qui se règle de ce qui
    /// s'annonce.
    /// </summary>
    /// <returns>Le conteneur du contrôle, à droite, dans lequel l'appelant pose ce qu'il veut.</returns>
    public static RectTransform SettingRow(Transform parent, string label, float labelWidth = 340f,
                                           float height = 56f)
    {
        var row = NewUiObject("Row_" + label, parent);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;

        var text = Label(row.transform, label, 22, UiPalette.OffWhite, TextAnchor.MiddleLeft);
        var textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(labelWidth, 0f);
        textRect.anchoredPosition = Vector2.zero;

        var control = NewUiObject("Control", row.transform).GetComponent<RectTransform>();
        control.anchorMin = new Vector2(0f, 0f);
        control.anchorMax = new Vector2(1f, 1f);
        control.offsetMin = new Vector2(labelWidth + 16f, 0f);
        control.offsetMax = Vector2.zero;

        return control;
    }

    /// <summary>
    /// Curseur horizontal — piste, remplissage et poignée, aux textures du jeu.
    ///
    /// <para>Le portage réglait les volumes par <b>paliers de 25 %</b> sur un bouton cyclique, au
    /// motif qu'un curseur se manie mal à la manette. C'est vrai d'un curseur qu'on <i>attrape</i> ;
    /// celui-ci est un <c>Selectable</c> comme les autres, que les flèches gauche/droite déplacent —
    /// donc jouable à la manette, et il montre en plus <b>où l'on se trouve dans la course</b>, ce
    /// qu'aucun palier ne dit.</para>
    /// </summary>
    public static Slider HSlider(Transform parent, float value, UnityEngine.Events.UnityAction<float> onChanged)
    {
        var go = NewUiObject("Slider", parent);
        var slider = go.AddComponent<Slider>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(380f, 22f);
        rect.anchoredPosition = Vector2.zero;

        // Piste creusée, puis remplissage cyan : la part parcourue se lit sans chercher la poignée.
        var track = NewUiObject("Track", go.transform);
        track.AddComponent<Image>().color = UiPalette.PanelSunken;
        var trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        trackRect.anchoredPosition = Vector2.zero;

        var fillArea = NewUiObject("FillArea", go.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(0f, 6f);
        fillAreaRect.anchoredPosition = Vector2.zero;

        var fill = NewUiObject("Fill", fillArea.transform);
        fill.AddComponent<Image>().color = UiPalette.Cyan;
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.sizeDelta = new Vector2(0f, 0f);

        // La zone de la poignée est rétrécie d'une demi-poignée à chaque bout : sans cette marge, la
        // poignée déborde de la piste aux deux extrémités de la course.
        var handleArea = NewUiObject("HandleArea", go.transform);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(9f, 0f);
        handleAreaRect.offsetMax = new Vector2(-9f, 0f);

        var handle = NewUiObject("Handle", handleArea.transform);
        var handleImage = handle.AddComponent<Image>();
        var grabber = Frame("ui_slider_grabber");
        if (grabber != null) handleImage.sprite = grabber;
        else handleImage.color = UiPalette.Cyan;

        // ⚠ La poignée s'ancre en HAUTEUR sur sa zone (0..1) et ne fixe que sa largeur : Unity pilote
        // lui-même son ancrage horizontal en fonction de la valeur. Lui donner une hauteur en dur
        // l'étire — le premier essai donnait un rectangle deux fois plus haut que large là où le jeu
        // publié montre un petit carré.
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0f);
        handleRect.anchorMax = new Vector2(0f, 1f);
        handleRect.sizeDelta = new Vector2(18f, 0f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;

        var hot = Frame("ui_slider_grabber_focus");
        if (grabber != null && hot != null)
        {
            slider.transition = Selectable.Transition.SpriteSwap;
            var state = slider.spriteState;
            state.highlightedSprite = hot;
            state.selectedSprite = hot;
            state.pressedSprite = hot;
            slider.spriteState = state;
        }

        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    /// <summary>
    /// Interrupteur à deux états, aux textures du jeu (<c>ui_toggle_on</c> / <c>ui_toggle_off</c>).
    /// Un interrupteur dit son état par sa <b>forme</b> — le curseur est à gauche ou à droite — là où
    /// « Activé / Désactivé » demande de lire un mot.
    /// </summary>
    public static Toggle Switch(Transform parent, bool value, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        var go = NewUiObject("Toggle", parent);
        var toggle = go.AddComponent<Toggle>();

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(66f, 34f);
        rect.anchoredPosition = Vector2.zero;

        var background = go.AddComponent<Image>();
        var off = Frame("ui_toggle_off");
        background.sprite = off;
        background.color = off != null ? Color.white : UiPalette.PanelSunken;

        // ⚠ L'état allumé est une TEXTURE posée par-dessus, et non une teinte : `graphic` est ce
        // qu'Unity affiche quand la case est cochée, et le laisser vide donne un interrupteur qui ne
        // bouge jamais — coché ou non, la même image.
        var onGo = NewUiObject("On", go.transform);
        var onImage = onGo.AddComponent<Image>();
        var on = Frame("ui_toggle_on");
        onImage.sprite = on;
        onImage.color = on != null ? Color.white : UiPalette.Cyan;
        Stretch(onGo, 0f);

        toggle.targetGraphic = background;
        toggle.graphic = onImage;
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(onChanged);

        return toggle;
    }

    /// <summary>
    /// Sélecteur de valeur : un bouton qui affiche l'option courante et un chevron, et fait défiler
    /// au clic ou aux flèches.
    ///
    /// <para>Le jeu publié emploie ici une vraie liste déroulante. Celle-ci en garde
    /// l'<b>apparence fermée</b> — cadre, valeur, chevron — mais pas le déroulé : une liste ouverte
    /// se manie à la souris, et cet écran doit rester intégralement jouable à la manette, où
    /// « suivant / précédent » est le geste naturel. Le nombre d'options est de trois.</para>
    /// </summary>
    public static Button Selector(Transform parent, string value, UnityEngine.Events.UnityAction onNext)
    {
        var button = TextButton(parent, value, FrameAccent.Cyan);

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 46f);
        rect.anchoredPosition = Vector2.zero;

        var chevron = Label(button.transform, "▼", 18, UiPalette.Cyan, TextAnchor.MiddleRight);
        chevron.raycastTarget = false;
        var chevronRect = chevron.GetComponent<RectTransform>();
        chevronRect.anchorMin = new Vector2(1f, 0f);
        chevronRect.anchorMax = new Vector2(1f, 1f);
        chevronRect.pivot = new Vector2(1f, 0.5f);
        chevronRect.sizeDelta = new Vector2(34f, 0f);
        chevronRect.anchoredPosition = new Vector2(-12f, 0f);

        button.onClick.AddListener(onNext);
        return button;
    }

    /// <summary>Séparateur horizontal teinté.</summary>
    public static GameObject Separator(Transform parent, Color accent, float height = 2f)
    {
        var go = NewUiObject("Separator", parent);
        go.AddComponent<Image>().color = UiPalette.WithAlpha(accent, 0.6f);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;

        return go;
    }

    /// <summary>
    /// Fond <b>opaque</b> des écrans secondaires — Hub, Options, Codex, Défis, choix du niveau.
    ///
    /// <para>Sous Godot ce sont de vraies scènes : elles <b>remplacent</b> le menu. Le portage les
    /// empile en surcouche translucide, si bien que le logo néon et la Chimère continuaient de
    /// transparaître derrière un tableau d'améliorations — l'œil ne savait plus quoi lire. Un voile
    /// ne remplace pas un écran : il annonce « ceci est temporaire, ce qu'il y a derrière compte
    /// encore », ce qui est faux ici.</para>
    /// </summary>
    public static GameObject ScreenBackdrop(Transform parent)
    {
        var go = NewUiObject("Backdrop", parent);
        go.AddComponent<Image>().color = UiPalette.BgDeep;
        Stretch(go, 0f);
        return go;
    }

    /// <summary>
    /// En-tête d'écran : titre centré et <b>liseré d'accent</b> juste dessous, à crans aux extrémités.
    ///
    /// <para>Ce liseré n'est pas un ornement : c'est lui qui sépare l'en-tête du contenu et donne à
    /// tous les écrans la même charpente. Sans lui, un titre flotte au-dessus d'une liste et l'écran
    /// paraît inachevé — c'est ce qui distinguait le plus les captures du portage de celles du jeu.</para>
    /// </summary>
    /// <returns>L'ordonnée du bas de l'en-tête, à partir du haut du panneau, en pixels de référence.</returns>
    public static float Header(Transform panel, string title, FrameAccent accent = FrameAccent.Cyan,
                               int size = 40, float top = 22f)
    {
        var color = ColorOf(accent);

        var label = Label(panel, title, size, color, TextAnchor.UpperCenter);
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(36f, -(top + size + 12f));
        labelRect.offsetMax = new Vector2(-36f, -top);

        float lineY = top + size + 20f;

        var line = NewUiObject("HeaderRule", panel);
        line.AddComponent<Image>().color = WithAlphaSafe(color, 0.85f);
        var lineRect = line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.offsetMin = new Vector2(36f, -(lineY + 2f));
        lineRect.offsetMax = new Vector2(-36f, -lineY);

        // Crans d'extrémité : le détail qui fait lire une pièce d'interface montée plutôt qu'un
        // trait tracé. Ils reprennent les embouts du liseré de Godot.
        foreach (float side in new[] { 0f, 1f })
        {
            var cap = NewUiObject("RuleCap", panel);
            cap.AddComponent<Image>().color = color;

            var capRect = cap.GetComponent<RectTransform>();
            capRect.anchorMin = capRect.anchorMax = new Vector2(side, 1f);
            capRect.pivot = new Vector2(side, 1f);
            capRect.sizeDelta = new Vector2(7f, 7f);
            capRect.anchoredPosition = new Vector2(side == 0f ? 36f : -36f, -(lineY - 2f));
        }

        return lineY + 12f;
    }

    private static Color WithAlphaSafe(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

    /// <summary>Voile plein écran qui assombrit le jeu derrière une modale.</summary>
    public static GameObject Scrim(Transform parent, float opacity = 0.72f)
    {
        var go = NewUiObject("Scrim", parent);
        go.AddComponent<Image>().color = UiPalette.WithAlpha(UiPalette.BgDeep, opacity);
        Stretch(go, 0f);
        return go;
    }

    /// <summary>Crée un objet d'interface avec son <c>RectTransform</c>.</summary>
    public static GameObject NewUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        return go;
    }

    /// <summary>Étire un élément sur tout son parent, avec une marge uniforme.</summary>
    public static void Stretch(GameObject go, float margin)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }
}
