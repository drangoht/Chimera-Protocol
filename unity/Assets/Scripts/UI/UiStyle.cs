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
/// <c>UiFrameImportPostprocessor</c>). Un rectangle plat approché à la main était le rendu provisoire
/// du lot 5 ; il donnait une interface juste en couleurs et en ancrages, mais qui ne ressemblait pas
/// au jeu. Aucun appelant n'a eu à changer.</para>
///
/// <para>⚠ Le repli sur le rendu plat subsiste, et il est <b>volontaire</b> : une texture absente
/// donnerait sinon des panneaux invisibles — c'est-à-dire des écrans qui paraissent vides.</para>
/// </summary>
public static class UiStyle
{
    /// <summary>Épaisseur du liseré d'un cadre, en pixels de référence.</summary>
    public const float BorderWidth = 2f;

    /// <summary>Marge intérieure standard d'un panneau.</summary>
    public const float PanelPadding = 24f;

    /// <summary>Sprite blanc partagé — voir <see cref="UiPrimitives.White"/>.</summary>
    public static Sprite WhiteSprite => UiPrimitives.White;

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
        var border = NewUiObject(name, parent);
        var image = border.AddComponent<Image>();

        var frame = Frame($"ui_frame_popup_{Slug(accent)}", "ui_frame_popup_cyan");
        if (frame != null)
        {
            // 9-slice : les coins gardent leur taille, seuls les bords se répètent. C'est ce qui
            // permet au même cadre de 48 px d'habiller un panneau de 1280.
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.color = ColorOf(accent);
            return border;
        }

        // Repli plat — voir la note en tête de classe.
        image.color = ColorOf(accent);

        var fill = NewUiObject("Fill", border.transform);
        fill.AddComponent<Image>().color = UiPalette.PanelBg;
        Stretch(fill, BorderWidth);

        return border;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite?> _frames = new();

    /// <summary>
    /// Cadre par nom, avec repli. Chargé depuis <c>Resources/</c> : une texture rangée ailleurs
    /// n'existe pas à l'exécution — le piège qui avait rendu 40 jeux d'animations introuvables.
    /// </summary>
    private static Sprite? Frame(string name, string fallbackName)
    {
        if (_frames.TryGetValue(name, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>("UiFrames/" + name)
                  ?? Resources.Load<Sprite>("UiFrames/" + fallbackName);

        if (sprite == null)
            Debug.LogWarning($"[UiStyle] cadre '{name}' introuvable — rendu plat.");

        _frames[name] = sprite;
        return sprite;
    }

    private static string Slug(FrameAccent accent) => accent switch
    {
        FrameAccent.Cyan   => "cyan",
        FrameAccent.Violet => "violet",
        FrameAccent.Gold   => "or",
        FrameAccent.Danger => "danger",
        _                  => "disabled",
    };

    /// <summary>Bouton stylé, avec ses états de survol et de pression.</summary>
    public static Button TextButton(Transform parent, string label, FrameAccent accent = FrameAccent.Cyan)
    {
        var go = NewUiObject("Button_" + label, parent);
        var image = go.AddComponent<Image>();

        var frame = Frame($"ui_frame_button_{Slug(accent)}", "ui_frame_button_cyan");
        if (frame != null)
        {
            image.sprite = frame;
            image.type = Image.Type.Sliced;
        }
        else
        {
            image.color = ColorOf(accent);

            var flat = NewUiObject("Fill", go.transform);
            flat.AddComponent<Image>().color = UiPalette.PanelBg;
            Stretch(flat, BorderWidth);
        }

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        // Transition par couleur du fond : les cadres restent stables, seul le remplissage réagit —
        // sans quoi le liseré d'accent « clignoterait » au survol.
        var colors = button.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.selectedColor    = colors.highlightedColor;
        button.colors = colors;

        var text = Label(go.transform, label, 22, UiPalette.OffWhite, TextAnchor.MiddleCenter);
        Stretch(text.gameObject, 0f);

        return button;
    }

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
