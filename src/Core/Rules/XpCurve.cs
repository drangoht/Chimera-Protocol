/// <summary>
/// Courbe d'expérience (logique pure, sans dépendance Godot — testable).
/// Seuil pour passer du niveau n au niveau n+1, inspiré de Vampire Survivors :
/// linéaire +10/niveau (L1=5), mur ×2 à L20, puis +13/niveau pour l'endgame.
/// </summary>
public static class XpCurve
{
    /// <summary>XP nécessaire pour passer du niveau <paramref name="level"/> au suivant.</summary>
    public static int Threshold(int level)
    {
        if (level < 20)
            return 5 + (level - 1) * 10;   // L1=5, L10=95, L19=185
        if (level == 20)
            return 390;                     // mur de mi-run
        return 208 + (level - 21) * 13;     // phase 2 : L21=208, L30=325
    }
}
