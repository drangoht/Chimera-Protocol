/// <summary>
/// Plafonds des effets de contrôle de foule appliqués aux ennemis (logique pure, testable).
/// Garde-fous d'équilibrage pour que slow/DoT ne trivialisent jamais le jeu.
/// </summary>
public static class CrowdControlCaps
{
    /// <summary>Multiplicateur de vitesse minimal sous slow (0.60 = ralentissement max -40 %).</summary>
    public const float MinSlowMult = 0.60f;

    /// <summary>Dégâts par seconde maximaux d'une brûlure (DoT non-stackable, plafonné).</summary>
    public const float MaxBurnDps = 60f;

    /// <summary>Borne un multiplicateur de slow dans [MinSlowMult, 1] (1 = pas de ralentissement).</summary>
    public static float CapSlowMult(float mult)
    {
        if (mult < MinSlowMult) return MinSlowMult;
        if (mult > 1f) return 1f;
        return mult;
    }

    /// <summary>Borne des dégâts/seconde de brûlure dans [0, MaxBurnDps].</summary>
    public static float CapBurnDps(float dps)
    {
        if (dps < 0f) return 0f;
        if (dps > MaxBurnDps) return MaxBurnDps;
        return dps;
    }
}
