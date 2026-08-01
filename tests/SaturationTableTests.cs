using Xunit;

/// <summary>
/// Tests de l'échelle de saturation (src/Core/Rules/SaturationTable.cs).
///
/// Ce qui est vérifié ici est ce qui casserait le contrat du plan (docs/ENDGAME_PLAN.md) :
/// l'équivalence rang 1 = ancien « Difficile » (dont dépend la validité des records déjà gagnés),
/// le fait qu'un cran ajoute une RÈGLE et non un multiplicateur, l'amortissement des champions, et
/// le déblocage global qui ne doit jamais laisser sauter un cran.
/// </summary>
public class SaturationTableTests
{
    // ─── Rang 1 = l'ancien « Difficile » ─────────────────────────────────────

    [Fact]
    public void Rang2_Reprend_Exactement_Les_Valeurs_De_Lancien_Difficile()
    {
        // « Meute » (les statistiques) est le cran II depuis le 2026-07-30 : mesuré seul en cran I, son
        // effet frôlait le seuil de détection du banc et le testeur ne voyait aucune différence.
        Assert.Equal(DifficultyTuning.EnemyHp(2),     SaturationTable.EnemyHpMult(2), 4);
        Assert.Equal(DifficultyTuning.EnemyDamage(2), SaturationTable.EnemyDamageMult(2), 4);
        Assert.Equal(DifficultyTuning.Spawn(2),       SaturationTable.SpawnMult(2), 4);
    }

    [Fact]
    public void Rang1_Est_Hemorragie_Et_Ne_Touche_Pas_Les_Statistiques()
    {
        // La porte d'entrée doit se SENTIR : elle coupe le canal de soin dominant, elle n'ajoute pas
        // des points de vie aux ennemis.
        Assert.Equal(0.60f, SaturationTable.HealingMult(1), 4);
        Assert.Equal(1.00f, SaturationTable.EnemyHpMult(1), 4);
        Assert.Equal(1.00f, SaturationTable.EnemyDamageMult(1), 4);
        Assert.Equal(1.00f, SaturationTable.SpawnMult(1), 4);
    }

    [Fact]
    public void Rang0_Est_Normal()
    {
        Assert.Equal(1.00f, SaturationTable.EnemyHpMult(0), 4);
        Assert.Equal(1.00f, SaturationTable.EnemyDamageMult(0), 4);
        Assert.Equal(1.00f, SaturationTable.SpawnMult(0), 4);
        Assert.Equal(1.00f, SaturationTable.HealingMult(0), 4);
        Assert.Equal(1.00f, SaturationTable.RunDurationMult(0), 4);
        Assert.True(SaturationTable.SafetyNetsEnabled(0));
        Assert.Equal(1.00,  SaturationTable.EchoMult(0), 4);
    }

    // ─── Un cran ajoute une RÈGLE, pas un multiplicateur ─────────────────────

    [Fact]
    public void Les_Statistiques_Ne_Montent_Plus_Apres_Le_Rang2()
    {
        // Le parti pris du plan : empiler des statistiques est l'échange que le joueur gagne toujours.
        // Si ce test casse, c'est que quelqu'un a « rééquilibré » en remontant les facteurs.
        for (int r = 3; r <= SaturationTable.MaxRank; r++)
        {
            Assert.Equal(SaturationTable.EnemyHpMult(2),     SaturationTable.EnemyHpMult(r), 4);
            Assert.Equal(SaturationTable.EnemyDamageMult(2), SaturationTable.EnemyDamageMult(r), 4);
            Assert.Equal(SaturationTable.SpawnMult(2),       SaturationTable.SpawnMult(r), 4);
        }
    }

    [Fact]
    public void Chaque_Cran_Ajoute_Exactement_Une_Regle()
    {
        // Une seule dimension doit changer d'un rang au suivant — c'est ce qui rend une mort
        // interprétable par le joueur.
        for (int r = 1; r <= SaturationTable.MaxRank; r++)
        {
            int changed = 0;
            if (SaturationTable.EnemyHpMult(r)         != SaturationTable.EnemyHpMult(r - 1))         changed++;
            if (SaturationTable.HealingMult(r)         != SaturationTable.HealingMult(r - 1))         changed++;
            if (SaturationTable.RunDurationMult(r)     != SaturationTable.RunDurationMult(r - 1))     changed++;
            // « Élite ordinaire » agit lui aussi sur deux leviers — la fréquence ET la prime (XP majorée,
            // orbe de PV privilégié) — pour UNE règle : quand l'élite devient la norme, elle cesse d'être
            // un événement, donc d'en payer la récompense. Même traitement que « Sans filet » ci-dessous.
            if (SaturationTable.EliteFrequencyMult(r)  != SaturationTable.EliteFrequencyMult(r - 1)
             || SaturationTable.ElitesKeepRewards(r)   != SaturationTable.ElitesKeepRewards(r - 1))    changed++;
            // « Sans filet » agit sur deux leviers (consommables méta + soin de passage de niveau) mais
            // énonce UNE règle — « plus aucun rattrapage automatique ». Ils comptent donc pour un, et le
            // test ci-dessous vérifie qu'ils basculent bien au même rang : s'ils se séparaient, ce
            // seraient deux règles déguisées en une, et une mort cesserait d'être interprétable.
            if (SaturationTable.SafetyNetsEnabled(r)   != SaturationTable.SafetyNetsEnabled(r - 1)
             || SaturationTable.LevelUpHealsEnabled(r) != SaturationTable.LevelUpHealsEnabled(r - 1))  changed++;

            Assert.Equal(1, changed);
        }
    }

