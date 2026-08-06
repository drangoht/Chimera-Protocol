using UnityEngine;

/// <summary>
/// Les <b>appendices</b> qu'une greffe fait pousser sur le porteur — le vocabulaire de formes dans
/// lequel se dit « je ne suis plus tout à fait humain ».
///
/// <para><b>Pourquoi un vocabulaire et non un sprite par greffe.</b> Huit greffes (cinq simples, trois
/// fusions) pour sept formes : les fusions ne sont pas des objets neufs, ce sont des <i>combinaisons</i>
/// de ce que le porteur avait déjà. La Charge Blindée doit donc se lire comme « la carapace, plus la
/// corne » — ce qu'un huitième dessin inédit ne dirait pas. C'est aussi ce qui permet d'ajouter une
/// greffe sans ouvrir un chantier graphique.</para>
///
/// <para><b>Matière ou énergie, jamais entre les deux.</b> Ce qui ne pivote pas est de la matière :
/// ombré par <see cref="Pseudo3D"/>, contour compris, comme tout le bestiaire. Ce qui pivote — l'œil
/// de visée, la pointe d'une antenne — est de l'énergie : lumineux, non ombré, parce qu'un ombrage
/// cuit suppose une lumière fixe et qu'une pièce qui tourne emporterait la sienne avec elle.</para>
///
/// <para>Les formes sont dessinées dans un <b>gris légèrement saturé</b> puis teintées par le
/// <c>SpriteRenderer</c> : les coefficients de face agissent sur la valeur <i>et</i> la saturation, et
/// un gris pur ne réagirait qu'à la première. Un seul exemplaire de chaque forme sert les huit
/// greffes.</para>
/// </summary>
public static class ChimeraParts
{
    /// <summary>Les formes disponibles.</summary>
    public enum Kind
    {
        /// <summary>Écaille de carapace — plaque bombée, portée dans le dos.</summary>
        Plate,
        /// <summary>Vérin de servomoteur — tige et bloc, portés aux flancs.</summary>
        Piston,
        /// <summary>Nodule de rouille vivante — excroissance qui bat sur le torse.</summary>
        Nodule,
        /// <summary>Antenne de rôdeur — tige effilée à pointe lumineuse.</summary>
        Antenna,
        /// <summary>Corne de charge — éperon frontal.</summary>
        Horn,
        /// <summary>Alvéole de ruche — logette hexagonale percée, portée dans le dos.</summary>
        Pod,
        /// <summary>Œil greffé — énergie, non ombré, tourné vers la visée.</summary>
        Eye,
    }

    /// <summary>
    /// Base de dessin : un gris <b>légèrement bleuté et saturé</b>. Un gris pur ne réagirait pas aux
    /// coefficients de saturation des faces, et les plaques paraîtraient plates.
    /// </summary>
    private static readonly Color Matter = new(0.60f, 0.58f, 0.66f);

    /// <summary>Cœur clair d'une pièce d'énergie — le même rôle que sur les sprites d'ennemis.</summary>
    private static readonly Color Bright = new(0.94f, 0.98f, 1f);

    private static readonly Sprite?[] Cache = new Sprite?[7];

    /// <summary>Forme demandée, fabriquée à la première demande puis partagée.</summary>
    public static Sprite Get(Kind kind)
    {
        int i = (int)kind;
        if (Cache[i] != null) return Cache[i]!;

        Cache[i] = kind switch
        {
            Kind.Plate   => BuildPlate(),
            Kind.Piston  => BuildPiston(),
            Kind.Nodule  => BuildNodule(),
            Kind.Antenna => BuildAntenna(),
            Kind.Horn    => BuildHorn(),
            Kind.Pod     => BuildPod(),
            _            => BuildEye(),
        };

        return Cache[i]!;
    }

    // ─── Matière ──────────────────────────────────────────────────────────────

    /// <summary>Écaille : un demi-ovale, plat côté dos et bombé vers l'extérieur.</summary>
    private static Sprite BuildPlate()
    {
        const int W = 14, H = 12;
        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            float dx = (x + 0.5f - W * 0.62f) / (W * 0.42f);
            float dy = (y + 0.5f - H * 0.5f) / (H * 0.46f);
            if (dx * dx + dy * dy > 1f) continue;

            // Bord interne franc : l'écaille s'emboîte dans le corps, elle n'y flotte pas.
            if (x < 2) continue;

            px[y * W + x] = Matter;
        }

