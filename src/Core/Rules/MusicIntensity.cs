using System;

/// <summary>
/// Musique adaptative : calcul de l'« intensité » d'une run et des gains des
/// pistes musicales (logique pure, sans dépendance Godot — testable).
///
/// Chaque biome fournit deux versions du même morceau — <c>calm</c> (couplet) et
/// <c>combat</c> (refrain) — plus une piste de boss commune à tous les biomes
/// (cf. <c>docs/AUDIO_AI_PROMPTS.md</c>). Les trois tournent en permanence et une
/// seule est audible à la fois : la bascule se fait par fondu croisé de
/// <see cref="CrossfadeSec"/>, jamais par une coupure.
///
/// Pourquoi pas de superposition permanente : ces pistes sont des morceaux
/// complets générés séparément, donc <b>non synchronisés à l'échantillon</b>. Les
/// mélanger en continu produirait deux batteries décalées. C'est la différence
/// avec l'ancienne architecture à 4 stems synchronisés, rendue caduque par le
/// passage à une bande-son metal (<c>docs/ART_BRIEF_AUDIO.md</c>, encart de tête).
/// </summary>
public static class MusicIntensity
{
    // -----------------------------------------------------------------------
    // Composantes de l'intensité
    // -----------------------------------------------------------------------

    /// <summary>Nombre d'ennemis à l'écran au-delà duquel la pression est maximale.</summary>
    public const int EnemySaturation = 55;

    /// <summary>Durée de run au-delà de laquelle la composante temporelle sature (12 min).</summary>
    public const float TimeSaturationSec = 720f;

    // Poids relatifs (somme = 1). La densité d'ennemis domine : c'est ce que le
    // joueur ressent immédiatement. Le temps garantit une montée de fond même
    // pendant une accalmie, et les PV bas font monter la tension sans tricher.
    public const float EnemyWeight  = 0.50f;
    public const float TimeWeight   = 0.30f;
    public const float DangerWeight = 0.20f;

    // -----------------------------------------------------------------------
    // Lissage
    // -----------------------------------------------------------------------

    /// <summary>Vitesse de montée (unités d'intensité par seconde) — réactif.</summary>
    public const float RiseRatePerSec = 0.55f;

    /// <summary>Vitesse de descente — volontairement 3× plus lente que la montée.</summary>
    /// <remarks>
    /// Asymétrie délibérée : une vague qui meurt en 2 s ne doit pas faire retomber
    /// la musique aussitôt, sinon les couches pompent en permanence pendant un
    /// enchaînement de vagues.
    /// </remarks>
    public const float FallRatePerSec = 0.18f;

    // -----------------------------------------------------------------------
    // Bascule entre pistes
    // -----------------------------------------------------------------------

    /// <summary>Intensité à partir de laquelle on passe au refrain (piste <c>combat</c>).</summary>
    public const float CombatEnter = 0.42f;

    /// <summary>Intensité en dessous de laquelle on revient au couplet (piste <c>calm</c>).</summary>
    /// <remarks>
    /// Nettement sous <see cref="CombatEnter"/> : sans cette hystérésis, une
    /// intensité qui oscille autour d'un seuil unique déclencherait des allers-retours
    /// permanents entre les deux pistes — le défaut le plus audible de ce type de
    /// système. <see cref="MinHoldSec"/> complète le dispositif côté temps.
    /// </remarks>
    public const float CombatExit = 0.26f;

    /// <summary>Durée minimale pendant laquelle une piste reste en place avant de pouvoir changer.</summary>
    public const float MinHoldSec = 10f;

    /// <summary>Durée du fondu croisé entre deux pistes.</summary>
    public const float CrossfadeSec = 3f;

    /// <summary>Durée du fondu d'entrée/sortie de la piste de boss — le boss s'annonce.</summary>
    public const float BossCrossfadeSec = 2f;

    // -----------------------------------------------------------------------
    // Calcul
    // -----------------------------------------------------------------------

