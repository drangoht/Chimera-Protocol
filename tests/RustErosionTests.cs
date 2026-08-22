using Xunit;

/// <summary>
/// Tests du front rongé de la Marée de Rouille
/// (unity/Assets/Scripts/Shared/Rules/RustErosion.cs).
///
/// <para>Ce qui est vérifié ici n'est pas de l'esthétique. La dentelure <b>déplace la limite des
/// dégâts</b> : c'est délibéré — le contour dessiné et le contour qui ronge doivent être le même,
/// sinon le liseré ment au joueur. Mais cela veut dire qu'une erreur de signe ou de borne ne produit
/// pas un vilain rendu, elle produit une <i>règle fausse</i> : du terrain sûr rendu après coup, une
/// poche d'abri qui survit à la fermeture, ou des dégâts pendant la minute de grâce.</para>
///
/// <para>⚠ La formule est écrite <b>deux fois</b> : ici en C#, et en HLSL dans
/// <c>Resources/Shaders/RustTide.shader</c>. Ces tests ne couvrent que la moitié C#. Rien ne peut
/// vérifier automatiquement que les deux transcriptions coïncident — c'est le prix d'un contour
/// évalué par pixel, et la raison pour laquelle les constantes sont écrites en clair des deux côtés
/// plutôt que passées en uniformes : elles doivent se relire ligne à ligne. Cf. docs/GDD.md §38,
/// docs/PITFALLS_UNITY.md §Fin de partie.</para>
/// </summary>
public class RustErosionTests
{
    private const float HalfW = 960f;
    private const float HalfH = 608f;

    private static readonly int[] Sides =
        { RustErosion.Right, RustErosion.Left, RustErosion.Top, RustErosion.Bottom };

    // ─── Le sens : la rouille MANGE, elle ne rend jamais ─────────────────────

    [Fact]
    public void La_Morsure_Ne_Va_Jamais_Vers_L_Exterieur()
    {
        // L'invariant dont dépend toute la garantie de fin de partie : le rectangle érodé est un
        // sous-ensemble du rectangle nominal. Tant qu'il l'est, il n'y a rien à re-démontrer sur la
        // fermeture — aucune bosse de bruit ne peut fabriquer la poche sûre que le §38 interdit.
        foreach (int side in Sides)
        for (float u = -1200f; u <= 1200f; u += 7f)
        for (float t = 0f; t <= 900f; t += 37f)
        {
            float offset = RustErosion.Offset(side, u, 400f, HalfW, t);
            Assert.True(offset <= 0f, $"cote {side}, u={u}, t={t} : le bord est repousse de {offset}");
        }
    }

    [Fact]
    public void Le_Bord_Erode_Reste_Entre_Zero_Et_Le_Bord_Nominal()
    {
        foreach (int side in Sides)
        for (float safe = 0f; safe <= HalfW; safe += 43f)
        for (float u = -1200f; u <= 1200f; u += 31f)
        {
            float edge = RustErosion.EdgeAt(side, u, safe, HalfW, 123f);
            Assert.InRange(edge, 0f, safe + 0.001f);
        }
    }

    // ─── La naissance : rien avant que la marée ne parte ─────────────────────

    [Fact]
    public void Pendant_La_Grace_Le_Bord_N_Est_Pas_Mordu()
    {
        // La minute de grâce existe pour que le joueur lise l'annonce pendant que rien ne bouge. Une
        // dentelure présente dès la première seconde d'overtime rongerait un joueur collé au bord
        // avant même que la marée n'ait démarré — et l'annonce arriverait après les dégâts.
        foreach (int side in Sides)
        for (float u = -1200f; u <= 1200f; u += 13f)
        {
            Assert.Equal(HalfW, RustErosion.EdgeAt(side, u, HalfW, HalfW, 30f), 3);
            Assert.Equal(0f, RustErosion.Offset(side, u, HalfW, HalfW, 30f), 5);
        }
    }

