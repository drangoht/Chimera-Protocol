using System.Linq;
using Xunit;

/// <summary>
/// Le calendrier de l'Aimant.
///
/// <para>Ces tests existent parce que l'Aimant <b>n'avait jamais été porté</b> sous Unity, alors que
/// l'amélioration du Hub qui l'étend (<c>bonus_magnet</c>, 770 Échos cumulés) est restée achetable
/// depuis la 2.0.0. Signalé en jouant le 2026-08-12 : « il manque l'aimant qui apparaissait dans la
/// version Godot ».</para>
/// </summary>
public class MagnetScheduleTests
{
    /// <summary>Durée impartie standard — <c>GameManager.RunDurationSeconds</c>.</summary>
    private const int BossArrival = 780;

    /// <summary>Tirage déterministe : toujours le bas de la fenêtre.</summary>
    private static float Low(int min, int max) => min;

    /// <summary>Tirage déterministe : toujours le haut de la fenêtre.</summary>
    private static float High(int min, int max) => max;

    [Fact]
    public void TroisApparitionsSansAmelioration()
    {
        Assert.Equal(3, MagnetSchedule.SpawnTimes(0, BossArrival, Low).Length);
    }

    /// <summary>
    /// « +1 apparition par run et par niveau » : c'est ce que la description promet au joueur, et
    /// c'est exactement ce qui ne se produisait pas — puisque l'objet n'existait pas.
    /// </summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    public void ChaqueNiveauDeBonusMagnetAjouteUneApparition(int charges, int expected)
    {
        Assert.Equal(expected, MagnetSchedule.SpawnTimes(charges, BossArrival, Low).Length);
    }

    /// <summary>Une sauvegarde éditée ne doit pas accorder plus que le maximum acheté.</summary>
    [Fact]
    public void UnNiveauAberrantEstBorne()
    {
        Assert.Equal(3 + MagnetSchedule.MaxBonusCharges,
                     MagnetSchedule.SpawnTimes(99, BossArrival, Low).Length);
        Assert.Equal(3, MagnetSchedule.SpawnTimes(-5, BossArrival, Low).Length);
    }

    /// <summary>
    /// Les fenêtres bonus vivent en <b>overtime</b> : c'est là que le sol se couvre d'orbes qu'un
    /// joueur ne peut plus aller chercher à la main. Une charge placée avant le boss ferait doublon
    /// avec la troisième fenêtre de base.
    /// </summary>
    [Fact]
    public void LesFenetresBonusTombentApresLArriveeDuBoss()
    {
        var bonus = MagnetSchedule.WindowsFor(2, BossArrival).Skip(MagnetSchedule.Windows.Count);

        Assert.All(bonus, w => Assert.True(w.Min > BossArrival, $"fenetre bonus a {w.Min}s"));
    }

    /// <summary>
    /// ⚠ Le spawner avance un index unique et ne revient jamais en arrière : une liste non triée
    /// ferait perdre une apparition au joueur, silencieusement.
    /// </summary>
    [Fact]
    public void LesInstantsSontTries()
    {
        foreach (var roll in new System.Func<int, int, float>[] { Low, High })
        {
            var times = MagnetSchedule.SpawnTimes(2, BossArrival, roll);
            Assert.Equal(times.OrderBy(t => t), times);
        }
    }

    /// <summary>
    /// La troisième fenêtre tombe juste avant le boss, jamais après : passé ce point le joueur
    /// affronte l'incarnation finale, et un aimant tiré pendant ce combat arrive trop tard pour
    /// nettoyer ce que la run a semé.
    /// </summary>
    [Fact]
    public void LaTroisiemeFenetrePrecedeLeBoss()
    {
        var derniere = MagnetSchedule.Windows[MagnetSchedule.Windows.Count - 1];

        Assert.True(derniere.Max < BossArrival, $"la 3e fenetre se ferme a {derniere.Max}s");
        Assert.True(derniere.Min > MagnetSchedule.Windows[1].Max);
    }

    /// <summary>Aucune fenêtre ne se chevauche : trois trouvailles distinctes, pas une rafale.</summary>
    [Fact]
    public void LesFenetresNeSeChevauchentPas()
    {
        var windows = MagnetSchedule.WindowsFor(MagnetSchedule.MaxBonusCharges, BossArrival);

        for (int i = 1; i < windows.Count; i++)
            Assert.True(windows[i].Min > windows[i - 1].Max,
                        $"fenetres {i - 1} et {i} se chevauchent");
    }
}
