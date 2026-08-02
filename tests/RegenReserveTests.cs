using Xunit;

/// <summary>
/// Tests de la réserve de régénération (src/Core/Rules/RegenReserve.cs).
///
/// Ce qui est vérifié ici est ce qui ferait échouer l'intention de la carte : que le surplus perdu à
/// PV pleins soit bien récupéré (la cause mesurée du « choix mort »), que soigner passe avant stocker,
/// que la réserve reste bornée, et qu'elle absorbe avant les PV. Cf. docs/GDD.md §33.6.
/// </summary>
public class RegenReserveTests
{
    // ─── Capacité ────────────────────────────────────────────────────────────

    [Fact]
    public void Capacite_Vaut_Vingt_Secondes_De_Debit()
    {
        // 12 PV/s × 20 s = 240, bien en dessous du garde-fou des 25 % de 4000 PV (= 1000).
        Assert.Equal(240f, RegenReserve.Capacity(12f, 4000f), 3);
    }

    [Fact]
    public void Capacite_Est_Bornee_Par_Une_Fraction_Des_Pv_Max()
    {
        // 60 PV/s × 20 s = 1200, mais 25 % de 2725 PV = 681,25 → le garde-fou prime.
        Assert.Equal(681.25f, RegenReserve.Capacity(60f, 2725f), 2);
    }

    [Fact]
    public void Sans_Debit_Aucune_Reserve()
    {
        Assert.Equal(0f, RegenReserve.Capacity(0f, 4000f));
    }

    [Fact]
    public void La_Capacite_Progresse_Avec_Le_Debit_Investi()
    {
        // Le point du design : une prise ne doit PAS finir par valoir quarante prises (GDD §33.6).
        float une = RegenReserve.Capacity(0.6f, 4000f);
        float quarante = RegenReserve.Capacity(24f, 4000f);
        Assert.True(quarante > une * 30f,
            $"la réserve doit suivre l'investissement : {une} → {quarante}");
    }

    // ─── Répartition du tick de régénération ─────────────────────────────────

    [Fact]
    public void Blesse_Le_Tick_Soigne_Sans_Rien_Stocker()
    {
        // Soigner d'abord : un PV rendu maintenant vaut mieux qu'un PV promis.
        var (healed, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 1000f, maxHp: 2000f, reserve: 0f, regenPerSecond: 20f, delta: 1f);

        Assert.Equal(20f, healed, 3);
        Assert.Equal(0f, stored, 3);
        Assert.Equal(0f, reserve, 3);
    }

    [Fact]
    public void A_Pv_Pleins_Le_Tick_Part_Entierement_En_Reserve()
    {
        // C'est LA correction : ces 20 PV/s étaient purement et simplement perdus (58 % du débit
        // mesuré en overtime, cf. docs/TEST_REPORT.md 2026-07-30).
        var (healed, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 2000f, maxHp: 2000f, reserve: 0f, regenPerSecond: 20f, delta: 1f);

        Assert.Equal(0f, healed, 3);
        Assert.Equal(20f, stored, 3);
        Assert.Equal(20f, reserve, 3);
    }

    [Fact]
    public void Un_Tick_A_Cheval_Soigne_Le_Manque_Puis_Stocke_Le_Reste()
    {
        // Il manque 5 PV et le tick en vaut 20 : 5 soignés, 15 stockés, rien perdu.
        var (healed, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 1995f, maxHp: 2000f, reserve: 0f, regenPerSecond: 20f, delta: 1f);

        Assert.Equal(5f, healed, 3);
        Assert.Equal(15f, stored, 3);
        Assert.Equal(15f, reserve, 3);
    }

    [Fact]
    public void La_Reserve_Ne_Depasse_Jamais_Sa_Capacite()
    {
        // Capacité = 10 × 20 = 200 ; elle est déjà à 195, donc seuls 5 PV entrent.
        var (_, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 2000f, maxHp: 2000f, reserve: 195f, regenPerSecond: 10f, delta: 1f);

        Assert.Equal(5f, stored, 3);
        Assert.Equal(200f, reserve, 3);
    }