    [Fact]
    public void La_Dentelure_Ne_Depasse_Jamais_Ce_Que_La_Maree_A_Mange()
    {
        // Elle naît de l'avancée et grandit avec elle. Vérifié plutôt que décrit : c'est la borne que
        // le premier jet n'avait pas, et son absence ne se voyait pas au rendu — seulement dans les
        // dégâts, une minute trop tôt.
        for (float mange = 0f; mange <= 300f; mange += 3f)
        {
            float safe = HalfW - mange;
            float amp = RustErosion.Amplitude(safe, HalfW);
            Assert.True(amp <= mange + 0.001f,
                        $"apres {mange} px avales, la dentelure creuse deja {amp} px");
        }
    }

    [Fact]
    public void La_Dentelure_Atteint_Sa_Pleine_Profondeur_Une_Fois_La_Maree_Lancee()
    {
        // L'autre bout : si la borne de naissance étouffait la dentelure durablement, le bord
        // resterait droit toute la partie et le chantier n'aurait servi à rien.
        Assert.Equal(RustErosion.AmplitudePx, RustErosion.Amplitude(HalfW - 400f, HalfW), 3);
    }

    // ─── La fermeture : la garantie de fin n'est pas entamée ─────────────────

    [Fact]
    public void Arene_Fermee_Le_Bord_Erode_Est_Nul()
    {
        foreach (int side in Sides)
        for (float u = -1200f; u <= 1200f; u += 11f)
            Assert.Equal(0f, RustErosion.EdgeAt(side, u, 0f, HalfW, 700f), 5);
    }

    [Fact]
    public void La_Dentelure_Se_Referme_Avec_L_Arene()
    {
        // Sans le plafond en part du demi-axe, la dernière minute donnerait une zone sûre de 40 px
        // traversée de part en part par une dentelure de 72 : elle clignoterait entre « il reste un
        // abri » et « il n'en reste pas », au pire moment possible.
        foreach (int side in Sides)
        for (float safe = 1f; safe <= 200f; safe += 1f)
        for (float u = -600f; u <= 600f; u += 29f)
        {
            float edge = RustErosion.EdgeAt(side, u, safe, HalfW, 456f);
            Assert.True(edge >= safe * (1f - RustErosion.MaxShare) - 0.001f,
                        $"safe={safe}, u={u} : il ne reste que {edge}");
        }
    }

    // ─── La forme : rongée, pas ondulée, pas symétrique ──────────────────────

    [Fact]
    public void Les_Quatre_Cotes_Ne_Sont_Pas_Le_Meme_Bord()
    {
        // Une symétrie parfaite est la seule chose qu'un motif naturel ne fait jamais : sans phase
        // par côté, gauche et droite recevraient la même échancrure au même endroit et l'arène se
        // lirait comme un test de Rorschach.
        foreach (int a in Sides)
        foreach (int b in Sides)
        {
            if (a >= b) continue;

            float ecart = 0f;
            for (float u = -900f; u <= 900f; u += 17f)
                ecart += System.Math.Abs(RustErosion.Bite01(a, u, 200f) - RustErosion.Bite01(b, u, 200f));

            Assert.True(ecart > 5f, $"les cotes {a} et {b} ont pratiquement le meme bord (ecart {ecart:0.00})");
        }
    }

    [Fact]
    public void La_Morsure_Reste_Dans_L_Intervalle_Unitaire()
    {
        foreach (int side in Sides)
        for (float u = -3000f; u <= 3000f; u += 7f)
        for (float t = 0f; t <= 1500f; t += 61f)
            Assert.InRange(RustErosion.Bite01(side, u, t), 0f, 1f);
    }

    [Fact]
    public void Le_Bord_N_Est_Pas_Droit()
    {
        // La raison d'être du fichier, vérifiée plutôt que supposée : sur la longueur d'un côté, le
        // bord doit varier de plusieurs dizaines de pixels. Si un jour quelqu'un met les trois poids
        // à zéro « pour simplifier », ce test tombe au lieu de laisser revenir un rectangle.
        float min = float.MaxValue, max = float.MinValue;
        for (float u = -HalfH; u <= HalfH; u += 3f)
        {
            float e = RustErosion.EdgeAt(RustErosion.Right, u, 500f, HalfW, 300f);
            min = System.Math.Min(min, e);
            max = System.Math.Max(max, e);
        }

        Assert.True(max - min > 40f, $"le bord ne varie que de {max - min:0.0} px : il est droit");
    }

