using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Obstacles de l'arène : ce qu'ils bloquent, et où (lot de parité visuelle).
///
/// <para><b>Un obstacle qui ne bloque pas est pire qu'aucun obstacle</b> : le joueur le voit, en tient
/// compte, le contourne — et découvre en le traversant que le décor mentait. La liste tenue ici est
/// donc consultée par le joueur <b>et</b> par la faune, exactement comme le calque de collision du
/// jeu d'origine bloquait les deux.</para>
///
/// <para>Une liste statique plutôt que des <c>Collider2D</c> : le projet n'a aucune physique
/// dynamique — les entités se déplacent par <c>transform</c> — et en introduire une pour une dizaine
/// de piliers coûterait bien plus que la boucle qui suit.</para>
/// </summary>
public static class ArenaObstacles
{
    private static readonly List<Vector2> _centers = new();

    /// <summary>Rayon bloquant commun, repris de la logique pure.</summary>
    public const float Radius = ArenaLayout.BlockRadius;

    /// <summary>Obstacles en place — observable par le banc et le diagnostic.</summary>
    public static int Count => _centers.Count;

    /// <summary>Positions des obstacles.</summary>
    public static IReadOnlyList<Vector2> Centers => _centers;

    /// <summary>Remplace la disposition courante.</summary>
    public static void Set(IEnumerable<Vector2> centers)
    {
        _centers.Clear();
        _centers.AddRange(centers);
    }

    /// <summary>Vide la liste — changement de scène ou nouvelle run.</summary>
    public static void Clear() => _centers.Clear();

    /// <summary>
    /// Écarte une position des obstacles qu'elle chevauche. <paramref name="bodyRadius"/> est le
    /// rayon du corps qui se déplace : un colosse ne s'approche pas autant qu'une nuée.
    ///
    /// <para>Le parcours est linéaire sur une dizaine d'éléments — à 300 entités, cela reste très
    /// en deçà du budget mesuré au lot 1 (0,168 ms par pas pour la simulation complète).</para>
    /// </summary>
    public static Vector2 Resolve(Vector2 position, float bodyRadius)
    {
        if (_centers.Count == 0) return position;

        float minDistance = Radius + bodyRadius;

        foreach (var center in _centers)
        {
            var (x, y) = ArenaLayout.PushOut(position.x, position.y, center.x, center.y, minDistance);
            position = new Vector2(x, y);
        }

        return position;
    }
}
