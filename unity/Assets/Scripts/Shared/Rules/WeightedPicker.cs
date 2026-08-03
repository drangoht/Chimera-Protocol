using System.Collections.Generic;

/// <summary>
/// Tirage pondéré (logique pure, testable). Étant donné des poids et une valeur de
/// tirage <paramref name="roll"/> dans [0, somme des poids], retourne l'index choisi :
/// le premier où la somme cumulée des poids atteint le tirage. Repli sur le dernier index.
/// </summary>
public static class WeightedPicker
{
    public static int PickIndex(IReadOnlyList<float> weights, float roll)
    {
        float acc = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            acc += weights[i];
            if (roll <= acc) return i;
        }
        return weights.Count - 1;
    }
}
