using UnityEngine;

/// <summary>
/// Silhouettes d'une tourelle de la Ruche : un <b>châssis</b> ombré et un <b>canon</b> qui pivote.
///
/// <para><b>Pourquoi deux pièces et pas une.</b> La première version reprenait telle quelle la flèche
/// du jeu publié — trois polygones plats empilés, tournés vers la cible. C'était lisible et
/// parfaitement étranger au reste du jeu, dont <i>tous</i> les sprites suivent le brief pseudo-3D
/// (<c>docs/ART_BRIEF_PSEUDO3D.md</c>) : lumière venue du haut-gauche, ombre au bas, contact assombri
/// au sol.</para>
///
/// <para>Or un ombrage cuit dans la texture suppose une lumière <b>fixe</b> — et une pièce qui tourne
/// emporte sa lumière avec elle, ce qui trahit immédiatement l'illusion. D'où la séparation, qui
/// n'est pas un contournement mais la bonne lecture de l'objet : le châssis est de la <b>matière</b>
/// (il ne tourne pas, il est ombré, il porte une ombre au sol), le canon est de l'<b>énergie</b> (il
/// pivote, il émet — une lumière ne s'ombre pas).</para>
/// </summary>
public static class TurretSprite
{
    private const int Size = 32;

    /// <summary>Diamètre du châssis à l'échelle 1, en pixels — le repère de dimensionnement.</summary>
    public const float BodyPx = 18f;

    private static Sprite? _body;
    private static Sprite? _barrel;

    /// <summary>Châssis ombré, immobile.</summary>
    public static Sprite Body => _body != null ? _body : _body = BuildBody();

    /// <summary>Canon lumineux, orienté vers la cible.</summary>
    public static Sprite Barrel => _barrel != null ? _barrel : _barrel = BuildBarrel();

    // ─── Palette ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cyan de la palette d'interface — <b>délibérément différent de la teinte de la greffe</b>.
    /// C'est un résultat de playtest (BUG-F01, 2026-07-07) : les tourelles doivent trancher sur une
    /// faune de rouille orange, et se confondre avec elle les rendrait inutiles à regarder.
    /// </summary>
    private static readonly Color Chassis = new(0.27f, 1f, 0.93f);

    /// <summary>Cœur clair — le même rôle que sur les sprites d'ennemis : il empêche l'aplat.</summary>
    private static readonly Color Core = new(0.85f, 1f, 0.98f);

    // ─── Châssis ──────────────────────────────────────────────────────────────

    private static Sprite BuildBody()
    {
        var px = new Color[Size * Size];
        float half = Size * 0.5f;

        // Ombre portée AVANT le corps, donc dessous : une ellipse écrasée au pied du châssis (§3 du
        // brief). C'est elle, plus que l'ombrage, qui décolle l'objet du sol — sans elle une tourelle
        // ombrée reste une vignette posée à plat.
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = (x + 0.5f - half) / (BodyPx * 0.42f);
            float dy = (y + 0.5f - (half - BodyPx * 0.44f)) / (BodyPx * 0.19f);

            float d = dx * dx + dy * dy;
            if (d > 1f) continue;

            px[y * Size + x] = new Color(0.02f, 0.02f, 0.05f, 0.34f * (1f - d * 0.6f));
        }

        // Châssis octogonal : ni disque (qui roule) ni carré (qui est un bloc de décor). Un octogone
        // se lit comme une pièce usinée, ce qu'est une tourelle.
        float r = BodyPx * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float ax = Mathf.Abs(x + 0.5f - half);
            float ay = Mathf.Abs(y + 0.5f - half);

            // Intersection d'un carré et d'un losange = octogone régulier.
            if (ax > r || ay > r || ax + ay > r * 1.42f) continue;

            float dist = Mathf.Sqrt(ax * ax + ay * ay) / r;
            px[y * Size + x] = dist < 0.34f ? Core : Chassis;
        }

        Pseudo3D.Shade(px, Size, Size);
        Pseudo3D.AddOutline(px, Size, Size);

        return Pseudo3D.Make(px, Size, Size);
    }

    // ─── Canon ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Canon : une lance courte qui s'affine et s'éclaircit vers la pointe.
    /// </summary>
    /// <remarks>
    /// Le pivot est posé à la <b>base</b> (0 sur l'axe long) et non au centre : la pièce doit tourner
    /// autour du châssis, pas autour d'elle-même. Un pivot centré ferait pendre le canon d'un côté
    /// puis de l'autre au lieu de viser.
    /// </remarks>
    private static Sprite BuildBarrel()
    {
        const int W = 16;
        const int H = 8;

        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);                    // 0 à la base, 1 à la pointe
            float halfWidth = Mathf.Lerp(2.6f, 0.9f, t);     // s'affine

            float dy = Mathf.Abs(y + 0.5f - H * 0.5f);
            if (dy > halfWidth) continue;

            // S'éclaircit vers la pointe : c'est ce dégradé qui le fait lire comme une émission et
            // non comme une tige peinte.
            px[y * W + x] = Color.Lerp(Chassis, Core, t * t);
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.12f, 0.5f), 1f);
    }

    // ─── Ombrage pseudo-3D ────────────────────────────────────────────────────
    //
    // Il vit dans `Pseudo3D` : le même ombrage habille les appendices de chimère, et deux copies
    // du brief dériveraient l'une de l'autre au premier réglage.
}
