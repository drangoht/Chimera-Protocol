using UnityEngine;

/// <summary>
/// Textures et matériaux partagés par tous les effets (Lot 5 quater).
///
/// <para><b>Tout est généré au démarrage</b>, comme sous Godot où <c>MakeRadialLightTexture</c>
/// fabriquait la texture de lumière à la volée. C'est délibéré : ces formes sont des dégradés
/// analytiques, et les stocker en PNG ajouterait des assets à importer, à filtrer et à tenir
/// synchronisés pour un résultat identique au pixel près.</para>
///
/// <para>Un seul exemplaire de chaque, en statique : une lueur est demandée des dizaines de fois par
/// seconde, et allouer une texture par effet suffirait à faire chuter la cadence bien avant que le
/// nombre d'entités ne pose problème.</para>
/// </summary>
public static class VfxPrimitives
{
    /// <summary>
    /// Effets qui passent <b>sous</b> les entités : champs au sol, auras, anneaux de zone. Au-dessus
    /// du décor (-90) et des orbes (5), sous la faune (10).
    /// </summary>
    public const int OrderGround = 8;

    /// <summary>
    /// Effets qui passent <b>au-dessus</b> des entités : faisceaux, éclairs, impacts, flashs. Un
    /// éclair masqué par la nuée qu'il frappe ne se lit pas — sous Godot ils étaient à ZIndex 4-6,
    /// donc au-dessus de tout ce qui bouge.
    /// </summary>
    public const int OrderOver = 30;

    private static Sprite? _glow;
    private static Sprite? _spark;
    private static Sprite? _beam;
    private static Sprite? _flat;
    private static Material? _additive;
    private static Material? _additiveBeam;
    private static Material? _additiveFlat;
    private static Material? _additiveSpark;

    /// <summary>Disque à dégradé linéaire, 64 px — le portage de la texture de PointLight2D.</summary>
    public static Sprite Glow => _glow != null ? _glow : _glow = MakeRadial(64);

    /// <summary>Même dégradé en 16 px, pour les particules.</summary>
    public static Sprite Spark => _spark != null ? _spark : _spark = MakeRadial(16);

    /// <summary>
    /// Bande douce sur la largeur, opaque sur la longueur : posée sur un <c>LineRenderer</c>, elle
    /// donne au trait des bords estompés au lieu d'une arête franche. C'est ce qui distingue un
    /// faisceau d'énergie d'un rectangle coloré.
    /// </summary>
    public static Sprite Beam => _beam != null ? _beam : _beam = MakeSoftBand(16);

    /// <summary>Blanc uni — le cœur des faisceaux, qui doit rester net.</summary>
    public static Sprite Flat
    {
        get
        {
            if (_flat != null) return _flat;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();

            _flat = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _flat;
        }
    }

    /// <summary>
    /// Matériau additif partagé. Le repli sur <c>Sprites/Default</c> n'est pas de la prudence
    /// décorative : sans lui, un shader introuvable donnerait des effets <b>roses</b> en jeu, alors
    /// qu'un mélange alpha reste parfaitement lisible.
    /// </summary>
    public static Material Additive
    {
        get
        {
            if (_additive != null) return _additive;

            var shader = Resources.Load<Shader>("Shaders/VfxAdditive");
            if (shader == null)
            {
                Debug.LogWarning("[Vfx] Shader additif introuvable — repli en melange alpha.");
                shader = Shader.Find("Sprites/Default");
            }

            _additive = new Material(shader) { name = "VfxAdditive (runtime)" };
            return _additive;
        }
    }

    /// <summary>Additif texturé de la bande douce — matériau des faisceaux et des arcs.</summary>
    /// <remarks>
    /// Un <c>LineRenderer</c> et un <c>ParticleSystem</c> lisent leur texture dans le <b>matériau</b>,
    /// là où un <c>SpriteRenderer</c> la fournit lui-même. D'où trois matériaux et non un : sans cela,
    /// changer la texture pour un faisceau la changerait aussi pour toutes les particules en vol.
    /// </remarks>
    public static Material AdditiveBeam
        => _additiveBeam != null ? _additiveBeam : _additiveBeam = Textured(Beam, "VfxAdditiveBeam");

    /// <summary>Additif blanc uni — cœur net des faisceaux.</summary>
    public static Material AdditiveFlat
        => _additiveFlat != null ? _additiveFlat : _additiveFlat = Textured(Flat, "VfxAdditiveFlat");

    /// <summary>Additif du grain de particule (disque estompé).</summary>
    public static Material AdditiveSpark
        => _additiveSpark != null ? _additiveSpark : _additiveSpark = Textured(Spark, "VfxAdditiveSpark");

    private static Material Textured(Sprite sprite, string name)
        => new(Additive) { name = name, mainTexture = sprite.texture };

    /// <summary>
    /// Disque blanc dont l'alpha décroît <b>linéairement</b> du centre au bord — exactement le
    /// <c>GradientTexture2D</c> radial de Godot, dont dépendait l'allure de toutes les lueurs du jeu.
    /// </summary>
    private static Sprite MakeRadial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        float half = size * 0.5f;
        var px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - half;
            float dy = y + 0.5f - half;
            float a = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / half);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        tex.SetPixels32(px);
        tex.Apply();

        // Pixels par unité = 1 : le sprite mesure alors `size` unités, et le monde compte 1 px par
        // unité. Une lueur de rayon r se pose donc à l'échelle 2r/size, sans facteur caché.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// Bande dont l'alpha décroît vers les deux bords horizontaux. Un <c>LineRenderer</c> mappe
    /// <c>v</c> sur la largeur du trait : le dégradé se retrouve donc perpendiculaire au faisceau.
    /// </summary>
    private static Sprite MakeSoftBand(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        var px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            byte a = (byte)(Mathf.Clamp01(1f - Mathf.Abs(2f * v - 1f)) * 255f);
            for (int x = 0; x < size; x++) px[y * size + x] = new Color32(255, 255, 255, a);
        }

        tex.SetPixels32(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }
}
