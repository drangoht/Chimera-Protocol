using Xunit;

/// <summary>
/// Tests de la Marée de Rouille (unity/Assets/Scripts/Shared/Rules/RustTide.cs).
///
/// Ce qui est vérifié ici est ce qui ferait échouer l'<i>intention</i> de la règle, et non son
/// arithmétique : que la zone sûre se referme bien à la date annoncée, qu'il ne reste <b>aucun</b>
/// abri passé cette date — le trou du premier jet, où le centre exact d'un rectangle dégénéré restait
/// indéfiniment sûr — et surtout que le temps de survie dans la marée soit <b>insensible aux PV max</b>,
/// ce qui est toute la raison de compter en fraction plutôt qu'en points. Cf. docs/GDD.md §38.
/// </summary>
public class RustTideTests
{
    // Dimensions réelles de l'arène (Arena.HalfWidth / HalfHeight côté moteur) : les tests doivent
    // porter sur la géométrie que le jeu utilise, pas sur un carré commode.
    private const float HalfW = 960f;
    private const float HalfH = 608f;

    // ─── Fermeture de la zone sûre ───────────────────────────────────────────

    [Fact]
    public void Avant_La_Grace_L_Arene_Est_Entiere()
    {
        Assert.Equal(1f, RustTide.SafeFraction(0f), 4);
        Assert.Equal(1f, RustTide.SafeFraction(RustTide.GraceMinutes), 4);
    }

    [Fact]
    public void La_Zone_Sure_Se_Ferme_A_La_Date_Annoncee()
    {
        Assert.Equal(0f, RustTide.SafeFraction(RustTide.CloseMinutes), 4);
    }

    [Fact]
    public void A_Mi_Chemin_Il_Reste_La_Moitie_Des_Demi_Dimensions()
    {
        float milieu = RustTide.GraceMinutes
                     + (RustTide.CloseMinutes - RustTide.GraceMinutes) / 2f;
        Assert.Equal(0.5f, RustTide.SafeFraction(milieu), 4);
    }

    [Fact]
    public void La_Zone_Sure_Ne_Repart_Jamais_A_La_Hausse()
    {
        float precedente = float.MaxValue;
        for (float t = 0f; t <= 20f; t += 0.25f)
        {
            float f = RustTide.SafeFraction(t);
            Assert.True(f <= precedente + 1e-4f,
                        $"La zone sûre remonte à {t} min d'overtime ({precedente} → {f}).");
            precedente = f;
        }
    }

    [Fact]
    public void L_Aire_Sure_S_Effondre_Plus_Vite_Que_La_Fraction()
    {
        // La fraction décroît linéairement, donc l'aire décroît en carré : à moitié de fraction il
        // reste le quart du terrain. C'est ce qui accélère la fin sans qu'aucune courbe soit écrite.
        float f = RustTide.SafeFraction(6f);
        Assert.Equal(0.5f, f, 4);
        Assert.Equal(0.25f, f * f, 4);
    }

    // ─── Profondeur ──────────────────────────────────────────────────────────

    [Fact]
    public void Le_Centre_Est_Sur_Tant_Qu_Il_Reste_Du_Terrain()
    {
        Assert.Equal(0f, RustTide.Depth(0f, 0f, HalfW, HalfH), 4);
    }

    [Fact]
    public void Un_Coin_Ronge_Plus_Fort_Qu_Un_Milieu_De_Bord()
    {
        // Le point du design : sans la distance euclidienne au rectangle, les quatre coins seraient
        // les meilleurs abris de la fin de partie.
        float bord = RustTide.Depth(HalfW, 0f, HalfW * 0.5f, HalfH * 0.5f);
        float coin = RustTide.Depth(HalfW, HalfH, HalfW * 0.5f, HalfH * 0.5f);
        Assert.True(coin > bord, $"Le coin ({coin}) devrait enfoncer plus que le bord ({bord}).");
    }

    [Fact]
    public void La_Profondeur_Croit_Avec_L_Eloignement()
    {
        float proche = RustTide.Depth(700f, 0f, 600f, 400f);
        float loin   = RustTide.Depth(900f, 0f, 600f, 400f);
        Assert.Equal(100f, proche, 3);
        Assert.Equal(300f, loin, 3);
    }

    // ─── Taux de rongement ───────────────────────────────────────────────────

    [Fact]
    public void Le_Terrain_Sur_Ne_Coute_Rien()
    {
        Assert.Equal(0f, RustTide.FractionPerSecond(0f, 5f), 5);
    }

