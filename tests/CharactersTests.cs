using Xunit;

/// <summary>
/// Tests des quatre personnages jouables
/// (unity/Assets/Scripts/Shared/Rules/Characters.cs).
///
/// <para>Ce qui est vérifié ici tient en une phrase : <b>un personnage n'existe que si tout ce qui
/// le rend jouable existe aussi</b>. Le système est né d'un trou où l'inverse était vrai — douze
/// clés de traduction, quatre jeux d'animations et une table d'armes de signature attendaient depuis
/// des mois un écran qui n'avait jamais été porté. Les tests ci-dessous relient chaque profil à ses
/// trois dépendances (arme, animations, clés de texte) pour qu'un cinquième personnage ajouté à la
/// va-vite tombe ici plutôt qu'en jeu.</para>
///
/// <para>⚠ Ce qu'ils ne vérifient <b>pas</b> : que les valeurs soient bien équilibrées. Aucun test
/// ne peut le dire, et ces chiffres n'ont jamais été joués — cf. l'avertissement en tête de
/// <c>Characters</c>.</para>
/// </summary>
public class CharactersTests
{
    [Fact]
    public void Il_Y_A_Quatre_Personnages()
    {
        Assert.Equal(4, Characters.All.Count);
    }

    [Fact]
    public void Les_Identifiants_Sont_Uniques()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var def in Characters.All)
            Assert.True(seen.Add(def.Id), $"identifiant en double : {def.Id}");
    }

    // ─── Le défaut : une sauvegarde existante ne doit rien sentir ────────────

    [Fact]
    public void Le_Defaut_Est_La_Chimere()
    {
        Assert.Equal("chimera", Characters.Default.Id);
        Assert.Equal("chimera", Characters.DefaultId);
    }

    [Fact]
    public void La_Chimere_Reprend_Exactement_Les_Valeurs_Codees_En_Dur_Avant_Elle()
    {
        // PlayerStats.ResetForRun posait 100 PV et 200 de vitesse ; RunBootstrap déclarait
        // impulse_cannon. Un joueur qui reprend sa sauvegarde ne doit RIEN sentir changer tant qu'il
        // n'a pas choisi autre chose — ce test est la seule chose qui le garantisse.
        var chimera = Characters.Get("chimera");

        Assert.Equal(100f, chimera.MaxHp, 3);
        Assert.Equal(200f, chimera.MoveSpeed, 3);
        Assert.Equal("impulse_cannon", chimera.WeaponId);
        Assert.Equal("player", chimera.FramesId);
    }

    [Fact]
    public void Un_Identifiant_Inconnu_Replie_Sur_Le_Defaut_Au_Lieu_De_Lever()
    {
        // Cette méthode lit une chaîne venue de la sauvegarde ou de --character= : une sauvegarde
        // écrite par une version future ne doit pas empêcher de jouer.
        Assert.Equal("chimera", Characters.Get("n_importe_quoi").Id);
        Assert.Equal("chimera", Characters.Get("").Id);
        Assert.Equal("chimera", Characters.Get(null).Id);
    }

    [Fact]
    public void Un_Identifiant_Inconnu_Est_Reconnaissable_Comme_Tel()
    {
        // Le repli est silencieux ; le moteur doit tout de même pouvoir DIRE qu'il a replié.
        Assert.True(Characters.IsKnown("vecteur"));
        Assert.False(Characters.IsKnown("n_importe_quoi"));
        Assert.False(Characters.IsKnown(null));
    }

    [Fact]
    public void L_Index_D_Affichage_Retombe_Sur_Zero_Pour_Un_Inconnu()
    {
        Assert.Equal(0, Characters.IndexOf("chimera"));
        Assert.Equal(3, Characters.IndexOf("vecteur"));
        Assert.Equal(0, Characters.IndexOf("n_importe_quoi"));
    }

    // ─── Chaque profil doit être jouable de bout en bout ─────────────────────

    [Fact]
    public void Chaque_Personnage_Porte_Une_Arme_Et_Des_Animations()
    {
        foreach (var def in Characters.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.WeaponId), $"{def.Id} : pas d'arme");
            Assert.False(string.IsNullOrWhiteSpace(def.FramesId), $"{def.Id} : pas d'animations");
        }
    }

    [Fact]
    public void Deux_Personnages_Ne_Partagent_Ni_Arme_Ni_Silhouette()
    {
        // Le personnage est un choix : deux profils qui démarrent avec la même arme et la même
        // silhouette ne se distinguent que par deux nombres, et l'écran ment sur ce qu'il propose.
        var armes = new System.Collections.Generic.HashSet<string>();
        var frames = new System.Collections.Generic.HashSet<string>();

        foreach (var def in Characters.All)
        {
            Assert.True(armes.Add(def.WeaponId), $"arme partagée : {def.WeaponId}");
            Assert.True(frames.Add(def.FramesId), $"animations partagées : {def.FramesId}");
        }
    }

    [Fact]
    public void Les_Cles_De_Texte_Se_Deduisent_De_L_Identifiant()
    {
        // Les douze clés existent déjà dans ui.csv, traduites en trois langues, depuis l'ère Godot :
        // c'est leur forme qui fait autorité, pas l'inverse.
        var titan = Characters.Get("titan");

        Assert.Equal("CHAR_TITAN_NAME", titan.NameKey);
        Assert.Equal("CHAR_TITAN_TAG", titan.TagKey);
        Assert.Equal("CHAR_TITAN_DESC", titan.DescKey);
    }

    // ─── Les profils doivent être distincts, et distincts dans le bon sens ───

    [Fact]
    public void Aucun_Personnage_N_Est_Une_Copie_D_Un_Autre()
    {
        foreach (var a in Characters.All)
        foreach (var b in Characters.All)
        {
            if (ReferenceEquals(a, b)) continue;

            bool identique = System.Math.Abs(a.MaxHp - b.MaxHp) < 0.001f
                          && System.Math.Abs(a.MoveSpeed - b.MoveSpeed) < 0.001f;

            Assert.False(identique, $"{a.Id} et {b.Id} ont le même profil");
        }
    }

    [Fact]
    public void Le_Profil_De_Chacun_Correspond_A_Sa_Description()
    {
        // Les descriptions sont écrites, traduites et EN LIGNE depuis l'ère Godot : ce sont elles la
        // promesse faite au joueur, et les chiffres doivent la tenir. Un jour où l'on rééquilibre,
        // c'est ce test qui rappelle qu'un nombre déplacé peut rendre un texte menteur — dans trois
        // langues à la fois.
        var chimera  = Characters.Get("chimera");
        var titan    = Characters.Get("titan");
        var vagabond = Characters.Get("vagabond");
        var vecteur  = Characters.Get("vecteur");

        // « beaucoup plus de PV mais plus lent »
        Assert.True(titan.MaxHp > chimera.MaxHp, "le Titan doit avoir plus de PV que la Chimère");
        Assert.True(titan.MoveSpeed < chimera.MoveSpeed, "le Titan doit être plus lent");

        // « peu de PV mais très rapide »
        Assert.True(vagabond.MaxHp < chimera.MaxHp, "le Vagabond doit avoir moins de PV");
        Assert.True(vagabond.MoveSpeed > chimera.MoveSpeed, "le Vagabond doit être plus rapide");

        // GDD §26 : « profil médian-fragile, ENTRE Chimera et Vagabond »
        Assert.True(vecteur.MaxHp < chimera.MaxHp && vecteur.MaxHp > vagabond.MaxHp,
                    "les PV du Vecteur doivent tomber entre ceux du Vagabond et de la Chimère");
        Assert.True(vecteur.MoveSpeed > chimera.MoveSpeed && vecteur.MoveSpeed < vagabond.MoveSpeed,
                    "la vitesse du Vecteur doit tomber entre celle de la Chimère et du Vagabond");

        // La seule valeur du lot qui soit documentée plutôt que décidée.
        Assert.Equal(90f, vecteur.MaxHp, 3);
        Assert.Equal(210f, vecteur.MoveSpeed, 3);
    }

    [Fact]
    public void Aucun_Profil_N_Est_Absurde()
    {
        // Garde-fou de rééquilibrage : un personnage à 20 PV n'est pas « fragile », il est
        // injouable — les i-frames plafonnent les dégâts entrants, pas la valeur d'un coup.
        foreach (var def in Characters.All)
        {
            Assert.InRange(def.MaxHp, 60f, 200f);
            Assert.InRange(def.MoveSpeed, 140f, 280f);
        }
    }
}