        return Finish(px, W, H);
    }

    /// <summary>Vérin : une tige verticale, un bloc en pied, une coiffe en tête.</summary>
    private static Sprite BuildPiston()
    {
        const int W = 8, H = 14;
        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            bool rod   = x >= 3 && x <= 4;
            bool foot  = y <= 3 && x >= 1 && x <= 6;
            bool head  = y >= 11 && x >= 2 && x <= 5;

            if (rod || foot || head) px[y * W + x] = Matter;
        }

        return Finish(px, W, H);
    }

    /// <summary>Nodule : une masse irrégulière — deux disques décalés, jamais un cercle propre.</summary>
    private static Sprite BuildNodule()
    {
        const int S = 10;
        var px = new Color[S * S];

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float ax = x + 0.5f, ay = y + 0.5f;

            bool big   = Sq(ax - 4.4f) + Sq(ay - 4.6f) <= 3.7f * 3.7f;
            bool bump  = Sq(ax - 6.8f) + Sq(ay - 6.6f) <= 2.1f * 2.1f;

            if (big || bump) px[y * S + x] = Matter;
        }

        return Finish(px, S, S);
    }

    /// <summary>
    /// Antenne : une tige effilée, ombrée, surmontée d'une pointe <b>lumineuse</b>.
    /// </summary>
    /// <remarks>
    /// La pointe est peinte <b>après</b> l'ombrage : passée dedans, elle serait traitée en « surface
    /// haute » et assombrie ou éclaircie selon sa place, alors qu'elle émet. C'est la séparation
    /// matière / énergie appliquée à l'intérieur d'une même pièce.
    /// </remarks>
    private static Sprite BuildAntenna()
    {
        const int W = 6, H = 16;
        var px = new Color[W * H];

        for (int y = 0; y < H - 3; y++)
        {
            float t = y / (float)(H - 4);              // 0 à la base, 1 sous la pointe
            float half = Mathf.Lerp(1.4f, 0.6f, t);
            float axis = W * 0.5f + Mathf.Sin(t * 2.1f) * 1.0f;   // légère courbure

            for (int x = 0; x < W; x++)
                if (Mathf.Abs(x + 0.5f - axis) <= half) px[y * W + x] = Matter;
        }

        Pseudo3D.Shade(px, W, H);

        // Pointe : trois pixels de lumière, posés sur l'axe une fois l'ombrage fait.
        float tipAxis = W * 0.5f + Mathf.Sin(2.1f) * 1.0f;
        int cx = Mathf.Clamp(Mathf.RoundToInt(tipAxis - 0.5f), 1, W - 2);

        for (int y = H - 4; y < H - 1; y++)
        for (int x = cx - 1; x <= cx + 1; x++)
            px[y * W + x] = (y == H - 3 && x == cx) ? Color.white : Bright;

        Pseudo3D.AddOutline(px, W, H);
        return Pseudo3D.Make(px, W, H, new Vector2(0.5f, 0.08f));
    }

    /// <summary>Corne : un éperon triangulaire, base à gauche, pointe à droite.</summary>
    private static Sprite BuildHorn()
    {
        const int W = 14, H = 10;
        var px = new Color[W * H];

        for (int x = 0; x < W; x++)
        {
            float t = x / (float)(W - 1);
            float half = Mathf.Lerp(4.2f, 0.5f, t * t);   // s'affine vite : une pointe, pas un coin

            for (int y = 0; y < H; y++)
                if (Mathf.Abs(y + 0.5f - H * 0.5f) <= half) px[y * W + x] = Matter;
        }

        // Pivot à la BASE : la corne doit tourner autour de son point d'attache au corps, pas autour
        // de son milieu — sinon elle balance au lieu de pointer.
        var shaded = Finish(px, W, H, new Vector2(0.08f, 0.5f));
        return shaded;
    }

    /// <summary>Alvéole : un hexagone percé — une logette, pas une bosse.</summary>
    private static Sprite BuildPod()
    {
        const int S = 12;
        var px = new Color[S * S];
        float half = S * 0.5f;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float ax = Mathf.Abs(x + 0.5f - half);
            float ay = Mathf.Abs(y + 0.5f - half);

            // Hexagone : bande verticale bornée, coins rabattus.
            if (ax > 4.6f || ay > 5.2f || ax * 1.15f + ay > 7.4f) continue;

            // Le trou : c'est lui qui fait lire une alvéole plutôt qu'un galet.
            if (Sq(x + 0.5f - half) + Sq(y + 0.5f - half) <= 2.0f * 2.0f) continue;

            px[y * S + x] = Matter;
        }

        return Finish(px, S, S);
    }

    // ─── Énergie ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Œil : sclérotique sombre, iris clair, pupille noire, éclat en haut-gauche.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Jamais ombré</b> : il pivote avec la visée, et un ombrage cuit tournerait avec lui — la
    /// lumière viendrait alors du bas dès que le joueur vise vers le bas. L'éclat fixe qu'il porte
    /// n'est pas un ombrage mais une <i>brillance</i>, qui appartient à l'œil et tourne avec lui.
    /// </remarks>
    private static Sprite BuildEye()
    {
        const int S = 12;
        var px = new Color[S * S];
        float half = S * 0.5f;

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d2 = Sq(x + 0.5f - half) + Sq(y + 0.5f - half);
            if (d2 > 5.4f * 5.4f) continue;

            px[y * S + x] = d2 > 4.1f * 4.1f ? new Color(0.10f, 0.09f, 0.14f) : Bright;
        }

        // Pupille décalée vers l'avant (+x) : un œil centré regarde dans le vide, un œil décalé
        // regarde quelque part — et c'est ce que la pièce doit dire en tournant.
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            if (Sq(x + 0.5f - (half + 1.4f)) + Sq(y + 0.5f - half) <= 1.9f * 1.9f)
                px[y * S + x] = new Color(0.06f, 0.04f, 0.09f);
        }

        px[(int)(half + 1) * S + (int)(half - 2)] = Color.white;   // éclat

        Pseudo3D.AddOutline(px, S, S);
        return Pseudo3D.Make(px, S, S);
    }

    // ─── Plomberie ────────────────────────────────────────────────────────────

    private static float Sq(float v) => v * v;

    private static Sprite Finish(Color[] px, int w, int h, Vector2? pivot = null)
    {
        Pseudo3D.Shade(px, w, h);
        Pseudo3D.AddOutline(px, w, h);
        return Pseudo3D.Make(px, w, h, pivot);
    }
}
