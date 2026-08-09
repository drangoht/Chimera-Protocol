using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Vérifie la lecture du bestiaire — <b>sur les vrais fichiers du jeu</b>, les deux : le bestiaire
/// de base et son extension par biome, qui doivent fusionner sans se recouvrir.
/// </summary>
public class EnemyTableTests
{
    private static string Read(string name)
        => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "data", name));

    /// <summary>
    /// Le bestiaire du jeu, tel que le moteur le charge : <b>uniquement</b> <c>enemies.json</c>.
    /// Voir <see cref="LeFichierDExtensionNeDoitPasEtreFusionne"/>.
    /// </summary>
    private static Dictionary<string, EnemyTable.EnemyDef> All()
        => EnemyTable.Parse(Read("enemies.json"));

    [Fact]
    public void LeBestiaireDuJeuSAnalyse()
    {
        var all = All();
        Assert.True(all.Count >= 31, $"bestiaire trop court : {all.Count}");

        // Les ennemis de biome sont déjà dans le fichier principal.
        Assert.Contains("sanctuary_marked_walker", all.Keys);
    }

    /// <summary>
    /// <b>Piège verrouillé ici.</b> <c>enemies_biome_expansion.json</c> ressemble à un fichier de
    /// données à charger — il n'en est pas un : <b>aucun code du jeu ne le lit</b>, il sert de
    /// document de conception à un générateur de sprites. Ses 20 entrées existent déjà dans
    /// <c>enemies.json</c>, mais <b>sans leur <c>framesPath</c></b>.
    ///
    /// <para>Le fusionner « pour être complet » effacerait donc le sprite de 20 ennemis — un
    /// bestiaire qui se charge sans erreur et produit des ennemis invisibles.</para>
    /// </summary>
    [Fact]
    public void LeFichierDExtensionNeDoitPasEtreFusionne()
    {
        var gameOnly = EnemyTable.Parse(Read("enemies.json"));
        var merged   = EnemyTable.Parse(Read("enemies.json"), Read("enemies_biome_expansion.json"));

        Assert.Equal(gameOnly.Count, merged.Count);   // l'extension n'ajoute aucun ennemi

        int lostSprites = merged.Values.Count(d => d.FramesPath.Length == 0)
                        - gameOnly.Values.Count(d => d.FramesPath.Length == 0);

        Assert.True(lostSprites > 0,
            "si ce test cesse d'être vrai, l'extension a changé de nature — revérifier avant de la charger");
    }

    [Fact]
    public void ChaqueEnnemiEstJouable()
    {
        foreach (var (id, d) in All())
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name), $"{id} sans nom");
            Assert.True(d.MaxHp > 0f, $"{id} sans PV");
            Assert.True(d.ContactRadius > 0f, $"{id} sans rayon de contact");
            Assert.True(d.XpValue >= 0, $"{id} : XP négative");
        }
    }

    /// <summary>
    /// Seul le boss a une vitesse nulle admissible ; un ennemi ordinaire immobile ne rejoindrait
    /// jamais le joueur et bloquerait le plafond de population sans jamais menacer.
    /// </summary>
    [Fact]
    public void SeulLeBossPeutEtreImmobile()
    {
        foreach (var (id, d) in All())
            if (d.Ai != EnemyTable.AiType.BossCore)
                Assert.True(d.Speed > 0f, $"{id} est immobile sans être le boss");
    }

    [Theory]
    [InlineData("straight_chase",   EnemyTable.AiType.StraightChase)]
    [InlineData("erratic_chase",    EnemyTable.AiType.ErraticChase)]
    [InlineData("ranged_kiter",     EnemyTable.AiType.RangedKiter)]
    [InlineData("slow_hunter",      EnemyTable.AiType.SlowHunter)]
    [InlineData("charging_bruiser", EnemyTable.AiType.ChargingBruiser)]
    [InlineData("boss_core",        EnemyTable.AiType.BossCore)]
    public void LesTypesDIaSontReconnus(string raw, EnemyTable.AiType expected)
    {
        Assert.Equal(expected, EnemyTable.ParseAi(raw));
    }

    /// <summary>
    /// L'aller-retour libellé → comportement → libellé doit être exact : c'est lui qui relie une
    /// élimination à sa jauge d'Assimilation. Un seul libellé faux rendrait une jauge inatteignable,
    /// donc une greffe jamais proposée — sans la moindre erreur.
    /// </summary>
    [Fact]
    public void LAllerRetourEntreLibelleEtComportementEstExact()
    {
        foreach (EnemyTable.AiType ai in System.Enum.GetValues(typeof(EnemyTable.AiType)))
            Assert.Equal(ai, EnemyTable.ParseAi(EnemyTable.AiKey(ai)));
    }

    /// <summary>
    /// Les archétypes qui alimentent une jauge doivent porter <b>exactement</b> le libellé attendu par
    /// <c>grafts.json</c> : les deux fichiers de données se rejoignent ici, et nulle part ailleurs.
    /// </summary>
    [Fact]
    public void LesLibellesCorrespondentAuxJaugesDassimilation()
    {
        var config = GraftTable.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepoRoot, "data", "grafts.json")));

        var known = new HashSet<string>();
        foreach (EnemyTable.AiType ai in System.Enum.GetValues(typeof(EnemyTable.AiType)))
            known.Add(EnemyTable.AiKey(ai));

        foreach (string aiType in config.AiTypeToGauge.Keys)
            Assert.True(known.Contains(aiType),
                $"grafts.json alimente une jauge depuis '{aiType}', que le bestiaire ne sait pas produire");
    }

    /// <summary>
    /// Un type d'IA mal saisi doit donner un ennemi <b>jouable</b>, pas faire échouer le chargement
    /// du bestiaire entier.
    /// </summary>
    [Fact]
    public void UnTypeDIaInconnuRetombeSurLaPoursuiteDirecte()
    {
        Assert.Equal(EnemyTable.AiType.StraightChase, EnemyTable.ParseAi("comportement_invente"));
        Assert.Equal(EnemyTable.AiType.StraightChase, EnemyTable.ParseAi(""));
    }

    [Fact]
    public void LeBossNApparaitJamaisDansUneVague()
    {
        var all = All();
        var pool = EnemyTable.Eligible(all.Values, minutes: 30f);

        Assert.DoesNotContain(pool, p => p.Def.Ai == EnemyTable.AiType.BossCore);
    }

    /// <summary>
    /// La fenêtre d'apparition ouvre progressivement le bestiaire : c'est ce qui fait qu'une run
    /// commence simple. Si tout était disponible dès la première minute, la courbe de difficulté
    /// n'existerait plus.
    /// </summary>
    [Fact]
    public void LePoolSElargitAvecLeTemps()
    {
        var all = All().Values.ToList();

        int early = EnemyTable.Eligible(all, 0f).Count;
        int late  = EnemyTable.Eligible(all, 20f).Count;

        Assert.True(early > 0, "aucun ennemi disponible au démarrage");
        Assert.True(late > early, $"le pool devrait s'élargir ({early} → {late})");
    }

    /// <summary>
    /// Les champions déclarent un plafond d'exemplaires simultanés, la faune non. Sans ce champ,
    /// le spawner ne peut pas distinguer un mini-boss d'un ennemi ordinaire — et le boss lui-même
    /// s'empilerait, ce qui rendait autrefois sa mise à mort impossible.
    /// </summary>
    [Fact]
    public void LesChampionsDeclarentUnPlafondDExemplaires()
    {
        var all = All();

        Assert.Equal(1, all["rusted_core"].MaxSimultaneous);
        Assert.True(all["molten_colossus"].IsChampion);
        Assert.False(all["sanctuary_marked_walker"].IsChampion);

        // La faune ordinaire est majoritaire : si l'inverse devenait vrai, le champ aurait changé de sens.
        int champions = all.Values.Count(d => d.IsChampion);
        Assert.True(champions > 0 && champions < all.Count / 2,
            $"{champions} champions sur {all.Count} entrées");
    }

    /// <summary>
    /// Un ennemi tagué n'apparaît que dans son biome.
    /// </summary>
    /// <remarks>
    /// ⚠ Ce test <b>passait en ne testant rien</b> jusqu'au 2026-08-09. Il cherchait un ennemi dont
    /// le champ <c>Biome</c> était renseigné, n'en trouvait aucun — la table lisait une clé qui
    /// n'existe pas — et sortait par son propre garde-fou « rien à vérifier ». Un test qui se
    /// désactive lui-même est pire qu'un test absent : il compte dans le total et rassure.
    /// La sortie anticipée est donc devenue une ASSERTION.
    /// </remarks>
    [Fact]
    public void UnEnnemiDeBiomeNApparaitQueDansLeSien()
    {
        var all = All().Values.ToList();

        int tagged = all.Count(d => d.Biomes.Length > 0);
        Assert.True(tagged > 20, $"seulement {tagged} ennemis tagués sur {all.Count} — la clé est-elle lue ?");

        var biomeSpecific = all.First(d => d.Biomes.Length > 0);

        var elsewhere = EnemyTable.Eligible(all, 30f, biome: "un_autre_biome");
        Assert.DoesNotContain(elsewhere, p => p.Def.Id == biomeSpecific.Id);

        var athome = EnemyTable.Eligible(all, 30f, biome: biomeSpecific.Biomes[0]);
        Assert.Contains(athome, p => p.Def.Id == biomeSpecific.Id);

        // Et le cloisonnement se VOIT : deux biomes ne proposent pas la même faune.
        var sanctuaire = EnemyTable.Eligible(all, 30f, biome: "sanctuaire");
        var givre = EnemyTable.Eligible(all, 30f, biome: "givre");
        var sansFiltre = EnemyTable.Eligible(all, 30f, biome: null);

        Assert.True(sanctuaire.Count < sansFiltre.Count,
            $"sanctuaire {sanctuaire.Count} contre {sansFiltre.Count} sans filtre");
        Assert.DoesNotContain(sanctuaire, p => p.Def.Id == "cryo_sentinel");
        Assert.Contains(givre, p => p.Def.Id == "cryo_sentinel");
    }
}
