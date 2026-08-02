using Godot;

/// <summary>
/// Palette unique de l'interface. Avant cette classe, les mêmes couleurs étaient redéclarées
/// à la main dans 8 blocs <c>static readonly</c>, une vingtaine de littéraux inline et 8 lignes
/// de <c>.tscn</c> — avec des arrondis divergents (0.267 vs 0.27) et deux « fonds officiels »
/// concurrents. Toute couleur d'UI doit venir d'ici.
///
/// Référence : CLAUDE.md § Conventions — fond #1A1A2E, cyan #44FFEE, violet #AA44FF,
/// or #FFCC44, blanc cassé #D9D9F2.
/// </summary>
public static class UiPalette
{
    // ── Couleurs de marque ────────────────────────────────────────────────────────────────
    /// <summary>#44FFEE — accent principal : titres, bordures au repos, valeurs.</summary>
    public static readonly Color Cyan = Color.Color8(0x44, 0xFF, 0xEE);

    /// <summary>#AA44FF — accent d'interaction : survol, focus, sélection.</summary>
    public static readonly Color Violet = Color.Color8(0xAA, 0x44, 0xFF);

    /// <summary>#FFCC44 — valeur/récompense : Échos, coûts, défis accomplis.</summary>
    public static readonly Color Gold = Color.Color8(0xFF, 0xCC, 0x44);

    /// <summary>#D9D9F2 — texte courant.</summary>
    public static readonly Color OffWhite = Color.Color8(0xD9, 0xD9, 0xF2);

    /// <summary>#1A1A2E — fond de référence de la charte.</summary>
    public static readonly Color Bg = Color.Color8(0x1A, 0x1A, 0x2E);

    // ── Fonds dérivés ─────────────────────────────────────────────────────────────────────
    /// <summary>Fond plein écran des écrans de menu — plus sombre que <see cref="Bg"/> pour
    /// que les cadres posés dessus ressortent.</summary>
    public static readonly Color BgDeep = new(0.06f, 0.06f, 0.11f, 1f);

    // ⚠ Les deux fonds ci-dessous n'ont plus d'appelant depuis la refonte des cadres « plaque
    // blindée » (1.16.0) : les panneaux sont désormais des StyleBoxTexture fabriqués par UiStyle.
    // Ils sont CONSERVÉS parce que ce fichier est la charte de couleurs du jeu, pas un utilitaire —
    // il documente les valeurs de référence, et une teinte retirée ici se retrouve réinventée en dur
    // au premier écran qui en aura besoin (ce que les conventions du projet interdisent).

    /// <summary>Fond d'un cadre posé sur <see cref="BgDeep"/>.</summary>
    public static readonly Color PanelBg = new(0.10f, 0.10f, 0.18f, 0.92f);

    /// <summary>Fond d'un cadre enfoncé (état pressé).</summary>
    public static readonly Color PanelSunken = new(0.03f, 0.03f, 0.08f, 1f);

    // ── Acier des cadres (ART_BRIEF_UI_FRAMES §3.0) ───────────────────────────────────────
    // Teintes DÉRIVÉES du fond #1A1A2E par la même formule HSV que tools/pseudo3d_lib.shade() :
    // l'UI et les sprites partagent ainsi la même direction de lumière (haut-gauche 45°).
    // Aucune teinte franche ajoutée à la charte.

    /// <summary>#242440 — fill de base d'une plaque de cadre. Volontairement plus clair que
    /// <see cref="Bg"/> : un cadre doit se détacher du fond, pas s'y fondre.</summary>
    public static readonly Color Steel = Color.Color8(0x24, 0x24, 0x40);

    /// <summary>#3A3A5C — bevel des côtés éclairés (haut + gauche). <c>shade(Steel, "highlight")</c>.</summary>
    public static readonly Color SteelHighlight = Color.Color8(0x3A, 0x3A, 0x5C);

    /// <summary>#121223 — bevel des côtés ombrés (bas + droite). <c>shade(Steel, "shadow")</c>.</summary>
    public static readonly Color SteelShadow = Color.Color8(0x12, 0x12, 0x23);

    /// <summary>#0B0B16 — ligne de contact/pose : trait externe qui ancre la plaque sur le fond.
    /// <c>shade(Steel, "contact")</c>. Sans appelant C# (les cadres sont pré-rendus par
    /// <c>tools/generate_ui_frames.py</c>) : cette constante complète les <b>quatre faces</b> de
    /// l'ombrage pseudo-3D et sert de référence au générateur — la retirer briserait la
    /// correspondance documentée entre la charte et les PNG produits.</summary>
    public static readonly Color SteelContact = Color.Color8(0x0B, 0x0B, 0x16);

    /// <summary>#997A1E — ambre sourd : action destructrice/irréversible. Même teinte que
    /// <see cref="Gold"/>, assombrie — distingue « Acheter » (or vif) de « Réinitialiser »
    /// sans introduire de rouge/orange hors charte.</summary>
    public static readonly Color Amber = Color.Color8(0x99, 0x7A, 0x1E);

    // ── Couleurs sémantiques ──────────────────────────────────────────────────────────────
    /// <summary>Texte ou élément inactif / verrouillé.</summary>
    public static readonly Color Dim = new(0.55f, 0.57f, 0.66f, 1f);

    /// <summary>Action destructrice (réinitialisation des améliorations).</summary>
    public static readonly Color Danger = new(1f, 0.55f, 0.20f, 1f);

    /// <summary>Confirmation / valeur positive.</summary>
    public static readonly Color Success = new(0.30f, 1f, 0.50f, 1f);

    /// <summary>Chair greffée — identité visuelle de l'Assimilation.</summary>
    public static readonly Color Magenta = new(0.85f, 0.30f, 0.80f, 1f);

    /// <summary>Métal corrodé — second accent de l'Assimilation.</summary>
    public static readonly Color Rust = new(0.85f, 0.45f, 0.30f, 1f);

    // ── Utilitaires ───────────────────────────────────────────────────────────────────────

    /// <summary>Même teinte, opacité imposée. Évite les <c>new Color(c.R, c.G, c.B, a)</c> épars.</summary>
    public static Color Alpha(this Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    /// <summary>Assombrit vers le noir (<paramref name="amount"/> = 0 aucun effet, 1 = noir).</summary>
    public static Color Darken(this Color color, float amount) =>
        new(color.R * (1f - amount), color.G * (1f - amount), color.B * (1f - amount), color.A);

    /// <summary>Éclaircit vers le blanc (<paramref name="amount"/> = 0 aucun effet, 1 = blanc).</summary>
    public static Color Lighten(this Color color, float amount) => new(
        color.R + (1f - color.R) * amount,
        color.G + (1f - color.G) * amount,
        color.B + (1f - color.B) * amount,
        color.A);
}
