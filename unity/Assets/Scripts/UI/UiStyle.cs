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
/// <para><b>Écart assumé avec Godot</b> : les cadres « plaque blindée » y sont des textures
/// 9-slice générées par un script Python. Ils sont reproduits ici par des <c>Image</c> superposées
/// (fond, liseré, ombre portée) plutôt que par les mêmes textures : cela évite de dépendre d'assets
/// dont la découpe 9-slice devrait être re-paramétrée à la main, et reste modifiable en un point.
/// La texture d'origine pourra remplacer ce rendu sans changer aucun appelant.</para>
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
        border.AddComponent<Image>().color = ColorOf(accent);

        var fill = NewUiObject("Fill", border.transform);
        fill.AddComponent<Image>().color = UiPalette.PanelBg;
        Stretch(fill, BorderWidth);

        return border;
    }

    /// <summary>Bouton stylé, avec ses états de survol et de pression.</summary>
    public static Button TextButton(Transform parent, string label, FrameAccent accent = FrameAccent.Cyan)
    {
        var go = Panel(parent, "Button_" + label, accent);
        var button = go.AddComponent<Button>();

        var fill = go.transform.GetChild(0).GetComponent<Image>();
        button.targetGraphic = fill;

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
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
