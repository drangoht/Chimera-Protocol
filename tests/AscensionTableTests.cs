using Xunit;

/// <summary>
/// Tests de l'échelle d'ascension (src/Core/Rules/AscensionTable.cs).
///
/// Ce qui est vérifié ici est ce qui casserait le contrat du plan (docs/ENDGAME_PLAN.md) :
/// l'équivalence rang 1 = ancien « Difficile » (dont dépend la validité des records déjà gagnés),
/// le fait qu'un cran ajoute une RÈGLE et non un multiplicateur, l'amortissement des champions, et
/// le déblocage global qui ne doit jamais laisser sauter un cran.
/// </summary>
public class AscensionTableTests
{
    // ─── Rang 1 = l'ancien « Difficile » ─────────────────────────────────────

    [Fact]
    public void Rang1_Reprend_Exactement_Les_Valeurs_De_Lancien_Difficile()
    {
        // C'est ce qui rend les complétions et records déjà gagnés valides sans les réinterpréter.
        Assert.Equal(DifficultyTuning.EnemyHp(2),     AscensionTable.EnemyHpMult(1), 4);
        Assert.Equal(DifficultyTuning.EnemyDamage(2), AscensionTable.EnemyDamageMult(1), 4);
        Assert.Equal(DifficultyTuning.Spawn(2),       AscensionTable.SpawnMult(1), 4);
    }

    [Fact]
    public void Rang0_Est_Normal()
    {
        Assert.Equal(1.00f, AscensionTable.EnemyHpMult(0), 4);
        Assert.Equal(1.00f, AscensionTable.EnemyDamageMult(0), 4);
        Assert.Equal(1.00f, AscensionTable.SpawnMult(0), 4);
        Assert.Equal(1.00f, AscensionTable.HealingMult(0), 4);
        Assert.Equal(1.00f, AscensionTable.RunDurationMult(0), 4);
        Assert.True(AscensionTable.SafetyNetsEnabled(0));
        Assert.Equal(1.00,  AscensionTable.EchoMult(0), 4);
    }

    // ─── Un cran ajoute une RÈGLE, pas un multiplicateur ─────────────────────

    [Fact]
    public void Les_Statistiques_Ne_Montent_Plus_Apres_Le_Rang1()
    {
        // Le parti pris du plan : empiler des statistiques est l'échange que le joueur gagne toujours.
        // Si ce test casse, c'est que quelqu'un a « rééquilibré » en remontant les facteurs.
        for (int r = 2; r <= AscensionTable.MaxRank; r++)
        {
            Assert.Equal(AscensionTable.EnemyHpMult(1),     AscensionTable.EnemyHpMult(r), 4);
            Assert.Equal(AscensionTable.EnemyDamageMult(1), AscensionTable.EnemyDamageMult(r), 4);
            Assert.Equal(AscensionTable.SpawnMult(1),       AscensionTable.SpawnMult(r), 4);
        }
    }

    [Fact]
    public void Chaque_Cran_Ajoute_Exactement_Une_Regle()
    {
        // Une seule dimension doit changer d'un rang au suivant — c'est ce qui rend une mort
        // interprétable par le joueur.
        for (int r = 1; r <= AscensionTable.MaxRank; r++)
        {
            int changed = 0;
            if (AscensionTable.EnemyHpMult(r)         != AscensionTable.EnemyHpMult(r - 1))         changed++;
            if (AscensionTable.HealingMult(r)         != AscensionTable.HealingMult(r - 1))         changed++;
            if (AscensionTable.RunDurationMult(r)     != AscensionTable.RunDurationMult(r - 1))     changed++;
            if (AscensionTable.SafetyNetsEnabled(r)   != AscensionTable.SafetyNetsEnabled(r - 1))   changed++;
            if (AscensionTable.EliteFrequencyMult(r)  != AscensionTable.EliteFrequencyMult(r - 1))  changed++;

            Assert.Equal(1, changed);
        }
    }

    // ─── Les règles elles-mêmes ──────────────────────────────────────────────

    [Fact]
    public void Cran2_Coupe_Les_Soins_Recus()
    {
        Assert.Equal(1.00f, AscensionTable.HealingMult(1), 4);
        Assert.Equal(0.60f, AscensionTable.HealingMult(2), 4);
        Assert.Equal(0.60f, AscensionTable.HealingMult(AscensionTable.MaxRank), 4);
    }

    [Fact]
    public void Cran3_Avance_L_Overtime_Autour_De_La_Dixieme_Minute()
    {
        // Référence actuelle : 780 s (13 min) dans data/meta_upgrades.json.
        float overtimeAt = 780f * AscensionTable.RunDurationMult(3) / 60f;
        Assert.InRange(overtimeAt, 9.8f, 10.2f);
    }

    [Fact]
    public void Cran4_Retire_Les_Filets_De_La_Meta()
    {
        // « Sans filet » : ce cran ne touche aucune statistique, il rend la première erreur définitive
        // — et il vise le power-creep méta (ces filets s'achètent une fois et servent à toutes les runs).
        Assert.True(AscensionTable.SafetyNetsEnabled(3));
        Assert.False(AscensionTable.SafetyNetsEnabled(4));
        Assert.False(AscensionTable.SafetyNetsEnabled(AscensionTable.MaxRank));
    }

    [Fact]
    public void Cran5_Triple_La_Frequence_D_Elites()
    {
        Assert.Equal(1.00f, AscensionTable.EliteFrequencyMult(4), 4);
        Assert.Equal(3.00f, AscensionTable.EliteFrequencyMult(5), 4);
    }

    // ─── Champions : jamais un mur ───────────────────────────────────────────