    [Fact]
    public void Le_Bord_De_La_Maree_Est_Traversable()
    {
        // 2 %/s : on peut y couper quelques secondes pour fuir un encerclement. Une bordure qui tue
        // au contact serait le mur que le design refuse.
        float taux = RustTide.FractionPerSecond(0.1f, 5f);
        Assert.True(taux < 0.05f, $"Le bord ronge trop fort ({taux:P1}/s) pour être traversable.");
    }

    [Fact]
    public void Le_Taux_Est_Plafonne()
    {
        Assert.Equal(RustTide.MaxFractionPerSecond, RustTide.FractionPerSecond(100000f, 5f), 5);
    }

    // ─── La garantie de fin ──────────────────────────────────────────────────

    [Fact]
    public void Passee_La_Fermeture_Aucun_Point_De_L_Arene_N_Est_Sur()
    {
        // LE test du chantier. Le premier jet échouait ici : à fermeture totale le rectangle sûr
        // dégénère en un point, et ce point — le centre exact — ne prenait aucun dégât. La fin
        // « garantie » ne l'était donc pas, précisément à l'instant où elle devait se refermer.
        float t = RustTide.CloseMinutes + RustTide.SubmersionRampMinutes;

        for (float x = -HalfW; x <= HalfW; x += 40f)
        for (float y = -HalfH; y <= HalfH; y += 40f)
        {
            float degats = RustTide.DamageOverTime(x, y, 1000f, t, 1f, HalfW, HalfH);
            Assert.True(degats > 0f, $"Le point ({x}, {y}) reste sûr à {t} min d'overtime.");
        }

        // Et le centre exact, qui est le cas dégénéré, prend le taux maximal comme tout le monde.
        Assert.Equal(1000f * RustTide.MaxFractionPerSecond,
                     RustTide.DamageOverTime(0f, 0f, 1000f, t, 1f, HalfW, HalfH), 2);
    }

    [Fact]
    public void La_Submersion_N_Existe_Pas_Avant_La_Fermeture()
    {
        // Sans quoi le placement cesserait d'être récompensé bien avant la fin.
        Assert.Equal(0f, RustTide.FloorFractionPerSecond(RustTide.CloseMinutes - 0.01f), 5);
        Assert.Equal(0f, RustTide.FloorFractionPerSecond(0f), 5);
    }

    [Fact]
    public void Le_Temps_De_Survie_Dans_La_Maree_Ne_Depend_Pas_Des_Pv_Max()
    {
        // Toute la raison de compter en FRACTION des PV max : Plating monte de +45 PV par prise, sans
        // plafond, ~13 niveaux par minute. Un montant absolu serait distancé en quelques minutes et
        // la marée cesserait d'être un chronomètre. Ici, doubler les PV double les dégâts subis :
        // le temps de survie est invariant, donc le build ne peut pas acheter du temps.
        const float t = 12f;
        float petit = RustTide.DamageOverTime(0f, 0f, 1_000f,  t, 1f, HalfW, HalfH);
        float gros  = RustTide.DamageOverTime(0f, 0f, 50_000f, t, 1f, HalfW, HalfH);

        Assert.Equal(petit / 1_000f, gros / 50_000f, 5);
    }

    [Fact]
    public void Une_Barre_Pleine_Est_Vidée_En_Quelques_Secondes_Apres_La_Fermeture()
    {
        // La garantie doit être rapide : une agonie de trente secondes n'ajoute pas de jeu.
        float t = RustTide.CloseMinutes + RustTide.SubmersionRampMinutes;
        float parSeconde = RustTide.DamageOverTime(0f, 0f, 1000f, t, 1f, HalfW, HalfH);

        Assert.True(1000f / parSeconde <= 6f,
                    $"Il faut {1000f / parSeconde:0.0} s pour vider une barre pleine — trop long.");
    }

    // ─── Garde-fous d'entrée ─────────────────────────────────────────────────

    [Fact]
    public void Hors_Overtime_La_Maree_Ne_Ronge_Rien()
    {
        // La marée n'existe pas pendant le temps imparti : le moteur appelle avec 0 minute.
        for (float x = -HalfW; x <= HalfW; x += 120f)
            Assert.Equal(0f, RustTide.DamageOverTime(x, HalfH, 1000f, 0f, 1f, HalfW, HalfH), 5);
    }

    [Fact]
    public void Sans_Pv_Ni_Duree_Aucun_Degat()
    {
        Assert.Equal(0f, RustTide.DamageOverTime(0f, 0f, 0f, 12f, 1f, HalfW, HalfH), 5);
        Assert.Equal(0f, RustTide.DamageOverTime(0f, 0f, 1000f, 12f, 0f, HalfW, HalfH), 5);
    }
}
