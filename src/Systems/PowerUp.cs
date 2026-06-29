using Godot;

/// <summary>Type de power-up temporaire ramassable.</summary>
public enum PowerUpType
{
    Overclock,   // cadence de tir ×Magnitude
    Berserk,     // +Magnitude au multiplicateur de dégâts
    Aegis,       // invulnérabilité le temps du buff
    Celerity,    // vitesse de déplacement ×Magnitude
}

/// <summary>
/// Registre des power-ups temporaires (source unique : couleur, durée, magnitude, clé de nom).
/// Buffs à durée limitée — aucun power-creep permanent.
/// </summary>
public static class PowerUps
{
    public readonly record struct Def(PowerUpType Type, string NameKey, Color Color, float Duration, float Magnitude);

    public static readonly Def[] All =
    {
        new(PowerUpType.Overclock, "POWERUP_OVERCLOCK", new Color(0.30f, 0.85f, 1f),   9f, 1.60f),
        new(PowerUpType.Berserk,   "POWERUP_BERSERK",   new Color(1f,    0.35f, 0.35f), 9f, 0.60f),
        new(PowerUpType.Aegis,     "POWERUP_AEGIS",     new Color(1f,    0.82f, 0.30f), 6f, 0f),
        new(PowerUpType.Celerity,  "POWERUP_CELERITY",  new Color(0.40f, 1f,    0.60f), 9f, 1.40f),
    };

    public static Def Get(PowerUpType t) => All[(int)t];

    public static PowerUpType Random(RandomNumberGenerator rng) => (PowerUpType)(int)(rng.Randi() % (uint)All.Length);
}