    [Fact]
    public void Les_Deux_Leviers_De_Sans_Filet_Basculent_Au_Meme_Rang()
    {
        for (int r = 0; r <= SaturationTable.MaxRank; r++)
            Assert.Equal(SaturationTable.SafetyNetsEnabled(r), SaturationTable.LevelUpHealsEnabled(r));
    }

    // ─── Les règles elles-mêmes ──────────────────────────────────────────────

    [Fact]
    public void Cran1_Coupe_Les_Soins_Recus_Et_Le_Reste_Des_Crans_Le_Conserve()
    {
        Assert.Equal(1.00f, SaturationTable.HealingMult(0), 4);
        Assert.Equal(0.60f, SaturationTable.HealingMult(1), 4);
        Assert.Equal(0.60f, SaturationTable.HealingMult(SaturationTable.MaxRank), 4);
    }

    [Fact]
    public void Cran3_Avance_L_Overtime_Autour_De_La_Dixieme_Minute()
    {
        // Référence actuelle : 780 s (13 min) dans data/meta_upgrades.json.
        float overtimeAt = 780f * SaturationTable.RunDurationMult(3) / 60f;
        Assert.InRange(overtimeAt, 9.8f, 10.2f);
    }

    [Fact]
    public void Cran4_Retire_Les_Filets_De_La_Meta()
    {
        // « Sans filet » : ce cran ne touche aucune statistique, il rend la première erreur définitive
        // — et il vise le power-creep méta (ces filets s'achètent une fois et servent à toutes les runs).
        Assert.True(SaturationTable.SafetyNetsEnabled(3));
        Assert.False(SaturationTable.SafetyNetsEnabled(4));
        Assert.False(SaturationTable.SafetyNetsEnabled(SaturationTable.MaxRank));
    }

    [Fact]
    public void Cran4_Retire_Aussi_Le_Soin_De_Passage_De_Niveau()
    {
        // Sans ce second levier, le cran ne retirait rien à un joueur n'ayant acheté ni Noyau de Secours
        // ni Plaque Adaptative — cas de la sauvegarde de référence du 2026-07-30, 84 runs et 25 186 Échos
        // en banque, aucun des deux acheté. Un cran conditionnel à un achat n'est pas une règle lisible.
        Assert.True(SaturationTable.LevelUpHealsEnabled(3));
        Assert.False(SaturationTable.LevelUpHealsEnabled(4));
        Assert.False(SaturationTable.LevelUpHealsEnabled(SaturationTable.MaxRank));
    }

    [Fact]
    public void Cran5_Triple_La_Frequence_D_Elites()
    {
        Assert.Equal(1.00f, SaturationTable.EliteFrequencyMult(4), 4);
        Assert.Equal(3.00f, SaturationTable.EliteFrequencyMult(5), 4);
    }

    [Fact]
    public void Cran5_Retire_La_Prime_Des_Elites()
    {
        // Mesuré le 2026-08-01 (4 graines appariées) : sans ce retrait, le cran 5 rendait au joueur
        // +41,4 % de soins ponctuels par rapport au cran 0 — 4/4, net — alors qu'« Hémorragie » (cran I)
        // les coupe de 40 %. Tripler la fréquence d'élite triplait aussi la source de soin, parce que
        // l'affixe soude trois rôles : plus dangereux, plus rémunérateur, plus généreux.
        Assert.True(SaturationTable.ElitesKeepRewards(4));
        Assert.False(SaturationTable.ElitesKeepRewards(5));
    }

    [Fact]
    public void Les_Deux_Leviers_D_Elite_Ordinaire_Basculent_Au_Meme_Rang()
    {
        // S'ils se séparaient, un cran augmenterait la fréquence sans couper la prime — et
        // redistribuerait la difficulté ET son antidote, ce que la mesure ci-dessus a sanctionné.
        for (int r = 0; r <= SaturationTable.MaxRank; r++)
        {
            bool frequencyRaised = SaturationTable.EliteFrequencyMult(r) > 1f;
            Assert.Equal(frequencyRaised, !SaturationTable.ElitesKeepRewards(r));
        }
    }

