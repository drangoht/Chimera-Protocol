/// <summary>
/// Extrapolation des stats d'arme au-delà des niveaux définis dans weapons.json
/// (logique pure, testable). Au-delà du dernier niveau défini, les dégâts gagnent
/// +10% par niveau supplémentaire ; les mécaniques (projectiles, chaînes…) plafonnent.
/// </summary>
public static class WeaponLeveling
{
    public static float ExtrapolatedDamage(float baseDamage, int level, int definedMax)
        => level > definedMax ? baseDamage * (1f + (level - definedMax) * 0.10f) : baseDamage;
}
