using System;
using System.Collections.Generic;

/// <summary>
/// Calendrier d'apparition de l'<b>Aimant</b> — logique pure, testable.
///
/// <para><b>Pourquoi l'Aimant n'apparaît pas à intervalle régulier.</b> Un Noyau d'Aether tombe
/// toutes les 45 s : c'est une cadence, elle se compte. L'Aimant, lui, est un <i>événement</i>, et
/// trois par run seulement. Le poser sur un minuteur en ferait une ressource à budgétiser ; le tirer
/// dans une <b>fenêtre</b> en fait une trouvaille, dont on sait seulement qu'elle viendra tôt, au
/// milieu, puis juste avant le boss.</para>
///
/// <para><b>Pourquoi cette classe existe séparément.</b> L'Aimant est un système entier qui n'a
/// <b>jamais été porté</b> sous Unity, alors que l'amélioration du Hub qui l'étend — <c>bonus_magnet</c>,
/// 220 puis 550 Échos — est restée achetable tout ce temps. Un joueur pouvait donc payer 770 Échos
/// pour « +1 apparition par run » d'un objet qui n'apparaissait <b>jamais</b>. C'est la quatrième
/// occurrence du même défaut dans ce projet (cf. le cran IV de saturation, qui coupait trois filets
/// du Hub qui n'existaient pas) : une règle qui marche exactement comme écrit, sur du vide.</para>
/// </summary>
public static class MagnetSchedule
{
    /// <summary>
    /// Les trois fenêtres de base, en secondes. La troisième tombe juste avant l'arrivée du boss
    /// (~780 s) : c'est le moment où le sol est le plus couvert d'orbes jamais ramassées.
    /// </summary>
    public static readonly IReadOnlyList<(int Min, int Max)> Windows = new[]
    {
        (120, 300),   // 1re : 2–5 min
        (360, 600),   // 2e  : 6–10 min
        (700, 760),   // 3e  : ~11,7–12,7 min
    };

    /// <summary>Espacement des fenêtres bonus (overtime), en secondes après l'arrivée du boss.</summary>
    public const int BonusSpacingSeconds = 480;

    /// <summary>Demi-largeur d'une fenêtre bonus, en secondes.</summary>
    public const int BonusHalfWidth = 40;

    /// <summary>Nombre maximal de niveaux de <c>bonus_magnet</c> — <c>meta_upgrades.json</c>.</summary>
    public const int MaxBonusCharges = 2;

    /// <summary>
    /// Fenêtres effectives pour une run : les trois de base, plus une par niveau de
    /// <c>bonus_magnet</c>, placées en overtime.
    /// </summary>
    /// <param name="bonusCharges">Niveau de l'amélioration <c>bonus_magnet</c> (0 à 2).</param>
    /// <param name="bossArrivalSeconds">
    /// Durée impartie avant l'overtime. Les fenêtres bonus s'y accrochent — l'overtime dure
    /// potentiellement très longtemps, et c'est précisément là que l'XP devient impossible à
    /// ramasser à la main dans le chaos.
    /// </param>
    public static List<(int Min, int Max)> WindowsFor(int bonusCharges, int bossArrivalSeconds)
    {
        var windows = new List<(int Min, int Max)>(Windows);

        int charges = Math.Clamp(bonusCharges, 0, MaxBonusCharges);
        for (int i = 1; i <= charges; i++)
        {
            int center = bossArrivalSeconds + BonusSpacingSeconds * i;
            windows.Add((center - BonusHalfWidth, center + BonusHalfWidth));
        }

        return windows;
    }

    /// <summary>
    /// Instants d'apparition d'une run, <b>triés</b>.
    /// </summary>
    /// <param name="roll">
    /// Tirage dans une fenêtre (bornes incluses), injecté pour rester déterministe sous
    /// <c>--seed</c>.
    /// </param>
    /// <remarks>
    /// ⚠ Le tri n'est pas cosmétique : le spawner avance un index unique dans la liste et ne revient
    /// jamais en arrière. Non triée, une fenêtre tardive tirée bas mangerait la charge d'une fenêtre
    /// précoce — le joueur perdrait une apparition sans que rien ne le signale.
    /// </remarks>
    public static float[] SpawnTimes(int bonusCharges, int bossArrivalSeconds, Func<int, int, float> roll)
    {
        var windows = WindowsFor(bonusCharges, bossArrivalSeconds);

        var times = new float[windows.Count];
        for (int i = 0; i < windows.Count; i++) times[i] = roll(windows[i].Min, windows[i].Max);

        Array.Sort(times);
        return times;
    }
}
