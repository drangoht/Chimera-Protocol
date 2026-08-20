using System.IO;
using Xunit;

/// <summary>
/// Vérifie la méta-progression sur le <b>vrai</b> <c>meta_upgrades.json</c> — celui dont la grille de
/// prix a été relevée ×3,11 le 2026-08-02.
/// </summary>
public class MetaUpgradeTableTests
{
    private static MetaUpgradeTable.Document Real()
        => MetaUpgradeTable.Parse(File.ReadAllText(
            Path.Combine(TestPaths.Data, "meta_upgrades.json")));

    [Fact]
    public void LesAmeliorationsDuJeuSeChargent()
    {
        var doc = Real();

        Assert.True(doc.Upgrades.Count >= 14, $"{doc.Upgrades.Count} améliorations");
        Assert.Equal(doc.Upgrades.Count, doc.Ordered.Count);   // l'ordre d'affichage est conservé

        foreach (var def in doc.Ordered)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Name), $"{def.Id} sans nom");
            Assert.True(def.MaxLevel > 0, $"{def.Id} sans niveau maximum");
            Assert.True(def.CostPerLevel.Count >= def.MaxLevel,
                $"{def.Id} : {def.CostPerLevel.Count} prix pour {def.MaxLevel} niveaux — " +
                "un niveau sans prix est un achat impossible");
        }
    }

    [Fact]
    public void LaFormuleDEchosEtLeTempsImpartiSontLus()
    {
        var doc = Real();

        Assert.Equal(780, doc.RunDurationSeconds);
        Assert.True(doc.Echoes.TimeDiv > 0);
        Assert.True(doc.Echoes.CapKills > 0);
        Assert.True(doc.Echoes.OvertimeBonusCap > 0);
    }

    /// <summary>
    /// Le prix affiché est celui du <b>prochain</b> niveau. Au maximum, il n'y en a pas : afficher un
    /// prix sur un bouton qui ne peut rien acheter serait un mensonge d'interface.
    /// </summary>
    [Fact]
    public void LePrixEstCeluiDuProchainNiveau()
    {
        var doc = Real();
        var def = doc.Upgrades["damage_boost"];

        Assert.Equal(def.CostPerLevel[0], MetaUpgradeTable.NextCost(def, 0));
        Assert.Equal(def.CostPerLevel[1], MetaUpgradeTable.NextCost(def, 1));
        Assert.Equal(-1, MetaUpgradeTable.NextCost(def, def.MaxLevel));
    }

    [Fact]
    public void LeRemboursementCumuleCeQuiAEteDepense()
    {
        var doc = Real();
        var def = doc.Upgrades["damage_boost"];

        Assert.Equal(0, MetaUpgradeTable.SpentUpTo(def, 0));
        Assert.Equal(def.CostPerLevel[0] + def.CostPerLevel[1], MetaUpgradeTable.SpentUpTo(def, 2));
    }

    /// <summary>
    /// La sauvegarde réelle du testeur : 14 améliorations achetées. Ses bonus doivent être non nuls
    /// et rester en deçà des plafonds une fois écrits dans les statistiques.
    /// </summary>
    [Fact]
    public void LesBonusDUneVraieSauvegardeSontAppliques()
    {
        string json = File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "tests", "fixtures", "legacy_save.json"));
        var save = SaveMigration.ReadSave(json);

        var bonuses = MetaUpgradeTable.BonusesFor(
            id => save.Meta.Upgrades.TryGetValue(id, out int lvl) ? lvl : 0);

        Assert.True(bonuses.MaxHp > 0f, "hp_boost 2 + hp_boost_2 1 doivent donner des PV");
        Assert.True(bonuses.DamageMultiplier > 0f);
        Assert.True(bonuses.HpRegenPerSecond > 0f, "hp_regen 3 doit donner de la régénération");

        // Les plafonds s'appliquent à l'écriture, avec les passifs — pas ici. On vérifie donc que le
        // total brut RESTE plafonnable, c'est-à-dire que la table ne produit pas déjà l'absurde.
        Assert.True(StatCaps.CapCooldownReduction(bonuses.CooldownReduction) <= StatCaps.MaxCooldownReduction);
        Assert.True(StatCaps.CapDamageReduction(bonuses.DamageReduction) <= StatCaps.MaxDamageReduction);
    }

    [Fact]
    public void UnJoueurNeufNAAucunBonus()
    {
        var bonuses = MetaUpgradeTable.BonusesFor(_ => 0);

        Assert.Equal(0f, bonuses.MaxHp);
        Assert.Equal(0f, bonuses.DamageMultiplier);
        Assert.Equal(0f, bonuses.HpRegenPerSecond);
    }

    [Fact]
    public void UnFichierVideNeLevePasDException()
    {
        Assert.Empty(MetaUpgradeTable.Parse(null).Upgrades);
        Assert.Equal(780, MetaUpgradeTable.Parse("{}").RunDurationSeconds);
    }

    // ─── Formule d'Échos : la calibration du fichier fait foi ────────────────────────────────────
    //
    // ⚠ Ces sept scénarios sont ceux que `meta_upgrades.json` inscrit lui-même sous `_calibration`,
    // avec les montants attendus. Ils sont ici parce que le code les a démentis pendant tout le
    // portage : l'appel passait `runSeconds` comme plafond de temps, donc le temps n'était jamais
    // plafonné et le Bonus de Surcharge jamais versé. Les trois runs d'overtime rapportaient
    // 237 / 357 / 452 au lieu de 224 / 288 / 311 — soit, sur la plus longue, **+45 %** et surtout un
    // gain non borné : exactement le farm que ce plafond existe pour fermer.
    //
    // Les tests d'`EchoFormula`, eux, passaient déjà 780 et étaient verts. **Une règle testée ne dit
    // rien de la façon dont on l'appelle** : c'est ce qui manquait, et c'est ce que ceci verrouille.
    //
    // ⚠ Montants REVUS le 2026-08-20 (Marée de Rouille, GDD §38) : l'amortissement passe de 0,15 à
    // 0,50 et le plafond de 100 à 600 Échos. Le réglage serré existait parce que l'overtime était
    // SANS FIN ; la marée le borne désormais à 11 minutes, donc le revenu de surcharge est borné par
    // construction et le jeu n'a plus de raison de payer le joueur pour s'arrêter.
    //
    // ⚠ Les deux derniers scénarios (40 et 60 minutes) ne décrivent plus une partie possible — la
    // marée ferme toute la surface bien avant. Ils restent ici parce qu'ils verrouillent la FORMULE
    // et son plafond, et parce qu'un test qui n'a plus d'équivalent en jeu reste le seul endroit où
    // la borne se vérifie sans jouer trois quarts d'heure.

    [Theory]
    [InlineData(30, 0, 0, 11)]           // mort précoce
    [InlineData(180, 120, 4, 51)]        // run standard courte
    [InlineData(300, 250, 8, 90)]        // run standard
    [InlineData(780, 520, 22, 211)]      // boss vaincu, pile aux plafonds, sans overtime
    [InlineData(1080, 920, 29, 255)]     // overtime modeste : +5 min après le boss
    [InlineData(2400, 3000, 60, 470)]    // overtime excellente : 40 min — PLUS ATTEIGNABLE, cf. ci-dessous
    [InlineData(3600, 8000, 100, 811)]   // overtime extrême : 60 min — PLUS ATTEIGNABLE, plafond atteint
    public void LesEchosSuiventLaCalibrationDuFichier(int seconds, int kills, int cores, int attendu)
        => Assert.Equal(attendu, Real().Echoes.Total(seconds, kills, cores));

    [Fact]
    public void LeBonusDeSurchargeEstBorne()
    {
        var echoes = Real().Echoes;

        // Une heure de survie et une journée entière rapportent le même bonus : il est plafonné.
        var (_, uneHeure) = echoes.Detailed(3600, 8000, 100);
        var (_, uneJournee) = echoes.Detailed(86400, 200000, 5000);

        Assert.Equal(echoes.OvertimeBonusCap, uneHeure);
        Assert.Equal(echoes.OvertimeBonusCap, uneJournee);
    }

    [Fact]
    public void UneRunSansOvertimeNeGagneAucunBonus()
    {
        var (total, bonus) = Real().Echoes.Detailed(780, 520, 22);

        Assert.Equal(0, bonus);
        Assert.Equal(211, total);
    }

    [Fact]
    public void LeCranCompteARebroursAvanceLaFrontiereDeSurcharge()
    {
        // « Compte à rebours » ramène le temps imparti de 780 s à 484 s. Une run de 780 s est alors
        // en overtime depuis presque 5 minutes : ce temps-là doit être AMORTI, pas payé plein tarif.
        var echoes = Real().Echoes;

        var (plein, sansBonus) = echoes.Detailed(780, 520, 22);
        var (raccourci, avecBonus) = echoes.Detailed(780, 520, 22, 1.0, capTimeSecs: 484);

        Assert.Equal(0, sansBonus);
        Assert.True(avecBonus > 0, "la frontière avancée doit produire du temps de surcharge");
        Assert.True(raccourci < plein,
            $"un temps amorti ne peut pas rapporter plus que le même temps plein ({raccourci} ≥ {plein})");
    }

    [Fact]
    public void LaFrontiereParDefautSuitLaDureeDeRunDuFichier()
    {
        var doc = Real();

        // Les deux champs disent la même chose, et le fichier impose leur égalité. Les laisser
        // diverger déplacerait l'entrée en surcharge du calcul sans la déplacer dans le jeu.
        Assert.Equal(doc.RunDurationSeconds, doc.Echoes.CapTimeSecs);
    }
}