    [Fact]
    public void Les_Pv_Des_Champions_Sont_Amortis()
    {
        // Battre le boss conditionne la progression et il est calibré sur un TTK JOUÉ : le bonus plein
        // en ferait un mur de patience.
        float basiques = AscensionTable.EnemyHpMult(AscensionTable.MaxRank);
        float champions = AscensionTable.ChampionHpMult(AscensionTable.MaxRank);

        Assert.True(champions < basiques, $"champions {champions} devrait être sous {basiques}");
        Assert.True(champions > 1f, "les champions doivent tout de même gagner des PV");
        Assert.Equal(1f + (basiques - 1f) * LevelThreat.ChampionHpSoftening, champions, 4);
    }

    // ─── Économie ────────────────────────────────────────────────────────────

    [Fact]
    public void Les_Echos_Progressent_De_Vingt_Pourcent_Par_Cran()
    {
        Assert.Equal(1.20, AscensionTable.EchoMult(1), 3);
        Assert.Equal(1.44, AscensionTable.EchoMult(2), 3);
        Assert.Equal(2.49, AscensionTable.EchoMult(5), 2);
    }

    [Fact]
    public void La_Pente_D_Echos_Depasse_Celle_Des_Paliers_De_Niveau()
    {
        // Le coût en compétence est plus élevé : la récompense doit suivre, sinon le joueur optimal
        // reste en bas de l'échelle.
        double parCranAscension = AscensionTable.EchoMult(1);
        double parPalierNiveau  = LevelThreat.EchoMult(1);
        Assert.True(parCranAscension > parPalierNiveau,
            $"ascension {parCranAscension} devrait dépasser palier {parPalierNiveau}");
    }

    // ─── Déblocage ───────────────────────────────────────────────────────────

    [Fact]
    public void On_Ne_Peut_Pas_Sauter_Un_Cran()
    {
        Assert.Equal(1, AscensionTable.MaxSelectable(0));   // jamais battu → rang 1 accessible
        Assert.Equal(3, AscensionTable.MaxSelectable(2));
        Assert.False(AscensionTable.CanSelect(3, 1));
        Assert.True(AscensionTable.CanSelect(2, 1));
    }

    [Fact]
    public void Le_Rang0_Reste_Toujours_Jouable()
    {
        Assert.True(AscensionTable.CanSelect(0, 0));
        Assert.True(AscensionTable.CanSelect(0, AscensionTable.MaxRank));
    }

    [Fact]
    public void Le_Deblocage_Est_Borne_Par_Le_Rang_Maximum()
    {
        Assert.Equal(AscensionTable.MaxRank, AscensionTable.MaxSelectable(AscensionTable.MaxRank));
        Assert.Equal(AscensionTable.MaxRank, AscensionTable.MaxSelectable(99));
        Assert.False(AscensionTable.CanSelect(AscensionTable.MaxRank + 1, 99));
    }

    [Fact]
    public void Un_Rang_Negatif_Ne_Passe_Pas()
    {
        Assert.False(AscensionTable.CanSelect(-1, 3));
        Assert.Equal(1.00f, AscensionTable.EnemyHpMult(-1), 4);
    }

    // ─── Règles actives (affichage avant la run) ─────────────────────────────

    [Fact]
    public void Les_Regles_Actives_Sont_Cumulatives()
    {
        Assert.Empty(AscensionTable.ActiveRanks(0));
        Assert.Single(AscensionTable.ActiveRanks(1));
        Assert.Equal(3, AscensionTable.ActiveRanks(3).Count);
        Assert.Equal(AscensionTable.MaxRank, AscensionTable.ActiveRanks(AscensionTable.MaxRank).Count);
    }

    [Fact]
    public void Chaque_Cran_A_Ses_Cles_De_Loc()
    {
        Assert.Equal(AscensionTable.MaxRank, AscensionTable.Ranks.Count);
        for (int i = 0; i < AscensionTable.Ranks.Count; i++)
        {
            var rank = AscensionTable.Ranks[i];
            Assert.Equal(i + 1, rank.Value);
            Assert.False(string.IsNullOrWhiteSpace(rank.NameKey));
            Assert.False(string.IsNullOrWhiteSpace(rank.RuleKey));
        }
    }

    // ─── Migration ───────────────────────────────────────────────────────────

    [Fact]
    public void Difficile_Devient_Normal_Plus_Ascension1()
    {
        var (diff, asc) = AscensionTable.MigrateLegacyDifficulty(2);
        Assert.Equal(1, diff);
        Assert.Equal(1, asc);
    }

    [Fact]
    public void Facile_Reste_De_L_Assistance_Hors_Echelle()
    {
        var (diff, asc) = AscensionTable.MigrateLegacyDifficulty(0);
        Assert.Equal(0, diff);
        Assert.Equal(0, asc);
    }

    [Fact]
    public void Normal_Reste_Normal()
    {
        var (diff, asc) = AscensionTable.MigrateLegacyDifficulty(1);
        Assert.Equal(1, diff);
        Assert.Equal(0, asc);
    }

    [Fact]
    public void La_Migration_De_Difficile_Conserve_La_Menace_A_L_Identique()
    {
        // Le point du §7.1 : un record gagné en « Difficile » ne doit être ni effacé, ni réinterprété
        // à la hausse. Les multiplicateurs doivent donc coïncider exactement.
        var (_, asc) = AscensionTable.MigrateLegacyDifficulty(2);
        Assert.Equal(DifficultyTuning.EnemyHp(2),     AscensionTable.EnemyHpMult(asc), 4);
        Assert.Equal(DifficultyTuning.EnemyDamage(2), AscensionTable.EnemyDamageMult(asc), 4);
        Assert.Equal(DifficultyTuning.Spawn(2),       AscensionTable.SpawnMult(asc), 4);
    }
}