    [Fact]
    public void Reserve_Pleine_Le_Surplus_Est_Le_Seul_Gaspillage_Restant()
    {
        var (healed, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 2000f, maxHp: 2000f, reserve: 200f, regenPerSecond: 10f, delta: 1f);

        Assert.Equal(0f, healed, 3);
        Assert.Equal(0f, stored, 3);
        Assert.Equal(200f, reserve, 3);
    }

    [Fact]
    public void Un_Joueur_Mort_Ne_Regenere_Pas()
    {
        var (healed, stored, _) = RegenReserve.ApplyRegen(
            currentHp: 0f, maxHp: 2000f, reserve: 0f, regenPerSecond: 20f, delta: 1f);

        Assert.Equal(0f, healed, 3);
        Assert.Equal(0f, stored, 3);
    }

    [Fact]
    public void Une_Reserve_Au_Dessus_De_Sa_Capacite_N_Est_Pas_Tronquee()
    {
        // Cas d'un débit qui vient de baisser (fin de buff) : on cesse de remplir, on ne confisque pas.
        var (_, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 2000f, maxHp: 2000f, reserve: 500f, regenPerSecond: 10f, delta: 1f);

        Assert.Equal(0f, stored, 3);
        Assert.Equal(500f, reserve, 3);
    }

    // ─── Absorption ──────────────────────────────────────────────────────────

    [Fact]
    public void La_Reserve_Absorbe_Avant_Les_Pv()
    {
        var (remaining, absorbed, reserve) = RegenReserve.Absorb(damage: 100f, reserve: 300f);

        Assert.Equal(0f, remaining, 3);
        Assert.Equal(100f, absorbed, 3);
        Assert.Equal(200f, reserve, 3);
    }

    [Fact]
    public void Un_Pic_Plus_Gros_Que_La_Reserve_Passe_En_Partie()
    {
        // Le mode de mort mesuré : un pic traverse. La réserve l'amortit, elle ne l'annule pas.
        var (remaining, absorbed, reserve) = RegenReserve.Absorb(damage: 900f, reserve: 480f);

        Assert.Equal(420f, remaining, 3);
        Assert.Equal(480f, absorbed, 3);
        Assert.Equal(0f, reserve, 3);
    }

    [Fact]
    public void Sans_Reserve_Les_Degats_Passent_Intacts()
    {
        var (remaining, absorbed, reserve) = RegenReserve.Absorb(damage: 250f, reserve: 0f);

        Assert.Equal(250f, remaining, 3);
        Assert.Equal(0f, absorbed, 3);
        Assert.Equal(0f, reserve, 3);
    }

    [Fact]
    public void Un_Coup_Nul_Ne_Consomme_Pas_La_Reserve()
    {
        var (remaining, absorbed, reserve) = RegenReserve.Absorb(damage: 0f, reserve: 300f);

        Assert.Equal(0f, remaining, 3);
        Assert.Equal(0f, absorbed, 3);
        Assert.Equal(300f, reserve, 3);
    }

    // ─── Suspension sous le feu (GDD §33.7) ──────────────────────────────────

    [Fact]
    public void Sous_Le_Feu_Le_Tick_Ne_Soigne_Ni_Ne_Stocke()
    {
        var (healed, stored, reserve) = RegenReserve.ApplyRegen(
            currentHp: 1000f, maxHp: 2000f, reserve: 120f, regenPerSecond: 40f, delta: 1f,
            suppressLeft: 2.5f);

        Assert.Equal(0f, healed, 3);
        Assert.Equal(0f, stored, 3);
        Assert.Equal(120f, reserve, 3);   // ce qui a été gagné avant le coup n'est pas confisqué
    }

    [Fact]
    public void Une_Reserve_Deja_Constituee_Absorbe_Meme_Sous_Le_Feu()
    {
        // On coupe la SOURCE, jamais le tampon : sinon un coup annulerait rétroactivement le
        // désengagement que le joueur a déjà payé.
        var (remaining, absorbed, _) = RegenReserve.Absorb(damage: 100f, reserve: 300f);

        Assert.Equal(0f, remaining, 3);
        Assert.Equal(100f, absorbed, 3);
    }

