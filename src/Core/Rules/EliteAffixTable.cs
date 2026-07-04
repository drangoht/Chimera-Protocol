/// <summary>
/// Affixes d'élite appliqués à une fraction des ennemis basiques (logique pure, testable).
/// Inspiré des « élites/affixes » de Risk of Rain 2 / Diablo : n'importe quel ennemi de base peut
/// recevoir UN affixe qui change radicalement sa menace, à coût de production quasi nul (aucune
/// nouvelle scène/sprite — seulement des multiplicateurs + un rendu teinté). Résout la limite
/// « silhouettes recolorées » : la variété vient désormais du comportement, pas de la forme.
///
/// Cette classe ne décide QUE des chiffres ; l'application (stats, VFX, comportement) vit dans
/// EnemyBase.ApplyElite et le tirage dans EnemySpawner. Aucune dépendance Godot (testée en xUnit).
/// </summary>
public enum EliteAffix
{
    None,
    Armored,       // Blindé      : encaisse (dégâts reçus fortement réduits, gros PV)
    Regenerating,  // Régénérant  : se soigne s'il n'est pas frappé (attrition — burst obligatoire)
    Explosive,     // Explosif    : explose en AoE à la mort (punit les kills au corps-à-corps)
    Frenzied,      // Frénétique  : très rapide et cognant fort, mais fragile (glass cannon)
    Vampiric,      // Vampirique  : se soigne d'une part des dégâts qu'il inflige au joueur
}

/// <summary>
/// Multiplicateurs et paramètres d'un affixe. Teinte exprimée en r/g/b flottants (multiplicatifs,
/// façon HitFlash) pour rester sans dépendance Godot ; EnemyBase construit la Color.
/// </summary>
public readonly struct EliteModifiers
{
    public readonly float HpMult;
    public readonly float SpeedMult;
    public readonly float DamageMult;
    public readonly float XpMult;
    public readonly float DamageTakenMult;        // <1 = encaisse mieux (blindé)
    public readonly float RegenFractionPerSecond; // fraction du MaxHp régénérée /s (0 = aucun)
    public readonly float LifestealFraction;      // part des dégâts infligés récupérée en PV (0 = aucun)
    public readonly float ExplodeDamageMult;      // dégâts d'explosion = Damage × ce mult (0 = pas d'explosion)
    public readonly float HpDropChance;           // proba de dropper un orbe de PV à la mort
    public readonly float TintR, TintG, TintB;

    public EliteModifiers(
        float hpMult, float speedMult, float damageMult, float xpMult,
        float damageTakenMult, float regenFractionPerSecond, float lifestealFraction,
        float explodeDamageMult, float hpDropChance,
        float tintR, float tintG, float tintB)
    {
        HpMult = hpMult;
        SpeedMult = speedMult;
        DamageMult = damageMult;
        XpMult = xpMult;
        DamageTakenMult = damageTakenMult;
        RegenFractionPerSecond = regenFractionPerSecond;
        LifestealFraction = lifestealFraction;
        ExplodeDamageMult = explodeDamageMult;
        HpDropChance = hpDropChance;
        TintR = tintR; TintG = tintG; TintB = tintB;
    }
}

public static class EliteAffixTable
{
    /// <summary>Les affixes tirables (None exclu — c'est l'absence d'affixe).</summary>
    public static readonly EliteAffix[] All =
    {
        EliteAffix.Armored,
        EliteAffix.Regenerating,
        EliteAffix.Explosive,
        EliteAffix.Frenzied,
        EliteAffix.Vampiric,
    };

    // Courbe de fréquence : rare en début de run, monte avec le temps, plafonnée.
    public const float BaseChance      = 0.03f;  // ~3 % au tout début
    public const float ChancePerMinute = 0.02f;  // +2 %/min
    public const float MaxChance       = 0.28f;  // plafond dur (jamais une horde d'élites)

    /// <summary>Rayon (px) et récompense : bonus d'échelle visuelle appliqué au sprite d'élite.</summary>
    public const float VisualScale = 1.35f;

    /// <summary>Probabilité qu'un ennemi basique devienne élite au temps <paramref name="tMinutes"/>.</summary>
    public static float EliteChance(float tMinutes)
    {
        float c = BaseChance + ChancePerMinute * tMinutes;
        if (c < 0f) c = 0f;
        if (c > MaxChance) c = MaxChance;
        return c;
    }

    /// <summary>Décision d'élite à partir d'un tirage uniforme [0,1) (testable/déterministe).</summary>
    public static bool ShouldBeElite(float tMinutes, float roll01)
        => roll01 < EliteChance(tMinutes);

    /// <summary>Choisit un affixe à partir d'un tirage uniforme [0,1) (répartition égale).</summary>
    public static EliteAffix Pick(float pickRoll01)
    {
        int i = (int)(pickRoll01 * All.Length);
        if (i < 0) i = 0;
        if (i >= All.Length) i = All.Length - 1;
        return All[i];
    }

    /// <summary>Table des multiplicateurs par affixe (équilibrage centralisé ici).</summary>
    public static EliteModifiers Modifiers(EliteAffix affix) => affix switch
    {
        //                        hp   spd  dmg  xp  dmgTaken regen  steal explode hpDrop  tintR tintG tintB
        EliteAffix.Armored      => new(1.7f, 1.0f, 1.0f, 3.0f, 0.45f, 0f,    0f,   0f,    0.30f,  0.55f, 0.75f, 1.35f), // bleu acier
        EliteAffix.Regenerating => new(1.5f, 1.0f, 1.0f, 3.0f, 1.0f,  0.06f, 0f,   0f,    0.30f,  0.55f, 1.35f, 0.65f), // vert
        EliteAffix.Explosive    => new(1.1f, 1.05f,1.0f, 2.5f, 1.0f,  0f,    0f,   2.2f,  0.25f,  1.5f,  0.65f, 0.35f), // orange
        EliteAffix.Frenzied     => new(0.7f, 1.7f, 1.3f, 2.5f, 1.0f,  0f,    0f,   0f,    0.20f,  1.6f,  0.45f, 0.45f), // rouge
        EliteAffix.Vampiric     => new(1.4f, 1.05f,1.0f, 3.0f, 1.0f,  0f,    0.5f, 0f,    0.30f,  1.3f,  0.45f, 1.25f), // magenta
        _                       => new(1f,   1f,   1f,   1f,   1f,    0f,    0f,   0f,    0.08f,  1f,    1f,    1f),
    };

    /// <summary>Délai (s) sans coup avant que la régénération d'un affixe Régénérant reprenne.</summary>
    public const float RegenDelaySeconds = 1.5f;

    /// <summary>Rayon (px) de l'explosion de mort d'un affixe Explosif.</summary>
    public const float ExplosionRadius = 84f;
}