    /// <summary>
    /// Intensité cible dans [0, 1] à partir de l'état de la run.
    /// </summary>
    /// <param name="enemiesAlive">Ennemis vivants à l'écran.</param>
    /// <param name="elapsedSeconds">Temps écoulé depuis le début de la run.</param>
    /// <param name="healthRatio">PV courants / PV max, dans [0, 1].</param>
    public static float Compute(int enemiesAlive, float elapsedSeconds, float healthRatio)
    {
        float crowd  = Clamp01(enemiesAlive / (float)EnemySaturation);
        float time   = Clamp01(elapsedSeconds / TimeSaturationSec);
        float danger = 1f - Clamp01(healthRatio);

        // Racine sur la densité : les premiers ennemis comptent plus que les
        // derniers (passer de 0 à 10 ennemis change tout, de 40 à 50 presque rien).
        crowd = (float)Math.Sqrt(crowd);

        return Clamp01(crowd * EnemyWeight + time * TimeWeight + danger * DangerWeight);
    }

    /// <summary>
    /// Rapproche <paramref name="current"/> de <paramref name="target"/> à vitesse
    /// bornée, plus vite en montée qu'en descente.
    /// </summary>
    public static float Smooth(float current, float target, float delta)
    {
        if (delta <= 0f) return current;

        float rate = target > current ? RiseRatePerSec : FallRatePerSec;
        float step = rate * delta;

        if (Math.Abs(target - current) <= step) return target;
        return current + Math.Sign(target - current) * step;
    }

    /// <summary>Interpolation douce (dérivée nulle aux bornes) — pas de rupture de pente.</summary>
    public static float SmoothStep(float x, float low, float high)
    {
        if (high <= low) return x >= high ? 1f : 0f;
        float t = Clamp01((x - low) / (high - low));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Piste qui devrait jouer, compte tenu de celle en cours (hystérésis).
    /// </summary>
    /// <param name="current">Piste actuellement au premier plan.</param>
    /// <param name="intensity">Intensité lissée de la run, dans [0, 1].</param>
    /// <param name="bossActive">Vrai tant qu'un boss ou mini-boss est en vie.</param>
    public static MusicLayer Select(MusicLayer current, float intensity, bool bossActive)
    {
        if (bossActive) return MusicLayer.Boss;

        // Depuis le refrain (ou la sortie de boss), il faut redescendre franchement
        // sous le seuil bas pour revenir au couplet.
        if (current != MusicLayer.Calm)
            return intensity <= CombatExit ? MusicLayer.Calm : MusicLayer.Combat;

        return intensity >= CombatEnter ? MusicLayer.Combat : MusicLayer.Calm;
    }

    /// <summary>
    /// Rapproche le poids d'une piste de sa cible (1 au premier plan, 0 sinon),
    /// à la vitesse du fondu croisé.
    /// </summary>
    public static float Approach(float weight, float target, float delta, float crossfadeSec)
    {
        if (delta <= 0f || crossfadeSec <= 0f) return target;

        float step = delta / crossfadeSec;
        if (Math.Abs(target - weight) <= step) return target;
        return weight + Math.Sign(target - weight) * step;
    }

    /// <summary>
    /// Convertit un poids de fondu [0, 1] en gain dB, à puissance constante.
    /// </summary>
    /// <remarks>
    /// Loi en racine (soit 10·log₁₀ au lieu de 20·log₁₀) : deux morceaux
    /// **décorrélés** qui se croisent à poids w et 1−w conservent une puissance
    /// totale constante si leurs amplitudes valent √w et √(1−w). Un fondu linéaire
    /// en amplitude, lui, creuse un trou de volume audible au milieu du croisement.
    /// </remarks>
    public static float WeightToDb(float weight)
    {
        weight = Clamp01(weight);
        if (weight <= 0.0005f) return Silence;
        if (weight >= 0.9995f) return 0f;

        return Math.Max(Silence, 10f * (float)Math.Log10(weight));
    }

    /// <summary>Valeur de gain considérée comme un silence total.</summary>
    public const float Silence = -80f;

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}

/// <summary>
/// Les trois pistes candidates pendant une run. L'ordre suit la montée de tension.
/// </summary>
public enum MusicLayer
{
    /// <summary>Couplet : riff en retenue, la piste par défaut.</summary>
    Calm,

    /// <summary>Refrain : le morceau ouvert en grand, quand la pression monte.</summary>
    Combat,

    /// <summary>Thème de boss, commun à tous les biomes.</summary>
    Boss,
}
