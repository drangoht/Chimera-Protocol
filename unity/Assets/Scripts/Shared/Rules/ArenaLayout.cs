using System.Collections.Generic;

/// <summary>
/// Placement des obstacles dans l'arène — logique pure et testable (lot de parité visuelle).
///
/// <para><b>Trois gabarits symétriques</b>, tirés au sort à chaque run : quadrants, anneau, allées.
/// Le nombre reste faible (6 à 10) et les pièces sont grandes : une arène parsemée de petits
/// obstacles se traverse sans y penser, alors que quelques masses lisibles créent des <b>couloirs</b>
/// et des angles morts — c'est ce qui donne au déplacement autre chose qu'une ligne droite.</para>
///
/// <para>La symétrie n'est pas décorative : elle garantit qu'aucun quart de l'arène n'est
/// structurellement plus sûr qu'un autre, ce qu'un placement purement aléatoire ne peut pas
/// promettre.</para>
/// </summary>
public static class ArenaLayout
{
    /// <summary>
    /// Rayon bloquant d'un obstacle, en pixels.
    ///
    /// <para><b>Il vaut la demi-largeur du décor, jamais plus.</b> Les sprites d'obstacle font 32 px
    /// de large ; le jeu d'origine bloque avec une capsule de rayon 13 pour une silhouette de la même
    /// largeur. Le portage était à 26 — soit une zone infranchissable <b>deux fois et demie</b> plus
    /// large que ce qui se voit à l'écran : le joueur s'arrêtait dans le vide, à côté du pilier.</para>
    ///
    /// <para>Un rayon supérieur à la silhouette et un rayon inférieur sont deux défauts distincts et
    /// tous deux visibles : trop grand, le décor ment par excès (on est arrêté par rien) ; trop
    /// petit, il ment par défaut (on entre dans la pierre).</para>
    /// </summary>
    public const float BlockRadius = 16f;

    /// <summary>Marge minimale entre un obstacle et le bord de l'arène.</summary>
    public const float EdgeMargin = 120f;

    /// <summary>Position d'un obstacle.</summary>
    public readonly struct Spot
    {
        public readonly float X;
        public readonly float Y;

        public Spot(float x, float y) { X = x; Y = y; }
    }

    /// <summary>
    /// Positions du gabarit tiré. <paramref name="next01"/> rend un flottant de [0,1[ — il est
    /// <b>injecté</b> parce que cette couche ne connaît ni le moteur ni le générateur du jeu, comme
    /// pour le tirage des cartes de niveau. Le résultat ne dépend donc que de la graine : deux runs
    /// de même graine donnent la même arène, ce qui rend une mesure de banc reproductible.
    /// </summary>
    public static List<Spot> Positions(System.Func<float> next01, float halfWidth, float halfHeight)
    {
        var spots = new List<Spot>();
        float J(float amplitude) => (next01() * 2f - 1f) * amplitude;

        switch ((int)(next01() * 3f) % 3)
        {
            case 0:   // Quadrants : amas de trois et paires, en alternance
            {
                int quadrant = 0;
                foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    float ax = sx * 470f + J(48f);
                    float ay = sy * 290f + J(36f);

                    if (quadrant++ % 2 == 0)
                    {
                        spots.Add(new Spot(ax, ay - 64f));
                        spots.Add(new Spot(ax - 60f, ay + 48f));
                        spots.Add(new Spot(ax + 60f, ay + 48f));
                    }
                    else
                    {
                        bool vertical = next01() < 0.5f;
                        float ox = vertical ? 0f : 64f;
                        float oy = vertical ? 60f : 0f;
                        spots.Add(new Spot(ax - ox, ay - oy));
                        spots.Add(new Spot(ax + ox, ay + oy));
                    }
                }
                break;
            }

            case 1:    // Anneau : six sur l'ellipse, deux sentinelles au centre
            {
                for (int k = 0; k < 6; k++)
                {
                    double angle = (60.0 * k + J(10f)) * System.Math.PI / 180.0;
                    spots.Add(new Spot((float)System.Math.Cos(angle) * 560f,
                                       (float)System.Math.Sin(angle) * 340f));
                }
                spots.Add(new Spot(-250f + J(24f), J(24f)));
                spots.Add(new Spot( 250f + J(24f), J(24f)));
                break;
            }

            default:   // Allées : deux rangées, donc trois couloirs de circulation
            {
                foreach (float x in new[] { -560f, 0f, 560f })
                {
                    spots.Add(new Spot(x + J(32f), -270f + J(24f)));
                    spots.Add(new Spot(x + J(32f),  270f + J(24f)));
                }
                break;
            }
        }

        // Aucun obstacle collé au mur : le joueur doit toujours pouvoir longer le bord, sinon un
        // encerclement contre une paroi devient une mort sans issue.
        var clamped = new List<Spot>(spots.Count);
        float limitX = halfWidth - EdgeMargin;
        float limitY = halfHeight - EdgeMargin;

        foreach (var s in spots)
            clamped.Add(new Spot(Clamp(s.X, -limitX, limitX), Clamp(s.Y, -limitY, limitY)));

        return clamped;
    }

    private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

    /// <summary>
    /// Écarte une position d'un obstacle si elle s'y enfonce. Renvoie la position corrigée.
    ///
    /// <para>On <b>glisse</b> autour de l'obstacle au lieu de bloquer net : un mur qui arrête
    /// brutalement transforme chaque angle en piège, alors que le jeu demande de circuler entre les
    /// masses en permanence.</para>
    /// </summary>
    public static (float X, float Y) PushOut(float x, float y, float ox, float oy, float minDistance)
    {
        float dx = x - ox;
        float dy = y - oy;
        float sqr = dx * dx + dy * dy;

        if (sqr >= minDistance * minDistance || minDistance <= 0f) return (x, y);

        // Pile au centre : on pousse dans une direction arbitraire mais stable, sinon la division
        // par zéro renverrait NaN et l'entité disparaîtrait de la carte.
        if (sqr < 0.0001f) return (ox + minDistance, oy);

        float dist = (float)System.Math.Sqrt(sqr);
        float scale = minDistance / dist;
        return (ox + dx * scale, oy + dy * scale);
    }
}