    [Fact]
    public void Cran5_Ne_Rend_Pas_Les_Elites_Moins_Dangereuses()
    {
        // Le cran retire une RÉCOMPENSE, jamais une menace : le plafond de fréquence monte, et rien
        // dans la table d'affixes ne s'adoucit. Verrou contre la dérive inverse (« les élites étant
        // partout, adoucissons-les ») qui viderait le cran de son sens.
        Assert.True(SaturationTable.EliteChanceCap(5) > SaturationTable.EliteChanceCap(4));
        foreach (var affix in EliteAffixTable.All)
        {
            var m = EliteAffixTable.Modifiers(affix);
            Assert.True(m.HpMult > 0f && m.DamageMult > 0f && m.SpeedMult > 0f);
        }
    }

    // ─── Champions : jamais un mur ───────────────────────────────────────────

    [Fact]
    public void Les_Pv_Des_Champions_Sont_Amortis()
    {
        // Battre le boss conditionne la progression et il est calibré sur un TTK JOUÉ : le bonus plein
        // en ferait un mur de patience.
        float basiques = SaturationTable.EnemyHpMult(SaturationTable.MaxRank);
        float champions = SaturationTable.ChampionHpMult(SaturationTable.MaxRank);

        Assert.True(champions < basiques, $"champions {champions} devrait être sous {basiques}");
        Assert.True(champions > 1f, "les champions doivent tout de même gagner des PV");
        Assert.Equal(1f + (basiques - 1f) * LevelThreat.ChampionHpSoftening, champions, 4);
    }

    // ─── Économie ────────────────────────────────────────────────────────────

    [Fact]
    public void Les_Echos_Progressent_De_Vingt_Pourcent_Par_Cran()
    {
        Assert.Equal(1.20, SaturationTable.EchoMult(1), 3);
        Assert.Equal(1.44, SaturationTable.EchoMult(2), 3);
        Assert.Equal(2.49, SaturationTable.EchoMult(5), 2);
    }

    [Fact]
    public void La_Pente_D_Echos_Depasse_Celle_Des_Paliers_De_Niveau()
    {
        // Le coût en compétence est plus élevé : la récompense doit suivre, sinon le joueur optimal
        // reste en bas de l'échelle.
        double parCranSaturation = SaturationTable.EchoMult(1);
        double parPalierNiveau  = LevelThreat.EchoMult(1);
        Assert.True(parCranSaturation > parPalierNiveau,
            $"saturation {parCranSaturation} devrait dépasser palier {parPalierNiveau}");
    }

    // ─── Déblocage ───────────────────────────────────────────────────────────

    [Fact]
    public void On_Ne_Peut_Pas_Sauter_Un_Cran()
    {
        Assert.Equal(1, SaturationTable.MaxSelectable(0));   // jamais battu → rang 1 accessible
        Assert.Equal(3, SaturationTable.MaxSelectable(2));
        Assert.False(SaturationTable.CanSelect(3, 1));
        Assert.True(SaturationTable.CanSelect(2, 1));
    }

    [Fact]
    public void Le_Rang0_Reste_Toujours_Jouable()
    {
        Assert.True(SaturationTable.CanSelect(0, 0));
        Assert.True(SaturationTable.CanSelect(0, SaturationTable.MaxRank));
    }

    [Fact]
    public void Le_Deblocage_Est_Borne_Par_Le_Rang_Maximum()
    {
        Assert.Equal(SaturationTable.MaxRank, SaturationTable.MaxSelectable(SaturationTable.MaxRank));
        Assert.Equal(SaturationTable.MaxRank, SaturationTable.MaxSelectable(99));
        Assert.False(SaturationTable.CanSelect(SaturationTable.MaxRank + 1, 99));
    }

    [Fact]
    public void Un_Rang_Negatif_Ne_Passe_Pas()
    {
        Assert.False(SaturationTable.CanSelect(-1, 3));
        Assert.Equal(1.00f, SaturationTable.EnemyHpMult(-1), 4);
    }

    // ─── Règles actives (affichage avant la run) ─────────────────────────────

    [Fact]
    public void Les_Regles_Actives_Sont_Cumulatives()
    {
        Assert.Empty(SaturationTable.ActiveRanks(0));
        Assert.Single(SaturationTable.ActiveRanks(1));
        Assert.Equal(3, SaturationTable.ActiveRanks(3).Count);
        Assert.Equal(SaturationTable.MaxRank, SaturationTable.ActiveRanks(SaturationTable.MaxRank).Count);
    }

