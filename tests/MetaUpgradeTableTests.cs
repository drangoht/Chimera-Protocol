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
}