    [Fact]
    public void Le_Bord_Est_Continu_Le_Long_Du_Cote()
    {
        // Un saut donnerait une marche franche — exactement l'arête droite que ce chantier supprime,
        // en pire parce qu'elle serait perpendiculaire au bord.
        foreach (int side in Sides)
        {
            float precedent = RustErosion.EdgeAt(side, -HalfH, 500f, HalfW, 88f);
            for (float u = -HalfH + 1f; u <= HalfH; u += 1f)
            {
                float e = RustErosion.EdgeAt(side, u, 500f, HalfW, 88f);
                Assert.True(System.Math.Abs(e - precedent) < 6f,
                            $"marche de {System.Math.Abs(e - precedent):0.0} px en u={u}");
                precedent = e;
            }
        }
    }

    // ─── Le grignotement : ça bouge, mais ça ne scintille pas ────────────────

    [Fact]
    public void Le_Front_Grignote_Au_Fil_Du_Temps()
    {
        // Sans évolution temporelle, la dentelure serait un décor figé : le bord aurait une jolie
        // forme et resterait tout aussi imperceptible, ce qui était le défaut d'origine.
        float ecart = 0f;
        for (float u = -600f; u <= 600f; u += 11f)
            ecart += System.Math.Abs(RustErosion.Bite01(RustErosion.Top, u, 0f)
                                     - RustErosion.Bite01(RustErosion.Top, u, 30f));

        Assert.True(ecart > 5f, $"en 30 s le front n'a bouge que de {ecart:0.00}");
    }

    [Fact]
    public void Le_Front_Ne_Scintille_Pas_D_Une_Image_A_L_Autre()
    {
        // L'excès inverse : un bord qui change vite se lit comme un grésillement, pas comme de la
        // corrosion. À 60 images/s, le déplacement doit rester bien en deçà du pixel.
        const float dt = 1f / 60f;
        foreach (int side in Sides)
        for (float t = 0f; t <= 600f; t += 7f)
        for (float u = -600f; u <= 600f; u += 53f)
        {
            float a = RustErosion.EdgeAt(side, u, 500f, HalfW, t);
            float b = RustErosion.EdgeAt(side, u, 500f, HalfW, t + dt);
            Assert.True(System.Math.Abs(a - b) < 1f,
                        $"le bord saute de {System.Math.Abs(a - b):0.00} px en une image");
        }
    }

    // ─── Le raccord avec les dégâts ──────────────────────────────────────────

    [Fact]
    public void Le_Contour_Dessine_Est_Celui_Qui_Ronge()
    {
        // Le point de tout le chantier : la profondeur est nulle SUR le bord érodé et positive juste
        // au-delà. Si un jour la dentelure redevenait un habillage posé sur une géométrie
        // rectangulaire, ce test tomberait — c'est la seule chose qui relie les deux fichiers.
        const float otMinutes = 6f;
        float fraction = RustTide.SafeFraction(otMinutes);
        float nominal = HalfW * fraction;

        // ⚠ Le balayage reste loin des bords HAUT et BAS. Un point choisi près d'eux sort de la zone
        // sûre par l'AUTRE axe, et la profondeur mesurée n'est alors plus celle du bord qu'on teste :
        // le premier jet de ce test échouait ainsi à y = 300, à 40 px dans la marée par le haut, en
        // accusant une règle parfaitement juste.
        for (float y = -180f; y <= 180f; y += 13f)
        {
            float bord = RustErosion.EdgeAt(RustErosion.Right, y, nominal, HalfW, otMinutes * 60f);

            Assert.Equal(0f, RustTide.DepthAt(bord - 1f, y, otMinutes, HalfW, HalfH), 3);
            Assert.True(RustTide.DepthAt(bord + 8f, y, otMinutes, HalfW, HalfH) > 0f,
                        $"a y={y}, huit pixels au-dela du bord ne rongent pas");
        }
    }

    [Fact]
    public void Hors_Overtime_La_Dentelure_Ne_Ronge_Rien()
    {
        // Le cas que le premier jet cassait : la règle pure doit être juste indépendamment de son
        // appelant, et le moteur l'interroge avec 0 minute pendant tout le temps imparti.
        for (float x = -HalfW; x <= HalfW; x += 40f)
            Assert.Equal(0f, RustTide.DepthAt(x, HalfH, 0f, HalfW, HalfH), 4);
    }
}