    [Fact]
    public void Chaque_Cran_A_Ses_Cles_De_Loc()
    {
        Assert.Equal(SaturationTable.MaxRank, SaturationTable.Ranks.Count);
        for (int i = 0; i < SaturationTable.Ranks.Count; i++)
        {
            var rank = SaturationTable.Ranks[i];
            Assert.Equal(i + 1, rank.Value);
            Assert.False(string.IsNullOrWhiteSpace(rank.NameKey));
            Assert.False(string.IsNullOrWhiteSpace(rank.RuleKey));
        }
    }

    // ─── Migration ───────────────────────────────────────────────────────────

    [Fact]
    public void Difficile_Ouvre_Le_Cran1_Sans_L_Activer()
    {
        // Le point décisif de la migration : un joueur de la 1.24.0 qui jouait en « Difficile » ne doit
        // pas se réveiller avec « Hémorragie » activée sans l'avoir choisie. On lui OUVRE la porte
        // (Beaten = 1), on ne le pousse pas dedans (Saturation = 0).
        var (diff, sat, beaten) = SaturationTable.MigrateLegacyDifficulty(2);
        Assert.Equal(1, diff);
        Assert.Equal(0, sat);
        Assert.Equal(1, beaten);
    }

    [Fact]
    public void Facile_Reste_De_L_Assistance_Hors_Echelle()
    {
        var (diff, sat, beaten) = SaturationTable.MigrateLegacyDifficulty(0);
        Assert.Equal(0, diff);
        Assert.Equal(0, sat);
        Assert.Equal(0, beaten);
    }

    [Fact]
    public void Normal_Reste_Normal()
    {
        var (diff, sat, beaten) = SaturationTable.MigrateLegacyDifficulty(1);
        Assert.Equal(1, diff);
        Assert.Equal(0, sat);
        Assert.Equal(0, beaten);
    }

    [Fact]
    public void Aucune_Migration_N_Active_Un_Cran_Non_Choisi()
    {
        // Vrai pour les trois difficultés : la migration ne change JAMAIS le gameplay de la prochaine
        // run d'un joueur existant, elle ne fait que lui ouvrir l'échelle.
        for (int legacy = 0; legacy <= 2; legacy++)
            Assert.Equal(0, SaturationTable.MigrateLegacyDifficulty(legacy).Saturation);
    }

    // ─── Schéma 1 (cran global) → schéma 2 (cran par niveau) ─────────────────

    private static readonly string[] Biomes =
        { "sanctuaire", "aether", "givre", "fournaise", "neon" };

    [Fact]
    public void Le_Cran_Global_Est_Diffuse_A_Tous_Les_Niveaux()
    {
        // Sous le schéma 1 le déblocage était GLOBAL : le joueur avait bien accès au cran 2 partout.
        // Le lui retirer sur quatre biomes serait une régression, et on ne sait pas où il l'a gagné.
        var (choice, beaten) = SaturationTable.DiffuseGlobalRanks(Biomes, globalChoice: 1, globalBeaten: 2);

        Assert.Equal(Biomes.Length, choice.Count);
        foreach (var b in Biomes)
        {
            Assert.Equal(1, choice[b]);
            Assert.Equal(2, beaten[b]);
        }
    }

    [Fact]
    public void La_Diffusion_Borne_Le_Choix_Par_Le_Deblocage()
    {
        // Fichier incohérent (cran 4 choisi, rien de battu) : la migration ne doit pas ouvrir l'échelle
        // par la porte de derrière. MaxSelectable(0) vaut 1.
        var (choice, _) = SaturationTable.DiffuseGlobalRanks(Biomes, globalChoice: 4, globalBeaten: 0);
        foreach (var b in Biomes) Assert.Equal(1, choice[b]);
    }

    [Fact]
    public void Un_Niveau_Absent_De_La_Table_Vaut_Cran_Zero()
    {
        // Un biome ajouté après coup ne doit hériter d'aucun cran : la table est la seule source, et
        // l'absence y vaut 0 (c'est ce que fait GameSettings.SaturationFor).
        var (choice, beaten) = SaturationTable.DiffuseGlobalRanks(Biomes, 3, 3);
        Assert.False(choice.ContainsKey("biome_futur"));
        Assert.False(beaten.ContainsKey("biome_futur"));
    }

    [Fact]
    public void Le_Deblocage_Ouvre_Le_Cran_Suivant_Niveau_Par_Niveau()
    {
        // Battre le cran 2 sur un niveau y ouvre le 3 — et ne dit rien des autres niveaux, qui gardent
        // leur propre progression (c'est tout le sens du déblocage par niveau).
        Assert.Equal(3, SaturationTable.MaxSelectable(2));
        Assert.Equal(1, SaturationTable.MaxSelectable(0));
        Assert.Equal(SaturationTable.MaxRank, SaturationTable.MaxSelectable(SaturationTable.MaxRank));
    }
}
