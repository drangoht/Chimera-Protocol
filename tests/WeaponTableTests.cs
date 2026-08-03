using System.IO;
using Xunit;

/// <summary>
/// Vérifie la lecture de <c>weapons.json</c> — <b>sur le vrai fichier du jeu</b>, pas sur un
/// échantillon fabriqué. Un test qui n'analyse qu'un extrait inventé prouve que l'analyseur
/// fonctionne ; il ne prouve pas que les données du jeu se chargent.
/// </summary>
public class WeaponTableTests
{
    private static string RealJson()
    {
        // Le fichier de référence reste celui du dépôt Godot : c'est la même source pour les deux
        // moteurs, ce qui garantit qu'ils lisent exactement les mêmes chiffres.
        string path = Path.Combine(TestPaths.RepoRoot, "data", "weapons.json");
        return File.ReadAllText(path);
    }

    [Fact]
    public void LeFichierDuJeuSAnalyse()
    {
        var (weapons, fusions) = WeaponTable.Parse(RealJson());

        Assert.Equal(12, weapons.Count);
        Assert.Equal(9, fusions.Count);
    }

    [Fact]
    public void ChaqueArmeADesNiveauxEtUnNom()
    {
        var (weapons, _) = WeaponTable.Parse(RealJson());

        foreach (var (id, def) in weapons)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Name), $"{id} sans nom");
            Assert.True(def.Levels.Count > 0, $"{id} sans palier de niveau");
            Assert.True(def.MaxLevel > 0, $"{id} sans niveau maximum");
        }
    }

    [Fact]
    public void ChaqueFusionDesigneUneArmeExistante()
    {
        var (weapons, fusions) = WeaponTable.Parse(RealJson());

        foreach (var (id, f) in fusions)
        {
            Assert.True(weapons.ContainsKey(f.Replaces),
                $"la fusion {id} remplace '{f.Replaces}', qui n'est pas une arme connue");
            Assert.False(string.IsNullOrWhiteSpace(f.RequiredPassive), $"{id} sans passif requis");
        }
    }

    [Fact]
    public void LesDegatsCroissentAvecLeNiveau()
    {
        var (weapons, _) = WeaponTable.Parse(RealJson());
        var cannon = weapons["impulse_cannon"];

        float l1 = WeaponTable.StatsAt(cannon, 1).Damage;
        float l5 = WeaponTable.StatsAt(cannon, 5).Damage;

        Assert.True(l5 > l1, $"niveau 5 ({l5}) devrait dépasser le niveau 1 ({l1})");
    }

    /// <summary>
    /// Au-delà des paliers décrits, les dégâts s'extrapolent mais les <b>mécaniques plafonnent</b>.
    /// C'est ce plafonnement qui empêche une arme de devenir absurde en fin de partie.
    /// </summary>
    [Fact]
    public void AuDelaDesPaliersLesDegatsMontentMaisPasLesMecaniques()
    {
        var (weapons, _) = WeaponTable.Parse(RealJson());
        var cannon = weapons["impulse_cannon"];

        var atMax  = WeaponTable.StatsAt(cannon, cannon.DefinedMax);
        var beyond = WeaponTable.StatsAt(cannon, cannon.DefinedMax + 10);

        Assert.True(beyond.Damage > atMax.Damage, "les dégâts doivent continuer de monter");
        Assert.Equal(atMax.ProjectileCount, beyond.ProjectileCount);
        Assert.Equal(atMax.Piercing, beyond.Piercing);
    }

    [Fact]
    public void UnNiveauInferieurAUnEstRamenerAuPremierPalier()
    {
        var (weapons, _) = WeaponTable.Parse(RealJson());
        var cannon = weapons["impulse_cannon"];

        Assert.Equal(WeaponTable.StatsAt(cannon, 1).Damage, WeaponTable.StatsAt(cannon, 0).Damage, 4);
    }

    [Fact]
    public void LesCadencesSontStrictementPositives()
    {
        var (weapons, _) = WeaponTable.Parse(RealJson());

        foreach (var (id, def) in weapons)
            for (int lvl = 1; lvl <= def.DefinedMax; lvl++)
                Assert.True(WeaponTable.StatsAt(def, lvl).Cooldown > 0f,
                    $"{id} niveau {lvl} : une cadence nulle ferait tirer à l'infini dans la frame");
    }
}
