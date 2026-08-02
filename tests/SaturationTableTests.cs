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
    // ─── Rang 2 ≥ l'ancien « Difficile » ─────────────────────────────────────

    [Fact]
    public void Rang2_Est_Au_Moins_Aussi_Dur_Que_Lancien_Difficile()
    {
        // « Meute » (les statistiques) est le cran II depuis le 2026-07-30 : mesuré seul en cran I, son
        // effet frôlait le seuil de détection du banc et le testeur ne voyait aucune différence.
        //
        // ⚠ L'ÉGALITÉ EXACTE A ÉTÉ ROMPUE LE 2026-08-02, sciemment. Elle existait pour que les records
        // gagnés en « Difficile » restent interprétables après la migration vers la saturation. Mais
        // l'échelle entière a été jouée jusqu'au cran VI et gagnée du premier coup : maintenir le cran II
        // au niveau d'un réglage de 2026-07 revenait à conserver un palier mort au milieu de l'échelle.
        // Le contrat devient une INÉGALITÉ — un ancien record « Difficile » reste un exploit valide,
        // simplement plus facile que le cran II d'aujourd'hui. C'est le sens de lecture acceptable :
        // l'inverse (un cran plus doux que l'ancien mode) invaliderait rétroactivement des records.
        Assert.True(SaturationTable.EnemyHpMult(2)     >= DifficultyTuning.EnemyHp(2));
        Assert.True(SaturationTable.EnemyDamageMult(2) >= DifficultyTuning.EnemyDamage(2));
        Assert.True(SaturationTable.SpawnMult(2)       >= DifficultyTuning.Spawn(2));
    }

    [Fact]
    public void Rang2_Pousse_Les_Degats_Plus_Que_Le_Spawn()
    {
        // Hiérarchie voulue (§34.3) et non un accident de dosage : les DÉGÂTS sont le seul des trois
        // facteurs qui touche la barre de vie, donc le seul capable de produire un frôlement (§34.6) ;
        // le SPAWN est le moins relevé parce que le cap simultané de 300 est saturé dès la 8ᵉ minute,
        // au-delà de laquelle le monter ne change rien. Si ce test casse, quelqu'un a durci le jeu par
        // le facteur qui n'a aucun effet en fin de partie.
        Assert.True(SaturationTable.EnemyDamageMult(2) > SaturationTable.EnemyHpMult(2));
        Assert.True(SaturationTable.EnemyHpMult(2)     > SaturationTable.SpawnMult(2));
    }

    [Fact]
    public void Rang2_Ne_Fait_Pas_Du_Boss_Un_Mur_De_Patience()
    {
        // Règle 3 du §34.4 : les PV des champions restent amortis par ChampionHpSoftening, parce que
        // battre le boss conditionne la progression et qu'il est calibré sur un TTK JOUÉ (§20.6).
        // Le seuil de 1,30 borne le relevage de EnemyHpMult : au-delà, le TTK du boss dériverait de
        // plus de 30 % et « plus dur » deviendrait « plus long ».
        Assert.True(SaturationTable.ChampionHpMult(SaturationTable.MaxRank) < 1.30f);
        Assert.True(SaturationTable.ChampionHpMult(2) < SaturationTable.EnemyHpMult(2));
    }

    [Fact]
    public void Rang1_Est_Hemorragie_Et_Ne_Touche_Pas_Les_Statistiques()
    {
        // La porte d'entrée doit se SENTIR : elle coupe le canal de soin dominant, elle n'ajoute pas
        // des points de vie aux ennemis.
        Assert.Equal(0.35f, SaturationTable.HealingMult(1), 4);
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
            if (SaturationTable.RunDurationMult(r)     != SaturationTable.RunDurationMult(r - 1))     changed++;
            if (SaturationTable.EliteFrequencyMult(r)  != SaturationTable.EliteFrequencyMult(r - 1))  changed++;
            // « Hémorragie » agit sur deux leviers (soins reçus + soin de passage de niveau) pour UNE
            // règle : « on ne se soigne presque plus ». Ils visent le même canal, donc ils comptent
            // pour un — et le test ci-dessous vérifie qu'ils basculent bien au même rang.
            if (SaturationTable.HealingMult(r)         != SaturationTable.HealingMult(r - 1)
             || SaturationTable.LevelUpHealsEnabled(r) != SaturationTable.LevelUpHealsEnabled(r - 1))  changed++;
            // « Sans filet » agit sur deux leviers (consommables méta + Stabilisateur de Surcharge)
            // mais énonce UNE règle — « aucun filet acheté ne survit ». Même raisonnement : s'ils se
            // séparaient, ce seraient deux règles déguisées en une, et une mort cesserait d'être
            // interprétable.
            if (SaturationTable.SafetyNetsEnabled(r)   != SaturationTable.SafetyNetsEnabled(r - 1)
             || SaturationTable.MetaOvertimeDampeningEnabled(r)
             != SaturationTable.MetaOvertimeDampeningEnabled(r - 1))                                    changed++;
            if (SaturationTable.ChampionMinDamageFraction(r)
             != SaturationTable.ChampionMinDamageFraction(r - 1))                                      changed++;

            Assert.Equal(1, changed);
        }
    }

    [Fact]
    public void Les_Deux_Leviers_De_Sans_Filet_Basculent_Au_Meme_Rang()
    {
        for (int r = 0; r <= SaturationTable.MaxRank; r++)
            Assert.Equal(SaturationTable.SafetyNetsEnabled(r), SaturationTable.MetaOvertimeDampeningEnabled(r));
    }

    [Fact]
    public void Les_Deux_Leviers_D_Hemorragie_Basculent_Au_Meme_Rang()
    {
        // Le soin de passage de niveau appartient au cran I depuis le 2026-08-02 : c'est le même canal
        // que les soins reçus, et de loin le plus gros (≈158 % des PV max rendus par minute d'overtime,
        // 25 % par niveau à ~18 niveaux/min). Le laisser au cran IV rendait tout durcissement des crans
        // inférieurs sans effet — le testeur devait « rester immobile pour vraiment mourir » au rang 1.
        for (int r = 0; r <= SaturationTable.MaxRank; r++)
            Assert.Equal(SaturationTable.HealingMult(r) < 1f, !SaturationTable.LevelUpHealsEnabled(r));
    }

    [Fact]
    public void Le_Soin_De_Passage_De_Niveau_Tombe_Des_Le_Cran1()
    {
        Assert.True(SaturationTable.LevelUpHealsEnabled(0));
        Assert.False(SaturationTable.LevelUpHealsEnabled(1));
        Assert.False(SaturationTable.LevelUpHealsEnabled(SaturationTable.MaxRank));
    }

    // ─── Les règles elles-mêmes ──────────────────────────────────────────────

    [Fact]
    public void Cran1_Coupe_Les_Soins_Recus_Et_Le_Reste_Des_Crans_Le_Conserve()
    {
        Assert.Equal(1.00f, SaturationTable.HealingMult(0), 4);
        Assert.Equal(0.35f, SaturationTable.HealingMult(1), 4);
        Assert.Equal(0.35f, SaturationTable.HealingMult(SaturationTable.MaxRank), 4);
    }

    [Fact]
    public void Cran1_Coupe_Assez_Pour_Depasser_Le_Gaspillage_Mesure()
    {
        // Le §34.4 ter a mesuré qu'au cran 0 le joueur reçoit 293,6 PV/s et n'en retient que 58,8 : tant
        // que l'offre coupée reste au-dessus de ce qu'il sait absorber, le cran ne retire RIEN — c'est
        // ce que faisait 0,60 (176 PV/s, encore trois fois trop). Verrou contre un retour en arrière
        // « pour adoucir la porte d'entrée » : la coupe doit laisser moins du double du retenu.
        const float offert = 293.6f, retenu = 58.8f;
        Assert.True(offert * SaturationTable.HealingMult(1) < 2f * retenu);
    }

    [Fact]
    public void Cran3_Avance_L_Overtime_Autour_De_La_Huitieme_Minute()
    {
        // Référence actuelle : 780 s (13 min) dans data/meta_upgrades.json.
        float overtimeAt = 780f * SaturationTable.RunDurationMult(3) / 60f;
        Assert.InRange(overtimeAt, 7.8f, 8.2f);
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
    public void Cran4_Coupe_Aussi_Le_Stabilisateur_De_Surcharge()
    {
        // Troisième filet acheté, et le plus fort sur une run longue : il aplatit la seule courbe du jeu
        // qui monte sans fin. Il survivait à l'échelle entière jusqu'au 2026-08-02.
        //
        // ⚠ Ce cran ne porte plus AUCUN levier universel depuis que le soin de passage de niveau est
        // parti au cran I : il ne retire rien à un joueur qui n'a rien acheté au Hub. Régression assumée
        // (cf. SaturationTable.LevelUpHealsEnabled) — ce test la rend visible plutôt que de la masquer.
        Assert.True(SaturationTable.MetaOvertimeDampeningEnabled(3));
        Assert.False(SaturationTable.MetaOvertimeDampeningEnabled(4));
        Assert.False(SaturationTable.MetaOvertimeDampeningEnabled(SaturationTable.MaxRank));
    }

    [Fact]
    public void Cran5_Quadruple_La_Frequence_D_Elites()
    {
        Assert.Equal(1.00f, SaturationTable.EliteFrequencyMult(4), 4);
        Assert.Equal(4.00f, SaturationTable.EliteFrequencyMult(5), 4);
    }

    [Fact]
    public void Cran5_Laisse_Toujours_Des_Ennemis_Ordinaires_Dans_La_Nuee()
    {
        // Le cran demande de LIRE la foule : une nuée entièrement composée d'élites n'a plus rien à
        // lire, et le coût des affixes sur 200-300 entités deviendrait un problème d'IPS avant d'être
        // un problème d'équilibrage. Le plafond peut monter, jamais atteindre 1.
        Assert.True(SaturationTable.EliteChanceCap(5) < 1.00f);
        Assert.True(SaturationTable.EliteChanceCap(5) > 0.50f);
    }

    [Fact]
    public void Cran5_Ne_Rend_Pas_Les_Elites_Moins_Dangereuses()
    {
        // Le cran V ajoute des élites, il ne les adoucit pas : le plafond de fréquence monte, et rien
        // dans la table d'affixes ne baisse. Verrou contre la dérive « les élites étant partout,
        // adoucissons-les », qui viderait le cran de son sens.
        Assert.True(SaturationTable.EliteChanceCap(5) > SaturationTable.EliteChanceCap(4));
        foreach (var affix in EliteAffixTable.All)
        {
            var m = EliteAffixTable.Modifiers(affix);
            Assert.True(m.HpMult > 0f && m.DamageMult > 0f && m.SpeedMult > 0f);
        }
    }

    // ─── Cran VI — « Purificateur » ──────────────────────────────────────────

    [Fact]
    public void Cran6_Fait_Frapper_Les_Champions_En_Part_Des_Pv_Max()
    {
        Assert.Equal(0f, SaturationTable.ChampionMinDamageFraction(5), 4);
        Assert.Equal(0.12f, SaturationTable.ChampionMinDamageFraction(6), 4);
    }

    [Fact]
    public void Sous_Le_Cran6_Le_Degat_Nominal_Est_Inchange()
    {
        // Verrou de non-régression : la règle ne doit fuir sur AUCUN cran inférieur, sans quoi les
        // records et complétions déjà gagnés cesseraient d'être comparables.
        for (int r = 0; r <= 5; r++)
            Assert.Equal(50f, SaturationTable.ChampionDamage(50f, 4000f, r), 3);
    }

    [Fact]
    public void Le_Plancher_Rend_Inoperant_L_Empilement_De_Pv_Max()
    {
        // LE point du cran : le joueur gagne 277 PV max/min en overtime (banc, cran 0) face à des
        // dégâts qui sont des valeurs ABSOLUES. Le coût d'un coup doit suivre sa barre, sinon la
        // barre finit toujours par gagner.
        float petit = SaturationTable.ChampionDamage(50f, 1000f, 6);
        float gros  = SaturationTable.ChampionDamage(50f, 5000f, 6);

        Assert.Equal(120f, petit, 3);
        Assert.Equal(600f, gros, 3);
        // Le nombre de coups pour vider la barre ne dépend PLUS des PV max.
        Assert.Equal(1000f / petit, 5000f / gros, 3);
    }

    [Fact]
    public void Un_Coup_Deja_Plus_Fort_Que_Le_Plancher_N_Est_Pas_Adouci()
    {
        // C'est un PLANCHER, jamais un remplacement : un cran ne doit pas pouvoir rendre le jeu plus
        // facile sur un coup déjà lourd.
        Assert.Equal(900f, SaturationTable.ChampionDamage(900f, 4000f, 6), 3);
    }

    [Fact]
    public void Sans_Pv_Max_Connus_Le_Nominal_Passe_Tel_Quel()
    {
        // Entre deux scènes, MaxHp peut valoir 0 : un plancher de 0 % ferait disparaître le coup.
        Assert.Equal(50f, SaturationTable.ChampionDamage(50f, 0f, 6), 3);
    }

    [Fact]
    public void Le_Plancher_Laisse_Au_Joueur_Une_Marge_De_Reaction()
    {
        // Garde-fou de conception : le cran doit rendre le contact d'un champion coûteux, jamais
        // instantanément mortel. Un joueur à PV pleins doit encaisser plusieurs coups — sinon la
        // règle cesse d'être « ne tanke plus le boss » pour devenir « ne l'approche jamais », ce qui
        // n'est pas une décision mais une interdiction.
        int coups = (int)(1f / SaturationTable.PurgeFraction);
        Assert.True(coups >= 6, $"{coups} coups pour vider la barre — trop peu pour réagir");
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
