using UnityEngine;

/// <summary>
/// Primitives graphiques partagées par l'interface (Lot 5).
///
/// <para>Vit dans la couche <b>Platform</b> parce que le HUD (assembly Gameplay) et les écrans
/// (assembly UI) en ont tous deux besoin : les placer dans l'un des deux créerait un cycle de
/// dépendances entre assemblies.</para>
/// </summary>
public static class UiPrimitives
{
    private static Sprite? _white;
    private static Sprite? _glowBox;
    private static Sprite? _ring;
    private static Sprite? _disc;

    /// <summary>Côté, en pixels, des primitives circulaires.</summary>
    /// <remarks>
    /// 128 px pour un élément dessiné jusqu'à ~160 px de large sur une tablette : au-dessous, le bord
    /// du cercle se crènele visiblement en s'agrandissant, et un contrôle tactile crénelé se lit
    /// comme un placeholder oublié.
    /// </remarks>
    private const int CircleSize = 128;

    /// <summary>
    /// Anneau doux, centré, pour la base du joystick tactile et le contour des boutons.
    /// </summary>
    /// <remarks>
    /// <para>Un contrôle tactile doit se lire <b>par-dessus une nuée</b> : un simple aplat se confond
    /// avec les effets, alors qu'un anneau garde sa forme reconnaissable quel que soit ce qui passe
    /// dessous, et laisse voir le champ de bataille en son centre — ce qui compte, puisqu'il est posé
    /// là où le pouce regarde le moins.</para>
    ///
    /// <para>Les deux bords sont adoucis sur un pixel et demi : un anneau à bord franc, dessiné à une
    /// taille qui n'est jamais celle de sa texture, produit un liseré en marches d'escalier.</para>
    /// </remarks>
    public static Sprite Ring => _ring ??= BuildCircle(annulus: true);

    /// <summary>Disque plein aux bords adoucis — le pommeau du joystick, le fond des boutons.</summary>
    public static Sprite Disc => _disc ??= BuildCircle(annulus: false);

    private static Sprite BuildCircle(bool annulus)
    {
        const float Outer = CircleSize * 0.5f - 1f;
        const float Thickness = CircleSize * 0.085f;   // ~11 px : visible sans peser
        const float Feather = 1.5f;

        var tex = new Texture2D(CircleSize, CircleSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var px = new Color[CircleSize * CircleSize];
        float center = CircleSize * 0.5f - 0.5f;

        for (int y = 0; y < CircleSize; y++)
        for (int x = 0; x < CircleSize; x++)
        {
            float dx = x - center;
            float dy = y - center;
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            float alpha = Mathf.Clamp01((Outer - d) / Feather);
            if (annulus)
                alpha = Mathf.Min(alpha, Mathf.Clamp01((d - (Outer - Thickness)) / Feather));

            px[y * CircleSize + x] = new Color(1f, 1f, 1f, alpha);
        }

        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Épaisseur du dégradé de <see cref="GlowBox"/>, en pixels — sa bordure 9-slice, et donc le
    /// <b>débordement maximal</b> exploitable par un appelant.
    /// </summary>
    /// <remarks>
    /// Un halo posé avec une marge <i>inférieure</i> à cette valeur garde une partie de son dégradé
    /// sous le contenu : il paraît alors plus fin et plus doux, ce qui est la façon d'obtenir une aura
    /// discrète sans second sprite. Une marge <i>supérieure</i>, en revanche, laisse un trou entre la
    /// lueur et le contenu — la zone centrale n'étant pas peinte (<c>fillCenter = false</c>).
    /// </remarks>
    public const int GlowBoxBorder = 40;

    /// <summary>
    /// Halo <b>rectangulaire</b> : un liseré de <see cref="GlowBoxBorder"/> px qui s'éteint vers
    /// l'extérieur, découpé en 9 zones pour épouser n'importe quelle taille de panneau.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Un dégradé radial ne convient pas pour border un rectangle</b>, et l'erreur ne se
    /// voit qu'à l'image. Étiré sur une carte de 420 × 540 px avec 38 px de débordement, le bord de
    /// la carte tombe à 85 % du rayon du dégradé — c'est-à-dire là où il ne reste presque plus rien.
    /// L'aura était bien créée, bien colorée, bien animée, et parfaitement <b>invisible</b> : le seul
    /// endroit où elle brillait était le centre, sous la carte qui la cache. Élargir la marge ne
    /// corrige pas la forme, il fabrique une nappe qui déborde sur les cartes voisines.</para>
    ///
    /// <para>Ici, le dégradé vit dans la <b>bordure</b> du 9-slice : il garde son épaisseur en pixels
    /// quelle que soit la taille de l'élément, et l'appelant pose <c>fillCenter = false</c> pour ne
    /// garder que le liseré — la zone centrale, invisible sous le contenu, n'est même pas dessinée.
    /// </para>
    ///
    /// <para>⚠ <c>pixelsPerUnit</c> vaut 100, comme <c>UiCanvas.referencePixelsPerUnit</c> : une
    /// <c>Image</c> met ses bordures 9-slice à l'échelle du rapport des deux, et les laisser diverger
    /// multiplierait l'épaisseur du dégradé d'autant.</para>
    /// </remarks>
    public static Sprite GlowBox
    {
        get
        {
            if (_glowBox != null) return _glowBox;

            // Strictement plus grand que deux bordures, sinon les zones du 9-slice se recouvrent.
            const int Size = 96;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color[Size * Size];

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int toEdge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                float t = Mathf.Clamp01(toEdge / (float)GlowBoxBorder);

                // Quadratique : une décroissance linéaire donne un bord franc à l'extérieur, qu'on
                // lit comme un second cadre plutôt que comme une lueur.
                px[y * Size + x] = new Color(1f, 1f, 1f, t * t);
            }

            tex.SetPixels(px);
            tex.Apply();

            var border = new Vector4(GlowBoxBorder, GlowBoxBorder, GlowBoxBorder, GlowBoxBorder);

            _glowBox = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f),
                                     100f, 0, SpriteMeshType.FullRect, border);
            return _glowBox;
        }
    }

    /// <summary>
    /// Sprite blanc uni, partagé.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Indispensable dès qu'une <c>Image</c> utilise <c>Image.Type.Filled</c>.</b> Sans sprite,
    /// Unity <b>ignore purement et simplement <c>fillAmount</c></b> et dessine le quad entier : une
    /// barre de vie ou d'XP reste alors visuellement pleine quoi qu'il arrive, sans erreur ni
    /// avertissement. Le symptôme se lit « les valeurs ne changent pas » alors que le jeu, lui,
    /// fonctionne parfaitement — c'est exactement ce qui a été observé en jouant.
    /// </remarks>
    public static Sprite White
    {
        get
        {
            if (_white != null) return _white;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();

            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _white;
        }
    }
}
