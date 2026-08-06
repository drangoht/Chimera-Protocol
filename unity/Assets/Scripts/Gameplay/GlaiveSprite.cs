using UnityEngine;

/// <summary>
/// Silhouette de la Lame Boomerang : une <b>croix à quatre branches</b>, pointes claires et moyeu
/// lumineux — la forme que dessine son icône (<c>ui_icon_glaive</c>).
///
/// <para><b>Ce qu'elle remplace.</b> Le projectile empruntait
/// <c>enemy_bullet_sentinel_01</c> — le tir d'une <i>sentinelle ennemie</i> — teinté en violet. Rien
/// ne cassait : la lame partait, revenait, touchait deux fois. Mais elle avait à l'écran la forme et
/// la couleur d'un projectile <b>hostile</b>, alors que la carte, le Codex et l'écran de montée de
/// niveau montrent tous une croix cyan. Troisième primitive d'emprunt arrivée jusqu'à l'écran dans
/// ce portage, après les carrés blancs des drones et les motifs de sol.</para>
///
/// <para><b>De l'énergie, pas de la matière</b> : elle tourne, donc elle ne peut pas porter d'ombrage
/// cuit (<see cref="Pseudo3D"/> suppose une lumière fixe venue du haut-gauche, qu'une pièce en
/// rotation emporterait avec elle). Elle garde en revanche le <b>contour</b> du brief, sans quoi un
/// objet cyan disparaît sur les sols clairs du Sanctuaire.</para>
/// </summary>
public static class GlaiveSprite
{
    private const int Size = 44;

    /// <summary>
    /// Longueur d'une branche depuis le centre, en pixels.
    /// </summary>
    /// <remarks>
    /// Calée sur le <b>rayon de frappe</b> (<c>GlaiveProjectile.HitRadius</c> = 20), et non choisie
    /// pour elle-même : c'est la leçon des champions de biome, dont la hitbox débordait la silhouette
    /// de moitié. Une lame qui touche plus loin qu'elle ne paraît se lit « ça m'a touché sans me
    /// toucher ».
    /// </remarks>
    private const float ArmLength = 19f;

    private const float ArmBase = 4.6f;   // demi-largeur au moyeu
    private const float ArmTip  = 1.6f;   // demi-largeur à la pointe
    private const float HubRadius = 5.6f;

    /// <summary>Cyan de la lame — la teinte du jeu publié (<c>Polygon2D</c> du glaive).</summary>
    public static readonly Color Blade = new(0.45f, 1f, 0.92f);

    /// <summary>Cœur et pointes : presque blanc, comme sur l'icône.</summary>
    private static readonly Color Edge = new(0.88f, 1f, 1f);

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var px = new Color[Size * Size];
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = x + 0.5f - half;
            float dy = y + 0.5f - half;

            float ax = Mathf.Abs(dx);
            float ay = Mathf.Abs(dy);

            // Sur une croix axiale, le plus grand des deux écarts dit la distance PARCOURUE le long
            // d'une branche, le plus petit l'écart à son axe. Deux lignes suffisent donc là où
            // quatre boucles orientées seraient nécessaires — et les quatre branches sortent
            // rigoureusement identiques, ce qu'un tracé branche par branche ne garantit pas.
            float along  = Mathf.Max(ax, ay);
            float across = Mathf.Min(ax, ay);

            bool inHub = dx * dx + dy * dy <= HubRadius * HubRadius;

            if (!inHub)
            {
                if (along > ArmLength) continue;

                float t = along / ArmLength;
                if (across > Mathf.Lerp(ArmBase, ArmTip, t)) continue;

                // La pointe s'éclaircit : c'est ce dégradé qui fait lire une lame qui coupe plutôt
                // qu'une croix peinte.
                px[y * Size + x] = t > 0.80f ? Edge : Blade;
                continue;
            }

            // Moyeu : cyan avec un cœur blanc. Sans lui, les quatre branches se rejoignent sur un
            // vide et la croix paraît cassée en son centre.
            px[y * Size + x] = dx * dx + dy * dy <= 2.4f * 2.4f ? Color.white : Blade;
        }

        Pseudo3D.AddOutline(px, Size, Size);
        _sprite = Pseudo3D.Make(px, Size, Size);
        return _sprite;
    }
}
