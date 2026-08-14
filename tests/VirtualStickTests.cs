using Xunit;

/// <summary>
/// Le joystick flottant tactile.
///
/// <para>Ces tests portent sur les trois choses qu'un stick virtuel rate en silence : rendre un
/// vecteur <b>plus long que 1</b> (le joueur dépasse alors sa vitesse maximale et aucun plafond ne le
/// dit), <b>dériver</b> sous un doigt posé mais immobile, et <b>saturer</b> — le pouce parti au bord
/// de l'écran ne peut plus ralentir sans traverser toute la course.</para>
/// </summary>
public class VirtualStickTests
{
    private const float R = VirtualStick.ReferenceRadius;
    private const float D = VirtualStick.ReferenceDeadZone;

    private static VirtualStick.Reading Read(float ox, float oy, float fx, float fy)
        => VirtualStick.Read(ox, oy, fx, fy, R, D);

    // ─── L'invariant : jamais plus vite qu'à fond ────────────────────────────

    /// <summary>
    /// Quelle que soit la distance parcourue par le pouce — y compris dix rayons, ce qu'un glissement
    /// sur une tablette atteint sans effort — l'intensité rendue reste dans [0, 1].
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(D / 2f)]
    [InlineData(R / 2f)]
    [InlineData(R)]
    [InlineData(R * 3f)]
    [InlineData(R * 10f)]
    public void LIntensiteNeDepasseJamaisUn(float travel)
    {
        var reading = Read(500f, 300f, 500f + travel, 300f);

        Assert.InRange(reading.Magnitude, 0f, 1.0001f);
    }

    /// <summary>La même chose en diagonale : c'est là que les implémentations naïves passent à 1,41.</summary>
    [Fact]
    public void UneDiagonaleAFondVautUn_PasRacineDeDeux()
    {
        var reading = Read(0f, 0f, R, R);

        Assert.InRange(reading.Magnitude, 0.99f, 1.0001f);
    }

    // ─── La zone morte : un doigt posé ne déplace pas ────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(D * 0.5f)]
    [InlineData(D)]
    public void SousLaZoneMorteLeJoueurNeBougePas(float jitter)
    {
        var reading = Read(400f, 400f, 400f + jitter, 400f);

        Assert.Equal(0f, reading.X);
        Assert.Equal(0f, reading.Y);
    }

    /// <summary>
    /// Juste au-dessus de la zone morte, le déplacement démarre <b>à zéro</b> et non à 13 % : sans ce
    /// rééchelonnage, le joueur ressent un à-coup au premier pixel utile.
    /// </summary>
    [Fact]
    public void LaSortieDeZoneMorteEstProgressive_PasUnSaut()
    {
        float justOut = Read(0f, 0f, D + 0.5f, 0f).Magnitude;

        Assert.InRange(justOut, 0f, 0.02f);
    }

    [Fact]
    public void AMiCourseLeJoueurAvanceAPeuPresAMiVitesse()
    {
        float half = Read(0f, 0f, (R + D) / 2f, 0f).Magnitude;

        Assert.InRange(half, 0.45f, 0.55f);
    }

    // ─── Le recentrage ───────────────────────────────────────────────────────

    /// <summary>
    /// Le pouce qui dépasse traîne l'origine derrière lui, à exactement un rayon — c'est ce qui lui
    /// laisse toujours une course de retour.
    /// </summary>
    [Fact]
    public void AuDelaDuRayonLOrigineSuitLeDoigt()
    {
        var reading = Read(0f, 0f, R * 4f, 0f);

        Assert.Equal(R * 3f, reading.OriginX, 2);
        Assert.Equal(0f, reading.OriginY, 2);
        Assert.Equal(1f, reading.Magnitude, 3);
    }

    /// <summary>
    /// Le défaut que le recentrage existe pour éviter : après un long glissement, <b>revenir d'un
    /// demi-rayon doit ralentir</b>. Sans recentrage, l'origine restée loin derrière laisse le joueur
    /// à fond.
    /// </summary>
    [Fact]
    public void ApresUnLongGlissementUnPetitRetourRalentitVraiment()
    {
        var far = Read(0f, 0f, R * 5f, 0f);

        // Le doigt revient d'un demi-rayon, en repartant de l'origine recentrée.
        var back = Read(far.OriginX, far.OriginY, R * 5f - R / 2f, 0f);

        Assert.True(back.Magnitude < 0.6f,
            $"le retour ne ralentit pas : intensite {back.Magnitude:0.00} au lieu de ~0,5");
    }

    /// <summary>Tant que le doigt reste dans le rayon, l'origine ne bouge pas d'un pixel.</summary>
    [Fact]
    public void DansLeRayonLOrigineNeBougePas()
    {
        var reading = Read(120f, 240f, 120f + R * 0.9f, 240f);

        Assert.Equal(120f, reading.OriginX, 4);
        Assert.Equal(240f, reading.OriginY, 4);
    }

    // ─── Le repère ───────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠ Pousser vers le <b>haut de l'écran</b> doit faire monter. Le repère du DOM a son Y vers le
    /// bas ; s'il entrait ici, le jeu serait injouable d'une manière qu'aucune erreur ne signalerait.
    /// </summary>
    [Fact]
    public void PousserVersLeHautRendUnYPositif()
    {
        Assert.True(Read(0f, 0f, 0f, R).Y > 0f);
        Assert.True(Read(0f, 0f, 0f, -R).Y < 0f);
    }

    // ─── Adaptation à la dalle ───────────────────────────────────────────────

    /// <summary>
    /// Sur un téléphone en paysage (hauteur ~400 px logiques), un rayon strictement proportionnel
    /// tomberait sous la taille d'un contact de pouce : le stick serait toujours à fond.
    /// </summary>
    [Theory]
    [InlineData(360f)]
    [InlineData(414f)]
    [InlineData(720f)]
    [InlineData(1080f)]
    [InlineData(2160f)]
    public void LeRayonResteJouableSurToutesLesDalles(float height)
    {
        float radius = VirtualStick.RadiusFor(height);

        Assert.True(radius >= 60f, $"rayon {radius} px : plus petit qu'un pouce");
        Assert.True(VirtualStick.DeadZoneFor(height) < radius / 2f,
            "la zone morte mange plus de la moitie de la course");
    }

    [Fact]
    public void ALaResolutionDeReferenceLesValeursSontCellesDeclarees()
    {
        Assert.Equal(VirtualStick.ReferenceRadius, VirtualStick.RadiusFor(VirtualStick.ReferenceHeight), 3);
        Assert.Equal(VirtualStick.ReferenceDeadZone, VirtualStick.DeadZoneFor(VirtualStick.ReferenceHeight), 3);
    }
}