    [Fact]
    public void La_Suspension_S_Epuise_Puis_La_Regeneration_Reprend()
    {
        float left = RegenReserve.Suppress();
        Assert.True(RegenReserve.IsSuppressed(left));

        // 4 s de jeu à 60 fps.
        for (int i = 0; i < 240; i++) left = RegenReserve.TickSuppression(left, 1f / 60f);

        Assert.False(RegenReserve.IsSuppressed(left));
        var (healed, _, _) = RegenReserve.ApplyRegen(1000f, 2000f, 0f, 40f, 1f, left);
        Assert.Equal(40f, healed, 3);
    }

    [Fact]
    public void La_Suspension_Ne_Descend_Jamais_Sous_Zero()
    {
        Assert.Equal(0f, RegenReserve.TickSuppression(0.1f, 5f), 3);
    }

    [Fact]
    public void Rester_Au_Contact__Aucun_Pv_Regenere_Quel_Que_Soit_Le_Debit()
    {
        // Le défaut corrigé : un débit non borné dépassait les dégâts entrants et rendait
        // l'immobilité gratuite (session jouée du 2026-08-02, cran VI, fin de partie). Touché à
        // chaque fenêtre d'invulnérabilité (0,45 s), le joueur ne doit RIEN récupérer — et le
        // résultat ne doit pas dépendre de la taille du débit, sans quoi le seuil binaire subsiste.
        const float dt = 1f / 60f;
        foreach (float debit in new[] { 5f, 40f, 500f })
        {
            float left = 0f, reserve = 0f, healed = 0f, stored = 0f;
            for (int frame = 0; frame < 60 * 30; frame++)   // 30 s au contact
            {
                left = RegenReserve.TickSuppression(left, dt);
                if (frame % 27 == 0) left = RegenReserve.Suppress();   // un coup toutes les 0,45 s
                var tick = RegenReserve.ApplyRegen(1000f, 2000f, reserve, debit, dt, left);
                healed += tick.Healed; stored += tick.Stored; reserve = tick.Reserve;
            }

            Assert.Equal(0f, healed, 3);
            Assert.Equal(0f, stored, 3);
            Assert.Equal(0f, reserve, 3);
        }
    }

    [Fact]
    public void Decrocher_Suffit_A_Relancer_La_Regeneration()
    {
        // Le pendant du test précédent : la règle doit rester une INVITATION à se désengager, pas une
        // annulation de la carte. Après 4 s sans coup, le débit revient entier.
        const float dt = 1f / 60f;
        float left = RegenReserve.Suppress();
        float reserve = 0f, healed = 0f;
        for (int frame = 0; frame < 60 * 10; frame++)   // 10 s sans être touché
        {
            left = RegenReserve.TickSuppression(left, dt);
            var tick = RegenReserve.ApplyRegen(1000f, 2000f, reserve, 40f, dt, left);
            healed += tick.Healed; reserve = tick.Reserve;
        }

        // 10 s − 4 s de suspension = 6 s de régénération à 40 PV/s, à une frame près (la reprise
        // tombe au milieu d'une frame ; encadrer plutôt que fixer évite un test qui casse au
        // moindre changement de pas de temps).
        Assert.InRange(healed, 239f, 241f);
    }

    // ─── Le scénario qui motive la fonctionnalité ────────────────────────────

    [Fact]
    public void Intact_Puis_Pic__Le_Debit_Autrefois_Perdu_Est_Integralement_Recupere()
    {
        // 20 PV/s pendant 20 s à PV pleins : 400 PV qui étaient jetés avant ce changement.
        float reserve = 0f;
        float stockeTotal = 0f;
        for (int i = 0; i < 20; i++)
        {
            var tick = RegenReserve.ApplyRegen(2000f, 2000f, reserve, 20f, 1f);
            reserve = tick.Reserve;
            stockeTotal += tick.Stored;
        }

        Assert.Equal(400f, stockeTotal, 2);   // 20 s × 20 PV/s, la capacité vaut exactement 400
        Assert.Equal(400f, reserve, 2);

        // Puis un pic de 600 : 400 absorbés, 200 seulement sur les PV.
        var coup = RegenReserve.Absorb(600f, reserve);
        Assert.Equal(200f, coup.Remaining, 2);
        Assert.Equal(400f, coup.Absorbed, 2);
        Assert.Equal(0f, coup.Reserve, 2);
    }
}
