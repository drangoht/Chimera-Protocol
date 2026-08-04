using UnityEngine;

/// <summary>
/// Palette de l'interface — <b>source unique</b> des couleurs (Lot 5).
///
/// <para>Règle du projet, reprise telle quelle : <b>aucune teinte n'est écrite en dur</b>, ni dans
/// le C#, ni dans les scènes. Une couleur en dur échappe à toute retouche d'ensemble et produit,
/// écran après écran, une dérive que personne ne voit venir — c'est ce qui avait motivé la
/// centralisation côté Godot.</para>
///
/// <para>Les valeurs sont celles du jeu publié, converties depuis leurs codes hexadécimaux.</para>
/// </summary>
public static class UiPalette
{
    // ─── Accents ──────────────────────────────────────────────────────────────

    /// <summary>Cyan #44FFEE — accent principal, énergie et interactif.</summary>
    public static readonly Color Cyan = Rgb(0x44, 0xFF, 0xEE);

    /// <summary>Violet #AA44FF — accent secondaire, chimère et greffes.</summary>
    public static readonly Color Violet = Rgb(0xAA, 0x44, 0xFF);

    /// <summary>Or #FFCC44 — récompense, progression, Échos.</summary>
    public static readonly Color Gold = Rgb(0xFF, 0xCC, 0x44);

    /// <summary>Blanc cassé #D9D9F2 — texte courant. Jamais de blanc pur : trop dur sur ce fond.</summary>
    public static readonly Color OffWhite = Rgb(0xD9, 0xD9, 0xF2);

    // ─── Fonds ────────────────────────────────────────────────────────────────

    /// <summary>Fond de référence #1A1A2E.</summary>
    public static readonly Color Bg = Rgb(0x1A, 0x1A, 0x2E);

    public static readonly Color BgDeep      = new(0.06f, 0.06f, 0.11f, 1f);
    public static readonly Color PanelBg     = new(0.10f, 0.10f, 0.18f, 0.92f);
    public static readonly Color PanelSunken = new(0.03f, 0.03f, 0.08f, 1f);

    // ─── Métal (cadres « plaque blindée ») ────────────────────────────────────

    public static readonly Color Steel          = Rgb(0x24, 0x24, 0x40);
    public static readonly Color SteelHighlight = Rgb(0x3A, 0x3A, 0x5C);
    public static readonly Color SteelShadow    = Rgb(0x12, 0x12, 0x23);
    public static readonly Color SteelContact   = Rgb(0x0B, 0x0B, 0x16);
    public static readonly Color Amber          = Rgb(0x99, 0x7A, 0x1E);

    // ─── États ────────────────────────────────────────────────────────────────

    public static readonly Color Dim     = new(0.55f, 0.57f, 0.66f, 1f);
    public static readonly Color Danger  = new(1f,    0.55f, 0.20f, 1f);
    public static readonly Color Success = new(0.30f, 1f,    0.50f, 1f);
    public static readonly Color Magenta = new(0.85f, 0.30f, 0.80f, 1f);
    public static readonly Color Rust    = new(0.85f, 0.45f, 0.30f, 1f);

    /// <summary>Couleur d'accent associée à une rareté de carte.</summary>
    public static Color ForRarity(string rarity) => rarity switch
    {
        "common"    => OffWhite,
        "uncommon"  => Success,
        "rare"      => Cyan,
        "epic"      => Violet,
        "legendary" => Gold,
        _           => OffWhite,
    };

    /// <summary>Même couleur, à l'opacité demandée — évite d'écrire des variantes en dur.</summary>
    public static Color WithAlpha(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

    private static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);
}
