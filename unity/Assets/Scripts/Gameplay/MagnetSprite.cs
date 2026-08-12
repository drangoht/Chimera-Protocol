using UnityEngine;

/// <summary>
/// Silhouette de l'Aimant : le <b>fer à cheval</b> à pointes rouges, la forme la plus immédiatement
/// lisible du jeu — personne n'a besoin qu'on lui explique ce que fait un aimant.
///
/// <para><b>Dessinée par code, et non générée en PNG.</b> Le pipeline d'assets veut un générateur
/// Python, un import éditeur et un <c>.meta</c> ; trois maillons dont chacun peut échouer en silence
/// et faire afficher « l'ancienne image » (cf. <c>tools/unity_paths.py</c>). Pour une pièce de cette
/// simplicité, tenue par les seules constantes ci-dessous, la même chaîne que
/// <see cref="GlaiveSprite"/> et <see cref="MissileSprite"/> est plus courte et ne peut pas se
/// tromper de dossier.</para>
///
/// <para><b>Il ne tourne pas</b>, contrairement au glaive : il porte donc l'ombrage pseudo-3D du
/// brief, lumière fixe venue du haut-gauche, comme tout le reste du décor.</para>
/// </summary>
public static class MagnetSprite
{
    private const int Size = 28;

    /// <summary>Rayon extérieur de l'arc, en pixels.</summary>
    private const float Outer = 11f;

    /// <summary>Rayon intérieur : c'est l'écart entre les deux branches.</summary>
    private const float Inner = 5.5f;

    /// <summary>
    /// Hauteur des deux branches droites sous le centre de l'arc, en pixels. Sans elles, le fer à
    /// cheval se lit comme un simple anneau ouvert — et un anneau n'est pas un aimant.
    /// </summary>
    private const float LegLength = 7f;

    /// <summary>Hauteur de la pointe colorée au bout de chaque branche.</summary>
    private const float TipLength = 3f;

    /// <summary>Corps : l'acier gris-bleu du jeu d'origine (<c>Polygon2D</c> de <c>Magnet.tscn</c>).</summary>
    private static readonly Color Steel = new(0.70f, 0.72f, 0.78f);

    /// <summary>Pointes : le rouge des deux <c>Polygon2D</c> nommés <c>TipLeft</c> / <c>TipRight</c>.</summary>
    private static readonly Color Tip = new(0.90f, 0.20f, 0.20f);

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var px = new Color[Size * Size];
        var isTip = new bool[Size * Size];

        float half = Size * 0.5f;

        // Centre de l'arc, remonté : les branches descendent sous lui et doivent tenir dans le canevas.
        float cx = half;
        float cy = half + LegLength * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = x + 0.5f - cx;
            float dy = y + 0.5f - cy;

            float r2 = dx * dx + dy * dy;
            float ax = Mathf.Abs(dx);

            // ⚠ Y monte en coordonnées de texture Unity : « sous l'arc » est donc dy < 0.
            bool inRing = dy >= 0f && r2 <= Outer * Outer && r2 >= Inner * Inner;
            bool inLeg = dy < 0f && dy >= -LegLength && ax <= Outer && ax >= Inner;

            if (!inRing && !inLeg) continue;

            px[y * Size + x] = Steel;
            isTip[y * Size + x] = inLeg && dy <= -(LegLength - TipLength);
        }

        // L'ombrage tourne sur l'acier SEUL, puis les pointes sont repeintes par-dessus.
        //
        // ⚠ Les faire ombrer avec le reste les tuerait : elles occupent les rangées du bas, que
        // `Shade` traite comme le contact au sol (valeur × 0,35). Le rouge y virerait au brun et
        // l'objet redeviendrait un fer à cheval gris — c'est-à-dire un débris de décor parmi les
        // obstacles de l'arène. Ces deux pointes sont tout ce qui dit « aimant ».
        Pseudo3D.Shade(px, Size, Size);

        for (int i = 0; i < px.Length; i++)
        {
            if (!isTip[i]) continue;

            // La lumière reste au haut-gauche : la branche de gauche garde son rouge plein, celle de
            // droite passe dans l'ombre. Sans cet écart, les deux pointes s'aplatissent.
            bool left = i % Size < Size * 0.5f;
            px[i] = left ? Tip : Tip * 0.72f;
            px[i].a = 1f;
        }

        Pseudo3D.AddOutline(px, Size, Size);

        _sprite = Pseudo3D.Make(px, Size, Size);
        return _sprite;
    }
}
